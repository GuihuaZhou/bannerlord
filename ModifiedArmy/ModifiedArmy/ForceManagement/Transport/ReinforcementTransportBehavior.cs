using Helpers;
using ModifiedArmy.Tool;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Transport
{
    public sealed class ReinforcementTransportBehavior : CampaignBehaviorBase
    {
        private readonly ReinforcementDispatchModel _dispatch =
            new ReinforcementDispatchModel();
        private readonly ReinforcementTransferService _transfer =
            new ReinforcementTransferService();

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(
                this, OnHourlyPartyTick);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(
                this, OnSettlementEntered);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this, OnGameLoadFinished);
        }

        public override void SyncData(IDataStore dataStore) { }

        public MobileParty TryDispatchControlled(
            Settlement source,
            Settlement destination,
            int requestedTroops,
            int requestedFood = 0)
        {
            ReinforcementDispatchPlan plan = _dispatch.BuildControlledPlan(
                source, destination, requestedTroops, requestedFood);
            if (!plan.IsApproved)
            {
                ModLogger.FileNotice("Reinforcement dispatch rejected: " +
                    plan.ReasonCode);
                return null;
            }
            try
            {
                MobileParty party = _transfer.TryCreate(plan);
                if (party == null) return null;
                MoveToSettlement(party, destination);
                Log("Dispatched", party,
                    party.Party.NumberOfAllMembers);
                return party;
            }
            catch (Exception exception)
            {
                ModLogger.Error("Reinforcement creation failed: " +
                    exception.Message);
                return null;
            }
        }

        private void OnHourlyPartyTick(MobileParty party)
        {
            ReinforcementPartyComponent component = GetComponent(party);
            if (component == null || !party.IsActive ||
                party.MapEvent != null) return;
            if (component.State ==
                ReinforcementTransportState.TravelingToDestination)
            {
                if (!IsMissionValidForDelivery(component) ||
                    (component.Deadline != CampaignTime.Zero &&
                        component.Deadline.IsPast))
                {
                    BeginOrContinueRecovery(party, component,
                        "DestinationInvalidOrExpired");
                    return;
                }
                EnsureMovingTo(party, component.Destination);
            }
            else if (component.State ==
                    ReinforcementTransportState.Unloading &&
                party.CurrentSettlement == component.Destination)
            {
                CompleteAtDestination(party, component);
            }
            else if (component.State ==
                ReinforcementTransportState.Recovering)
            {
                BeginOrContinueRecovery(party, component, "HourlyRecovery");
            }
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            ReinforcementPartyComponent component = GetComponent(party);
            if (component == null || !party.IsActive) return;
            if (component.State == ReinforcementTransportState
                    .TravelingToDestination &&
                settlement == component.Destination)
            {
                component.BeginUnloading();
                CompleteAtDestination(party, component);
            }
            else if (component.State ==
                    ReinforcementTransportState.Recovering &&
                settlement == component.RecoverySettlement)
            {
                CompleteRecovery(party, component, settlement);
            }
        }

        private void CompleteAtDestination(
            MobileParty party,
            ReinforcementPartyComponent component)
        {
            if (!IsMissionValidForDelivery(component))
            {
                BeginOrContinueRecovery(party, component,
                    "DestinationInvalid");
                return;
            }
            int delivered = _transfer.DeliverTroops(party, component);
            bool hasRemainingTroops =
                party.MemberRoster.TotalManCount > 0;
            _transfer.DeliverItemsAtDestination(
                party, component, hasRemainingTroops);
            if (!_transfer.TryDestroyEmpty(party, component))
            {
                Log("PartialDelivery", party, delivered);
                BeginOrContinueRecovery(party, component,
                    "TargetCapacityReached");
                return;
            }
            Log("Completed", party, delivered);
        }

        private void BeginOrContinueRecovery(
            MobileParty party,
            ReinforcementPartyComponent component,
            string reason)
        {
            Settlement recovery = component.RecoverySettlement;
            if (!IsValidRecovery(recovery, component))
            {
                recovery = _dispatch.FindRecoverySettlement(
                    party, component);
                component.BeginRecovery(recovery);
                Log("Recovering:" + reason, party, 0);
            }
            if (recovery == null) return;
            if (party.CurrentSettlement == recovery)
            {
                CompleteRecovery(party, component, recovery);
                return;
            }
            EnsureMovingTo(party, recovery);
        }

        private void CompleteRecovery(
            MobileParty party,
            ReinforcementPartyComponent component,
            Settlement recovery)
        {
            if (!IsValidRecovery(recovery, component))
            {
                component.BeginRecovery(null);
                return;
            }
            int recovered = _transfer.RecoverTroops(
                party, component, recovery);
            if (party.MemberRoster.TotalManCount > 0)
            {
                component.BeginRecovery(null);
                Log("RecoveryPartial", party, recovered);
                return;
            }
            _transfer.RecoverAllItems(party, component, recovery);
            if (_transfer.TryDestroyEmpty(party, component))
                Log("Recovered", party, recovered);
        }

        private void OnGameLoadFinished()
        {
            foreach (MobileParty party in MobileParty.All)
            {
                ReinforcementPartyComponent component = GetComponent(party);
                if (component == null || !party.IsActive) continue;
                party.ActualClan = component.DispatchClan;
                party.Ai.SetDoNotMakeNewDecisions(true);
                if (component.State ==
                    ReinforcementTransportState.TravelingToDestination)
                    EnsureMovingTo(party, component.Destination);
                else if (component.State ==
                    ReinforcementTransportState.Recovering)
                    BeginOrContinueRecovery(
                        party, component, "LoadRecovery");
            }
        }

        private static bool IsMissionValidForDelivery(
            ReinforcementPartyComponent component) =>
            component?.Destination?.Town?.GarrisonParty != null &&
            component.SourceSettlement?.OwnerClan ==
                component.DispatchClan &&
            component.DispatchClan != null &&
            component.Destination.OwnerClan == component.DispatchClan &&
            !component.Destination.IsUnderSiege;

        private static bool IsValidRecovery(
            Settlement settlement,
            ReinforcementPartyComponent component) =>
            settlement?.Town?.GarrisonParty != null &&
            component?.DispatchClan != null &&
            settlement.OwnerClan == component.DispatchClan &&
            !settlement.IsUnderSiege;

        private static void EnsureMovingTo(
            MobileParty party,
            Settlement destination)
        {
            if (destination == null || party.CurrentSettlement == destination ||
                (party.DefaultBehavior == AiBehavior.GoToSettlement &&
                    party.TargetSettlement == destination)) return;
            MoveToSettlement(party, destination);
        }

        private static void MoveToSettlement(
            MobileParty party,
            Settlement destination)
        {
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party, destination, false,
                out MobileParty.NavigationType navigationType,
                out _, out bool isFromPort);
            SetPartyAiAction.GetActionForVisitingSettlement(
                party, destination, navigationType, isFromPort, false);
        }

        private static ReinforcementPartyComponent GetComponent(
            MobileParty party) =>
            party?.PartyComponent as ReinforcementPartyComponent;

        private static void Log(
            string state,
            MobileParty party,
            int troops)
        {
            ReinforcementPartyComponent component = GetComponent(party);
            ModLogger.FileNotice(string.Format(
                "Reinforcement party {0}: Party={1}, Clan={2}, " +
                "Source={3}, Destination={4}, Troops={5}",
                state, party?.StringId ?? "-",
                component?.DispatchClan?.StringId ?? "-",
                component?.SourceSettlement?.StringId ?? "-",
                component?.Destination?.StringId ?? "-", troops));
        }
    }
}
