using Helpers;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>
    /// Persistent M5 transport runner. No automatic mission is created here;
    /// controlled callers must provide a mission and a route-approved plan.
    /// </summary>
    public sealed class ReinforcementMissionBehavior : CampaignBehaviorBase
    {
        private List<ReinforcementMission> _missions =
            new List<ReinforcementMission>();

        public override void RegisterEvents()
        {
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(
                this,
                OnHourlyPartyTick);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(
                this,
                OnSettlementEntered);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(
                this,
                OnMobilePartyDestroyed);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this,
                OnGameLoadFinished);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_reinforcement_missions",
                ref _missions);
            _missions ??= new List<ReinforcementMission>();
        }

        public bool StartControlledMission(ReinforcementMission mission)
        {
            int plannedTroops = Math.Max(0,
                mission?.RequestedTroopCount ?? 0);
            ForceManagementFacade.Current.ReinforcementCargo
                .RegisterOwnTroopCargo(mission);
            ForceManagementFacade.Current.FoodTransfers
                .RegisterExistingCarrierFood(mission);
            int inbound = ForceManagementFacade.Current.ReinforcementMissions
                .GetInboundTroopCommitment(mission?.Destination);
            int targetSlots = ModifiedArmy.Garrison.Models
                .GarrisonRecruitmentCapacity.GetTransferFreeSlots(
                    mission?.Destination?.Town?.GarrisonParty);
            if (plannedTroops > Math.Max(0, targetSlots - inbound))
            {
                return false;
            }
            if (!ForceManagementFacade.Current.ReinforcementMissions
                .TryRegister(mission, true))
            {
                return false;
            }
            _missions.Add(mission);
            BeginTravel(mission);
            if (mission.Carrier.CurrentSettlement ==
                (GetNextSource(mission) ?? mission.Destination))
            {
                OnSettlementEntered(
                    mission.Carrier,
                    mission.Carrier.CurrentSettlement,
                    mission.Carrier.LeaderHero);
            }
            return true;
        }

        private void OnGameLoadFinished()
        {
            ReinforcementMissionService service = ForceManagementFacade
                .Current.ReinforcementMissions;
            service.Restore(_missions.Where(IsRecoverable));
            foreach (ReinforcementMission mission in _missions
                .Where(item => item?.IsActive == true).ToList())
            {
                if (!service.IsProtected(mission.Carrier))
                {
                    mission.State = ReinforcementMissionState.Cancelled;
                }
            }
        }

        private void OnHourlyPartyTick(MobileParty party)
        {
            ReinforcementMission mission = ForceManagementFacade.Current
                .ReinforcementMissions.GetMission(party);
            if (mission == null) return;
            ForceManagementFacade.Current.ReinforcementMissions
                .RefreshSources(mission);
            if (GetNextSource(mission) != null &&
                mission.State != ReinforcementMissionState.Loading &&
                mission.State != ReinforcementMissionState.Unloading)
            {
                mission.State = ReinforcementMissionState.ToSource;
            }
            if (!party.IsActive)
            {
                Cancel(mission, ReinforcementCancelReason.NoCargo);
                return;
            }
            if (party.Army != null)
            {
                Cancel(mission, ReinforcementCancelReason.ArmyConflict);
                return;
            }
            if (mission.Deadline != CampaignTime.Zero &&
                mission.Deadline.IsPast)
            {
                Cancel(mission, ReinforcementCancelReason.Expired);
                return;
            }
            if (!HasValidOwnership(mission))
            {
                Cancel(mission, ReinforcementCancelReason.InvalidOwner);
                return;
            }
            if (mission.Destination.IsUnderSiege ||
                (mission.State == ReinforcementMissionState.ToSource &&
                    GetNextSource(mission)?.IsUnderSiege == true))
            {
                Cancel(mission, ReinforcementCancelReason.EnemyThreat);
                return;
            }
            if (party.MapEvent != null) return;

            Settlement destination = mission.State ==
                    ReinforcementMissionState.ToSource
                ? GetNextSource(mission)
                : mission.Destination;
            EnsureMovingTo(party, destination);
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            ReinforcementMission mission = ForceManagementFacade.Current
                .ReinforcementMissions.GetMission(party);
            if (mission == null) return;
            Settlement source = GetNextSource(mission);
            if (mission.State == ReinforcementMissionState.ToSource &&
                settlement == source)
            {
                mission.State = ReinforcementMissionState.Loading;
                if (settlement == mission.TroopSource)
                {
                    ForceManagementFacade.Current.ReinforcementCargo
                        .LoadFromSourceGarrison(mission);
                    mission.TroopSource = null;
                }
                if (settlement == mission.FoodSource)
                {
                    ForceManagementFacade.Current.FoodTransfers.Load(mission);
                    mission.FoodSource = null;
                }
                Settlement nextSource = GetNextSource(mission);
                mission.State = nextSource == null
                    ? ReinforcementMissionState.ToDestination
                    : ReinforcementMissionState.ToSource;
                if (settlement == mission.Destination)
                {
                    OnSettlementEntered(party, settlement, hero);
                    return;
                }
                EnsureMovingTo(party, nextSource ?? mission.Destination);
                return;
            }
            if (mission.State == ReinforcementMissionState.ToDestination &&
                settlement == mission.Destination)
            {
                mission.State = ReinforcementMissionState.Unloading;
                ForceManagementFacade.Current.ReinforcementCargo
                    .UnloadTroops(mission);
                ForceManagementFacade.Current.FoodTransfers.Unload(mission);
                Complete(mission);
            }
        }

        private void OnMobilePartyDestroyed(
            MobileParty party,
            PartyBase destroyerParty)
        {
            ReinforcementMission mission = ForceManagementFacade.Current
                .ReinforcementMissions.GetMission(party);
            if (mission != null)
            {
                Cancel(mission, ReinforcementCancelReason.NoCargo);
            }
        }

        private static bool IsRecoverable(ReinforcementMission mission) =>
            mission?.IsActive == true && mission.Carrier?.IsActive == true &&
            mission.Carrier.Army == null && HasValidOwnership(mission);

        private static bool HasValidOwnership(ReinforcementMission mission) =>
            mission?.Carrier?.ActualClan != null &&
            mission.Destination?.OwnerClan ==
                mission.Carrier.ActualClan &&
            (mission.TroopSource == null ||
                mission.TroopSource.OwnerClan == mission.Carrier.ActualClan) &&
            (mission.FoodSource == null ||
                mission.FoodSource.OwnerClan == mission.Carrier.ActualClan);

        private static void BeginTravel(ReinforcementMission mission)
        {
            Settlement source = GetNextSource(mission);
            mission.State = source == null
                ? ReinforcementMissionState.ToDestination
                : ReinforcementMissionState.ToSource;
            EnsureMovingTo(
                mission.Carrier,
                source ?? mission.Destination);
        }

        private static Settlement GetNextSource(
            ReinforcementMission mission)
        {
            if (mission == null) return null;
            if (mission.RequestedTroopCount > 0 &&
                mission.TroopSource != null)
                return mission.TroopSource;
            if (mission.RequestedFoodCount > 0 &&
                mission.FoodSource != null)
                return mission.FoodSource;
            return null;
        }

        private static void EnsureMovingTo(
            MobileParty party,
            Settlement destination)
        {
            if (party == null || destination == null ||
                party.CurrentSettlement == destination ||
                (party.DefaultBehavior == AiBehavior.GoToSettlement &&
                    party.TargetSettlement == destination))
            {
                return;
            }
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party,
                destination,
                false,
                out MobileParty.NavigationType navigationType,
                out _,
                out bool isFromPort);
            SetPartyAiAction.GetActionForVisitingSettlement(
                party,
                destination,
                navigationType,
                isFromPort,
                false);
        }

        private static void Complete(ReinforcementMission mission)
        {
            ForceManagementFacade.Current.ReinforcementMissions
                .Complete(mission.Carrier);
            ModLogger.FileNotice("Reinforcement mission completed: " +
                (mission.Carrier?.StringId ?? "unknown"));
        }

        private static void Cancel(
            ReinforcementMission mission,
            ReinforcementCancelReason reason)
        {
            ForceManagementFacade.Current.ReinforcementMissions
                .Cancel(mission.Carrier, reason);
            ModLogger.FileNotice("Reinforcement mission cancelled: " +
                (mission.Carrier?.StringId ?? "unknown") +
                ", reason=" + reason);
        }
    }
}
