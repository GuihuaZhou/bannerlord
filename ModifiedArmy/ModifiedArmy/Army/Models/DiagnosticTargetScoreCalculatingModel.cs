using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
using ModifiedArmy.Army.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using CampaignArmy = TaleWorlds.CampaignSystem.Army;

namespace ModifiedArmy.Army.Models
{
    /// <summary>
    /// Delegates every unrelated score to the installed model chain. For a
    /// sampled land siege only, executes the audited 1.4.7 formula once and
    /// records the actual branch locals used to produce the returned score.
    /// </summary>
    public sealed class DiagnosticTargetScoreCalculatingModel
        : TargetScoreCalculatingModel
    {
        public override float TravelingToAssignmentFactor =>
            BaseModel.TravelingToAssignmentFactor;
        public override float BesiegingFactor => BaseModel.BesiegingFactor;
        public override float AssaultingTownFactor => BaseModel.AssaultingTownFactor;
        public override float RaidingFactor => BaseModel.RaidingFactor;
        public override float DefendingFactor => BaseModel.DefendingFactor;
        public override float GetDefensivePatrollingFactor(bool naval) =>
            BaseModel.GetDefensivePatrollingFactor(naval);
        public override float GetOffensivePatrollingFactor(bool naval) =>
            BaseModel.GetOffensivePatrollingFactor(naval);
        public override float CalculateDefensivePatrollingScoreForSettlement(
            Settlement settlement, bool targetingPort, MobileParty party) =>
            BaseModel.CalculateDefensivePatrollingScoreForSettlement(
                settlement, targetingPort, party);
        public override float CalculateOffensivePatrollingScoreForSettlement(
            Settlement settlement, bool targetingPort, MobileParty party) =>
            BaseModel.CalculateOffensivePatrollingScoreForSettlement(
                settlement, targetingPort, party);
        public override float CurrentObjectiveValue(MobileParty party) =>
            BaseModel.CurrentObjectiveValue(party);

        public override float GetTargetScoreForFaction(
            Settlement target,
            CampaignArmy.ArmyTypes missionType,
            MobileParty party,
            float ourStrength)
        {
            if (missionType != CampaignArmy.ArmyTypes.Besieger ||
                target?.IsFortification != true || party == null ||
                !MarshalArmyDebugTrace.TryGetTargetContext(
                    party, missionType, out string traceId))
                return BaseModel.GetTargetScoreForFaction(
                    target, missionType, party, ourStrength);

            if (!IsVerifiedBaseChain(out string chain))
            {
                float delegated = BaseModel.GetTargetScoreForFaction(
                    target, missionType, party, ourStrength);
                MarshalArmyDebugTrace.RecordTargetResult(
                    traceId, target, party,
                    "armyType=Besieger calculationMode=DELEGATED_UNVERIFIED " +
                    "activeScoreModel=" + GetType().FullName +
                    " baseModelChain=\"" + chain + "\" ourSiegeStrength=" +
                    MarshalArmyDebugTrace.F(ourStrength) +
                    " returnedScore=" + MarshalArmyDebugTrace.F(delegated) +
                    " zeroReasonCode=" + (delegated == 0f
                        ? "ZERO_REASON_UNCONFIRMED" : "POSITIVE_SCORE") +
                    " decisionStage=MODEL_CHAIN_UNVERIFIED");
                return delegated;
            }

            return CalculateInstrumentedSiege(
                target, party, ourStrength, traceId, chain);
        }

        private float CalculateInstrumentedSiege(
            Settlement target,
            MobileParty party,
            float ourStrength,
            string traceId,
            string chain)
        {
            IFaction faction = party.MapFaction;
            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party, target, false, out MobileParty.NavigationType navigation,
                out float distanceToTarget, out bool isFromPort);

            float localStrength = 0f;
            float garrisonStrength = 0f;
            float militiaStrength = 0f;
            float otherLocalStrength = 0f;
            float localLordStrength = 0f;
            bool playerInside = Hero.MainHero.CurrentSettlement == target;
            foreach (MobileParty local in target.Parties)
            {
                if (local.Aggressiveness <= 0.01f && !local.IsGarrison &&
                    !local.IsMilitia)
                    continue;
                float mainPartyFactor = local == MobileParty.MainParty
                    ? 0.5f
                    : local.Army?.LeaderParty == MobileParty.MainParty ? 0.8f : 1f;
                float playerInsideFactor = playerInside ? 0.8f : 1f;
                float strength = mainPartyFactor * playerInsideFactor *
                    local.Party.EstimatedStrength;
                localStrength += strength;
                if (local.IsGarrison) garrisonStrength += strength;
                else if (local.IsMilitia) militiaStrength += strength;
                else otherLocalStrength += strength;
                if (!local.IsGarrison && !local.IsMilitia &&
                    local.LeaderHero != null)
                    localLordStrength += strength;
            }

            float initialKingdomSupport = 0f;
            float supportMultiplier = party.BesiegedSettlement != target
                ? (target.IsTown ? 4f : 3f) : 1f;
            const float settlementCaptureFactor = 0.7f;
            supportMultiplier *= 1f - 0.6f *
                (1f - settlementCaptureFactor) * (1f - settlementCaptureFactor);
            if (localStrength < 100f)
                supportMultiplier *= 0.5f + 0.5f * (localStrength / 100f);
            float enemyKingdomStrength = target.MapFaction.IsKingdomFaction
                ? target.MapFaction.CurrentTotalStrength : 0f;
            int enemySettlementCount = target.MapFaction.IsKingdomFaction
                ? target.MapFaction.Settlements.Count : 0;
            if ((party.MapEvent == null ||
                 party.MapEvent.MapEventSettlement != target) &&
                target.MapFaction.IsKingdomFaction)
            {
                float supportBase = (enemyKingdomStrength * 0.25f -
                    localStrength - localLordStrength) /
                    (enemySettlementCount + 10f);
                initialKingdomSupport = MathF.Max(0f, supportBase) *
                    supportMultiplier;
            }

            int wallLevel = target.Town.GetWallLevel();
            float wallFactor = 1f + 0.33f * wallLevel;
            float foodStocks = target.Town.FoodStocks;
            if (foodStocks < 100f)
                wallFactor -= 0.5f * (wallFactor - 1f) *
                    ((100f - foodStocks) / 100f);
            float randomFactor = party.LeaderHero != null
                ? party.LeaderHero.RandomFloat(0.1f) +
                  (MathF.Max(MathF.Min(1.2f, party.Aggressiveness), 0.8f) -
                   0.8f) * 0.5f
                : 0f;
            float initialDefense = localStrength *
                (wallFactor - randomFactor) + initialKingdomSupport + 0.1f;
            float safetyFactor = party.SiegeEvent != null &&
                party.BesiegedSettlement == target ? 1.5f : 2f;
            float requiredStrength = initialDefense * safetyFactor;
            bool passedInitialGate = target.SiegeEvent != null ||
                ourStrength >= requiredStrength;
            if (!passedInitialGate)
            {
                LogResult(traceId, target, party, chain, ourStrength,
                    localStrength, garrisonStrength, militiaStrength,
                    otherLocalStrength, enemyKingdomStrength,
                    enemySettlementCount, initialKingdomSupport, 0f,
                    wallLevel, wallFactor, foodStocks, randomFactor,
                    initialDefense, safetyFactor, requiredStrength,
                    requiredStrength - ourStrength, false, 0f,
                    initialDefense, 0f, 0f, 0f, 0f, 0f, 0f, 0f,
                    "ZERO_INITIAL_SIEGE_STRENGTH_GATE", "none",
                    "INITIAL_GATE", chain);
                return 0f;
            }

            float nearbyEnemyStrength = 0f;
            float searchRadius = Campaign.Current.EstimatedAverageLordPartySpeed * 2f;
            float averageTownDistance = Campaign.Current
                .GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(
                    MobileParty.NavigationType.All);
            if (distanceToTarget < Math.Max(averageTownDistance, searchRadius))
            {
                LocatableSearchData<MobileParty> search =
                    MobileParty.StartFindingLocatablesAroundPosition(
                        target.GatePosition.ToVec2(), searchRadius);
                for (MobileParty enemy = MobileParty.FindNextLocatable(ref search);
                     enemy != null;
                     enemy = MobileParty.FindNextLocatable(ref search))
                {
                    if (enemy.CurrentSettlement == target ||
                        enemy.Aggressiveness <= 0.01f ||
                        enemy.MapFaction != target.Party.MapFaction ||
                        enemy.IsMainParty && enemy.ShouldBeIgnored)
                        continue;
                    float playerFactor = enemy == MobileParty.MainParty ||
                        enemy.Army?.LeaderParty == MobileParty.MainParty ? 0.5f : 1f;
                    float enemyDistance;
                    if (enemy.CurrentSettlement != null)
                        enemyDistance = Campaign.Current.Models.MapDistanceModel
                            .GetDistance(enemy.CurrentSettlement, target, false,
                                false, MobileParty.NavigationType.All);
                    else
                        enemyDistance = Campaign.Current.Models.MapDistanceModel
                            .GetDistance(enemy, target, false,
                                MobileParty.NavigationType.All, out float waterDistance);
                    float proximity = MBMath.ClampFloat(
                        searchRadius / (enemyDistance + 0.001f), 0.3f, 1.2f);
                    nearbyEnemyStrength += proximity *
                        enemy.Party.EstimatedStrength * playerFactor;
                }
            }
            if (nearbyEnemyStrength < ourStrength)
                nearbyEnemyStrength = MathF.Max(0f,
                    nearbyEnemyStrength - ourStrength * 0.33f);
            float projectedDefense = initialDefense + nearbyEnemyStrength -
                initialKingdomSupport;
            float subsequentKingdomSupport = 0f;
            if (target.MapFaction.IsKingdomFaction)
            {
                float supportBase = (enemyKingdomStrength * 0.5f -
                    (localLordStrength + nearbyEnemyStrength)) /
                    (enemySettlementCount + 10f);
                subsequentKingdomSupport = MathF.Max(0f, supportBase) *
                    supportMultiplier;
            }
            projectedDefense += subsequentKingdomSupport;
            float relativeStrengthFactor = ourStrength / projectedDefense;
            relativeStrengthFactor = relativeStrengthFactor > 2f
                ? 2f : relativeStrengthFactor;
            float distanceFactor = MBMath.Map(
                (5f * averageTownDistance - distanceToTarget) /
                averageTownDistance, 0f, 5f, 0.9f, 10f);
            float targetValue = target.GetSettlementValueForFaction(faction);
            float valueFactor = MathF.Pow(targetValue / 50000f, 0.33f);
            const float objectiveFactor = 0.8f;
            float relationFactor = 1f;
            if (target.OwnerClan != null && party.LeaderHero != null)
            {
                int relation = party.LeaderHero.Clan
                    .GetRelationWithClan(target.OwnerClan);
                if (relation > 0)
                    relationFactor = 1f - 0.4f *
                        (MathF.Sqrt(relation) / 10f);
                else if (relation < 0)
                    relationFactor = 1f + 0.1f *
                        (MathF.Sqrt(-relation) / 10f);
            }
            float strategyFactor = 1f;
            if (party.MapFaction?.IsKingdomFaction == true &&
                party.MapFaction.Leader == Hero.MainHero)
            {
                StanceLink stance = Hero.MainHero.MapFaction
                    .GetStanceWith(target.MapFaction);
                if (stance?.BehaviorPriority == 1) strategyFactor = 0.65f;
                else if (stance?.BehaviorPriority == 2) strategyFactor = 1.3f;
            }
            float behaviorFactor = 1f;
            if (party.TargetSettlement == target)
            {
                behaviorFactor = 1.3f;
                if (party.SiegeEvent?.BesiegedSettlement == target)
                    behaviorFactor = 4f;
            }
            float siegeOverlapFactor = 1f;
            if (party.SiegeEvent == null && target.SiegeEvent != null &&
                target.SiegeEvent.BesiegerCamp.MapFaction == party.MapFaction)
            {
                float attacker = target.SiegeEvent.BesiegerCamp
                    .GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)
                    .Sum(item => item.EstimatedStrength);
                float defender = target
                    .GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege)
                    .Sum(item => item.EstimatedStrength);
                if (defender > 0f)
                {
                    float overlap = attacker / defender - 5f;
                    if (overlap > 0f)
                    {
                        overlap = MBMath.ClampFloat(overlap, 0f, 1f);
                        siegeOverlapFactor = MBMath.Map(
                            overlap, 0f, 1f, 1f, 0f);
                    }
                }
                else siegeOverlapFactor = 0f;
            }
            float rawScore = relationFactor * relativeStrengthFactor *
                valueFactor * 1f * objectiveFactor * strategyFactor *
                behaviorFactor * siegeOverlapFactor * distanceFactor;
            if (party.Objective == MobileParty.PartyObjective.Aggressive)
                rawScore *= 1.2f;
            float returnedScore = rawScore < 0f ? 0f : rawScore;
            string zeroFactors = string.Join(",", new[]
            {
                relationFactor == 0f ? "relationFactor" : null,
                relativeStrengthFactor == 0f ? "relativeStrengthFactor" : null,
                valueFactor == 0f ? "valueFactor" : null,
                objectiveFactor == 0f ? "objectiveFactor" : null,
                strategyFactor == 0f ? "strategyFactor" : null,
                behaviorFactor == 0f ? "behaviorFactor" : null,
                siegeOverlapFactor == 0f ? "siegeOverlapFactor" : null,
                distanceFactor == 0f ? "distanceFactor" : null
            }.Where(item => item != null));
            string reason = float.IsNaN(returnedScore) ||
                float.IsInfinity(returnedScore)
                    ? "NONFINITE_SCORE"
                    : rawScore < 0f
                        ? "ZERO_FINAL_NEGATIVE_CLAMP"
                        : returnedScore == 0f && zeroFactors.Length > 0
                            ? "ZERO_FINAL_MULTIPLIER"
                            : returnedScore == 0f
                                ? "ZERO_FINAL_SCORE_OTHER"
                                : "POSITIVE_SCORE";
            LogResult(traceId, target, party, chain, ourStrength,
                localStrength, garrisonStrength, militiaStrength,
                otherLocalStrength, enemyKingdomStrength,
                enemySettlementCount, initialKingdomSupport,
                subsequentKingdomSupport, wallLevel, wallFactor, foodStocks,
                randomFactor, initialDefense, safetyFactor, requiredStrength,
                MathF.Max(0f, requiredStrength - ourStrength), true,
                nearbyEnemyStrength, projectedDefense, relativeStrengthFactor,
                relationFactor, valueFactor, objectiveFactor, distanceFactor,
                siegeOverlapFactor, rawScore, reason,
                zeroFactors.Length == 0 ? "none" : zeroFactors,
                "FINAL_SCORE", chain, returnedScore, strategyFactor,
                behaviorFactor);
            return returnedScore;
        }

        private static void LogResult(
            string traceId, Settlement target, MobileParty party,
            string baseChain, float ourStrength, float localStrength,
            float garrison, float militia, float otherLocal,
            float enemyKingdomStrength, int enemySettlementCount,
            float initialSupport, float subsequentSupport, int wallLevel,
            float wallFactor, float foodStocks, float randomFactor,
            float initialDefense, float safetyFactor, float requiredStrength,
            float shortfall, bool passed, float nearbyEnemy,
            float projectedDefense, float relativeStrength,
            float relationFactor, float valueFactor, float objectiveFactor,
            float distanceFactor, float overlapFactor, float rawScore,
            string reason, string zeroFactors, string stage, string chain,
            float returnedScore = 0f, float strategyFactor = 0f,
            float behaviorFactor = 0f)
        {
            string F(float value) => MarshalArmyDebugTrace.F(value);
            MarshalArmyDebugTrace.RecordTargetResult(traceId, target, party,
                "armyType=Besieger calculationMode=INSTRUMENTED_SINGLE_PASS " +
                "activeScoreModel=" + typeof(DiagnosticTargetScoreCalculatingModel).FullName +
                " baseModelChain=\"" + baseChain + "\" " +
                "ourSiegeStrength=" + F(ourStrength) +
                " localStrength=" + F(localStrength) +
                " garrisonStrength=" + F(garrison) +
                " militiaStrength=" + F(militia) +
                " otherLocalStrength=" + F(otherLocal) +
                " enemyKingdomStrength=" + F(enemyKingdomStrength) +
                " enemySettlementCount=" + enemySettlementCount +
                " initialKingdomSupport=" + F(initialSupport) +
                " subsequentKingdomSupport=" + F(subsequentSupport) +
                " wallLevel=" + wallLevel + " wallFactor=" + F(wallFactor) +
                " foodStocks=" + F(foodStocks) +
                " randomFactor=" + F(randomFactor) +
                " targetHasActiveSiege=" + (target.SiegeEvent != null) +
                " initialDefenseStrength=" + F(initialDefense) +
                " safetyFactor=" + F(safetyFactor) +
                " requiredSiegeStrength=" + F(requiredStrength) +
                " strengthShortfall=" + F(shortfall) +
                " passedInitialGate=" + passed +
                " nearbyEnemyStrength=" + F(nearbyEnemy) +
                " projectedFinalDefense=" + F(projectedDefense) +
                " relativeStrengthFactor=" + F(relativeStrength) +
                " relationFactor=" + F(relationFactor) +
                " valueFactor=" + F(valueFactor) +
                " objectiveFactor=" + F(objectiveFactor) +
                " distanceFactor=" + F(distanceFactor) +
                " strategyFactor=" + F(strategyFactor) +
                " behaviorFactor=" + F(behaviorFactor) +
                " siegeOverlapFactor=" + F(overlapFactor) +
                " rawScore=" + F(rawScore) +
                " returnedScore=" + F(returnedScore) +
                " zeroReasonCode=" + reason +
                " zeroFactorName=" + zeroFactors +
                " decisionStage=" + stage);
        }

        internal bool IsVerifiedBaseChain(out string chain)
        {
            var names = new List<string>();
            var seen = new HashSet<object>();
            object current = BaseModel;
            bool valid = current != null;
            while (current != null && names.Count < 12)
            {
                if (!seen.Add(current)) { valid = false; break; }
                string name = current.GetType().FullName ?? "unknown";
                names.Add(name);
                bool allowed = name ==
                        "TaleWorlds.CampaignSystem.GameComponents.DefaultTargetScoreCalculatingModel" ||
                    name ==
                        "NavalDLC.GameComponents.NavalTargetScoreCalculatingModel";
                if (!allowed) valid = false;
                PropertyInfo property = current.GetType().GetProperty(
                    "BaseModel", BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                current = property?.GetValue(current);
            }
            chain = string.Join(" -> ", names);
            return valid && names.Any(name => name.EndsWith(
                ".DefaultTargetScoreCalculatingModel",
                StringComparison.Ordinal));
        }
    }
}
