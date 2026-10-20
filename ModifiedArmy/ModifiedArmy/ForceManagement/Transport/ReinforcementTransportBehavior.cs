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
                EnsureMovingTo(party, component.Destination);
            }
            else if (component.State ==
                    ReinforcementTransportState.Unloading &&
                party.CurrentSettlement == component.Destination)
            {
                CompleteAtDestination(party, component);
            }
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            ReinforcementPartyComponent component = GetComponent(party);
            if (component == null || !party.IsActive ||
                settlement != component.Destination ||
                component.State != ReinforcementTransportState
                    .TravelingToDestination) return;
            component.BeginUnloading();
            CompleteAtDestination(party, component);
        }

        private void CompleteAtDestination(
            MobileParty party,
            ReinforcementPartyComponent component)
        {
            if (component.Destination?.OwnerClan !=
                    component.DispatchClan ||
                component.Destination.IsUnderSiege)
            {
                component.BeginRecovery(null);
                Log("RecoveryRequired", party, 0);
                return;
            }
            int delivered = _transfer.DeliverTroops(party, component);
            _transfer.DeliverAllItems(party, component);
            if (!_transfer.TryDestroyEmpty(party, component))
            {
                component.BeginRecovery(null);
                Log("PartialDelivery", party, delivered);
                return;
            }
            Log("Completed", party, delivered);
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
            }
        }

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
