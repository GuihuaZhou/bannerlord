using HarmonyLib;
using Helpers;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Patch
{
    /// <summary>
    /// 补丁：严格限制 AI 领主向定居点捐赠/抽取士兵的行为。
    /// 
    /// 原始逻辑：只要同派系且非玩家封地（或刚被攻占），AI 领主进入定居点就会尝试优化兵力。
    /// 修改后逻辑：
    /// 1. 对于军队统帅 (Army Leader)，保留原有逻辑不变。
    /// 2. 对于单独行动的 AI 领主 (满足 !mobileParty.IsMainParty)，必须是定居点的拥有者 (OwnerClan) 才能进行兵力操作。
    /// 
    /// 此修改旨在防止 AI 在路过盟友城市时随意捐兵，导致其自身兵力枯竭。
    /// </summary>
    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior))]
    [HarmonyPatch("OnSettlementEntered")]
    public static class GarrisonTroopsCampaignBehavior_OnSettlementEntered_Patch
    {
        /// <summary>
        /// Prefix 补丁，在原方法执行前运行。
        /// 我们将复用原方法的大部分判断，但对 "else if (!mobileParty.IsMainParty)" 分支增加 Clan 所有权检查。
        /// </summary>
        /// <param name="__instance">GarrisonTroopsCampaignBehavior 的实例</param>
        /// <param name="mobileParty">进入定居点的部队</param>
        /// <param name="settlement">被进入的定居点</param>
        /// <param name="hero">英雄（通常与 mobileParty.LeaderHero 相同）</param>
        /// <returns>如果返回 false，则跳过原方法；否则继续执行原方法。</returns>
        /// 
        public static bool Prefix(GarrisonTroopsCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, Hero hero)
        {
            if (!Campaign.Current.GameStarted)
            {
                return false;
            }
            if (mobileParty != null
                && mobileParty.IsLordParty
                && !mobileParty.IsDisbanding
                && mobileParty.LeaderHero != null
                && settlement.IsFortification
                && DiplomacyHelper.IsSameFactionAndNotEliminated(mobileParty.MapFaction, settlement.MapFaction))
            {
                FieldInfo newlyConqueredField = AccessTools.Field(typeof(GarrisonTroopsCampaignBehavior), "_newlyConqueredFortification");
                Settlement _newlyConqueredFortification = (Settlement)newlyConqueredField.GetValue(__instance);

                // 如果不是新占定居点且不是所属clan的定居点，禁止捐兵
                if (settlement != _newlyConqueredFortification 
                    && mobileParty.Owner.Clan != settlement.OwnerClan)
                    return false;
            }

            return true;
        }
    }
}
