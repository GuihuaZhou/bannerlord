using HarmonyLib;
using ModifiedArmy.Models;
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

namespace ModifiedArmy.Patch
{
    [HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior), "RecruitPrisonersAi")]
    public static class RecruitPrisonersAiPatch
    {
        //private static void ApplyPrisonerRecruitmentEffects(MobileParty mobileParty, CharacterObject troop, int num)
        //{
        //    int prisonerRecruitmentMoraleEffect = Campaign.Current.Models.PrisonerRecruitmentCalculationModel.GetPrisonerRecruitmentMoraleEffect(mobileParty.Party, troop, num);
        //    mobileParty.RecentEventsMorale += (float)prisonerRecruitmentMoraleEffect;
        //}

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
                mobileParty.PrisonRoster.GetElementNumber(troop);
                mobileParty.PrisonRoster.GetElementXp(troop);
                mobileParty.PrisonRoster.AddToCounts(troop, -num, false, 0, -conformityCost * num, true, -1);
                mobileParty.MemberRoster.AddToCounts(troop, num, false, 0, 0, true, -1);
                CampaignEventDispatcher.Instance.OnTroopRecruited(mobileParty.LeaderHero, null, null, troop, num);
                //ApplyPrisonerRecruitmentEffects(mobileParty, troop, num);

                var cost = GetGoldCostForRecruitment(troop, num, mobileParty.LeaderHero);
                cost = (int)(cost * 0.3f);
                if (cost > 0)
                {
                    GiveGoldAction.ApplyBetweenCharacters(mobileParty.LeaderHero, null, cost, false);
                    ModLogger.Info($"{mobileParty.Name}从俘虏中招募{num}名{troop.Name}，花费：{cost}");
                }
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
