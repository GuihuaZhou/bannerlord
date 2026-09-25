using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models
{
    public class NewPartyWageModel : DefaultPartyWageModel
    {
        public const float WarWageMultiplier = 1.5f;
        public const int Tier4TroopWage = 16;
        public const int Tier5TroopWage = 24;
        public const int Tier6TroopWage = 34;
        public const int Tier4TroopRecruitmentCost = 300;
        public const int Tier5TroopRecruitmentCost = 600;
        public const int Tier6TroopRecruitmentCost = 900;
        public const float MercenaryRecruitmentCostMultiplier = 1.25f;

        /// <summary>
        /// Shared expected wage for a professional garrison troop. The value
        /// is calculated once when this wage-model type is initialized and can
        /// be reused by other systems without duplicating wage constants.
        /// </summary>
        public static readonly float Tier4AndTier5AverageWage =
            (Tier4TroopWage + Tier5TroopWage) / 2f;

        private static readonly TextObject WarWageText =
            new TextObject("{=ModifiedArmyWarPartyWage}War Wage");

        private static readonly TextObject FiefWageExemptionText =
            new TextObject(
                "{=ModifiedArmyFiefWageExemption}" +
                "Fief troop wage exemption");

        /// <summary>
        /// 在本体完成所有工资修正后, 应用正式战争期间的 WarParty 工资倍率.
        /// 驻军, 商队和其他非 WarParty 不受影响.
        /// </summary>
        public override ExplainedNumber GetTotalWage(
            MobileParty mobileParty,
            TroopRoster troopRoster,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = CalculateBaseWageWithFiefExemption(
                mobileParty,
                troopRoster,
                includeDescriptions);

            ApplyWarWageMultiplier(mobileParty, ref result);
            return result;
        }

        /// <summary>
        /// Calculates the sustainable long-term wage after temporary fief
        /// exemptions expire. Recruitment uses this value to avoid creating a
        /// party that will collapse as soon as its grace period ends.
        /// </summary>
        public ExplainedNumber GetTotalWageWithoutFiefExemption(
            MobileParty mobileParty,
            TroopRoster troopRoster,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = base.GetTotalWage(
                mobileParty,
                troopRoster,
                includeDescriptions);

            ApplyWarWageMultiplier(mobileParty, ref result);
            return result;
        }

        public static float GetWarWageMultiplier(
            MobileParty mobileParty)
        {
            return ShouldApplyWarWage(mobileParty)
                ? WarWageMultiplier
                : 1f;
        }

        private ExplainedNumber CalculateBaseWageWithFiefExemption(
            MobileParty mobileParty,
            TroopRoster troopRoster,
            bool includeDescriptions)
        {
            ExplainedNumber fullWage = base.GetTotalWage(
                mobileParty,
                troopRoster,
                includeDescriptions);
            FiefWageExemptionManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefWageExemptionManager>();
            int exemptionCount =
                manager?.GetExemptableTroopCount(mobileParty) ?? 0;

            if (exemptionCount <= 0)
            {
                return fullWage;
            }

            TroopRoster chargeableRoster =
                BuildChargeableRoster(
                    troopRoster,
                    exemptionCount,
                    out int exemptedTroops);

            if (exemptedTroops <= 0)
            {
                return fullWage;
            }

            ExplainedNumber reducedWage = base.GetTotalWage(
                mobileParty,
                chargeableRoster,
                false);
            float reduction = Math.Max(
                0f,
                fullWage.ResultNumber - reducedWage.ResultNumber);

            if (reduction > 0f)
            {
                fullWage.Add(
                    -reduction,
                    FiefWageExemptionText);
                fullWage.LimitMin(0f);
            }

            return fullWage;
        }

        private static TroopRoster BuildChargeableRoster(
            TroopRoster source,
            int exemptionCount,
            out int exemptedTroops)
        {
            TroopRoster result =
                TroopRoster.CreateDummyTroopRoster();
            int remainingExemption = exemptionCount;
            exemptedTroops = 0;

            foreach (TroopRosterElement element in
                source.GetTroopRoster())
            {
                int chargeableCount = element.Number;

                if (!element.Character.IsHero
                    && remainingExemption > 0
                    && SoldierTypeClassifier.IsFiefTroop(
                        element.Character))
                {
                    int exempted = Math.Min(
                        chargeableCount,
                        remainingExemption);
                    chargeableCount -= exempted;
                    remainingExemption -= exempted;
                    exemptedTroops += exempted;
                }

                if (chargeableCount <= 0)
                {
                    continue;
                }

                result.AddToCounts(
                    element.Character,
                    chargeableCount,
                    false,
                    Math.Min(
                        element.WoundedNumber,
                        chargeableCount),
                    0,
                    true,
                    -1);
            }

            return result;
        }

        private static void ApplyWarWageMultiplier(
            MobileParty mobileParty,
            ref ExplainedNumber result)
        {

            if (!ShouldApplyWarWage(mobileParty))
            {
                return;
            }

            // Apply the configured multiplier after all perks, policies and
            // other modifiers have been calculated.
            float normalWage = result.ResultNumber;

            if (normalWage > 0f)
            {
                result.Add(
                    normalWage * (WarWageMultiplier - 1f),
                    WarWageText);
            }
        }

        private static bool ShouldApplyWarWage(MobileParty mobileParty)
        {
            if (mobileParty == null
                || !mobileParty.IsActive
                || mobileParty.WarPartyComponent == null)
            {
                return false;
            }

            IFaction faction = mobileParty.MapFaction;

            if (faction == null || faction.IsEliminated)
            {
                return false;
            }

            foreach (IFaction enemy in faction.FactionsAtWarWith)
            {
                // Only formal wars against a living kingdom activate war wages.
                if (enemy != null
                    && enemy.IsKingdomFaction
                    && !enemy.IsEliminated
                    && FactionManager.IsAtWarAgainstFaction(faction, enemy))
                {
                    return true;
                }
            }

            return false;
        }

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
                result = new ExplainedNumber(
                    Tier4TroopRecruitmentCost,
                    false,
                    null);
            }
            else if (troop.Level <= 26)
            {
                result = new ExplainedNumber(
                    Tier5TroopRecruitmentCost,
                    false,
                    null);
            }
            else if (troop.Level <= 31)
            {
                result = new ExplainedNumber(
                    Tier6TroopRecruitmentCost,
                    false,
                    null);
            }
            else if (troop.Level <= 36)
            {
                result = new ExplainedNumber(2000f, false, null);      // 原：1000
            }
            else
            {
                result = new ExplainedNumber(3000f, false, null);      // 原：1500
            }
            if (troop.Equipment.Horse.Item != null && !withoutItemCost)
            {
                if (troop.Level < 26)
                {
                    result.Add(400f, null, null);  // 原：150
                }
                else
                {
                    result.Add(1200f, null, null);  // 原：500
                }
            }
            bool flag = troop.Occupation == Occupation.Mercenary || troop.Occupation == Occupation.Gangster || troop.Occupation == Occupation.CaravanGuard;
            if (flag)
            {
                // AddFactor accepts the increase over the base value. A
                // factor of 0.25 therefore produces a final 1.25 multiplier.
                result.AddFactor(
                    MercenaryRecruitmentCostMultiplier - 1f,
                    null);
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
                    num = Tier4TroopWage; // 原来8
                    break;
                case 5:
                    num = Tier5TroopWage;
                    break;
                case 6:
                    num = Tier6TroopWage;
                    break;
                default:
                    num = 46; // 原来23
                    break;
            }
            if (character.Occupation == Occupation.Mercenary)
            {
                num = (int)((float)num * 1.25f);
            }
            return num;
        }
    }
}
