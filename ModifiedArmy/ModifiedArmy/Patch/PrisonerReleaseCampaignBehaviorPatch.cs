using HarmonyLib;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

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
                    float escapeChance = Settings.Instance.PlayerSettlementPrisonerEscapeChance;
                    ExplainedNumber explainedNumber = new ExplainedNumber(escapeChance, false, null);
                    if (MBRandom.RandomFloat < explainedNumber.ResultNumber)
                    {
                        EndCaptivityAction.ApplyByEscape(hero, null, true);
                    }
                    return false;
                }
            }

            return true;
        }
    }
}
