using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Patch
{
    ///
    /// 招募志愿兵消耗繁荣度或户数
    /// 
    [HarmonyPatch(typeof(RecruitmentVM), "OnDone")]
    public static class RecruitmentVMPatch
    {
        public static void Prefix(RecruitmentVM __instance)
        {
            // __instance.RefreshPartyProperties();
            int num = __instance.TroopsInCart.Sum((RecruitVolunteerTroopVM t) => t.Cost);
            if (num > Hero.MainHero.Gold)
            {
                Debug.FailedAssert("Execution shouldn't come here. The checks should happen before", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\GameMenu\\Recruitment\\RecruitmentVM.cs", "OnDone", 229);
                return;
            }

            Settlement settlement = Settlement.CurrentSettlement;

            int prosperityCost = 0;
            int hearthCost = 0;
            int count = 0;

            foreach (RecruitVolunteerTroopVM recruitVolunteerTroopVM in __instance.TroopsInCart)
            {
                recruitVolunteerTroopVM.Owner.OwnerHero.VolunteerTypes[recruitVolunteerTroopVM.Index] = null;
                MobileParty.MainParty.MemberRoster.AddToCounts(recruitVolunteerTroopVM.Character, 1, false, 0, 0, true, -1);
                CampaignEventDispatcher.Instance.OnUnitRecruited(recruitVolunteerTroopVM.Character, 1);

                if (settlement.IsTown)
                {
                    prosperityCost += Settings.Instance.TownProsperityCostPerTier * recruitVolunteerTroopVM.Character.Tier;
                }
                else if (settlement.IsVillage)
                {
                    hearthCost += Settings.Instance.VillageHearthCostPer;
                }
                count += 1;
            }

            if (settlement.IsTown)
            {
                // 扣除繁荣度
                settlement.Town.Prosperity = Math.Max(0f, settlement.Town.Prosperity - prosperityCost);

                TextObject msg = GameTexts.FindText("str_recruitment_prosperity_cost");
                msg.SetTextVariable("PARTY_NAME", MobileParty.MainParty.Name.ToString());
                msg.SetTextVariable("SETTLEMENT_NAME", settlement.Name.ToString());
                msg.SetTextVariable("TROOP_COUNT", count);
                msg.SetTextVariable("PROSPERITY_COST", prosperityCost);

                ModLogger.Notice(msg.ToString());
            }
            else if (settlement.IsVillage)
            {
                // 扣除户数
                settlement.Village.Hearth = Math.Max(0f, settlement.Village.Hearth - hearthCost);

                TextObject msg = GameTexts.FindText("str_recruitment_hearth_cost");
                msg.SetTextVariable("PARTY_NAME", MobileParty.MainParty.Name.ToString());
                msg.SetTextVariable("SETTLEMENT_NAME", settlement.Name.ToString());
                msg.SetTextVariable("TROOP_COUNT", count);
                msg.SetTextVariable("HEARTH_COST", hearthCost);

                ModLogger.Notice(msg.ToString());
            }

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, num, true);
            if (num > 0)
            {
                MBTextManager.SetTextVariable("GOLD_AMOUNT", MathF.Abs(num));
                InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_gold_removed_with_icon", null).ToString(), "event:/ui/notification/coins_negative"));
            }
            __instance.Deactivate();
        }
    }
}
