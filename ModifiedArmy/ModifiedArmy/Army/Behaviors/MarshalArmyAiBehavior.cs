using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedArmy.Army.Diagnostics;
using ModifiedArmy.Army.Services;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using CampaignArmy = TaleWorlds.CampaignSystem.Army;

namespace ModifiedArmy.Army.Behaviors
{
    /// <summary>
    /// Adds vanilla military target scoring for appointed marshals and a ruler who leads a party.
    /// </summary>
    public sealed class MarshalArmyAiBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<MobileParty, string> _lastStates = new Dictionary<MobileParty, string>();
        private AiMilitaryBehavior _aiMilitaryBehavior;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, OnAiHourlyTick);
            CampaignEvents.ArmyCreated.AddNonSerializedListener(this, OnArmyCreated);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _aiMilitaryBehavior = Campaign.Current?.GetCampaignBehavior<AiMilitaryBehavior>();
            MarshalArmyEligibilityTrace.AuditRuntimeModelChain();
        }

        private void OnAiHourlyTick(MobileParty party, PartyThinkParams thinkParams)
        {
            if (party?.LeaderHero == null || thinkParams == null || !party.IsActive)
                return;

            Kingdom kingdom = party.MapFaction as Kingdom;
            if (kingdom == null || kingdom.IsEliminated)
                return;

            Hero marshal = party.LeaderHero;
            bool isRuler = marshal == kingdom.Leader;
            if (!isRuler && !IsAppointedMarshal(marshal, kingdom))
                return;

            string traceId = MarshalArmyDebugTrace.BeginDecision(party, out bool detailedTrace);
            if (detailedTrace)
                TraceDecision(party, kingdom, traceId, "ENTRY", "READY_FOR_NATIVE_CHECK", isRuler, true);

            if (marshal == Hero.MainHero)
            {
                TraceDecision(party, kingdom, traceId, "EARLY_EXIT", "PLAYER_MANUAL", isRuler);
                SetState(party, kingdom, "player", "MP_MarshalArmyAiPlayer", "marshal is the player and remains under manual control");
                return;
            }

            if (!isRuler && !ArmyAuthorityProviderRegistry
                    .IsEligibleMarshal(marshal, kingdom))
            {
                TraceDecision(party, kingdom, traceId, "EARLY_EXIT", "OFFICE_INELIGIBLE", isRuler);
                SetState(party, kingdom, "ineligible", "MP_MarshalArmyAiIneligible", "marshal no longer leads an eligible war party");
                return;
            }

            if (party.Army != null)
            {
                bool leadsArmy = party.Army.LeaderParty == party;
                TraceDecision(party, kingdom, traceId, "EARLY_EXIT",
                    leadsArmy ? "ALREADY_LEADING_ARMY" : "ATTACHED_TO_ARMY", isRuler);
                SetState(
                    party,
                    kingdom,
                    leadsArmy ? "army-active" : "attached",
                    leadsArmy ? "MP_MarshalArmyAiArmyActive" : "MP_MarshalArmyAiAttached",
                    leadsArmy ? "marshal already leads an army" : "marshal is attached to another army");
                return;
            }

            if (_aiMilitaryBehavior == null)
                _aiMilitaryBehavior = Campaign.Current?.GetCampaignBehavior<AiMilitaryBehavior>();
            if (_aiMilitaryBehavior == null)
            {
                TraceDecision(party, kingdom, traceId, "EARLY_EXIT", "MISSING_NATIVE_AI", isRuler);
                SetState(party, kingdom, "missing-ai", "MP_MarshalArmyAiMissingBehavior", "native military AI behavior is unavailable");
                return;
            }

            if (party.IsCurrentlyAtSea || party.MapEvent != null
                || party.CurrentSettlement?.SiegeEvent != null)
            {
                string busyReason = party.IsCurrentlyAtSea ? "BUSY_SEA"
                    : party.MapEvent != null ? "BUSY_BATTLE" : "BUSY_SIEGE";
                TraceDecision(party, kingdom, traceId, "EARLY_EXIT", busyReason, isRuler);
                SetState(party, kingdom, "busy", "MP_MarshalArmyAiBusy", "marshal party is currently busy");
                return;
            }

            if (party.PartySizeRatio <= 0.6f)
            {
                TraceDecision(party, kingdom, traceId, "EARLY_EXIT", "LOW_PARTY_RATIO", isRuler);
                SetState(party, kingdom, "strength", "MP_MarshalArmyAiLowStrength", "marshal party strength is below 60 percent");
                return;
            }

            if (!kingdom.FactionsAtWarWith.Any(faction => faction?.Fiefs?.Any() == true))
            {
                TraceDecision(party, kingdom, traceId, "EARLY_EXIT", "NO_ENEMY_FIEF", isRuler);
                SetState(party, kingdom, "peace", "MP_MarshalArmyAiNoTarget", "kingdom has no enemy faction with a fief");
                return;
            }

            // The current game model owns influence, food, distance and callable-party checks.
            // This bypasses only AiMilitaryBehavior's clan-leader identity gate.
            bool canCreateArmy = Campaign.Current.Models.ArmyManagementCalculationModel.CanLordCreateArmy(
                party,
                out MBList<MobileParty> candidates);
            bool instrumented = MarshalArmyEligibilityTrace.TryConsumeResult(
                party, out string eligibilityReason, out string modelTraceId,
                out int candidatesBeforeClear, out int candidatesReturned);
            MarshalArmyDebugTrace.Write(
                "phase=ELIGIBILITY path=ArmyEligibility traceId=" + traceId
                + " modelTraceId=" + modelTraceId
                + " party=" + MarshalArmyDebugTrace.DescribeParty(party)
                + " result=" + canCreateArmy
                + " candidates=" + (candidates?.Count ?? 0)
                + " reason=" + (canCreateArmy ? "ELIGIBILITY_PASS" : eligibilityReason)
                + " reasonUnavailable=" + (!instrumented)
                + " candidatesBeforeClear=" + candidatesBeforeClear
                + " candidatesReturned=" + candidatesReturned
                + " influence=" + MarshalArmyDebugTrace.F(marshal.Clan?.Influence ?? 0f)
                + " foodDays=" + party.GetNumDaysForFoodToLast()
                + " sizeRatio=" + MarshalArmyDebugTrace.F(party.PartySizeRatio)
                + " nativeModel=" + Campaign.Current.Models.ArmyManagementCalculationModel.GetType().FullName
                + " identityAdaptation=MODEL_DIRECT");
            if (!canCreateArmy)
            {
                SetDiagnosticState(
                    kingdom,
                    party,
                    "conditions",
                    "MP_MarshalArmyAiConditionsDetailed",
                    candidates?.Count ?? 0,
                    0,
                    0f,
                    0f);
                return;
            }

            if (candidates == null || candidates.Count == 0)
            {
                SetState(party, kingdom, "members", "MP_MarshalArmyAiNoMembers", "no party can be called to the army");
                return;
            }

            thinkParams.SetArmyMembers(candidates);
            float gatheringStrength = party.Party.GetCustomStrength(
                BattleSideEnum.Attacker,
                MapEvent.PowerCalculationContext.Siege);
            foreach (MobileParty candidate in candidates)
            {
                float candidateStrength = candidate.Party.GetCustomStrength(
                    BattleSideEnum.Attacker,
                    MapEvent.PowerCalculationContext.Siege);
                gatheringStrength += candidateStrength;
                if (detailedTrace)
                {
                    MarshalArmyDebugTrace.Write(
                        "phase=ARMY_CANDIDATE traceId=" + traceId
                        + " party=" + MarshalArmyDebugTrace.DescribeParty(candidate)
                        + " leader=" + MarshalArmyDebugTrace.DescribeHero(candidate.LeaderHero)
                        + " clan=" + (candidate.ActualClan?.Name?.ToString() ?? "none")
                        + " members=" + candidate.MemberRoster.TotalManCount
                        + " sizeRatio=" + MarshalArmyDebugTrace.F(candidate.PartySizeRatio)
                        + " foodDays=" + candidate.GetNumDaysForFoodToLast()
                        + " inArmy=" + (candidate.Army != null)
                        + " attachedTo=" + MarshalArmyDebugTrace.DescribeParty(candidate.Army?.LeaderParty)
                        + " atSea=" + candidate.IsCurrentlyAtSea
                        + " siegeStrength=" + MarshalArmyDebugTrace.F(candidateStrength));
                }
            }

            int totalFoodDays = party.GetNumDaysForFoodToLast();
            int minimumFoodDays = totalFoodDays;
            foreach (MobileParty candidate in candidates)
            {
                int foodDays = candidate.GetNumDaysForFoodToLast();
                totalFoodDays += foodDays;
                minimumFoodDays = System.Math.Min(minimumFoodDays, foodDays);
            }
            int averageFoodDays = totalFoodDays / (candidates.Count + 1);
            float siegeFoodThreshold = Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege;
            if (detailedTrace)
            {
                MarshalArmyDebugTrace.Write(
                    "phase=ARMY_CANDIDATE_SUMMARY traceId=" + traceId
                    + " leader=" + MarshalArmyDebugTrace.DescribeParty(party)
                    + " candidateCount=" + candidates.Count
                    + " gatheringStrength=" + MarshalArmyDebugTrace.F(gatheringStrength)
                    + " averageFoodDays=" + averageFoodDays
                    + " minimumFoodDays=" + minimumFoodDays
                    + " siegeFoodThreshold=" + MarshalArmyDebugTrace.F(siegeFoodThreshold));
            }

            bool previousWillGatherArmy = thinkParams.WillGatherAnArmy;
            thinkParams.WillGatherAnArmy = true;
            int scoreCountBefore = thinkParams.AIBehaviorScores?.Count ?? 0;
            float bestExistingScore = scoreCountBefore == 0
                ? 0f
                : thinkParams.AIBehaviorScores.Max(item => item.Item2);
            float armyCreationMultiplier = CalculateArmyCreationMultiplier(
                kingdom,
                party,
                out float armyCountFactor,
                out float influenceFactor,
                out float partySizeFactor);
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    CampaignArmy.ArmyTypes armyType = (CampaignArmy.ArmyTypes)i;
                    if (armyType != CampaignArmy.ArmyTypes.Raider)
                    {
                        int typeStart = thinkParams.AIBehaviorScores?.Count ?? 0;
                        MarshalArmyDebugTrace.Write(
                            "phase=TYPE_START traceId=" + traceId + " armyType=" + armyType
                            + " preCount=" + typeStart
                            + " gatheringStrength=" + MarshalArmyDebugTrace.F(gatheringStrength)
                            + " averageFoodDays=" + averageFoodDays
                            + " siegeFoodThreshold=" + MarshalArmyDebugTrace.F(siegeFoodThreshold));
                        using (MarshalArmyDebugTrace.PushTargetContext(party, armyType, traceId, detailedTrace))
                        {
                            int modelCallsBefore =
                                MarshalArmyDebugTrace.CurrentScoredTargets;
                            _aiMilitaryBehavior.FindBestTargetAndItsValueForFaction(
                                armyType,
                                thinkParams,
                                gatheringStrength);
                            int typeEnd = thinkParams.AIBehaviorScores?.Count ?? 0;
                            var added = thinkParams.AIBehaviorScores.Skip(typeStart).Take(typeEnd - typeStart).ToList();
                            int positive = added.Count(item => item.Item2 > 0f);
                            int zero = added.Count(item => item.Item2 == 0f);
                            int invalid = added.Count(item => float.IsNaN(item.Item2) || float.IsInfinity(item.Item2));
                            int modelCalls = MarshalArmyDebugTrace
                                .CurrentScoredTargets - modelCallsBefore;
                            MarshalArmyDebugTrace.Write(
                                "phase=TYPE_SUMMARY traceId=" + traceId + " armyType=" + armyType
                                + " postCount=" + typeEnd + " added=" + added.Count
                                + " positive=" + positive + " zero=" + zero + " nonFinite=" + invalid
                                + " modelCalls=" + (detailedTrace
                                    ? modelCalls.ToString() : "not_sampled")
                                + " zeroReasonSummary=\"" +
                                    MarshalArmyDebugTrace.CurrentReasonSummary + "\""
                                + " targetNotScored=" +
                                    (!detailedTrace ? "not_sampled" :
                                        modelCalls == 0
                                            ? "TARGET_NOT_SCORED" : "false")
                                + " omittedTargets=" + MarshalArmyDebugTrace.CurrentOmitted
                                + " result=" + (added.Count > 0 && positive == 0 ? "ALL_NEW_SCORES_ZERO" : "TYPE_SCORES_OBSERVED"));
                        }
                    }
                }
            }
            finally
            {
                thinkParams.WillGatherAnArmy = previousWillGatherArmy;
            }

            int scoreCountAfter = thinkParams.AIBehaviorScores?.Count ?? 0;
            int addedScores = System.Math.Max(0, scoreCountAfter - scoreCountBefore);
            // The installed public API exposes only the three-argument target evaluator.
            // Apply the native-style army preference to the scores it just appended.
            for (int i = scoreCountBefore; i < scoreCountAfter; i++)
            {
                var score = thinkParams.AIBehaviorScores[i];
                float finalScore = score.Item2 * armyCreationMultiplier;
                thinkParams.SetBehaviorScore(score.Item1, finalScore);
                if (detailedTrace)
                    MarshalArmyDebugTrace.Write(
                        "phase=SCORE_MULTIPLIER traceId=" + traceId
                        + " behavior=" + score.Item1.AiBehavior
                        + " target=" + (score.Item1.Party?.ToString() ?? "none")
                        + " rawScore=" + MarshalArmyDebugTrace.F(score.Item2)
                        + " multiplier=" + MarshalArmyDebugTrace.F(armyCreationMultiplier)
                        + " armyCountFactor=" + MarshalArmyDebugTrace.F(armyCountFactor)
                        + " influenceFactor=" + MarshalArmyDebugTrace.F(influenceFactor)
                        + " partySizeFactor=" + MarshalArmyDebugTrace.F(partySizeFactor)
                        + " finalScore=" + MarshalArmyDebugTrace.F(finalScore));
            }
            int positiveAddedScores = addedScores == 0
                ? 0
                : thinkParams.AIBehaviorScores
                    .Skip(scoreCountBefore)
                    .Count(item => item.Item2 > 0f);
            float bestAddedScore = addedScores == 0
                ? 0f
                : thinkParams.AIBehaviorScores.Skip(scoreCountBefore).Max(item => item.Item2);
            float bestOverallScore = scoreCountAfter == 0
                ? 0f
                : thinkParams.AIBehaviorScores.Max(item => item.Item2);
            SetDiagnosticState(
                kingdom,
                party,
                addedScores == 0 ? "no-scored-target" : "evaluating",
                addedScores == 0 ? "MP_MarshalArmyAiNoScoredTarget" : "MP_MarshalArmyAiEvaluatingDetailed",
                candidates.Count,
                addedScores,
                bestAddedScore,
                bestOverallScore,
                bestExistingScore,
                armyCreationMultiplier,
                positiveAddedScores,
                gatheringStrength,
                averageFoodDays,
                minimumFoodDays,
                siegeFoodThreshold);
        }

        private void OnArmyCreated(CampaignArmy army)
        {
            MobileParty leaderParty = army?.LeaderParty;
            if (!MarshalArmyDebugTrace.IsRelevant(leaderParty))
                return;

            MarshalArmyDebugTrace.Write(
                "phase=CREATE_ARMY_SUCCESS_EVENT kingdom="
                + MarshalArmyDebugTrace.DescribeKingdom(leaderParty.MapFaction as Kingdom)
                + " leader=" + MarshalArmyDebugTrace.DescribeHero(leaderParty.LeaderHero)
                + " party=" + MarshalArmyDebugTrace.DescribeParty(leaderParty)
                + " armyType=" + army.ArmyType
                + " target=" + (army.AiBehaviorObject?.ToString() ?? "none")
                + " initialMembers=" + (army.Parties?.Count ?? 0)
                + " campaignHour=" + (int)CampaignTime.Now.ToHours);
        }

        private static void TraceDecision(
            MobileParty party,
            Kingdom kingdom,
            string traceId,
            string phase,
            string reason,
            bool isRuler)
        {
            TraceDecision(party, kingdom, traceId, phase, reason, isRuler, false);
        }

        private static void TraceDecision(
            MobileParty party,
            Kingdom kingdom,
            string traceId,
            string phase,
            string reason,
            bool isRuler,
            bool force)
        {
            if (!force && !MarshalArmyDebugTrace.ShouldTraceState(party, reason))
                return;
            MarshalArmyDebugTrace.Write(
                "phase=" + phase + " traceId=" + traceId
                + " kingdom=" + MarshalArmyDebugTrace.DescribeKingdom(kingdom)
                + " hero=" + MarshalArmyDebugTrace.DescribeHero(party?.LeaderHero)
                + " party=" + MarshalArmyDebugTrace.DescribeParty(party)
                + " isRuler=" + isRuler
                + " isMarshal=" + IsAppointedMarshal(party?.LeaderHero, kingdom)
                + " armyState=" + (party?.Army == null ? "none" : party.Army.LeaderParty == party ? "leader" : "attached")
                + " partySizeRatio=" + MarshalArmyDebugTrace.F(party?.PartySizeRatio ?? 0f)
                + " influence=" + MarshalArmyDebugTrace.F(party?.LeaderHero?.Clan?.Influence ?? 0f)
                + " foodDays=" + (party?.GetNumDaysForFoodToLast() ?? 0)
                + " atSea=" + (party?.IsCurrentlyAtSea ?? false)
                + " mapEvent=" + (party?.MapEvent != null)
                + " settlementSiege=" + (party?.CurrentSettlement?.SiegeEvent != null)
                + " reasonCode=" + reason);
        }

        private static float CalculateArmyCreationMultiplier(
            Kingdom kingdom,
            MobileParty party,
            out float armyCountFactor,
            out float influenceFactor,
            out float partySizeFactor)
        {
            int existingArmyCount = kingdom.Armies.Count(army => !army.Parties.Contains(MobileParty.MainParty));
            armyCountFactor = 1f - existingArmyCount * 0.4f;
            influenceFactor = (10f + MathF.Sqrt(MathF.Min(900f, party.LeaderHero.Clan.Influence))) / 50f;
            partySizeFactor = MathF.Sqrt(party.PartySizeRatio);

            // LastArmyCreationDay is not exposed by the installed public API. The remaining
            // native factors still preserve army-count, influence and party-size competition.
            return influenceFactor * armyCountFactor * partySizeFactor;
        }

        private static bool IsAppointedMarshal(Hero hero, Kingdom kingdom)
        {
            return ArmyAuthorityProviderRegistry.IsEligibleMarshal(
                hero, kingdom);
        }

        private void SetState(
            MobileParty party,
            Kingdom kingdom,
            string state,
            string textId,
            string fallbackReason)
        {
            if (party == null || kingdom == null
                || _lastStates.TryGetValue(party, out string previous) && previous == state)
                return;

            _lastStates[party] = state;
            if (Clan.PlayerClan?.Kingdom != kingdom)
                return;

            TextObject message = new TextObject(
                $"{{={textId}}}[Hero Offices] Marshal army AI in {{KINGDOM}} for {{MARSHAL}}: {fallbackReason}.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("MARSHAL", party.LeaderHero?.Name ?? TextObject.GetEmpty());
            ModLogger.Info(message.ToString());
        }

        private void SetDiagnosticState(
            Kingdom kingdom,
            MobileParty party,
            string state,
            string textId,
            int candidateCount,
            int addedScoreCount,
            float bestAddedScore,
            float bestOverallScore,
            float bestExistingScore = 0f,
            float armyCreationMultiplier = 0f,
            int positiveAddedScoreCount = 0,
            float gatheringStrength = 0f,
            int averageFoodDays = 0,
            int minimumFoodDays = 0,
            float siegeFoodThreshold = 0f)
        {
            if (kingdom == null || party == null
                || _lastStates.TryGetValue(party, out string previous) && previous == state)
                return;

            _lastStates[party] = state;
            if (Clan.PlayerClan?.Kingdom != kingdom)
                return;

            TextObject message = new TextObject(
                $"{{={textId}}}[Hero Offices] Marshal army diagnostic for {{KINGDOM}}: marshal={{MARSHAL}}, party={{PARTY}}, influence={{INFLUENCE}}, food={{FOOD}}, size={{SIZE}}%, candidates={{CANDIDATES}}, added scores={{ADDED_SCORES}}, best army score={{BEST_ARMY_SCORE}}, best overall score={{BEST_OVERALL_SCORE}}, identity adaptation={{IDENTITY_MODE}}.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("MARSHAL", party?.LeaderHero?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("PARTY", party?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("INFLUENCE", (int)(party?.LeaderHero?.Clan?.Influence ?? 0f));
            message.SetTextVariable("FOOD", party?.GetNumDaysForFoodToLast() ?? 0);
            message.SetTextVariable("SIZE", (int)((party?.PartySizeRatio ?? 0f) * 100f));
            message.SetTextVariable("CANDIDATES", candidateCount);
            message.SetTextVariable("ADDED_SCORES", addedScoreCount);
            message.SetTextVariable("BEST_ARMY_SCORE", bestAddedScore.ToString("0.000000"));
            message.SetTextVariable("BEST_OVERALL_SCORE", bestOverallScore.ToString("0.000000"));
            message.SetTextVariable("BEST_EXISTING_SCORE", bestExistingScore.ToString("0.000000"));
            message.SetTextVariable("ARMY_MULTIPLIER", armyCreationMultiplier.ToString("0.000"));
            message.SetTextVariable("POSITIVE_SCORES", positiveAddedScoreCount);
            message.SetTextVariable("GATHERING_STRENGTH", gatheringStrength.ToString("0"));
            message.SetTextVariable("AVERAGE_FOOD", averageFoodDays);
            message.SetTextVariable("MINIMUM_FOOD", minimumFoodDays);
            message.SetTextVariable("FOOD_THRESHOLD", siegeFoodThreshold.ToString("0.0"));
            message.SetTextVariable("IDENTITY_MODE", "MODEL_DIRECT");
            ModLogger.Info(message.ToString());
        }
    }
}
