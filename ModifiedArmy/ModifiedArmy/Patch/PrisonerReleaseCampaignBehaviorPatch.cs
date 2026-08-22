using HarmonyLib;
using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

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
                    float escapeChance = CommonConstants.PlayerSettlementPrisonerEscapeChance;
                    ExplainedNumber explainedNumber = new ExplainedNumber(escapeChance, false, null);
                    if (MBRandom.RandomFloat < explainedNumber.ResultNumber)
                    {
                        EndCaptivityAction.ApplyByEscape(hero, null, true);
                    }
                    return false;
                }

                if (hero.PartyBelongedToAsPrisoner.IsMobile && hero.PartyBelongedToAsPrisoner.MobileParty.LeaderHero == Hero.MainHero)
                {
                    float escapeChance = CommonConstants.PlayerSettlementPrisonerEscapeChance;
                    ExplainedNumber explainedNumber = new ExplainedNumber(escapeChance, false, null);
                    if (MBRandom.RandomFloat < explainedNumber.ResultNumber)
                    {
                        EndCaptivityAction.ApplyByEscape(hero, null, true);
                    }
                    return false;
                }
            }


            if (hero.IsPrisoner && hero.PartyBelongedToAsPrisoner != null && hero != Hero.MainHero)
            {
                float num = 0.04f;
                if (hero.PartyBelongedToAsPrisoner.IsMobile && hero.PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement == null)
                {
                    num *= 5f - MathF.Pow((float)MathF.Min(81, hero.PartyBelongedToAsPrisoner.NumberOfHealthyMembers), 0.25f);
                }
                if (hero.PartyBelongedToAsPrisoner == PartyBase.MainParty || (hero.PartyBelongedToAsPrisoner.IsSettlement && hero.PartyBelongedToAsPrisoner.Settlement.OwnerClan == Clan.PlayerClan) || (hero.PartyBelongedToAsPrisoner.IsMobile && hero.PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement != null && hero.PartyBelongedToAsPrisoner.MobileParty.CurrentSettlement.OwnerClan == Clan.PlayerClan))
                {
                    num *= 0.5f;
                }
                ExplainedNumber explainedNumber = new ExplainedNumber(num, false, null);
                if (hero.PartyBelongedToAsPrisoner.IsSettlement && hero.PartyBelongedToAsPrisoner.Settlement.Town != null && hero.PartyBelongedToAsPrisoner.Settlement.Town.Governor != null)
                {
                    Town town = hero.PartyBelongedToAsPrisoner.Settlement.Town;
                    if (hero.PartyBelongedToAsPrisoner.Settlement.IsTown || hero.PartyBelongedToAsPrisoner.Settlement.IsCastle)
                    {
                        if (town.Governor.GetPerkValue(DefaultPerks.Roguery.SweetTalker))
                        {
                            explainedNumber.AddFactor(DefaultPerks.Roguery.SweetTalker.SecondaryBonus, DefaultPerks.Roguery.SweetTalker.Description);
                        }
                        if (town.Governor.GetPerkValue(DefaultPerks.Engineering.DungeonArchitect))
                        {
                            explainedNumber.AddFactor(DefaultPerks.Engineering.DungeonArchitect.SecondaryBonus, DefaultPerks.Engineering.DungeonArchitect.Description);
                        }
                        if (town.Governor.GetPerkValue(DefaultPerks.Riding.MountedPatrols))
                        {
                            explainedNumber.AddFactor(DefaultPerks.Riding.MountedPatrols.SecondaryBonus, DefaultPerks.Riding.MountedPatrols.Description);
                        }
                    }
                }
                if (hero.PartyBelongedToAsPrisoner.IsMobile)
                {
                    if (hero.GetPerkValue(DefaultPerks.Roguery.FleetFooted))
                    {
                        explainedNumber.AddFactor(DefaultPerks.Roguery.FleetFooted.SecondaryBonus, null);
                    }
                    if (hero.PartyBelongedToAsPrisoner.MobileParty.HasPerk(DefaultPerks.Riding.MountedPatrols, false))
                    {
                        PerkHelper.AddPerkBonusForParty(DefaultPerks.Riding.MountedPatrols, hero.PartyBelongedToAsPrisoner.MobileParty, true, ref explainedNumber, false);
                    }
                    if (hero.PartyBelongedToAsPrisoner.MobileParty.HasPerk(DefaultPerks.Roguery.RansomBroker, false))
                    {
                        PerkHelper.AddPerkBonusForParty(DefaultPerks.Roguery.RansomBroker, hero.PartyBelongedToAsPrisoner.MobileParty, false, ref explainedNumber, false);
                    }
                }
                if (hero.PartyBelongedToAsPrisoner.IsMobile && !hero.PartyBelongedToAsPrisoner.MobileParty.IsCurrentlyAtSea)
                {
                    PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.KeenSight, hero.PartyBelongedToAsPrisoner.MobileParty, false, ref explainedNumber, false);
                }
                if (MBRandom.RandomFloat < explainedNumber.ResultNumber)
                {
                    EndCaptivityAction.ApplyByEscape(hero, null, true);
                }
            }

            return true;
        }
    }
}
