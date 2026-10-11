using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.HeroOffices.Patches;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Behaviors
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
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            _aiMilitaryBehavior = Campaign.Current?.GetCampaignBehavior<AiMilitaryBehavior>();
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

            if (marshal == Hero.MainHero)
            {
                SetState(party, kingdom, "player", "MP_MarshalArmyAiPlayer", "marshal is the player and remains under manual control");
                return;
            }

            if (!isRuler && !OfficeRules.IsEligible(marshal, kingdom, OfficeType.Marshal))
            {
                SetState(party, kingdom, "ineligible", "MP_MarshalArmyAiIneligible", "marshal no longer leads an eligible war party");
                return;
            }

            if (party.Army != null)
            {
                bool leadsArmy = party.Army.LeaderParty == party;
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
                SetState(party, kingdom, "missing-ai", "MP_MarshalArmyAiMissingBehavior", "native military AI behavior is unavailable");
                return;
            }

            if (party.IsCurrentlyAtSea || party.MapEvent != null
                || party.CurrentSettlement?.SiegeEvent != null)
            {
                SetState(party, kingdom, "busy", "MP_MarshalArmyAiBusy", "marshal party is currently busy");
                return;
            }

            if (party.PartySizeRatio <= 0.6f)
            {
                SetState(party, kingdom, "strength", "MP_MarshalArmyAiLowStrength", "marshal party strength is below 60 percent");
                return;
            }

            if (!kingdom.FactionsAtWarWith.Any(faction => faction?.Fiefs?.Any() == true))
            {
                SetState(party, kingdom, "peace", "MP_MarshalArmyAiNoTarget", "kingdom has no enemy faction with a fief");
                return;
            }

            // The current game model owns influence, food, distance and callable-party checks.
            // This bypasses only AiMilitaryBehavior's clan-leader identity gate.
            if (!Campaign.Current.Models.ArmyManagementCalculationModel.CanLordCreateArmy(
                    party,
                    out MBList<MobileParty> candidates))
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
                gatheringStrength += candidate.Party.GetCustomStrength(
                    BattleSideEnum.Attacker,
                    MapEvent.PowerCalculationContext.Siege);
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

            thinkParams.WillGatherAnArmy = true;
            int scoreCountBefore = thinkParams.AIBehaviorScores?.Count ?? 0;
            float bestExistingScore = scoreCountBefore == 0
                ? 0f
                : thinkParams.AIBehaviorScores.Max(item => item.Item2);
            float armyCreationMultiplier = CalculateArmyCreationMultiplier(kingdom, party);
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    Army.ArmyTypes armyType = (Army.ArmyTypes)i;
                    if (armyType != Army.ArmyTypes.Raider)
                    {
                        _aiMilitaryBehavior.FindBestTargetAndItsValueForFaction(
                            armyType,
                            thinkParams,
                            gatheringStrength);
                    }
                }
            }
            finally
            {
                thinkParams.WillGatherAnArmy = false;
            }

            int scoreCountAfter = thinkParams.AIBehaviorScores?.Count ?? 0;
            int addedScores = System.Math.Max(0, scoreCountAfter - scoreCountBefore);
            // The installed public API exposes only the three-argument target evaluator.
            // Apply the native-style army preference to the scores it just appended.
            for (int i = scoreCountBefore; i < scoreCountAfter; i++)
            {
                var score = thinkParams.AIBehaviorScores[i];
                thinkParams.SetBehaviorScore(score.Item1, score.Item2 * armyCreationMultiplier);
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

        private static float CalculateArmyCreationMultiplier(Kingdom kingdom, MobileParty party)
        {
            int existingArmyCount = kingdom.Armies.Count(army => !army.Parties.Contains(MobileParty.MainParty));
            float armyCountFactor = 1f - existingArmyCount * 0.4f;
            float influenceFactor = (10f + MathF.Sqrt(MathF.Min(900f, party.LeaderHero.Clan.Influence))) / 50f;
            float partySizeFactor = MathF.Sqrt(party.PartySizeRatio);

            // LastArmyCreationDay is not exposed by the installed public API. The remaining
            // native factors still preserve army-count, influence and party-size competition.
            return influenceFactor * armyCountFactor * partySizeFactor;
        }

        private static bool IsAppointedMarshal(Hero hero, Kingdom kingdom)
        {
            return hero != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(assignment => assignment?.Hero == hero) == true;
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
            ModLogger.Notice(message.ToString());
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
                $"{{={textId}}}[Hero Offices] Marshal army diagnostic for {{KINGDOM}}: marshal={{MARSHAL}}, party={{PARTY}}, influence={{INFLUENCE}}, food={{FOOD}}, size={{SIZE}}%, candidates={{CANDIDATES}}, added scores={{ADDED_SCORES}}, best army score={{BEST_ARMY_SCORE}}, best overall score={{BEST_OVERALL_SCORE}}, leader-gate replacements={{PATCH_COUNT}}.");
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
            message.SetTextVariable("PATCH_COUNT", MarshalArmyPatches.LeaderGateReplacementCount);
            ModLogger.Notice(message.ToString());
        }
    }
}
