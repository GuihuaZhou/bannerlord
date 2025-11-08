using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Models
{
    public class NewPartyWageModel : DefaultPartyWageModel
    {
        // 增加tier4-6 troop的招募费用
        public override ExplainedNumber GetTroopRecruitmentCost(CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)
        {

            //InformationManager.DisplayMessage(new InformationMessage($"[MOD] Call NewPartyWageModel::GetTroopRecruitmentCost"));

            ExplainedNumber result;
            if (troop.Level <= 1)
            {
                result = new ExplainedNumber(10f, false, null);
            }
            else if (troop.Level <= 6)
            {
                result = new ExplainedNumber(20f, false, null);
            }
            else if (troop.Level <= 11)
            {
                result = new ExplainedNumber(50f, false, null);
            }
            else if (troop.Level <= 16)
            {
                result = new ExplainedNumber(100f, false, null);
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
                result = new ExplainedNumber(2800f, false, null);      // 原：1000
            }
            else
            {
                result = new ExplainedNumber(4000f, false, null);      // 原：1500
            }
            if (troop.Equipment.Horse.Item != null && !withoutItemCost)
            {
                if (troop.Level < 26)
                {
                    result.Add(1000f, null, null);  // 原：150
                }
                else
                {
                    result.Add(2500f, null, null);  // 原：500
                }
            }
            bool flag = troop.Occupation == Occupation.Mercenary || troop.Occupation == Occupation.Gangster || troop.Occupation == Occupation.CaravanGuard;
            if (flag)
            {
                result.AddFactor(2f, null);
            }
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
            return result;
        }


        // 增加tier4-6 troop的工资
        public override int GetCharacterWage(CharacterObject character)
        {
            //InformationManager.DisplayMessage(new InformationMessage($"[MOD] Call NewPartyWageModel::GetCharacterWage"));
            int num;
            switch (character.Tier)
            {
                case 0:
                    num = 1;
                    break;
                case 1:
                    num = 2;
                    break;
                case 2:
                    num = 3;
                    break;
                case 3:
                    num = 5;
                    break;
                case 4:
                    num = 16; // 原来8
                    break;
                case 5:
                    num = 24; // 原来12
                    break;
                case 6:
                    num = 34; // 原来17
                    break;
                default:
                    num = 46; // 原来23
                    break;
            }
            if (character.Occupation == Occupation.Mercenary)
            {
                num = (int)((float)num * 1.5f);
            }
            return num;
        }
    }
}
