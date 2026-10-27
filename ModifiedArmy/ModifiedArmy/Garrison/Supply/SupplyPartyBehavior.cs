using Helpers;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Garrison.Supply.Models;
using ModifiedArmy.Garrison.Supply.Services;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment.Diagnostics;
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

        private const int ProvisionSafetyDays = 2;

        private const int DestroyedPartyCooldownDays = 10;

        private const int MinimumAidReturnFood = 10;

        private const int SourceGranaryFoodReserve = 100;

        private Dictionary<Settlement, CampaignTime>
            _nextDispatchTimes =
                new Dictionary<Settlement, CampaignTime>();
        private readonly SupplyRouteController _routeController =
            new SupplyRouteController();
        private static readonly SupplyCargoService Cargo =
            new SupplyCargoService();
        private readonly SupplyDispatchService _dispatchService =
            new SupplyDispatchService();
        private readonly SupplyRecoveryService _recoveryService =
            new SupplyRecoveryService();
        private readonly Dictionary<string, SupplyHealthSnapshot>
            _lastHealthSnapshots =
                new Dictionary<string, SupplyHealthSnapshot>();
        private readonly Dictionary<string, int> _lastFoodRiskDay =
            new Dictionary<string, int>();

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
                requestedFood <= 0 ||
                HasActiveLocalForagingMission(destination) ||
                CanActuallyDispatchLocalForaging(destination))
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
                    RemovePurchasedPackAnimals(party.ItemRoster,
                        packAnimals.Purchased);
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
            _dispatchService.EvaluateDaily(
                settlement,
                _nextDispatchTimes,
                CanDispatchFrom,
                home => FindSupplyPartyForHome(home) != null,
                ShouldDispatchAfterLocalSupply,
                status =>
                {
                    SourceSelection source = FindBestSource(
                        settlement, status.FoodDeficit, null);
                    if (source == null) return false;
                    return TryCreateSupplyParty(settlement, source,
                        Math.Min(status.FoodDeficit,
                            MaximumRequestedFood));
                });
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

            LogDailySupplyHealth(party, component);

            if (ShouldPauseForThreat(party, component)) return;

            if (component.IsAidMission)
            {
                AdvanceAidMission(party, component);
                return;
            }

            // Food-risk handling runs only after native threat protection. It
            // may select a nearer legal source or return home, but never
            // overwrites an active flee/avoidance order.
            HandleLocalMissionFoodRisk(party, component);

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

                LogUnexpectedRouteOverride(
                    party,
                    component,
                    component.SourceSettlement);
                EnsureMovingTo(party, component.SourceSettlement,
                    "TravelingToSource");
                return;
            }

            if (component.MissionState
                == SupplyPartyMissionState.ReturningHome)
            {
                LogUnexpectedRouteOverride(
                    party,
                    component,
                    component.HomeSettlement);
                EnsureMovingTo(party, component.HomeSettlement,
                    "ReturningHome");
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
            {
                _routeController.ForgetParty(party.StringId);
                _lastHealthSnapshots.Remove(party.StringId);
                _lastFoodRiskDay.Remove(party.StringId);
            }
            SupplyPartyComponent component = GetComponent(party);
            _recoveryService.HandleDestroyedMission(component,
                DestroyedPartyCooldownDays, SetDispatchCooldown);
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
            _recoveryService.RestoreLoadedMissions(
                AdvanceAidMission,
                (party, destination) => MoveToSettlement(
                    party, destination, "LoadRecovery"));
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
            return _routeController.ShouldPauseForThreat(
                party,
                (nativeAvoidance, superiorEnemy) =>
                {
                    LogSupplyRoute("AvoidingThreat", party, component);
                    LogThreatDiagnostic(party, component, nativeAvoidance,
                        superiorEnemy, false);
                },
                () =>
                {
                    LogSupplyRoute("RouteResumed", party, component);
                    LogThreatDiagnostic(party, component, false, false, true);
                });
        }

        private static void LogSupplyRoute(
            string state,
            MobileParty party,
            SupplyPartyComponent component)
        {
            // Route state changes are frequent across the whole map. Keep the
            // file log focused on transports belonging to the player's kingdom.
            if (!RecruitmentLogFilter.ShouldLog(party) &&
                !RecruitmentLogFilter.ShouldLog(
                    component?.SupplyHomeSettlement?.OwnerClan))
                return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_route");
            message.SetTextVariable("EVENT", TransportLogText.Event(state));
            message.SetTextVariable("PARTY",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("MODE", GetSupplyModeText(component));
            message.SetTextVariable("STATE", GetSupplyStateText(component));
            GetSupplyRouteEndpoints(component, out Settlement routeSource,
                out Settlement routeDestination);
            message.SetTextVariable("SOURCE",
                routeSource?.Name ??
                    TextObject.GetEmpty());
            message.SetTextVariable("DESTINATION",
                routeDestination?.Name ??
                    TextObject.GetEmpty());
            // A route-state transition is useful campaign history but normally
            // requires no player intervention, so keep it as file-only Info.
            ModLogger.Info(message.ToString());
        }

        /// <summary>
        /// Formats route endpoints in travel order. Return journeys must not be
        /// logged as though the party were still traveling outward.
        /// </summary>
        private static void GetSupplyRouteEndpoints(
            SupplyPartyComponent component,
            out Settlement source,
            out Settlement destination)
        {
            source = component?.SupplyHomeSettlement;
            destination = component?.IsAidMission == true
                ? component.AidDestination
                : component?.SourceSettlement;
            if (component == null) return;
            bool returning = component.MissionState ==
                    SupplyPartyMissionState.ReturningHome ||
                component.MissionState ==
                    SupplyPartyMissionState.ReturningFromAid;
            if (!returning) return;
            Settlement outwardDestination = destination;
            destination = source;
            source = outwardDestination;
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

        private static bool HasActiveLocalForagingMission(
            Settlement destination)
        {
            MobileParty active = FindSupplyPartyForHome(destination);
            return active?.PartyComponent is SupplyPartyComponent component &&
                !component.IsAidMission &&
                component.MissionState != SupplyPartyMissionState.Completed;
        }

        /// <summary>
        /// Performs a read-only feasibility check for local foraging. A dispatch
        /// cooldown deliberately is not considered proof that local supply can
        /// satisfy the settlement, because doing so would suppress valid aid.
        /// </summary>
        private bool CanActuallyDispatchLocalForaging(
            Settlement destination)
        {
            if (!CanDispatchFrom(destination) ||
                HasActiveLocalForagingMission(destination))
                return false;
            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(destination.Town);
            if (!GarrisonLogisticsModel.NeedsReplenishment(status) ||
                !ShouldDispatchAfterLocalSupply(destination, status))
                return false;
            SourceSelection source = FindBestSource(destination,
                status.FoodDeficit, null);
            if (source == null ||
                GetSafeAidEscortCount(destination) < EscortSize)
                return false;

            // Before a real party exists, use the configured escort size and
            // the native food ratio as a conservative lower-bound estimate.
            float peoplePerFood = Campaign.Current.Models
                .MobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood;
            if (peoplePerFood <= 0f) peoplePerFood = 10f;
            const float conservativeMapSpeed = 3f;
            float distance = GetRouteDistance(destination,
                source.Settlement);
            int requiredDays = Math.Max(OutboundProvisionDays,
                (int)Math.Ceiling(distance / conservativeMapSpeed / 24f * 2f
                    + ProvisionSafetyDays));
            int minimumFood = (int)Math.Ceiling(
                EscortSize / peoplePerFood * requiredDays);
            int residentReserve = (int)Math.Ceiling(Math.Max(0f,
                status.DailyConsumption));
            int available = Math.Max(0,
                destination.Town.GarrisonParty.ItemRoster.TotalFood -
                    residentReserve);
            return available >= minimumFood;
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
            ModLogger.Info(message.ToString());
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
            SupplyProvisionPlan provisionPlan = null;

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
                provisionPlan = TryLoadOutboundProvisions(
                    home,
                    source.Settlement,
                    garrison,
                    supplyParty);
                LogProvisionPlan(home, source.Settlement, provisionPlan);
                if (!provisionPlan.CanDepart)
                {
                    RollbackSupplyDeparture(home, garrison, supplyParty,
                        escortRoster, packAnimals.Purchased,
                        packAnimals.GoldCost);
                    SetDispatchCooldown(home, 1f);
                    LogProvisionRejected(home, source.Settlement,
                        provisionPlan);
                    return false;
                }
                MoveToSettlement(supplyParty, source.Settlement,
                    "InitialDispatch");
            }
            catch (Exception exception)
            {
                RollbackSupplyDeparture(home, garrison, supplyParty,
                    escortRoster, packAnimals.Purchased,
                    packAnimals.GoldCost);
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
                message.SetTextVariable("PROVISIONS",
                    provisionPlan?.PlannedLoad ?? 0);
                message.SetTextVariable("PROVISION_DAYS",
                    provisionPlan?.RequiredDays ?? 0);
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

        private void CompleteMission(
            MobileParty party,
            SupplyPartyComponent component)
        {
            _recoveryService.CompleteReturn(
                party, component, Cargo, LogSupplyEscortCapacity);
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
                excess > 0
                    ? "str_modifiedarmy_supply_escort_capacity_overflow"
                    : "str_modifiedarmy_supply_escort_capacity_returned");
            message.SetTextVariable(
                "SETTLEMENT_NAME",
                home?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable(
                "PARTY_NAME",
                supplyParty?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("GARRISON_BEFORE", garrisonBefore);
            message.SetTextVariable("RETURNING", returningTroops);
            message.SetTextVariable("RESERVED_BEFORE", reservedBefore);
            message.SetTextVariable("GARRISON_AFTER", garrisonAfter);
            message.SetTextVariable("RESERVED_AFTER", reservedAfter);
            message.SetTextVariable("LIMIT", partySizeLimit);
            message.SetTextVariable("EXCESS", excess);
            ModLogger.Debug(message.ToString());
        }

        private static void RestoreTroops(
            TroopRoster source,
            TroopRoster destination)
        {
            Cargo.RestoreTroops(source, destination);
        }

        private static void TransferAllItems(
            ItemRoster source,
            ItemRoster destination)
        {
            Cargo.TransferAllItems(source, destination);
        }

        private static int TransferFood(
            ItemRoster source,
            ItemRoster destination,
            int requestedAmount)
        {
            return Cargo.TransferFood(source, destination, requestedAmount);
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
            return _dispatchService.IsCoolingDown(
                _nextDispatchTimes, home);
        }

        private void SetDispatchCooldown(
            Settlement home,
            float days)
        {
            _dispatchService.SetCooldown(
                _nextDispatchTimes, home, days);
        }

        private void EnsureMovingTo(
            MobileParty party,
            Settlement destination,
            string reason = "MissionStateCheck")
        {
            if (destination == null
                || party.CurrentSettlement == destination
                || (party.DefaultBehavior == AiBehavior.GoToSettlement
                    && party.TargetSettlement == destination))
            {
                return;
            }

            MoveToSettlement(party, destination, reason);
        }

        private void MoveToSettlement(
            MobileParty party,
            Settlement destination,
            string reason = "MissionStateChange")
        {
            if (party == null || destination == null)
            {
                return;
            }

            LogRouteCommand(party, destination, reason);

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
