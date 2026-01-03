using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedArmy.Patch
{
    /// <summary>
    /// 禁止囚犯从玩家的定居点监狱逃逸
    /// 其他场景仍可按原版逻辑处理
    /// </summary>
    [HarmonyPatch(typeof(PrisonerReleaseCampaignBehavior), "DailyHeroTick")]
    public static class PrisonerReleaseCampaignBehaviorPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Hero hero)
        {
            if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner != null && hero != Hero.MainHero)
            {
                if (hero.PartyBelongedToAsPrisoner.IsSettlement &&
                    hero.PartyBelongedToAsPrisoner.Settlement.Town != null &&
                    (hero.PartyBelongedToAsPrisoner.Settlement.IsTown || hero.PartyBelongedToAsPrisoner.Settlement.IsCastle) &&
                    hero.PartyBelongedToAsPrisoner.Settlement.OwnerClan == Clan.PlayerClan)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}
