using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Garrison.Supply;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Transport
{
    public sealed class ReinforcementTransportBehavior : CampaignBehaviorBase
    {
        private readonly ReinforcementDispatchModel _dispatch =
            new ReinforcementDispatchModel();
        private readonly ReinforcementTransferService _transfer =
            new ReinforcementTransferService();
        private const float ThreatRecoveryDelayHours = 6f;
        private readonly Dictionary<string, CampaignTime>
            _threatAvoidanceUntil =
                new Dictionary<string, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(
                this, OnHourlyPartyTick);
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(
                this, OnDailyClanTick);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(
                this, OnSettlementEntered);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this, OnGameLoadFinished);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyClanTick(Clan clan)
        {
            if (clan == null || clan.IsEliminated ||
                MilitaryConfigManager.Instance.GetActiveConfig()?
                    .AiRecruitment.ForceManagement
                    .EnableAutomaticReinforcement != true)
                return;
            SupplyPartyBehavior supply = Campaign.Current
                ?.GetCampaignBehavior<SupplyPartyBehavior>();
            int dispatchedTroops = 0;
            int dispatchedAid = 0;
            foreach (Settlement destination in clan.Settlements
                .Where(settlement => settlement?.IsFortification == true &&
                    settlement.Town?.GarrisonParty != null &&
                    !settlement.IsUnderSiege).ToList())
            {
                ReinforcementNeedResult need = _dispatch.GetNeed(destination);
                int foodDeficit = Math.Max(0, GarrisonLogisticsModel
                    .Calculate(destination.Town).FoodDeficit);
                if (need.IsRequested)
                {
                    ReinforcementDispatchPlan plan = _dispatch.FindBestSource(
                        destination, need.RequestedTroops,
                        Math.Min(SupplyPartyBehavior.MaximumRequestedFood,
                            foodDeficit));
                    if (plan.IsApproved && TryDispatchControlled(plan.Source,
                            destination, plan.TroopCount,
                            plan.FoodCount) != null)
                        dispatchedTroops++;
                }

                int remainingFood = Math.Max(0, GarrisonLogisticsModel
                    .Calculate(destination.Town).FoodDeficit);
                if (remainingFood > 0 && supply?.TryDispatchBestAid(
                        destination, Math.Min(
                            SupplyPartyBehavior.MaximumRequestedFood,
                            remainingFood)) != null)
                    dispatchedAid++;
            }
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_transport_scan");
            message.SetTextVariable("CLAN",
                PartyLogFormatter.GetClanDisplayName(clan));
            message.SetTextVariable("MILITARY", dispatchedTroops);
            message.SetTextVariable("AID", dispatchedAid);
            ModLogger.Info(message.ToString());
        }

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
                ModLogger.Debug("Reinforcement dispatch was rejected because " +
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
            RouteEvaluation route = ForceManagementFacade.Current
                .ReinforcementRoutes.Evaluate(party,
                    component.SourceSettlement,
                    component.State == ReinforcementTransportState.Recovering
                        ? component.RecoverySettlement
                        : component.Destination);
            if (TransportThreatModel.IsNativeAvoidanceActive(party) ||
                route.ReasonCode == "EnemyThreat")
            {
                if (!_threatAvoidanceUntil.ContainsKey(party.StringId))
                    Log("AvoidingThreat", party, 0);
                _threatAvoidanceUntil[party.StringId] = CampaignTime.Now +
                    CampaignTime.Hours(ThreatRecoveryDelayHours);
                return;
            }
            if (_threatAvoidanceUntil.TryGetValue(party.StringId,
                    out CampaignTime avoidanceUntil))
            {
                if (!avoidanceUntil.IsPast) return;
                _threatAvoidanceUntil.Remove(party.StringId);
                Log("RouteResumed", party, 0);
            }
            if (!route.IsSafe && route.ReasonCode == "RouteBlocked")
            {
                BeginOrContinueRecovery(party, component,
                    "RouteBlocked");
                return;
            }
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
            _threatAvoidanceUntil.Remove(party.StringId);
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
            {
                _threatAvoidanceUntil.Remove(party.StringId);
                Log("Recovered", party, recovered);
            }
        }

        private void OnGameLoadFinished()
        {
            foreach (MobileParty party in MobileParty.All)
            {
                ReinforcementPartyComponent component = GetComponent(party);
                if (component == null || !party.IsActive) continue;
                party.ActualClan = component.DispatchClan;
                party.Ai.SetDoNotMakeNewDecisions(false);
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
            // Military transport events can be very frequent; foreign
            // kingdoms remain silent unless a separate global audit is added.
            ReinforcementPartyComponent component = GetComponent(party);
            if (!RecruitmentLogFilter.ShouldLog(party) &&
                !RecruitmentLogFilter.ShouldLog(component?.DispatchClan))
                return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_transport_event");
            message.SetTextVariable("MISSION",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("MODE",
                new TextObject("{=ModifiedArmy_TransportModeMilitary}" +
                    "Military reinforcement"));
            message.SetTextVariable("EVENT", TransportLogText.Event(state));
            message.SetTextVariable("STATE", component?.StateValue ?? -1);
            message.SetTextVariable("CLAN",
                PartyLogFormatter.GetClanDisplayName(
                    component?.DispatchClan));
            message.SetTextVariable("SOURCE",
                component?.SourceSettlement?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("DESTINATION",
                component?.Destination?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("REQUESTED_TROOPS",
                component?.RequestedTroopCount ?? 0);
            message.SetTextVariable("REQUESTED_FOOD",
                component?.RequestedFoodCount ?? 0);
            message.SetTextVariable("ACTUAL_TROOPS",
                party?.MemberRoster?.TotalManCount ?? 0);
            message.SetTextVariable("ACTUAL_FOOD",
                party?.ItemRoster?.TotalFood ?? 0);
            message.SetTextVariable("EVENT_AMOUNT", troops);
            ModLogger.Info(message.ToString());
        }
    }
}
