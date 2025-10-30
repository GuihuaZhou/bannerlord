using HarmonyLib;
using System.Reflection;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Patches
{
    [HarmonyPatch(typeof(DefaultPartyWageModel), "GetTroopRecruitmentCost")]
    public class RecruitmentCostPatch
    {
        public static bool Prefix(
            CharacterObject troop,
            Hero buyerHero,
            bool withoutItemCost,
            ref ExplainedNumber __result)
        {
            // ========== 自定义基础成本 ==========
            ExplainedNumber result;
            if (troop.Level <= 1)
            {
                result = new ExplainedNumber(10f, false, null);        // 原：10
            }
            else if (troop.Level <= 6)
            {
                result = new ExplainedNumber(20f, false, null);        // 原：20
            }
            else if (troop.Level <= 11)
            {
                result = new ExplainedNumber(50f, false, null);        // 原：50
            }
            else if (troop.Level <= 16)
            {
                result = new ExplainedNumber(100f, false, null);       // 原：100
            }
            else if (troop.Level <= 21)
            {
                result = new ExplainedNumber(500f, false, null);       // 原：200
            }
            else if (troop.Level <= 26)
            {
                result = new ExplainedNumber(1000f, false, null);      // 原：400
            }
            else if (troop.Level <= 31)
            {
                result = new ExplainedNumber(1800f, false, null);      // 原：600
            }
            else if (troop.Level <= 36)
            {
                result = new ExplainedNumber(3000f, false, null);      // 原：1000
            }
            else
            {
                result = new ExplainedNumber(5000f, false, null);      // 原：1500
            }

            // ========== 马匹成本 ==========
            if (troop.Equipment.Horse.Item != null && !withoutItemCost)
            {
                if (troop.Level < 26)
                {
                    result.Add(1500f, null, null);  // 原：150
                }
                else
                {
                    result.Add(5000f, null, null);  // 原：500
                }
            }

            // ========== 职业 ×2 ==========
            bool flag = troop.Occupation == Occupation.Mercenary ||
                        troop.Occupation == Occupation.Gangster ||
                        troop.Occupation == Occupation.CaravanGuard;
            if (flag)
            {
                result.AddFactor(2f, null);
            }

            // ========== 完整保留 buyerHero 所有 perk 逻辑 ==========
            if (buyerHero != null)
            {
                if (troop.Tier >= 2 && buyerHero.GetPerkValue(DefaultPerks.Throwing.HeadHunter))
                {
                    result.AddFactor(DefaultPerks.Throwing.HeadHunter.SecondaryBonus, null);
                }
                if (troop.IsInfantry)
                {
                    if (buyerHero.GetPerkValue(DefaultPerks.OneHanded.ChinkInTheArmor))
                    {
                        result.AddFactor(DefaultPerks.OneHanded.ChinkInTheArmor.SecondaryBonus, null);
                    }
                    if (buyerHero.GetPerkValue(DefaultPerks.TwoHanded.ShowOfStrength))
                    {
                        result.AddFactor(DefaultPerks.TwoHanded.ShowOfStrength.SecondaryBonus, null);
                    }
                    if (buyerHero.GetPerkValue(DefaultPerks.Polearm.HardyFrontline))
                    {
                        result.AddFactor(DefaultPerks.Polearm.HardyFrontline.SecondaryBonus, null);
                    }
                }
                else if (troop.IsRanged)
                {
                    if (buyerHero.GetPerkValue(DefaultPerks.Bow.RenownedArcher))
                    {
                        result.AddFactor(DefaultPerks.Bow.RenownedArcher.SecondaryBonus, null);
                    }
                    if (buyerHero.GetPerkValue(DefaultPerks.Crossbow.Piercer))
                    {
                        result.AddFactor(DefaultPerks.Crossbow.Piercer.SecondaryBonus, null);
                    }
                }
                if (troop.IsMounted && buyerHero.Culture.HasFeat(DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat))
                {
                    result.AddFactor(DefaultCulturalFeats.KhuzaitRecruitUpgradeFeat.EffectBonus, GameTexts.FindText("str_culture", null));
                }
                if (buyerHero.IsPartyLeader && buyerHero.GetPerkValue(DefaultPerks.Steward.Frugal))
                {
                    result.AddFactor(DefaultPerks.Steward.Frugal.SecondaryBonus, null);
                }
                if (flag)
                {
                    if (buyerHero.GetPerkValue(DefaultPerks.Trade.SwordForBarter))
                    {
                        result.AddFactor(DefaultPerks.Trade.SwordForBarter.PrimaryBonus, null);
                    }
                    if (buyerHero.GetPerkValue(DefaultPerks.Charm.SlickNegotiator))
                    {
                        result.AddFactor(DefaultPerks.Charm.SlickNegotiator.PrimaryBonus, null);
                    }
                }
                result.LimitMin(1f);
            }

            __result = result;
            return false; 
        }
    }
}