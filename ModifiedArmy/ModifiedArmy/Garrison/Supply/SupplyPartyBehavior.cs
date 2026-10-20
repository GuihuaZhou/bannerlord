using Helpers;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Supply
{
    /// <summary>
    /// Creates and controls temporary supply parties for fortifications whose
    /// military granaries cannot be restored by local sources alone.
    /// </summary>
    public partial class SupplyPartyBehavior : CampaignBehaviorBase
    {
        public const int EscortSize = 50;

        public const int MaximumRequestedFood = 500;

        private const float MaximumSourceDistance = 150f;

        private const int VillagePurchaseBatchSize = 100;

        private const int TownPurchaseBatchSize = 75;

        private const int MinimumTownMarketFoodReserve = 100;

        private const int EmergencySupplyDays = 15;

        private const int OutboundProvisionDays = 5;

        private const int DestroyedPartyCooldownDays = 10;

        private const int MinimumAidReturnFood = 10;

        private const int SourceGranaryFoodReserve = 100;

        private Dictionary<Settlement, CampaignTime>
            _nextDispatchTimes =
                new Dictionary<Settlement, CampaignTime>();
        private const float ThreatRecoveryDelayHours = 6f;
        private readonly Dictionary<string, CampaignTime>
            _threatAvoidanceUntil =
                new Dictionary<string, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent
                .AddNonSerializedListener(this, OnDailySettlementTick);
            CampaignEvents.HourlyTickPartyEvent
                .AddNonSerializedListener(this, OnHourlyPartyTick);
            CampaignEvents.SettlementEntered
                .AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.MobilePartyDestroyed
                .AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.OnSettlementOwnerChangedEvent
                .AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.OnGameLoadFinishedEvent
                .AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.OnSessionLaunchedEvent
                .AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "_modifiedArmySupplyNextDispatchTimes",
                ref _nextDispatchTimes);

            _nextDispatchTimes ??=
                new Dictionary<Settlement, CampaignTime>();
        }

        public MobileParty TryDispatchControlledAid(
            Settlement source,
            Settlement destination,
            int requestedFood)
        {
            if (!IsValidAidRoute(source, destination) ||
                requestedFood <= 0 || CanOrganizeLocalForaging(destination))
                return null;
            RouteEvaluation route = ForceManagementFacade.Current
                .ReinforcementRoutes.Evaluate(null, source, destination);
            if (!route.IsSafe) return null;

            int deficit = GetRemainingAidNeed(destination,
                destination.OwnerClan);
            int available = GetAidFoodAvailable(source);
            int cargo = Math.Min(MaximumRequestedFood,
                Math.Min(requestedFood, Math.Min(deficit, available)));
            if (cargo <= 0 ||
                CanAttachAidToMilitaryReinforcement(destination, cargo) ||
                GetSafeAidEscortCount(source) < EscortSize)
                return null;

            MobileParty garrison = source.Town.GarrisonParty;
            TroopRoster escort = ExtractAidEscortRoster(garrison);
            if (escort.TotalHealthyCount < EscortSize)
            {
                RestoreTroops(escort, garrison.MemberRoster);
                return null;
            }

            MobileParty party = null;
            PackAnimalPreparationResult packAnimals =
                new PackAnimalPreparationResult();
            try
            {
                party = SupplyPartyComponent.CreateAidParty(source,
                    destination, destination.OwnerClan, cargo, escort);
                EnterSettlementAction.ApplyForParty(party, source);
                packAnimals = PreparePackAnimals(garrison, party,
                    source.OwnerClan?.Leader);
                TransferRidingMountsFromGranary(garrison, party);
                int returnFood = CalculateAidReturnFood(party);
                int loaded = TransferFood(garrison.ItemRoster,
                    party.ItemRoster, cargo + returnFood);
                if (loaded < cargo + returnFood)
                    throw new InvalidOperationException(
                        "InsufficientAidFood");
                MoveToSettlement(party, destination);
                LogAid("Dispatched", party, cargo);
                return party;
            }
            catch (Exception exception)
            {
                if (party?.IsActive == true)
                {
                    SupplyPartyComponent failed = GetComponent(party);
                    failed?.CompleteMission();
                    RestoreTroops(party.MemberRoster,
                        garrison.MemberRoster);
                    TransferAllItems(party.ItemRoster,
                        garrison.ItemRoster);
                    DestroyPartyAction.Apply(null, party);
                }
                else
                {
                    RestoreTroops(escort, garrison.MemberRoster);
                }
                if (packAnimals.GoldCost > 0 &&
                    source.OwnerClan?.Leader != null)
                    GiveGoldAction.ApplyBetweenCharacters(null,
                        source.OwnerClan.Leader, packAnimals.GoldCost, true);
                ModLogger.Error("Supply aid creation failed: " +
                    exception.Message);
                return null;
            }
        }

        public MobileParty TryDispatchBestAid(
            Settlement destination,
            int requestedFood)
        {
            if (destination?.OwnerClan == null || requestedFood <= 0)
                return null;
            foreach (Settlement source in Settlement.All
                .Where(candidate => IsValidAidRoute(candidate, destination))
                .OrderByDescending(GetAidFoodAvailable)
                .ThenBy(candidate => candidate.GetPosition2D.DistanceSquared(
                    destination.GetPosition2D)))
            {
                MobileParty party = TryDispatchControlledAid(source,
                    destination, requestedFood);
                if (party != null) return party;
            }
            return null;
        }

        private void OnDailySettlementTick(Settlement settlement)
        {
            if (!CanDispatchFrom(settlement)
                || FindSupplyPartyForHome(settlement) != null
                || IsDispatchCoolingDown(settlement))
            {
                return;
            }

            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(settlement.Town);

            if (!GarrisonLogisticsModel.NeedsReplenishment(status)
                || !ShouldDispatchAfterLocalSupply(settlement, status))
            {
                return;
            }

            SourceSelection source = FindBestSource(
                settlement,
                status.FoodDeficit,
                null);

            if (source == null)
            {
                SetDispatchCooldown(settlement, 1f);
                return;
            }

            TryCreateSupplyParty(
                settlement,
                source,
                Math.Min(status.FoodDeficit, MaximumRequestedFood));
        }

        private void OnHourlyPartyTick(MobileParty party)
        {
            SupplyPartyComponent component = GetComponent(party);

            if (component == null
                || !party.IsActive
                || party.MapEvent != null)
            {
                return;
            }

            if (ShouldPauseForThreat(party, component)) return;

            if (component.IsAidMission)
            {
                AdvanceAidMission(party, component);
                return;
            }

            if (component.MissionState
                == SupplyPartyMissionState.TravelingToSource)
            {
                if (!IsValidSource(
                    component.HomeSettlement,
                    component.SourceSettlement))
                {
                    SourceSelection replacement = FindBestSource(
                        component.HomeSettlement,
                        component.RequestedFood,
                        party);

                    if (replacement == null)
                    {
                        component.BeginReturnJourney();
                        MoveToSettlement(
                            party,
                            component.HomeSettlement);
                        return;
                    }

                    component.ChangeSource(replacement.Settlement);
                }

                EnsureMovingTo(
                    party,
                    component.SourceSettlement);
                return;
            }

            if (component.MissionState
                == SupplyPartyMissionState.ReturningHome)
            {
                EnsureMovingTo(party, component.HomeSettlement);
            }
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            SupplyPartyComponent component = GetComponent(party);

            if (component == null || !party.IsActive)
            {
                return;
            }

            if (component.IsAidMission)
            {
                OnAidSettlementEntered(party, component, settlement);
                return;
            }

            if (component.MissionState
                    == SupplyPartyMissionState.TravelingToSource
                && settlement == component.SourceSettlement)
            {
                SupplyAcquisitionResult acquisition =
                    AcquireFoodAtSource(party, component, settlement);
                component.RecordSourceVisit(
                    settlement,
                    acquisition.FoodAcquired);

                if (component.HomeSettlement.OwnerClan == Clan.PlayerClan)
                {
                    TextObject message = GameTexts.FindText(
                        "str_modifiedarmy_supply_party_loaded");
                    message.SetTextVariable(
                        "HOME_NAME",
                        component.HomeSettlement.Name);
                    message.SetTextVariable("SOURCE_NAME", settlement.Name);
                    message.SetTextVariable("FOOD", acquisition.FoodAcquired);
                    message.SetTextVariable("COST", acquisition.GoldCost);
                    message.SetTextVariable(
                        "MOUNTS",
                        acquisition.MountsPurchased);
                    message.SetTextVariable(
                        "MOUNT_COST",
                        acquisition.MountGoldCost);
                    message.SetTextVariable(
                        "CARGO_FOOD",
                        party.ItemRoster.TotalFood);
                    ModLogger.Info(message.ToString());
                }

                Hero payer = component.HomeSettlement.OwnerClan?.Leader;
                bool canContinue = component.RequestedFood > 0
                    && party.TotalWeightCarried < party.InventoryCapacity
                    && payer?.Gold > 0;
                SourceSelection nextSource = canContinue
                    ? FindBestSource(
                        component.HomeSettlement,
                        component.RequestedFood,
                        party)
                    : null;

                if (nextSource != null)
                {
                    component.ChangeSource(nextSource.Settlement);
                    MoveToSettlement(party, nextSource.Settlement);
                }
                else
                {
                    component.BeginReturnJourney();
                    MoveToSettlement(party, component.HomeSettlement);
                }

                return;
            }

            if (component.MissionState
                    == SupplyPartyMissionState.ReturningHome
                && settlement == component.HomeSettlement)
            {
                CompleteMission(party, component);
            }
        }

        private void OnMobilePartyDestroyed(
            MobileParty party,
            PartyBase destroyerParty)
        {
            if (party != null)
                _threatAvoidanceUntil.Remove(party.StringId);
            SupplyPartyComponent component = GetComponent(party);

            if (component == null
                || component.MissionState
                    == SupplyPartyMissionState.Completed)
            {
                return;
            }

            // Losing the escort, animals, and cargo temporarily interrupts the
            // home settlement's ability to organize another supply mission.
            SetDispatchCooldown(
                component.HomeSettlement,
                DestroyedPartyCooldownDays);

            if (component.HomeSettlement?.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_destroyed");
                message.SetTextVariable(
                    "HOME_NAME",
                    component.HomeSettlement.Name);
                message.SetTextVariable(
                    "COOLDOWN_DAYS",
                    DestroyedPartyCooldownDays);
                ModLogger.Notice(message.ToString());
            }
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            foreach (MobileParty party in MobileParty.All.ToList())
            {
                SupplyPartyComponent component = GetComponent(party);

                if (component == null)
                {
                    continue;
                }

                if (component.IsAidMission)
                {
                    if (component.SupplyHomeSettlement == settlement ||
                        component.AidDestination == settlement ||
                        component.RecoverySettlement == settlement)
                        AdvanceAidMission(party, component);
                    continue;
                }

                if (component.HomeSettlement == settlement)
                {
                    component.SynchronizeOwnership();
                }

                if (component.HomeSettlement == settlement
                    || component.SourceSettlement == settlement)
                {
                    if (component.MissionState
                            == SupplyPartyMissionState.TravelingToSource
                        && !IsValidSource(
                            component.HomeSettlement,
                            component.SourceSettlement))
                    {
                        SourceSelection replacement = FindBestSource(
                            component.HomeSettlement,
                            component.RequestedFood,
                            party);

                        if (replacement != null)
                        {
                            component.ChangeSource(replacement.Settlement);
                            MoveToSettlement(
                                party,
                                replacement.Settlement);
                        }
                        else
                        {
                            component.BeginReturnJourney();
                            MoveToSettlement(
                                party,
                                component.HomeSettlement);
                        }
                    }
                }
            }
        }

        private void OnGameLoadFinished()
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = GetComponent(party);

                if (component == null)
                {
                    continue;
                }

                component.SynchronizeOwnership();
                // This flag is not saveable in every game version. Restore
                // it after loading so native AI cannot compete with the
                // supply mission route controller.
                party.Ai.SetDoNotMakeNewDecisions(false);

                if (component.IsAidMission)
                {
                    AdvanceAidMission(party, component);
                    continue;
                }

                Settlement destination = component.MissionState
                    == SupplyPartyMissionState.TravelingToSource
                        ? component.SourceSettlement
                        : component.HomeSettlement;

                if (destination != null)
                {
                    MoveToSettlement(party, destination);
                }
            }
        }

        private void AdvanceAidMission(
            MobileParty party,
            SupplyPartyComponent component)
        {
            if (party?.IsActive != true || party.MapEvent != null) return;
            component.SynchronizeOwnership();
            if (component.Deadline != CampaignTime.Zero &&
                component.Deadline.IsPast &&
                component.MissionState ==
                    SupplyPartyMissionState.DeliveringAid)
            {
                component.BeginAidReturn();
                LogAid("DeadlineExpired", party, 0);
            }
            if (component.MissionState ==
                SupplyPartyMissionState.DeliveringAid)
            {
                RouteEvaluation route = ForceManagementFacade.Current
                    .ReinforcementRoutes.Evaluate(party,
                        component.SupplyHomeSettlement,
                        component.AidDestination);
                if (!route.IsSafe && route.ReasonCode == "RouteBlocked")
                {
                    component.BeginAidReturn();
                    LogAid("RouteBlocked", party, 0);
                    AdvanceAidMission(party, component);
                    return;
                }
                if (!IsValidAidHome(component))
                {
                    BeginOrContinueAidRecovery(party, component);
                    return;
                }
                if (!IsValidAidDestination(component))
                {
                    component.BeginAidReturn();
                    LogAid("DestinationInvalid", party, 0);
                }
                else
                {
                    EnsureMovingTo(party, component.AidDestination);
                    return;
                }
            }

            if (component.MissionState ==
                SupplyPartyMissionState.ReturningFromAid)
            {
                if (IsValidAidHome(component))
                {
                    EnsureMovingTo(party,
                        component.SupplyHomeSettlement);
                    return;
                }
                BeginOrContinueAidRecovery(party, component);
                return;
            }

            if (component.MissionState ==
                SupplyPartyMissionState.Recovering)
                BeginOrContinueAidRecovery(party, component);
        }

        private bool ShouldPauseForThreat(
            MobileParty party,
            SupplyPartyComponent component)
        {
            bool threatened =
                TransportThreatModel.IsNativeAvoidanceActive(party) ||
                TransportThreatModel.HasSuperiorEnemy(party,
                    party.Position, party.MapFaction);
            if (threatened)
            {
                if (!_threatAvoidanceUntil.ContainsKey(party.StringId))
                    LogSupplyRoute("AvoidingThreat", party, component);
                _threatAvoidanceUntil[party.StringId] = CampaignTime.Now +
                    CampaignTime.Hours(ThreatRecoveryDelayHours);
                return true;
            }
            if (_threatAvoidanceUntil.TryGetValue(party.StringId,
                    out CampaignTime avoidanceUntil))
            {
                if (!avoidanceUntil.IsPast) return true;
                _threatAvoidanceUntil.Remove(party.StringId);
                LogSupplyRoute("RouteResumed", party, component);
            }
            return false;
        }

        private static void LogSupplyRoute(
            string state,
            MobileParty party,
            SupplyPartyComponent component)
        {
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_route");
            message.SetTextVariable("EVENT", TransportLogText.Event(state));
            message.SetTextVariable("PARTY",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("MODE", GetSupplyModeText(component));
            message.SetTextVariable("STATE", GetSupplyStateText(component));
            message.SetTextVariable("SOURCE",
                component?.SupplyHomeSettlement?.Name ??
                    TextObject.GetEmpty());
            message.SetTextVariable("DESTINATION",
                component?.AidDestination?.Name ??
                    component?.SourceSettlement?.Name ??
                    TextObject.GetEmpty());
            ModLogger.FileNotice(message.ToString());
        }

        private static TextObject GetSupplyModeText(
            SupplyPartyComponent component) => component?.IsAidMission == true
                ? new TextObject("{=ModifiedArmy_TransportModeFoodAid}" +
                    "Food aid")
                : new TextObject("{=ModifiedArmy_TransportModeLocalSupply}" +
                    "Local foraging");

        private static TextObject GetSupplyStateText(
            SupplyPartyComponent component)
        {
            if (component == null) return new TextObject("-");
            switch (component.MissionState)
            {
                case SupplyPartyMissionState.TravelingToSource:
                    return new TextObject(
                        "{=ModifiedArmy_TransportTravelingToSource}" +
                        "Traveling to source");
                case SupplyPartyMissionState.ReturningHome:
                    return new TextObject(
                        "{=ModifiedArmy_TransportReturningHome}" +
                        "Returning home");
                case SupplyPartyMissionState.DeliveringAid:
                    return new TextObject(
                        "{=ModifiedArmy_TransportDeliveringAid}" +
                        "Delivering aid");
                case SupplyPartyMissionState.ReturningFromAid:
                    return new TextObject(
                        "{=ModifiedArmy_TransportReturningFromAid}" +
                        "Returning from aid");
                case SupplyPartyMissionState.Recovering:
                    return TransportLogText.Event("Recovering");
                case SupplyPartyMissionState.Completed:
                    return TransportLogText.Event("Completed");
                default:
                    return new TextObject("-");
            }
        }

        private void OnAidSettlementEntered(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement settlement)
        {
            if (component.MissionState ==
                    SupplyPartyMissionState.DeliveringAid &&
                settlement == component.AidDestination)
            {
                if (IsValidAidHome(component) &&
                    IsValidAidDestination(component))
                {
                    int delivered = DeliverAidFood(party, component);
                    LogAid("Delivered", party, delivered);
                }
                component.BeginAidReturn();
                AdvanceAidMission(party, component);
                return;
            }

            if (component.MissionState ==
                    SupplyPartyMissionState.ReturningFromAid &&
                settlement == component.SupplyHomeSettlement)
            {
                if (IsValidAidHome(component))
                    CompleteAidReturn(party, component, settlement);
                else
                    BeginOrContinueAidRecovery(party, component);
                return;
            }

            if (component.MissionState ==
                    SupplyPartyMissionState.Recovering &&
                settlement == component.RecoverySettlement)
                CompleteAidReturn(party, component, settlement);
        }

        private void BeginOrContinueAidRecovery(
            MobileParty party,
            SupplyPartyComponent component)
        {
            Settlement recovery = component.RecoverySettlement;
            if (!IsValidAidRecovery(recovery, component))
            {
                recovery = FindAidRecoverySettlement(party, component);
                component.BeginRecovery(recovery);
                LogAid("Recovering", party, 0);
            }
            if (recovery == null) return;
            if (party.CurrentSettlement == recovery)
                CompleteAidReturn(party, component, recovery);
            else
                EnsureMovingTo(party, recovery);
        }

        private void CompleteAidReturn(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement settlement)
        {
            if (!IsValidAidRecovery(settlement, component))
            {
                component.BeginRecovery(null);
                return;
            }
            MobileParty garrison = settlement.Town.GarrisonParty;
            int reservedEscort = party.MemberRoster.TotalManCount;
            int moved = TransferTroopsLimited(party.MemberRoster,
                garrison.MemberRoster,
                GarrisonRecruitmentCapacity.GetHardFreeSlots(garrison) +
                    reservedEscort);
            if (party.MemberRoster.TotalManCount > 0)
            {
                component.BeginRecovery(null);
                LogAid("RecoveryPartial", party, moved);
                return;
            }
            TransferAllItems(party.ItemRoster, garrison.ItemRoster);
            component.CompleteMission();
            LogAid("Completed", party, moved);
            DestroyPartyAction.Apply(null, party);
        }

        private bool CanOrganizeLocalForaging(Settlement destination)
        {
            if (!CanDispatchFrom(destination)) return false;
            MobileParty active = FindSupplyPartyForHome(destination);
            if (active?.PartyComponent is SupplyPartyComponent component &&
                !component.IsAidMission)
                return true;
            if (IsDispatchCoolingDown(destination)) return true;
            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(destination.Town);
            return GarrisonLogisticsModel.NeedsReplenishment(status) &&
                ShouldDispatchAfterLocalSupply(destination, status) &&
                FindBestSource(destination, status.FoodDeficit, null) != null;
        }

        private static bool CanAttachAidToMilitaryReinforcement(
            Settlement destination,
            int requestedFood)
        {
            ReinforcementDispatchModel model =
                new ReinforcementDispatchModel();
            ReinforcementNeedResult need = model.GetNeed(destination);
            if (!need.IsRequested) return false;
            ReinforcementDispatchPlan plan = model.FindBestSource(
                destination, need.RequestedTroops, requestedFood);
            return plan.IsApproved &&
                GetAidFoodAvailable(plan.Source) >= requestedFood;
        }

        private static bool IsValidAidRoute(
            Settlement source,
            Settlement destination) =>
            source?.Town?.GarrisonParty != null &&
            destination?.Town?.GarrisonParty != null &&
            source != destination && source.OwnerClan != null &&
            source.OwnerClan == destination.OwnerClan &&
            !source.IsUnderSiege && !destination.IsUnderSiege;

        private static bool IsValidAidDestination(
            SupplyPartyComponent component) =>
            component?.DispatchClan != null &&
            component.AidDestination?.Town?.GarrisonParty != null &&
            component.AidDestination.OwnerClan == component.DispatchClan &&
            !component.AidDestination.IsUnderSiege;

        private static bool IsValidAidHome(
            SupplyPartyComponent component) =>
            IsValidAidRecovery(component?.SupplyHomeSettlement, component);

        private static bool IsValidAidRecovery(
            Settlement settlement,
            SupplyPartyComponent component) =>
            component?.DispatchClan != null &&
            settlement?.Town?.GarrisonParty != null &&
            settlement.OwnerClan == component.DispatchClan &&
            !settlement.IsUnderSiege;

        private static Settlement FindAidRecoverySettlement(
            MobileParty party,
            SupplyPartyComponent component)
        {
            if (party == null || component?.DispatchClan == null) return null;
            bool needsCapacity = party.MemberRoster.TotalManCount > 0;
            return Settlement.All.Where(settlement =>
                    IsValidAidRecovery(settlement, component) &&
                    (!needsCapacity || GarrisonRecruitmentCapacity
                        .GetHardFreeSlots(settlement.Town.GarrisonParty) > 0))
                .OrderBy(settlement => party.Position.DistanceSquared(
                    settlement.GatePosition))
                .FirstOrDefault();
        }

        private static int GetAidFoodAvailable(Settlement source) =>
            Math.Max(0, (source?.Town?.GarrisonParty?.ItemRoster.TotalFood ?? 0)
                - SourceGranaryFoodReserve - MinimumAidReturnFood);

        private static int GetRemainingAidNeed(
            Settlement destination,
            Clan dispatchClan)
        {
            if (destination?.Town == null || dispatchClan == null) return 0;
            int deficit = Math.Max(0,
                GarrisonLogisticsModel.Calculate(destination.Town).FoodDeficit);
            int inboundAid = MobileParty.All.Where(party =>
                    party?.IsActive == true &&
                    party.PartyComponent is SupplyPartyComponent c &&
                    c.IsAidMission && c.DispatchClan == dispatchClan &&
                    c.AidDestination == destination &&
                    c.MissionState ==
                        SupplyPartyMissionState.DeliveringAid)
                .Sum(party => Math.Max(0,
                    party.ItemRoster.TotalFood -
                        CalculateAidReturnFood(party)));
            int inboundMilitary = MobileParty.All.Where(party =>
                    party?.IsActive == true &&
                    party.PartyComponent is ReinforcementPartyComponent c &&
                    c.DispatchClan == dispatchClan &&
                    c.Destination == destination &&
                    (c.State == ReinforcementTransportState
                        .TravelingToDestination ||
                     c.State == ReinforcementTransportState.Unloading))
                .Sum(party => Math.Max(0,
                    party.ItemRoster.TotalFood -
                        MinimumAidReturnFood));
            return Math.Max(0, deficit - inboundAid - inboundMilitary);
        }

        private static int CalculateAidReturnFood(MobileParty party) =>
            Math.Max(MinimumAidReturnFood, (int)Math.Ceiling(
                Math.Abs(party?.FoodChange ?? 0f) * OutboundProvisionDays));

        private static int GetSafeAidEscortCount(Settlement source)
        {
            MobileParty garrison = source?.Town?.GarrisonParty;
            if (garrison == null) return 0;
            GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                .GarrisonDefense.Evaluate(source);
            int safe = Math.Max(0,
                defense.ResidentCount - defense.MinimumCount);
            Dictionary<CharacterObject, int> protectedCounts =
                Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                    .GetServingTroopCounts(garrison) ??
                new Dictionary<CharacterObject, int>();
            int eligible = garrison.MemberRoster.GetTroopRoster()
                .Where(x => x.Character != null && !x.Character.IsHero)
                .Sum(x => Math.Max(0, x.Number - x.WoundedNumber -
                    (protectedCounts.TryGetValue(x.Character, out int count)
                        ? count : 0)));
            return Math.Min(safe, eligible);
        }

        private static TroopRoster ExtractAidEscortRoster(
            MobileParty garrison)
        {
            TroopRoster result = TroopRoster.CreateDummyTroopRoster();
            Dictionary<CharacterObject, int> protectedCounts =
                Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                    .GetServingTroopCounts(garrison) ??
                new Dictionary<CharacterObject, int>();
            int remaining = EscortSize;
            foreach (TroopRosterElement element in garrison.MemberRoster
                .GetTroopRoster().Where(x => x.Character != null &&
                    !x.Character.IsHero).OrderBy(x => x.Character.Tier)
                .ToList())
            {
                int protectedCount = protectedCounts.TryGetValue(
                    element.Character, out int count) ? count : 0;
                int eligible = Math.Max(0, element.Number -
                    element.WoundedNumber - protectedCount);
                int amount = Math.Min(remaining, eligible);
                if (amount <= 0) continue;
                garrison.MemberRoster.AddToCounts(element.Character,
                    -amount, false, 0, 0, true, -1);
                result.AddToCounts(element.Character, amount,
                    false, 0, 0, true, -1);
                remaining -= amount;
                if (remaining <= 0) break;
            }
            return result;
        }

        private static int TransferTroopsLimited(
            TroopRoster source,
            TroopRoster destination,
            int maximum)
        {
            int remaining = Math.Max(0, maximum);
            int moved = 0;
            foreach (TroopRosterElement element in source.GetTroopRoster()
                .Where(x => x.Character != null && !x.Character.IsHero)
                .ToList())
            {
                int amount = Math.Min(remaining, element.Number);
                if (amount <= 0) continue;
                int wounded = Math.Min(amount, element.WoundedNumber);
                source.AddToCounts(element.Character, -amount, false,
                    -wounded, 0, true, -1);
                destination.AddToCounts(element.Character, amount, false,
                    wounded, 0, true, -1);
                moved += amount;
                remaining -= amount;
                if (remaining <= 0) break;
            }
            return moved;
        }

        private static void LogAid(
            string state,
            MobileParty party,
            int food)
        {
            SupplyPartyComponent component = GetComponent(party);
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_transport_event");
            message.SetTextVariable("MISSION",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("MODE",
                new TextObject("{=ModifiedArmy_TransportModeFoodAid}" +
                    "Food aid"));
            message.SetTextVariable("EVENT", TransportLogText.Event(state));
            message.SetTextVariable("STATE",
                component?.MissionStateValue ?? -1);
            message.SetTextVariable("CLAN",
                PartyLogFormatter.GetClanDisplayName(
                    component?.DispatchClan));
            message.SetTextVariable("SOURCE",
                component?.SupplyHomeSettlement?.Name ??
                    TextObject.GetEmpty());
            message.SetTextVariable("DESTINATION",
                component?.AidDestination?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("REQUESTED_TROOPS", 0);
            message.SetTextVariable("REQUESTED_FOOD",
                component?.RequestedFood ?? 0);
            message.SetTextVariable("ACTUAL_TROOPS",
                party?.MemberRoster?.TotalManCount ?? 0);
            message.SetTextVariable("ACTUAL_FOOD",
                party?.ItemRoster?.TotalFood ?? 0);
            message.SetTextVariable("EVENT_AMOUNT", food);
            ModLogger.FileNotice(message.ToString());
        }

        private bool TryCreateSupplyParty(
            Settlement home,
            SourceSelection source,
            int requestedFood)
        {
            MobileParty garrison = home.Town.GarrisonParty;
            TroopRoster escortRoster = ExtractEscortRoster(garrison);

            if (escortRoster.TotalHealthyCount < EscortSize)
            {
                RestoreTroops(escortRoster, garrison.MemberRoster);
                SetDispatchCooldown(home, 1f);
                return false;
            }

            MobileParty supplyParty = null;
            PackAnimalPreparationResult packAnimals =
                new PackAnimalPreparationResult();
            int ridingMountsFromGranary = 0;

            try
            {
                supplyParty = SupplyPartyComponent.CreateSupplyParty(
                    home,
                    source.Settlement,
                    requestedFood,
                    escortRoster);

                EnterSettlementAction.ApplyForParty(supplyParty, home);
                packAnimals = PreparePackAnimals(
                    garrison,
                    supplyParty,
                    home.OwnerClan?.Leader);
                ridingMountsFromGranary =
                    TransferRidingMountsFromGranary(
                        garrison,
                        supplyParty);
                TransferOutboundProvisions(garrison, supplyParty);
                MoveToSettlement(supplyParty, source.Settlement);
            }
            catch (Exception exception)
            {
                if (supplyParty != null && supplyParty.IsActive)
                {
                    SupplyPartyComponent failedComponent =
                        GetComponent(supplyParty);
                    int garrisonBefore = garrison.Party.NumberOfAllMembers;
                    int returning = supplyParty.Party.NumberOfAllMembers;
                    int reservedBefore = GarrisonRecruitmentCapacity
                        .GetActiveSupplyEscortCount(home);
                    int limit = garrison.Party.PartySizeLimit;
                    int missionState = failedComponent?.MissionStateValue ?? -1;
                    failedComponent?.CompleteMission();
                    RestoreTroops(
                        supplyParty.MemberRoster,
                        garrison.MemberRoster);
                    TransferAllItems(
                        supplyParty.ItemRoster,
                        garrison.ItemRoster);
                    LogSupplyEscortCapacity(
                        home,
                        supplyParty,
                        garrisonBefore,
                        returning,
                        reservedBefore,
                        limit,
                        missionState);
                    DestroyPartyAction.Apply(null, supplyParty);
                }
                else
                {
                    RestoreTroops(
                        escortRoster,
                        garrison.MemberRoster);
                }

                SetDispatchCooldown(home, 1f);
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_creation_failed");
                message.SetTextVariable("HOME_NAME", home.Name);
                message.SetTextVariable("ERROR", exception.Message);
                ModLogger.Error(message.ToString());
                return false;
            }

            if (home.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_dispatched");
                message.SetTextVariable("HOME_NAME", home.Name);
                message.SetTextVariable(
                    "SOURCE_NAME",
                    source.Settlement.Name);
                message.SetTextVariable("ESCORT", EscortSize);
                message.SetTextVariable(
                    "PACK_ANIMALS",
                    supplyParty.ItemRoster.NumberOfPackAnimals);
                message.SetTextVariable(
                    "FROM_GRANARY",
                    packAnimals.FromGranary);
                message.SetTextVariable("PURCHASED", packAnimals.Purchased);
                message.SetTextVariable("ANIMAL_COST", packAnimals.GoldCost);
                message.SetTextVariable(
                    "MOUNTS_FROM_GRANARY",
                    ridingMountsFromGranary);
                message.SetTextVariable("REQUESTED", requestedFood);
                ModLogger.Notice(message.ToString());
            }

            return true;
        }

        private static TroopRoster ExtractEscortRoster(
            MobileParty garrison)
        {
            TroopRoster escort = TroopRoster.CreateDummyTroopRoster();
            int remaining = EscortSize;

            IEnumerable<TroopRosterElement> candidates = garrison
                .MemberRoster
                .GetTroopRoster()
                .Where(element =>
                    !element.Character.IsHero
                    && element.Number - element.WoundedNumber > 0)
                .OrderBy(element => element.Character.Tier);

            foreach (TroopRosterElement element in candidates)
            {
                int healthy = element.Number - element.WoundedNumber;
                int count = Math.Min(healthy, remaining);

                if (count <= 0)
                {
                    continue;
                }

                garrison.MemberRoster.AddToCounts(
                    element.Character,
                    -count,
                    false,
                    0,
                    0,
                    true,
                    -1);
                escort.AddToCounts(
                    element.Character,
                    count,
                    false,
                    0,
                    0,
                    true,
                    -1);
                remaining -= count;

                if (remaining <= 0)
                {
                    break;
                }
            }

            return escort;
        }

        private static void CompleteMission(
            MobileParty party,
            SupplyPartyComponent component)
        {
            MobileParty garrison =
                component.HomeSettlement.Town.GarrisonParty;

            if (garrison == null)
            {
                return;
            }

            int returnedTroops = party.MemberRoster.TotalManCount;
            int returnedFood = party.ItemRoster.TotalFood;
            int returnedPackAnimals =
                party.ItemRoster.NumberOfPackAnimals;
            int returnedMounts = party.ItemRoster.NumberOfMounts;
            int garrisonBefore = garrison.Party.NumberOfAllMembers;
            int reservedBefore = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(component.HomeSettlement);
            int partySizeLimit = garrison.Party.PartySizeLimit;
            int missionState = component.MissionStateValue;

            component.CompleteMission();
            RestoreTroops(party.MemberRoster, garrison.MemberRoster);
            TransferAllItems(party.ItemRoster, garrison.ItemRoster);

            LogSupplyEscortCapacity(
                component.HomeSettlement,
                party,
                garrisonBefore,
                returnedTroops,
                reservedBefore,
                partySizeLimit,
                missionState);

            if (component.HomeSettlement.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_returned");
                message.SetTextVariable(
                    "HOME_NAME",
                    component.HomeSettlement.Name);
                message.SetTextVariable("ESCORT", returnedTroops);
                message.SetTextVariable("FOOD", returnedFood);
                message.SetTextVariable(
                    "PACK_ANIMALS",
                    returnedPackAnimals);
                message.SetTextVariable("MOUNTS", returnedMounts);
                ModLogger.Notice(message.ToString());
            }

            DestroyPartyAction.Apply(null, party);
        }

        private static void LogSupplyEscortCapacity(
            Settlement home,
            MobileParty supplyParty,
            int garrisonBefore,
            int returningTroops,
            int reservedBefore,
            int partySizeLimit,
            int missionState)
        {
            MobileParty garrison = home?.Town?.GarrisonParty;
            int reservedAfter = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(home);
            int garrisonAfter = garrison?.Party.NumberOfAllMembers ?? 0;
            int excess = Math.Max(
                0,
                garrisonAfter + reservedAfter - partySizeLimit);
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_escort_capacity");
            message.SetTextVariable("SETTLEMENT_ID", home?.StringId ?? "-");
            message.SetTextVariable("PARTY_ID", supplyParty?.StringId ?? "-");
            message.SetTextVariable("GARRISON_BEFORE", garrisonBefore);
            message.SetTextVariable("RETURNING", returningTroops);
            message.SetTextVariable("RESERVED_BEFORE", reservedBefore);
            message.SetTextVariable("GARRISON_AFTER", garrisonAfter);
            message.SetTextVariable("RESERVED_AFTER", reservedAfter);
            message.SetTextVariable("LIMIT", partySizeLimit);
            message.SetTextVariable("EXCESS", excess);
            message.SetTextVariable("MISSION_STATE", missionState);
            ModLogger.Debug(message.ToString());
        }

        private static void RestoreTroops(
            TroopRoster source,
            TroopRoster destination)
        {
            foreach (TroopRosterElement element in
                source.GetTroopRoster())
            {
                destination.AddToCounts(
                    element.Character,
                    element.Number,
                    false,
                    element.WoundedNumber,
                    0,
                    true,
                    -1);
            }
        }

        private static void TransferAllItems(
            ItemRoster source,
            ItemRoster destination)
        {
            for (int index = source.Count - 1; index >= 0; index--)
            {
                ItemRosterElement element = source[index];
                destination.AddToCounts(
                    element.EquipmentElement,
                    element.Amount);
                source.AddToCounts(
                    element.EquipmentElement,
                    -element.Amount);
            }
        }

        private static void TransferOutboundProvisions(
            MobileParty garrison,
            MobileParty supplyParty)
        {
            int provisionTarget = (int)Math.Ceiling(
                Math.Abs(supplyParty.FoodChange)
                * OutboundProvisionDays);
            TransferFood(
                garrison.ItemRoster,
                supplyParty.ItemRoster,
                provisionTarget);
        }

        private static int TransferFood(
            ItemRoster source,
            ItemRoster destination,
            int requestedAmount)
        {
            int remaining = requestedAmount;

            for (int index = source.Count - 1;
                index >= 0 && remaining > 0;
                index--)
            {
                ItemRosterElement element = source[index];

                if (element.EquipmentElement.Item?.IsFood != true
                    || element.Amount <= 0)
                {
                    continue;
                }

                int amount = Math.Min(element.Amount, remaining);
                source.AddToCounts(element.EquipmentElement, -amount);
                destination.AddToCounts(element.EquipmentElement, amount);
                remaining -= amount;
            }

            return requestedAmount - remaining;
        }

        private SourceSelection FindBestSource(
            Settlement home,
            int requestedFood,
            MobileParty requestingParty)
        {
            SourceSelection best = null;

            foreach (Settlement candidate in Settlement.All)
            {
                SupplyPartyComponent requestingComponent =
                    GetComponent(requestingParty);

                if (!IsValidSource(home, candidate)
                    || requestingComponent?.HasVisited(candidate) == true)
                {
                    continue;
                }

                int availableFood = CalculateAvailableFood(candidate)
                    - CalculateReservedFood(candidate, requestingParty);

                if (availableFood <= 0)
                {
                    continue;
                }

                float distance = Campaign.Current.Models.MapDistanceModel
                    .GetDistance(
                        home,
                        candidate,
                        false,
                        false,
                        MobileParty.NavigationType.Default);

                if (distance <= 0f || float.IsNaN(distance))
                {
                    continue;
                }

                if (distance > MaximumSourceDistance)
                {
                    continue;
                }

                int batchSize = candidate.IsVillage
                    ? VillagePurchaseBatchSize
                    : TownPurchaseBatchSize;
                int request = Math.Min(
                    Math.Min(
                        Math.Min(availableFood, requestedFood),
                        MaximumRequestedFood),
                    batchSize);
                int priority = candidate.IsVillage ? 2 : 1;
                float score = request / distance;

                if (best == null
                    || priority > best.Priority
                    || priority == best.Priority && score > best.Score)
                {
                    best = new SourceSelection(
                        candidate,
                        request,
                        priority,
                        score);
                }
            }

            return best;
        }

        private static bool IsValidSource(
            Settlement home,
            Settlement candidate)
        {
            if (home == null
                || candidate == null
                || candidate == home
                || (!candidate.IsTown
                    && !IsFoodProducingVillage(candidate))
                || candidate.IsUnderSiege
                || GetSourceOwnerClan(candidate) == null
                || home.OwnerClan == null
                || home.MapFaction == null
                || candidate.MapFaction == null
                || FactionManager.IsAtWarAgainstFaction(
                    home.MapFaction,
                    candidate.MapFaction))
            {
                return false;
            }

            return true;
        }

        private static int CalculateAvailableFood(Settlement source)
        {
            if (source.IsTown)
            {
                return Math.Max(
                    0,
                    (source.ItemRoster?.TotalFood ?? 0)
                        - MinimumTownMarketFoodReserve);
            }

            if (IsFoodProducingVillage(source))
            {
                return source.ItemRoster?.TotalFood ?? 0;
            }

            return 0;
        }

        private static bool IsFoodProducingVillage(Settlement settlement)
        {
            return settlement?.IsVillage == true
                && settlement.Village?.VillageType?.PrimaryProduction?.IsFood
                    == true;
        }

        private static Clan GetSourceOwnerClan(Settlement settlement)
        {
            return settlement?.OwnerClan
                ?? settlement?.Village?.Bound?.OwnerClan;
        }

        private static int CalculateReservedFood(
            Settlement source,
            MobileParty requestingParty)
        {
            int reserved = 0;

            foreach (MobileParty party in MobileParty.All)
            {
                if (party == requestingParty)
                {
                    continue;
                }

                SupplyPartyComponent component = GetComponent(party);

                if (component?.MissionState
                        == SupplyPartyMissionState.TravelingToSource
                    && component.SourceSettlement == source)
                {
                    reserved += Math.Min(
                        component.RequestedFood,
                        CalculateAvailableFood(source));
                }
            }

            return reserved;
        }

        private static bool CanDispatchFrom(Settlement settlement)
        {
            return settlement?.IsFortification == true
                && !settlement.IsUnderSiege
                && settlement.OwnerClan != null
                && settlement.Town?.GarrisonParty != null;
        }

        private static bool ShouldDispatchAfterLocalSupply(
            Settlement settlement,
            GarrisonLogisticsStatus status)
        {
            if (settlement.IsCastle
                || status.FoodSupplyDays < EmergencySupplyDays)
            {
                return true;
            }

            int localMarketFood =
                settlement.ItemRoster?.TotalFood ?? 0;
            int fiveDayDemand = (int)Math.Ceiling(
                status.DailyConsumption * 5f);
            Hero payer = settlement.OwnerClan?.Leader;

            return localMarketFood < fiveDayDemand
                || payer == null
                || payer.Gold <= 0;
        }

        private static MobileParty FindSupplyPartyForHome(
            Settlement home)
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = GetComponent(party);

                if (component?.HomeSettlement == home
                    && component.MissionState
                        != SupplyPartyMissionState.Completed)
                {
                    return party;
                }
            }

            return null;
        }

        private bool IsDispatchCoolingDown(Settlement home)
        {
            if (!_nextDispatchTimes.TryGetValue(
                home,
                out CampaignTime nextTime))
            {
                return false;
            }

            if (nextTime.IsPast)
            {
                _nextDispatchTimes.Remove(home);
                return false;
            }

            return true;
        }

        private void SetDispatchCooldown(
            Settlement home,
            float days)
        {
            if (home != null)
            {
                _nextDispatchTimes[home] =
                    CampaignTime.Now + CampaignTime.Days(days);
            }
        }

        private static void EnsureMovingTo(
            MobileParty party,
            Settlement destination)
        {
            if (destination == null
                || party.CurrentSettlement == destination
                || (party.DefaultBehavior == AiBehavior.GoToSettlement
                    && party.TargetSettlement == destination))
            {
                return;
            }

            MoveToSettlement(party, destination);
        }

        private static void MoveToSettlement(
            MobileParty party,
            Settlement destination)
        {
            if (party == null || destination == null)
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

        private static SupplyPartyComponent GetComponent(
            MobileParty party)
        {
            return party?.PartyComponent as SupplyPartyComponent;
        }

        private sealed class SourceSelection
        {
            public SourceSelection(
                Settlement settlement,
                int requestedFood,
                int priority,
                float score)
            {
                Settlement = settlement;
                RequestedFood = requestedFood;
                Priority = priority;
                Score = score;
            }

            public Settlement Settlement { get; }

            public int RequestedFood { get; }

            public int Priority { get; }

            public float Score { get; }
        }
    }
}
