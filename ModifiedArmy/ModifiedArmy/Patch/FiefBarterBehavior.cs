using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.CampaignBehaviors.BarterBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Patch
{
    [HarmonyPatch(typeof(FiefBarterBehavior), "CheckForBarters")]
    public static class FiefBarterBehavior_CheckForBarters_Patch
    {
        /// <summary>
        /// 前置补丁 (Prefix)。
        /// 修改原方法的 if 条件判断逻辑：
        /// 如果玩家是国王，则跳过对 EverythingHasAPrice 技能的检查。
        /// </summary>
        [HarmonyPrefix]
        public static bool Prefix(BarterData args)
        {
            if (args.OffererHero != null
                && args.OtherHero != null
                && (!args.OtherHero.Clan.IsMinorFaction || args.OtherHero.Clan == Clan.PlayerClan)
                && !args.OtherHero.Clan.IsUnderMercenaryService
                && !args.OffererHero.Clan.IsUnderMercenaryService)
            {
                // 玩家为君主时，可以直接交易定居点
                if ((args.OffererHero == Hero.MainHero && args.OffererHero.Clan.Kingdom.RulingClan == args.OffererHero.Clan)
                    || (args.OffererHero.GetPerkValue(DefaultPerks.Trade.EverythingHasAPrice)))
                {
                    foreach (Town town in Town.AllFiefs)
                    {
                        Clan ownerClan = town.OwnerClan;
                        if (((ownerClan != null) ? ownerClan.Leader : null) == args.OffererHero)
                        {
                            Barterable barterable = new FiefBarterable(town.Settlement, args.OffererHero, args.OtherHero);
                            args.AddBarterable<FiefBarterGroup>(barterable, false);
                        }
                        else
                        {
                            Clan ownerClan2 = town.OwnerClan;
                            if (((ownerClan2 != null) ? ownerClan2.Leader : null) == args.OtherHero)
                            {
                                Barterable barterable2 = new FiefBarterable(town.Settlement, args.OtherHero, args.OffererHero);
                                args.AddBarterable<FiefBarterGroup>(barterable2, false);
                            }
                        }
                    }
                }

            }

            return false;
        }
    }
}
