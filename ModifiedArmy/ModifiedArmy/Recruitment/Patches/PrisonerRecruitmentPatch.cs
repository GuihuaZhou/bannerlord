using HarmonyLib;
using ModifiedArmy.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Patches
{
    [HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior), "RecruitPrisonersAi")]
    public static class RecruitPrisonersAiPatch
    {
        private static void ApplyPrisonerRecruitmentEffects(MobileParty mobileParty, CharacterObject troop, int num)
        {
            int prisonerRecruitmentMoraleEffect = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect(mobileParty.Party, troop, num);
            mobileParty.RecentEventsMorale += (float)prisonerRecruitmentMoraleEffect;
        }

        private static int GetGoldCostForRecruitment(CharacterObject troop, int count, Hero buyerHero)
        {
            var customModel = Campaign.Current.Models.PrisonerRecruitmentCalculationModel as NewPrisonerRecruitmentCalculationModel;

            if (customModel != null)
            {
                return customModel.CalculateGoldCostForRecruitment(troop, count, buyerHero);
            }

            return 0;
        }

        // ========== 补丁: 完全替换 RecruitPrisonersAi ==========
        public static bool Prefix(
            RecruitPrisonersCampaignBehavior __instance,
            MobileParty mobileParty,
            CharacterObject troop,
            int num,
            int conformityCost)
        {
            // 禁止AI招募野怪
            if (troop != null
                && (troop.Occupation == Occupation.Mercenary
                || troop.Occupation == Occupation.Soldier))
            {
                RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                    mobileParty,
                    new List<RecruitmentCandidate>
                    {
                        new RecruitmentCandidate(
                            troop,
                            num,
                            RecruitmentSource.Prisoner)
                    });
                RecruitmentEvaluationResult evaluation =
                    plan.Evaluations.Count > 0
                        ? plan.Evaluations[0]
                        : null;
                int available = mobileParty.PrisonRoster
                    .GetTroopCount(troop);
                int approved = Math.Min(
                    evaluation?.RecruitableCount ?? 0,
                    available);

                if (approved <= 0)
                {
                    return false;
                }

                mobileParty.PrisonRoster.AddToCounts(
                    troop,
                    -approved,
                    false,
                    0,
                    -conformityCost * approved,
                    true,
                    -1);
                mobileParty.MemberRoster.AddToCounts(
                    troop,
                    approved,
                    false,
                    0,
                    0,
                    true,
                    -1);
                int totalCost = evaluation.UnitRecruitmentCost * approved;
                if (totalCost > 0 && mobileParty.LeaderHero != null)
                {
                    GiveGoldAction.ApplyBetweenCharacters(
                        mobileParty.LeaderHero,
                        null,
                        totalCost,
                        false);
                }

                ClanRecruitmentBudgetManager.CommitRecruitment(
                    mobileParty,
                    approved,
                    evaluation.UnitRecruitmentCost,
                    evaluation.UnitDailyWage);
                CampaignEventDispatcher.Instance.OnTroopRecruited(
                    mobileParty.LeaderHero,
                    null,
                    null,
                    troop,
                    approved);
                ApplyPrisonerRecruitmentEffects(
                    mobileParty,
                    troop,
                    approved);
            }

            return false;
        }
    }


    [HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior), "OnMainPartyPrisonerRecruited")]
    public static class OnMainPartyPrisonerRecruitedPatch
    {
        private static int GetGoldCostForRecruitment(CharacterObject troop, int count, Hero buyerHero)
        {
            var customModel = Campaign.Current.Models.PrisonerRecruitmentCalculationModel as NewPrisonerRecruitmentCalculationModel;

            if (customModel != null)
            {
                return customModel.CalculateGoldCostForRecruitment(troop, count, buyerHero);
            }

            return 0;
        }

        // ========== 补丁: 完全替换 OnMainPartyPrisonerRecruited ==========
        public static bool OnMainPartyPrisonerRecruitedPrefix(FlattenedTroopRoster flattenedTroopRosters)
        {
            int cost = 0;
            foreach (CharacterObject characterObject in flattenedTroopRosters.Troops)
            {
                CampaignEventDispatcher.Instance.OnUnitRecruited(characterObject, 1);
                //ApplyPrisonerRecruitmentEffects(MobileParty.MainParty, characterObject, 1);

                cost += GetGoldCostForRecruitment(characterObject, 1, Hero.MainHero);
            }

            if (cost > 0)
            {
                cost = (int)(cost * 0.5f);
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, false);
            }

            return false;
        }
    }
}
