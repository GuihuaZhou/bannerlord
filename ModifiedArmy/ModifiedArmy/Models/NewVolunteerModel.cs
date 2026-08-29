using HarmonyLib;
using Helpers;
using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models
{
    public class NewVolunteerModel : DefaultVolunteerModel
    {
        // ============================================================
        // 获取不同文化的志愿兵生成概率倍率
        //
        // 具体数值统一由 CommonConstants 管理。
        //
        // Vlandia：
        //     封建部队强，因此城镇志愿兵生成频率极低。
        //
        // Empire：
        //     封建部队较少，因此更加依赖城镇志愿兵。
        //
        // Aserai：
        //     封建部队较少，同时依赖地方志愿兵、
        //     军事奴隶和雇佣兵。
        //
        // 其他文化：
        //     当前保持原有生成概率。
        // ============================================================
        private float GetCultureVolunteerGenerationMultiplier(
            Hero hero,
            Settlement settlement)
        {
            if (hero == null ||
                settlement == null ||
                hero.Culture == null)
            {
                return 1.0f;
            }

            string cultureId =
                hero.Culture.StringId?.ToLowerInvariant();

            switch (cultureId)
            {
                case "vlandia":
                    return CommonConstants
                        .VLANDIA_VOLUNTEER_GENERATION_MULTIPLIER;

                case "empire":
                    return CommonConstants
                        .EMPIRE_VOLUNTEER_GENERATION_MULTIPLIER;

                case "aserai":
                    return CommonConstants
                        .ASERAI_VOLUNTEER_GENERATION_MULTIPLIER;

                default:
                    return 1.0f;
            }
        }


        // ============================================================
        // 每日志愿兵生成概率
        // ============================================================
        public override float GetDailyVolunteerProductionProbability(
            Hero hero,
            int index,
            Settlement settlement)
        {
            float num = 0.4f; // 原 0.7

            int num2 = 0;

            foreach (Town town in hero.CurrentSettlement.MapFaction.Fiefs)
            {
                num2 +=
                    town.IsTown
                        ? (
                            (
                                town.Prosperity < 3000f
                                    ? 1
                                    : (
                                        town.Prosperity < 6000f
                                            ? 2
                                            : 3
                                      )
                            )
                            + town.Villages.Count
                          )
                        : town.Villages.Count;
            }

            float num3 =
                num2 < 46
                    ? (
                        (float)num2 / 46f *
                        ((float)num2 / 46f)
                      )
                    : 1f;

            num +=
                (
                    hero.CurrentSettlement != null &&
                    num3 < 1f
                )
                    ? ((1f - num3) * 0.2f)
                    : 0f;

            float baseNumber =
                0.75f *
                MathF.Clamp(
                    MathF.Pow(
                        num,
                        (float)(index + 1)),
                    0f,
                    1f);

            ExplainedNumber explainedNumber =
                new ExplainedNumber(
                    baseNumber,
                    false,
                    null);


            // ============================================================
            // Cantons 政策
            // ============================================================
            Clan clan = hero.Clan;

            if (
                ((clan != null)
                    ? clan.Kingdom
                    : null) != null &&
                hero.Clan.Kingdom.ActivePolicies.Contains(
                    DefaultPolicies.Cantons))
            {
                explainedNumber.AddFactor(
                    0.2f,
                    null);
            }


            // ============================================================
            // Cavalry Tactics
            // ============================================================
            Town town2;

            if (!settlement.IsTown)
            {
                Settlement tradeBound =
                    settlement.Village.TradeBound;

                town2 =
                    tradeBound != null
                        ? tradeBound.Town
                        : null;
            }
            else
            {
                town2 = settlement.Town;
            }

            Town town3 = town2;

            if (
                town3 != null &&
                hero.IsAlive &&
                hero.VolunteerTypes[index] != null &&
                hero.VolunteerTypes[index].IsMounted &&
                PerkHelper.GetPerkValueForTown(
                    DefaultPerks.Riding.CavalryTactics,
                    town3))
            {
                explainedNumber.AddFactor(
                    DefaultPerks.Riding.CavalryTactics.PrimaryBonus,
                    null);
            }


            // ============================================================
            // 文化生成倍率
            //
            // 注意：
            // 这里是在原有概率计算完成以后，
            // 再乘以文化倍率。
            //
            // 因此不会改变：
            // - 原有 Prosperity / Fief 数量修正
            // - Volunteer Slot index 修正
            // - Cantons
            // - Cavalry Tactics
            //
            // 只改变不同文化最终的生成频率。
            // ============================================================
            float cultureMultiplier =
                GetCultureVolunteerGenerationMultiplier(
                    hero,
                    settlement);

            explainedNumber.AddFactor(
                cultureMultiplier - 1.0f,
                null);


            return explainedNumber.ResultNumber;
        }


        // ============================================================
        // 计算 Hero 可以提供多少个招募位置
        // ============================================================
        private int MaximumIndexCanPartyRecruitFromHeroInternal(
            Hero buyerHero,
            Hero sellerHero)
        {
            Settlement currentSettlement =
                sellerHero.CurrentSettlement;

            int num = 1;

            int num2 =
                buyerHero == Hero.MainHero
                    ? Campaign.Current.Models.DifficultyModel
                        .GetPlayerRecruitSlotBonus()
                    : 0;

            int num3 = 0;

            if (
                sellerHero.IsGangLeader &&
                currentSettlement != null &&
                currentSettlement.OwnerClan == buyerHero.Clan)
            {
                if (currentSettlement.IsTown)
                {
                    Hero governor =
                        currentSettlement.Town.Governor;

                    if (
                        governor != null &&
                        governor.GetPerkValue(
                            DefaultPerks.Roguery.OneOfTheFamily))
                    {
                        goto IL_9A;
                    }
                }

                if (!currentSettlement.IsVillage)
                {
                    goto IL_A8;
                }

                Hero governor2 =
                    currentSettlement.Village.Bound.Town.Governor;

                if (
                    governor2 == null ||
                    !governor2.GetPerkValue(
                        DefaultPerks.Roguery.OneOfTheFamily))
                {
                    goto IL_A8;
                }

            IL_9A:

                num3 +=
                    (int)DefaultPerks.Roguery
                        .OneOfTheFamily
                        .SecondaryBonus;
            }

        IL_A8:

            return MathF.Min(
                6,
                MathF.Max(
                    0,
                    num + num2 + num3));
        }


        // ============================================================
        // 限制招募志愿兵
        //
        // NPC 只能在 Clan 拥有的 Settlement 招募。
        // ============================================================
        public override int MaximumIndexHeroCanRecruitFromHero(
            Hero buyerHero,
            Hero sellerHero,
            int useValueAsRelation = -101)
        {
            Settlement settlement =
                sellerHero.CurrentSettlement;

            if (settlement == null)
            {
                return -1;
            }

            MobileParty buyerParty =
                buyerHero.PartyBelongedTo;

            Clan buyerClan =
                buyerParty.ActualClan;

            if (buyerClan == null)
            {
                return -1;
            }

            Clan ownerClan =
                settlement.OwnerClan;

            bool canRecruitFromSettlement =
                ownerClan == buyerClan;

            if (!canRecruitFromSettlement)
            {
                return -1;
            }

            if (
                buyerHero.MapFaction.IsAtWarWith(
                    settlement.MapFaction))
            {
                return -1;
            }

            int num =
                MaximumIndexCanPartyRecruitFromHeroInternal(
                    buyerHero,
                    sellerHero);

            int num2 =
                useValueAsRelation < -100
                    ? buyerHero.GetRelation(sellerHero)
                    : useValueAsRelation;

            int num3 =
                num2 >= 100
                    ? 7
                    : (
                        num2 >= 80
                            ? 6
                            : (
                                num2 >= 60
                                    ? 5
                                    : (
                                        num2 >= 40
                                            ? 4
                                            : (
                                                num2 >= 20
                                                    ? 3
                                                    : (
                                                        num2 >= 10
                                                            ? 2
                                                            : (
                                                                num2 >= 5
                                                                    ? 1
                                                                    : (
                                                                        num2 >= 0
                                                                            ? 0
                                                                            : -1
                                                                      )
                                                              )
                                                      )
                                              )
                                      )
                              )
                      );

            int num4 =
                sellerHero.CurrentSettlement != null &&
                buyerHero.MapFaction ==
                sellerHero.CurrentSettlement.MapFaction
                    ? 1
                    : 0;

            int num5 =
                buyerHero != Hero.MainHero
                    ? 1
                    : 0;

            int num6 =
                sellerHero.CurrentSettlement != null &&
                buyerHero.MapFaction.IsAtWarWith(
                    sellerHero.CurrentSettlement.MapFaction)
                    ? -(1 + num5)
                    : 0;

            if (
                buyerHero.IsMinorFactionHero &&
                sellerHero.CurrentSettlement != null &&
                sellerHero.CurrentSettlement.IsVillage)
            {
                num6 = 0;
            }

            int num7 = 0;

            if (
                sellerHero.IsMerchant &&
                buyerHero.GetPerkValue(
                    DefaultPerks.Trade.ArtisanCommunity))
            {
                num7 +=
                    (int)DefaultPerks.Trade
                        .ArtisanCommunity
                        .SecondaryBonus;
            }

            if (
                sellerHero.Culture == buyerHero.Culture &&
                buyerHero.GetPerkValue(
                    DefaultPerks.Leadership.CombatTips))
            {
                num7 +=
                    (int)DefaultPerks.Leadership
                        .CombatTips
                        .SecondaryBonus;
            }

            if (
                sellerHero.IsRuralNotable &&
                buyerHero.GetPerkValue(
                    DefaultPerks.Charm.Firebrand))
            {
                num7 +=
                    (int)DefaultPerks.Charm
                        .Firebrand
                        .SecondaryBonus;
            }

            if (
                sellerHero.IsUrbanNotable &&
                buyerHero.GetPerkValue(
                    DefaultPerks.Charm.FlexibleEthics))
            {
                num7 +=
                    (int)DefaultPerks.Charm
                        .FlexibleEthics
                        .SecondaryBonus;
            }

            if (
                sellerHero.IsArtisan &&
                buyerHero.PartyBelongedTo != null &&
                buyerHero.PartyBelongedTo.EffectiveEngineer != null &&
                buyerHero.PartyBelongedTo.EffectiveEngineer
                    .GetPerkValue(
                        DefaultPerks.Engineering.EngineeringGuilds))
            {
                num7 +=
                    (int)DefaultPerks.Engineering
                        .EngineeringGuilds
                        .PrimaryBonus;
            }

            return MathF.Min(
                6,
                num + num3 + num4 + num5 + num6 + num7);
        }


        // ============================================================
        // 按权重选择 Volunteer
        // ============================================================
        private CharacterObject WeightedRandomSelect(
            List<CharacterObject> troops,
            List<int> weights)
        {
            if (
                troops.Count == 0 ||
                weights.Count == 0 ||
                troops.Count != weights.Count)
            {
                return null;
            }

            int total =
                weights.Sum();

            if (total <= 0)
            {
                return troops.Count > 0
                    ? troops[0]
                    : null;
            }

            int rand =
                MBRandom.RandomInt(total);

            int sum = 0;

            for (int i = 0; i < troops.Count; i++)
            {
                sum += weights[i];

                if (rand < sum)
                {
                    return troops[i];
                }
            }

            return troops[troops.Count - 1];
        }


        // ============================================================
        // 按权重返回 Volunteer
        // ============================================================
        public override CharacterObject GetBasicVolunteer(
            Hero sellerHero)
        {
            if (
                !CampaignState.IsReady ||
                sellerHero?.Culture == null ||
                sellerHero.CurrentSettlement == null)
            {
                return sellerHero?.Culture?.BasicTroop;
            }

            var culture =
                sellerHero.Culture;

            var settlement =
                sellerHero.CurrentSettlement;

            if (
                !settlement.IsTown &&
                !settlement.IsVillage)
            {
                return culture.BasicTroop;
            }

            var group =
                BasicTroopGroupManager.GetGroupForCulture(
                    culture);

            if (group != null)
            {
                var allEntries =
                    new List<BasicTroopEntry>();

                if (settlement.IsTown)
                {
                    allEntries.AddRange(
                        group.TroopsByType[
                            SoldierType.Sergeant]);

                    allEntries.AddRange(
                        group.TroopsByType[
                            SoldierType.Retinue]);

                    if (settlement.HasPort)
                    {
                        allEntries.AddRange(
                            group.TroopsByType[
                                SoldierType.Marine]);
                    }
                }
                else if (settlement.IsVillage)
                {
                    allEntries.AddRange(
                        group.TroopsByType[
                            SoldierType.Militia]);
                }

                if (allEntries.Count > 0)
                {
                    var troops =
                        allEntries
                            .Select(entry => entry.Troop)
                            .ToList();

                    var weights =
                        allEntries
                            .Select(entry => entry.Weight)
                            .ToList();

                    CharacterObject selectedTroop =
                        WeightedRandomSelect(
                            troops,
                            weights);

                    return selectedTroop ??
                           culture.BasicTroop;
                }
            }

            return culture.BasicTroop;
        }


        // ============================================================
        // 每日更新维护定居点 Hero 提供的志愿兵
        // ============================================================
        private void UpdateVolunteersOfNotablesInSettlement(
            Settlement settlement)
        {
            // ============================================================
            // Town / Village 叛乱检查
            // ============================================================
            if (!(
                (settlement.IsTown &&
                 !settlement.Town.InRebelliousState)
                ||
                (
                    settlement.IsVillage &&
                    !settlement.Village.Bound.Town.InRebelliousState
                )
            ))
            {
                return;
            }


            foreach (Hero hero in settlement.Notables)
            {
                if (!hero.CanHaveRecruits || !hero.IsAlive)
                    continue;


                // ========================================================
                // Village
                //
                // Village 只有 Militia。
                //
                // 不参与 Town：
                // Retinue / Sergeant / Marine
                // 的比例控制。
                // ========================================================
                if (settlement.IsVillage)
                {
                    CharacterObject basicVolunteer =
                        Campaign.Current.Models.VolunteerModel
                            .GetBasicVolunteer(hero);

                    for (int i = 0; i < 6; i++)
                    {
                        if (
                            MBRandom.RandomFloat <
                            Campaign.Current.Models.VolunteerModel
                                .GetDailyVolunteerProductionProbability(
                                    hero,
                                    i,
                                    settlement))
                        {
                            CharacterObject characterObject =
                                hero.VolunteerTypes[i];

                            if (characterObject == null)
                            {
                                hero.VolunteerTypes[i] =
                                    basicVolunteer;
                            }
                        }
                    }

                    continue;
                }


                // ========================================================
                // Town
                //
                // 有 Marine：
                //
                //     Retinue : Sergeant : Marine
                //          2   :     3    :   1
                //
                // 无 Marine：
                //
                //     Retinue : Sergeant
                //          1   :     2
                //
                // 注意：
                //
                // 不是强制按照比例生成。
                //
                // 仍然先随机选择兵种。
                //
                // 如果随机结果导致比例失衡：
                //
                //     不重新随机
                //     不生成该兵
                //     当天该 Hero 停止生成
                //
                // 第二天重新处理。
                // ========================================================

                bool hasMarine =
                    settlement.HasPort;


                // --------------------------------------------------------
                // 统计当前 Volunteer 构成
                // --------------------------------------------------------
                int retinueCount = 0;
                int sergeantCount = 0;
                int marineCount = 0;

                for (int i = 0; i < 6; i++)
                {
                    CharacterObject volunteer =
                        hero.VolunteerTypes[i];

                    if (volunteer == null)
                        continue;

                    SoldierType type =
                        SoldierTypeClassifier.GetSoldierType(
                            volunteer);

                    if (type == SoldierType.Retinue)
                    {
                        retinueCount++;
                    }
                    else if (type == SoldierType.Sergeant)
                    {
                        sergeantCount++;
                    }
                    else if (type == SoldierType.Marine)
                    {
                        marineCount++;
                    }
                }


                // --------------------------------------------------------
                // 逐个检查空槽位
                // --------------------------------------------------------
                for (int i = 0; i < 6; i++)
                {
                    if (hero.VolunteerTypes[i] != null)
                        continue;


                    // ====================================================
                    // 生成概率
                    //
                    // 这里已经包含：
                    //
                    // Vlandia = CommonConstants 中的倍率
                    // Empire  = CommonConstants 中的倍率
                    // Aserai  = CommonConstants 中的倍率
                    //
                    // ====================================================
                    if (
                        MBRandom.RandomFloat >=
                        Campaign.Current.Models.VolunteerModel
                            .GetDailyVolunteerProductionProbability(
                                hero,
                                i,
                                settlement))
                    {
                        continue;
                    }


                    // ====================================================
                    // 随机决定候选兵种
                    // ====================================================
                    CharacterObject candidate =
                        Campaign.Current.Models.VolunteerModel
                            .GetBasicVolunteer(hero);

                    if (candidate == null)
                        continue;


                    SoldierType candidateType =
                        SoldierTypeClassifier.GetSoldierType(
                            candidate);


                    // ====================================================
                    // 如果不是需要比例控制的兵种，
                    // 按原逻辑加入。
                    // ====================================================
                    if (
                        candidateType != SoldierType.Retinue &&
                        candidateType != SoldierType.Sergeant &&
                        candidateType != SoldierType.Marine)
                    {
                        hero.VolunteerTypes[i] =
                            candidate;

                        continue;
                    }


                    // ====================================================
                    // 无 Marine
                    //
                    // Retinue : Sergeant = 1 : 2
                    //
                    // 最大：
                    //
                    // Retinue  = 2
                    // Sergeant = 4
                    // ====================================================
                    if (!hasMarine)
                    {
                        int newRetinueCount =
                            retinueCount;

                        int newSergeantCount =
                            sergeantCount;

                        if (
                            candidateType ==
                            SoldierType.Retinue)
                        {
                            newRetinueCount++;
                        }
                        else if (
                            candidateType ==
                            SoldierType.Sergeant)
                        {
                            newSergeantCount++;
                        }
                        else
                        {
                            // 无港口时不应该出现 Marine。
                            continue;
                        }


                        if (
                            newRetinueCount > 2 ||
                            newSergeantCount > 4)
                        {
                            // 不重新随机。
                            // 今天该 Hero 停止生成。
                            break;
                        }


                        hero.VolunteerTypes[i] =
                            candidate;

                        retinueCount =
                            newRetinueCount;

                        sergeantCount =
                            newSergeantCount;

                        continue;
                    }


                    // ====================================================
                    // 有 Marine
                    //
                    // Retinue : Sergeant : Marine
                    //      2  :     3    :  1
                    //
                    // 最大：
                    //
                    // Retinue  = 2
                    // Sergeant = 3
                    // Marine   = 1
                    // ====================================================
                    int simulatedRetinueCount =
                        retinueCount;

                    int simulatedSergeantCount =
                        sergeantCount;

                    int simulatedMarineCount =
                        marineCount;


                    if (
                        candidateType ==
                        SoldierType.Retinue)
                    {
                        simulatedRetinueCount++;
                    }
                    else if (
                        candidateType ==
                        SoldierType.Sergeant)
                    {
                        simulatedSergeantCount++;
                    }
                    else if (
                        candidateType ==
                        SoldierType.Marine)
                    {
                        simulatedMarineCount++;
                    }


                    bool invalidRatio =
                        simulatedRetinueCount > 2 ||
                        simulatedSergeantCount > 3 ||
                        simulatedMarineCount > 1;


                    if (invalidRatio)
                    {
                        // 不重新随机。
                        // 今天该 Hero 停止生成。
                        break;
                    }


                    hero.VolunteerTypes[i] =
                        candidate;

                    retinueCount =
                        simulatedRetinueCount;

                    sergeantCount =
                        simulatedSergeantCount;

                    marineCount =
                        simulatedMarineCount;
                }
            }
        }
    }
}