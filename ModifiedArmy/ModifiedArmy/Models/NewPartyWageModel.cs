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
                result = new ExplainedNumber(400f, false, null);       // 原：200
            }
            else if (troop.Level <= 26)
            {
                result = new ExplainedNumber(800f, false, null);      // 原：400
            }
            else if (troop.Level <= 31)
            {
                result = new ExplainedNumber(1200f, false, null);      // 原：600
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
                    num = 30; // 原来12
                    break;
                case 6:
                    num = 51; // 原来17
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


        //private void CalculatePartialGarrisonWageReduction(float troopRatio, MobileParty mobileParty, PerkObject perk, ref ExplainedNumber garrisonWageReductionMultiplier, bool isSecondaryEffect)
        //{
        //    if (troopRatio > 0f && mobileParty.CurrentSettlement.Town.Governor != null && PerkHelper.GetPerkValueForTown(perk, mobileParty.CurrentSettlement.Town))
        //    {
        //        garrisonWageReductionMultiplier.AddFactor(isSecondaryEffect ? (perk.SecondaryBonus * troopRatio) : (perk.PrimaryBonus * troopRatio), perk.Name);
        //    }
        //}
        //public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
        //{
        //    // === 1. 快速退出 ===
        //    if (mobileParty == null || troopRoster == null || !mobileParty.IsActive)
        //        return new ExplainedNumber(0f, includeDescriptions, null);

        //    // === 2. 获取当前可用豁免人数（自动推进时间）===
        //    int exemptableCount = 0;
        //    var exemptionManager = Campaign.Current?.GetCampaignBehavior<FiefWageExemptionManager>();
        //    if (exemptionManager != null)
        //    {
        //        exemptableCount = exemptionManager.GetExemptableTroopCount(mobileParty);
        //    }

        //    // === 3. 初始化工资统计变量（仿原版）===
        //    int totalWage = 0;
        //    int heroWage = 0;
        //    int infantryWage = 0;
        //    int mountedWage = 0;
        //    int rangedWage = 0;
        //    int highTierRangedWage = 0; // Tier >= 4 ranged
        //    int banditWage = 0;
        //    int caravanGuardWage = 0;
        //    int mercenaryWage = 0;

        //    bool flagAidCorps = !mobileParty.HasPerk(DefaultPerks.Steward.AidCorps, false);

        //    // === 4. 遍历 roster，累加原始工资（核心：跳过豁免的采邑兵）===
        //    for (int i = 0; i < troopRoster.Count; i++)
        //    {
        //        TroopRosterElement element = troopRoster.GetElementCopyAtIndex(i);
        //        CharacterObject character = element.Character;
        //        if (character == null) continue;

        //        int number = element.Number;
        //        if (number <= 0) continue;

        //        // === 处理英雄 ===
        //        if (character.IsHero)
        //        {
        //            bool isPlayerLordInMainParty = mobileParty.IsMainParty &&
        //                                           character.HeroObject.Clan == Clan.PlayerClan &&
        //                                           character.HeroObject.Occupation == Occupation.Lord;

        //            Hero hero = character.HeroObject;
        //            Clan clan = hero?.Clan;
        //            bool isClanLeader = hero == (clan?.Leader);

        //            if (!isClanLeader && !isPlayerLordInMainParty)
        //            {
        //                int wage = character.TroopWage;
        //                if (mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Steward.PaidInPromise))
        //                {
        //                    wage = MathF.Round(wage * (1f + DefaultPerks.Steward.PaidInPromise.PrimaryBonus));
        //                }
        //                heroWage += wage;
        //            }
        //            continue;
        //        }

        //        // === 计算本次应计费的采邑兵数量 ===
        //        int countToCharge = number;
        //        if (exemptableCount > 0)
        //        {
        //            if (SoldierTypeClassifier.IsFiefTroop(character))
        //            {
        //                int consume = Math.Min(number, exemptableCount);
        //                countToCharge = number - consume;
        //                exemptableCount -= consume;
        //            }
        //        }

        //        // === 只对 countToCharge > 0 的部分累加工资 ===
        //        if (countToCharge > 0)
        //        {
        //            int wagePerTroop = GetCharacterWage(character); // ← 使用你的自定义工资
        //            int wageForThis = wagePerTroop * countToCharge;

        //            totalWage += wageForThis;

        //            // 分类统计（用于后续 perk 减免）
        //            if (character.Culture.IsBandit)
        //                banditWage += wageForThis;
        //            if (character.IsInfantry)
        //                infantryWage += wageForThis;
        //            if (character.IsMounted)
        //                mountedWage += wageForThis;
        //            if (character.Occupation == Occupation.CaravanGuard)
        //                caravanGuardWage += wageForThis;
        //            if (character.Occupation == Occupation.Mercenary)
        //                mercenaryWage += wageForThis;
        //            if (character.IsRanged)
        //            {
        //                rangedWage += wageForThis;
        //                if (character.Tier >= 4)
        //                    highTierRangedWage += wageForThis;
        //            }
        //        }
        //    }

        //    totalWage += heroWage;

        //    // === 5. 应用原版所有减免逻辑（完整复制并适配）===

        //    ExplainedNumber result = new ExplainedNumber(totalWage, includeDescriptions, null);
        //    result.LimitMin(0f);

        //    // DeepPockets: 匪徒工资减免
        //    if (mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Roguery.DeepPockets))
        //    {
        //        result.Add(-banditWage, DefaultPerks.Roguery.DeepPockets.Name);
        //        ExplainedNumber deepPocketsBonus = new ExplainedNumber(banditWage, false, null);
        //        PerkHelper.AddPerkBonusForCharacter(DefaultPerks.Roguery.DeepPockets, mobileParty.LeaderHero.CharacterObject, false, ref deepPocketsBonus, false);
        //        result.Add(deepPocketsBonus.ResultNumber, DefaultPerks.Roguery.DeepPockets.Name);
        //    }

        //    // PickedShots: 高阶远程减免
        //    if (highTierRangedWage > 0)
        //    {
        //        result.Add(-highTierRangedWage, DefaultPerks.Crossbow.PickedShots.Name);
        //        ExplainedNumber pickedShotsBonus = new ExplainedNumber(highTierRangedWage, false, null);
        //        PerkHelper.AddPerkBonusForParty(DefaultPerks.Crossbow.PickedShots, mobileParty, true, ref pickedShotsBonus, mobileParty.IsCurrentlyAtSea);
        //        result.Add(pickedShotsBonus.ResultNumber, DefaultPerks.Crossbow.PickedShots.Name);
        //    }

        //    // 驻军减免（城堡/要塞）
        //    if (mobileParty.IsGarrison && mobileParty.CurrentSettlement?.Town != null)
        //    {
        //        Settlement settlement = mobileParty.CurrentSettlement;
        //        Town town = settlement.Town;

        //        if (settlement.IsFortification)
        //        {
        //            PerkHelper.AddPerkBonusForTown(DefaultPerks.OneHanded.MilitaryTradition, town, ref result);
        //            PerkHelper.AddPerkBonusForTown(DefaultPerks.TwoHanded.Berserker, town, ref result);
        //            PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.DrillSergant, town, ref result);

        //            CalculatePartialGarrisonWageReduction((float)infantryWage / result.BaseNumber, mobileParty, DefaultPerks.Polearm.StandardBearer, ref result, true);
        //            CalculatePartialGarrisonWageReduction((float)rangedWage / result.BaseNumber, mobileParty, DefaultPerks.Crossbow.PeasantLeader, ref result, true);
        //            CalculatePartialGarrisonWageReduction((float)mountedWage / result.BaseNumber, mobileParty, DefaultPerks.Riding.CavalryTactics, ref result, true);
        //        }

        //        if (settlement.IsCastle)
        //        {
        //            PerkHelper.AddPerkBonusForTown(DefaultPerks.Bow.HunterClan, town, ref result);
        //            PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.StiffUpperLip, town, ref result);
        //        }

        //        if (settlement.Owner?.Culture.HasFeat(DefaultCulturalFeats.EmpireGarrisonWageFeat) == true)
        //        {
        //            result.AddFactor(DefaultCulturalFeats.EmpireGarrisonWageFeat.EffectBonus, GameTexts.FindText("str_culture", null));
        //        }

        //        ExplainedNumber buildingEffect = new ExplainedNumber(1f, false, null);
        //        town.AddEffectOfBuildings(BuildingEffectEnum.GarrisonWageReduction, ref buildingEffect);
        //        result.AddFactor(buildingEffect.ResultNumber - 1f, _buildingEffects);
        //    }

        //    // Military Coronae 政策
        //    float militaryCoronaeFactor = (mobileParty.LeaderHero?.Clan.Kingdom != null &&
        //                                   !mobileParty.LeaderHero.Clan.IsUnderMercenaryService &&
        //                                   mobileParty.LeaderHero.Clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.MilitaryCoronae))
        //                                  ? 0.1f : 0f;
        //    if (militaryCoronaeFactor > 0f)
        //        result.AddFactor(militaryCoronaeFactor, DefaultPolicies.MilitaryCoronae.Name);

        //    // SwordForBarter: 商队护卫减免
        //    if (mobileParty.HasPerk(DefaultPerks.Trade.SwordForBarter, true) && caravanGuardWage > 0)
        //    {
        //        float ratio = (float)caravanGuardWage / result.BaseNumber;
        //        float bonus = DefaultPerks.Trade.SwordForBarter.SecondaryBonus * ratio;
        //        result.AddFactor(bonus, DefaultPerks.Trade.SwordForBarter.Name);
        //    }

        //    // Contractors & MercenaryConnections: 佣兵减免
        //    if (mercenaryWage > 0)
        //    {
        //        if (mobileParty.HasPerk(DefaultPerks.Steward.Contractors, false))
        //        {
        //            float ratio = (float)mercenaryWage / result.BaseNumber;
        //            result.AddFactor(DefaultPerks.Steward.Contractors.PrimaryBonus * ratio, DefaultPerks.Steward.Contractors.Name);
        //        }
        //        if (mobileParty.HasPerk(DefaultPerks.Trade.MercenaryConnections, true))
        //        {
        //            float ratio = (float)mercenaryWage / result.BaseNumber;
        //            result.AddFactor(DefaultPerks.Trade.MercenaryConnections.SecondaryBonus * ratio, DefaultPerks.Trade.MercenaryConnections.Name);
        //        }
        //    }

        //    // Frugal: 节俭（非海上）
        //    if (!mobileParty.IsCurrentlyAtSea && mobileParty.HasPerk(DefaultPerks.Steward.Frugal, false))
        //    {
        //        result.AddFactor(DefaultPerks.Steward.Frugal.PrimaryBonus, DefaultPerks.Steward.Frugal.Name);
        //    }

        //    // EfficientCampaigner: 军需官技能
        //    if (mobileParty.Army != null)
        //    {
        //        PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.EfficientCampaigner, mobileParty, false, ref result, mobileParty.IsCurrentlyAtSea);
        //    }

        //    // MasterOfWarcraft: 围城时减免
        //    if (mobileParty.SiegeEvent != null &&
        //        mobileParty.SiegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(mobileParty.Party, MapEvent.BattleTypes.Siege) &&
        //        mobileParty.HasPerk(DefaultPerks.Steward.MasterOfWarcraft, false))
        //    {
        //        result.AddFactor(DefaultPerks.Steward.MasterOfWarcraft.PrimaryBonus, DefaultPerks.Steward.MasterOfWarcraft.Name);
        //    }

        //    // PriceOfLoyalty: 史诗级军需官
        //    if (mobileParty.EffectiveQuartermaster != null)
        //    {
        //        PerkHelper.AddEpicPerkBonusForCharacter(
        //            DefaultPerks.Steward.PriceOfLoyalty,
        //            mobileParty.EffectiveQuartermaster.CharacterObject,
        //            DefaultSkills.Steward,
        //            true,
        //            ref result,
        //            Campaign.Current.Models.CharacterDevelopmentModel.MaxSkillRequiredForEpicPerkBonus,
        //            false);
        //    }

        //    // ContentTrades: 满载贸易
        //    if (mobileParty.CurrentSettlement != null &&
        //        mobileParty.LeaderHero != null &&
        //        mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Trade.ContentTrades))
        //    {
        //        result.AddFactor(DefaultPerks.Trade.ContentTrades.SecondaryBonus, DefaultPerks.Trade.ContentTrades.Name);
        //    }

        //    // Aserai 文化增益（注意：这是增加工资！）
        //    if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiIncreasedWageFeat))
        //    {
        //        result.AddFactor(DefaultCulturalFeats.AseraiIncreasedWageFeat.EffectBonus, _cultureText);
        //    }

        //    result.LimitMin(0f);
        //    return result;
        //}

        //private static readonly TextObject _cultureText = GameTexts.FindText("str_culture", null);
        //private static readonly TextObject _buildingEffects = GameTexts.FindText("str_building_effects", null);
    }
}
