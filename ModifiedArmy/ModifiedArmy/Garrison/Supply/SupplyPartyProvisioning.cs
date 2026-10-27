using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Garrison.Supply.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
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
    /// Contains the non-persistent planning and diagnostics used by supply
    /// missions. None of these values are save data; they are rebuilt from the
    /// party, settlement, and route whenever the game needs them.
    /// </summary>
    public partial class SupplyPartyBehavior
    {
        private SupplyProvisionPlan TryLoadOutboundProvisions(
            Settlement home,
            Settlement source,
            MobileParty garrison,
            MobileParty supplyParty)
        {
            float distance = GetRouteDistance(home, source);
            float speed = Math.Max(0.1f, supplyParty.Speed);
            float outboundDays = Math.Max(0f, distance / speed / 24f);
            float returnDays = outboundDays;
            float dailyDemand = GetDailyFoodDemand(supplyParty);
            int existingFood = supplyParty.ItemRoster.TotalFood;
            int granaryFood = garrison?.ItemRoster?.TotalFood ?? 0;
            // Keep one day of the resident garrison's current demand. This is
            // intentionally smaller than the aid-route reserve: a starving
            // settlement must still be able to organize a short local trip,
            // while the last daily ration is never emptied into the convoy.
            int granaryReserve = (int)Math.Ceiling(Math.Max(0f,
                GarrisonLogisticsModel.Calculate(home.Town)
                    .DailyConsumption));
            int available = Math.Max(0, granaryFood - granaryReserve);
            int capacityLimited = CalculateFoodCapacity(
                garrison?.ItemRoster, supplyParty);
            SupplyProvisionPlan planned = SupplyProvisionModel.Plan(
                new SupplyProvisionInput
                {
                    RouteDistance = distance,
                    TravelDaysToSource = outboundDays,
                    EstimatedReturnDays = returnDays,
                    DailyFoodDemand = dailyDemand,
                    MinimumProvisionDays = OutboundProvisionDays,
                    SafetyDays = ProvisionSafetyDays,
                    ExistingFood = existingFood,
                    AvailableGranaryFood = available,
                    GranaryReserveFood = granaryReserve,
                    CargoCapacity = capacityLimited
                });
            int loaded = TransferFoodWithinCapacity(garrison.ItemRoster,
                supplyParty.ItemRoster, planned.PlannedLoad, supplyParty);
            int finalFood = supplyParty.ItemRoster.TotalFood;

            return planned.WithCommitResult(loaded, finalFood);
        }

        /// <summary>
        /// Uses the native food-change result when available. During the short
        /// initialization window where it is zero or invalid, the escort count
        /// and the campaign food ratio provide a conservative fallback.
        /// </summary>
        private static float GetDailyFoodDemand(MobileParty party)
        {
            float foodChange = party?.FoodChange ?? 0f;
            if (!float.IsNaN(foodChange) && !float.IsInfinity(foodChange) &&
                foodChange < -0.01f)
                return -foodChange;

            float peoplePerFood = Campaign.Current.Models
                .MobilePartyFoodConsumptionModel.NumberOfMenOnMapToEatOneFood;
            if (peoplePerFood <= 0f) peoplePerFood = 10f;
            return Math.Max(0.1f,
                (party?.MemberRoster?.TotalManCount ?? EscortSize) /
                    peoplePerFood);
        }

        private static float GetRouteDistance(
            Settlement from,
            Settlement to)
        {
            if (from == null || to == null) return 0f;
            float distance = Campaign.Current.Models.MapDistanceModel
                .GetDistance(from, to, false, false,
                    MobileParty.NavigationType.Default);
            return float.IsNaN(distance) || float.IsInfinity(distance)
                ? 0f
                : Math.Max(0f, distance);
        }

        /// <summary>
        /// Calculates how many actual food items fit without making the party
        /// exceed its current inventory capacity. Each source stack keeps its
        /// own weight, so this is safer than assuming all food weighs the same.
        /// </summary>
        private static int CalculateFoodCapacity(
            ItemRoster source,
            MobileParty party)
        {
            return Cargo.CalculateFoodCapacity(source, party);
        }

        private static int TransferFoodWithinCapacity(
            ItemRoster source,
            ItemRoster destination,
            int requested,
            MobileParty party = null)
        {
            return Cargo.TransferFoodWithinCapacity(
                source, destination, requested, party);
        }

        /// <summary>
        /// Restores every reversible resource from a failed departure exactly
        /// once: escort troops, all inventory, and pack-animal purchase gold.
        /// </summary>
        private static void RollbackSupplyDeparture(
            Settlement home,
            MobileParty garrison,
            MobileParty supplyParty,
            TroopRoster detachedEscort,
            int purchasedPackAnimals,
            int refundableGold)
        {
            Cargo.RollbackDeparture(home, garrison, supplyParty,
                detachedEscort, purchasedPackAnimals, refundableGold,
                LogSupplyEscortCapacity);
        }

        /// <summary>
        /// Purchased pack animals were created in exchange for gold rather than
        /// removed from the granary. When that payment is refunded they must be
        /// removed before the remaining inventory is restored, or the rollback
        /// would create free animals.
        /// </summary>
        private static void RemovePurchasedPackAnimals(
            ItemRoster roster,
            int requested)
        {
            Cargo.RemovePurchasedPackAnimalsForRecovery(roster, requested);
        }

        private static void LogProvisionPlan(
            Settlement home,
            Settlement source,
            SupplyProvisionPlan plan)
        {
            if (!RecruitmentLogFilter.ShouldLog(home?.OwnerClan)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_provision_plan");
            SetProvisionVariables(message, home, source, plan);
            ModLogger.Debug(message.ToString());
        }

        private static void LogProvisionRejected(
            Settlement home,
            Settlement source,
            SupplyProvisionPlan plan)
        {
            if (!RecruitmentLogFilter.ShouldLog(home?.OwnerClan)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_provision_rejected");
            SetProvisionVariables(message, home, source, plan);
            ModLogger.Warn(message.ToString());
        }

        private static void SetProvisionVariables(
            TextObject message,
            Settlement home,
            Settlement source,
            SupplyProvisionPlan plan)
        {
            message.SetTextVariable("HOME_NAME", home?.Name ??
                TextObject.GetEmpty());
            message.SetTextVariable("SOURCE_NAME", source?.Name ??
                TextObject.GetEmpty());
            message.SetTextVariable("DISTANCE", plan?.RouteDistance ?? 0f);
            message.SetTextVariable("TRAVEL_DAYS",
                plan?.TravelDaysToSource ?? 0f);
            message.SetTextVariable("RETURN_DAYS",
                plan?.EstimatedReturnDays ?? 0f);
            message.SetTextVariable("SAFETY_DAYS", plan?.SafetyDays ?? 0);
            message.SetTextVariable("REQUIRED_DAYS", plan?.RequiredDays ?? 0);
            message.SetTextVariable("DAILY_DEMAND",
                plan?.DailyFoodDemand ?? 0f);
            message.SetTextVariable("REQUIRED", plan?.RequiredFood ?? 0);
            message.SetTextVariable("AVAILABLE",
                plan?.AvailableGranaryFood ?? 0);
            message.SetTextVariable("RESERVE",
                plan?.GranaryReserveFood ?? 0);
            message.SetTextVariable("LOADED", plan?.PlannedLoad ?? 0);
            message.SetTextVariable("REASON",
                GetProvisionReasonText(plan?.Reason));
        }

        private static TextObject GetProvisionReasonText(string reason)
        {
            switch (reason)
            {
                case "InsufficientCargoCapacity":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyProvisionCapacity}" +
                        "insufficient cargo capacity");
                case "InsufficientGranaryFood":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyProvisionGranary}" +
                        "insufficient granary food");
                case "InvalidFoodDemand":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyProvisionDemand}" +
                        "food demand could not be calculated");
                default:
                    return new TextObject(
                        "{=ModifiedArmy_SupplyProvisionReady}ready");
            }
        }

        /// <summary>
        /// Emits a route snapshot only when this behavior is about to replace
        /// an existing movement order. This makes competing AI orders visible
        /// without producing one line for every hourly tick.
        /// </summary>
        private void LogRouteCommand(
            MobileParty party,
            Settlement destination,
            string reason)
        {
            SupplyPartyComponent component = GetComponent(party);
            if (component == null ||
                (!RecruitmentLogFilter.ShouldLog(party) &&
                 !RecruitmentLogFilter.ShouldLog(
                     component.HomeSettlement?.OwnerClan)))
                return;
            _routeController.RecordRouteCommand(party);
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_route_command");
            SetRouteVariables(message, party, component, destination);
            message.SetTextVariable("REASON", GetRouteReasonText(reason));
            ModLogger.Debug(message.ToString());
        }

        /// <summary>
        /// Reports an unexplained target mismatch at most once per party and
        /// campaign day. Threat handling runs before route restoration, so a
        /// legitimate native avoidance order is not classified as a conflict.
        /// </summary>
        private void LogUnexpectedRouteOverride(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement expected)
        {
            if (!_routeController.ShouldReportUnexpectedOverride(
                    party, expected))
                return;
            if (!RecruitmentLogFilter.ShouldLog(party) &&
                !RecruitmentLogFilter.ShouldLog(
                    component.HomeSettlement?.OwnerClan))
                return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_route_mismatch");
            SetRouteVariables(message, party, component, expected);
            ModLogger.Warn(message.ToString());
        }

        private static void SetRouteVariables(
            TextObject message,
            MobileParty party,
            SupplyPartyComponent component,
            Settlement expected)
        {
            bool superiorEnemy = TransportThreatModel.HasSuperiorEnemy(
                party, party.Position, party.MapFaction);
            message.SetTextVariable("PARTY",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("HOME",
                component.HomeSettlement?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("SOURCE",
                component.SourceSettlement?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("STATE", GetSupplyStateText(component));
            message.SetTextVariable("REQUESTED",
                component.RequestedFood);
            message.SetTextVariable("DEFAULT",
                GetAiBehaviorText(party.DefaultBehavior));
            message.SetTextVariable("CURRENT_TARGET",
                GetSettlementNameOrNone(party.TargetSettlement));
            message.SetTextVariable("EXPECTED_TARGET",
                GetSettlementNameOrNone(expected));
            message.SetTextVariable("SHORT_TERM",
                GetAiBehaviorText(party.ShortTermBehavior));
            message.SetTextVariable("SHORT_TARGET",
                party.ShortTermTargetParty?.Name ?? GetNoneText());
            message.SetTextVariable("POSITION",
                party.Position.X.ToString("0.0") + "," +
                party.Position.Y.ToString("0.0"));
            float distance = 0f;
            if (expected != null)
            {
                float deltaX = party.Position.X - expected.GetPosition2D.X;
                float deltaY = party.Position.Y - expected.GetPosition2D.Y;
                distance = (float)Math.Sqrt(deltaX * deltaX +
                    deltaY * deltaY);
            }
            message.SetTextVariable("DISTANCE", distance);
            message.SetTextVariable("SUPERIOR_ENEMY",
                GetSuperiorEnemyText(superiorEnemy));
        }

        /// <summary>
        /// Captures one health sample per campaign day, plus immediate samples
        /// when starvation or wounded counts change. It deliberately does not
        /// claim that starvation caused wounds; it only records chronology.
        /// </summary>
        private void LogDailySupplyHealth(
            MobileParty party,
            SupplyPartyComponent component)
        {
            int day = (int)Math.Floor(CampaignTime.Now.ToDays);
            int wounded = 0;
            foreach (TroopRosterElement element in
                party.MemberRoster.GetTroopRoster())
                wounded += Math.Max(0, element.WoundedNumber);
            bool starving = party.Party.IsStarving;
            if (_lastHealthSnapshots.TryGetValue(party.StringId,
                    out SupplyHealthSnapshot previous) &&
                previous.Day == day && previous.Wounded == wounded &&
                previous.Starving == starving)
                return;
            _lastHealthSnapshots[party.StringId] =
                new SupplyHealthSnapshot(day, wounded, starving);
            if (!RecruitmentLogFilter.ShouldLog(party) &&
                !RecruitmentLogFilter.ShouldLog(
                    component.HomeSettlement?.OwnerClan))
                return;
            float demand = GetDailyFoodDemand(party);
            float foodDays = demand <= 0f ? 0f :
                party.ItemRoster.TotalFood / demand;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_health");
            message.SetTextVariable("PARTY",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("TOTAL",
                party.MemberRoster.TotalManCount);
            message.SetTextVariable("WOUNDED", wounded);
            message.SetTextVariable("FOOD", party.ItemRoster.TotalFood);
            message.SetTextVariable("FOOD_CHANGE",
                Math.Abs(party.FoodChange));
            message.SetTextVariable("FOOD_DAYS", foodDays);
            message.SetTextVariable("STARVING",
                starving
                    ? new TextObject(
                        "{=ModifiedArmy_SupplyStarvingYes}still marked as starving")
                    : new TextObject(
                        "{=ModifiedArmy_SupplyStarvingNo}not starving"));
            message.SetTextVariable("STATE", GetSupplyStateText(component));
            message.SetTextVariable("TARGET",
                GetSettlementNameOrNone(party.TargetSettlement));
            ModLogger.Debug(message.ToString());
        }

        /// <summary>
        /// Re-evaluates only the current leg when provisions are unsafe. The
        /// handler prefers a nearer legal food source, otherwise a nearer home,
        /// and leaves the current order intact when it is already the safest
        /// reachable option. It never creates emergency food.
        /// </summary>
        private void HandleLocalMissionFoodRisk(
            MobileParty party,
            SupplyPartyComponent component)
        {
            Settlement expected = component.MissionState ==
                    SupplyPartyMissionState.TravelingToSource
                ? component.SourceSettlement
                : component.MissionState ==
                    SupplyPartyMissionState.ReturningHome
                    ? component.HomeSettlement
                    : null;
            if (expected == null) return;

            float speed = Math.Max(0.1f, party.Speed);
            float expectedDistance = GetStraightLineDistance(party,
                expected);
            float requiredDays = expectedDistance / speed / 24f + 1f;
            float demand = GetDailyFoodDemand(party);
            float foodDays = demand <= 0f ? 0f :
                party.ItemRoster.TotalFood / demand;
            // IsStarving can remain true until the native daily food tick even
            // after a source has loaded ample food. Actual food coverage is the
            // authoritative safety check; the flag remains diagnostic only.
            if (foodDays >= requiredDays) return;

            Settlement replacement = null;
            string action = "ContinueToNearestSafeTarget";
            if (component.MissionState ==
                SupplyPartyMissionState.TravelingToSource)
            {
                replacement = FindNearestEmergencyFoodSource(party,
                    component.HomeSettlement, component);
                if (replacement != null && replacement != expected &&
                    GetStraightLineDistance(party, replacement) <
                        expectedDistance)
                {
                    component.ChangeSource(replacement);
                    MoveToSettlement(party, replacement,
                        "EmergencyFoodSource");
                    action = "ChangedToNearerFoodSource";
                }
                else
                {
                    replacement = null;
                }
            }

            if (replacement == null && component.HomeSettlement != null &&
                component.MissionState !=
                    SupplyPartyMissionState.ReturningHome &&
                GetStraightLineDistance(party, component.HomeSettlement) <
                    expectedDistance)
            {
                component.BeginReturnJourney();
                MoveToSettlement(party, component.HomeSettlement,
                    "UnsafeFoodReturn");
                action = "ReturningHome";
            }

            LogFoodRisk(party, component, foodDays, requiredDays, action);
        }

        private static Settlement FindNearestEmergencyFoodSource(
            MobileParty party,
            Settlement home,
            SupplyPartyComponent component)
        {
            Settlement best = null;
            float bestDistance = float.MaxValue;
            foreach (Settlement candidate in Settlement.All)
            {
                if (!IsValidSource(home, candidate) ||
                    component.HasVisited(candidate) ||
                    CalculateAvailableFood(candidate) <= 0)
                    continue;
                float distance = GetStraightLineDistance(party, candidate);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private static float GetStraightLineDistance(
            MobileParty party,
            Settlement settlement)
        {
            if (party == null || settlement == null) return float.MaxValue;
            float deltaX = party.Position.X - settlement.GetPosition2D.X;
            float deltaY = party.Position.Y - settlement.GetPosition2D.Y;
            return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private void LogFoodRisk(
            MobileParty party,
            SupplyPartyComponent component,
            float foodDays,
            float requiredDays,
            string action)
        {
            int day = (int)Math.Floor(CampaignTime.Now.ToDays);
            if (_lastFoodRiskDay.TryGetValue(party.StringId,
                    out int loggedDay) && loggedDay == day)
                return;
            _lastFoodRiskDay[party.StringId] = day;
            if (!RecruitmentLogFilter.ShouldLog(party) &&
                !RecruitmentLogFilter.ShouldLog(
                    component.HomeSettlement?.OwnerClan))
                return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_food_risk");
            message.SetTextVariable("PARTY",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("FOOD", party.ItemRoster.TotalFood);
            message.SetTextVariable("FOOD_DAYS", foodDays);
            message.SetTextVariable("REQUIRED_DAYS", requiredDays);
            message.SetTextVariable("ACTION",
                GetFoodRiskActionText(action));
            message.SetTextVariable("TARGET",
                GetSettlementNameOrNone(party.TargetSettlement));
            ModLogger.Warn(message.ToString());
        }

        private static TextObject GetFoodRiskActionText(string action)
        {
            switch (action)
            {
                case "ChangedToNearerFoodSource":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyFoodActionCloserSource}" +
                        "heading to a nearer food source");
                case "ReturningHome":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyFoodActionReturnHome}" +
                        "returning home");
                default:
                    return new TextObject(
                        "{=ModifiedArmy_SupplyFoodActionContinue}" +
                        "continuing to the nearest safe target");
            }
        }

        private static TextObject GetRouteReasonText(string reason)
        {
            switch (reason)
            {
                case "InitialDispatch":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyRouteInitialDispatch}" +
                        "initial dispatch");
                case "TravelingToSource":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyRouteToSource}" +
                        "traveling to source");
                case "ReturningHome":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyRouteReturnHome}" +
                        "returning home");
                case "EmergencyFoodSource":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyRouteEmergencySource}" +
                        "emergency food source");
                case "UnsafeFoodReturn":
                    return new TextObject(
                        "{=ModifiedArmy_SupplyRouteUnsafeReturn}" +
                        "unsafe provisions, returning home");
                default:
                    return new TextObject(
                        "{=ModifiedArmy_SupplyRouteStateChange}" +
                        "mission state change");
            }
        }

        private static void LogThreatDiagnostic(
            MobileParty party,
            SupplyPartyComponent component,
            bool nativeAvoidance,
            bool superiorEnemy,
            bool resumed)
        {
            if (!RecruitmentLogFilter.ShouldLog(party) &&
                !RecruitmentLogFilter.ShouldLog(
                    component?.HomeSettlement?.OwnerClan))
                return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_threat_diagnostic");
            message.SetTextVariable("PARTY",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("SUPERIOR_ENEMY",
                GetSuperiorEnemyText(superiorEnemy));
            message.SetTextVariable("SHORT_TERM",
                GetAiBehaviorText(party.ShortTermBehavior));
            message.SetTextVariable("SHORT_TARGET",
                party.ShortTermTargetParty?.Name ?? GetNoneText());
            message.SetTextVariable("STATUS", resumed
                ? new TextObject(
                    "{=ModifiedArmy_SupplyThreatResumed}threat cleared")
                : new TextObject(
                    "{=ModifiedArmy_SupplyThreatPaused}route paused"));
            if (superiorEnemy && !nativeAvoidance)
                ModLogger.Warn(message.ToString());
            else
                ModLogger.Debug(message.ToString());
        }

        private static TextObject GetSettlementNameOrNone(
            Settlement settlement) => settlement?.Name ?? GetNoneText();

        private static TextObject GetNoneText() => new TextObject(
            "{=ModifiedArmy_CommonNone}none");

        private static TextObject GetSuperiorEnemyText(bool value) => value
            ? new TextObject(
                "{=ModifiedArmy_SuperiorEnemyFound}a stronger enemy was found")
            : new TextObject(
                "{=ModifiedArmy_SuperiorEnemyNotFound}" +
                "no stronger enemy was found");

        private static TextObject GetAiBehaviorText(AiBehavior behavior)
        {
            switch (behavior)
            {
                case AiBehavior.GoToSettlement:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorGoToSettlement}" +
                        "traveling to a settlement");
                case AiBehavior.Hold:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorHold}waiting in place");
                case AiBehavior.FleeToPoint:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorFleeToPoint}" +
                        "fleeing to a safe point");
                case AiBehavior.FleeToGate:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorFleeToGate}" +
                        "fleeing to a settlement gate");
                case AiBehavior.FleeToParty:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorFleeToParty}" +
                        "fleeing toward a friendly party");
                case AiBehavior.GoAroundParty:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorGoAroundParty}" +
                        "detouring around another party");
                case AiBehavior.GoToPoint:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorGoToPoint}" +
                        "traveling to a map position");
                case AiBehavior.None:
                    return GetNoneText();
                default:
                    return new TextObject(
                        "{=ModifiedArmy_AiBehaviorOther}another movement order");
            }
        }

        private sealed class SupplyHealthSnapshot
        {
            public SupplyHealthSnapshot(int day, int wounded, bool starving)
            {
                Day = day;
                Wounded = wounded;
                Starving = starving;
            }

            public int Day { get; }
            public int Wounded { get; }
            public bool Starving { get; }
        }
    }
}
