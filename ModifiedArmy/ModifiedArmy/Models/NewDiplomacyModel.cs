using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using static ModifiedArmy.common.CommonConstants;
using static System.Collections.Specialized.BitVector32;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// 自定义外交模型：引入 Clan 封地繁荣度对战争/和平倾向的影响
    /// </summary>
    public class NewDiplomacyModel : DefaultDiplomacyModel
    {
        private struct WarStats
        {
            // Token: 0x0400173A RID: 5946
            public float Strength;

            // Token: 0x0400173B RID: 5947
            public float ValueOfSettlements;

            // Token: 0x0400173C RID: 5948
            public float TotalStrengthOfEnemies;
        }

        private static NewDiplomacyModel.WarStats CalculateWarStatsForPeace(IFaction faction, IFaction targetFaction, IFaction evaluatingFaction)
        {
            float num = 0f;
            float num2 = 0f;
            bool flag = evaluatingFaction.MapFaction == faction.MapFaction;
            float val = faction.WarPartyComponents.Sum((WarPartyComponent x) => x.Party.EstimatedStrength);
            float num3;
            if (!flag)
            {
                num3 = faction.Fiefs.Sum(delegate (Town x)
                {
                    MobileParty garrisonParty = x.GarrisonParty;
                    if (garrisonParty == null)
                    {
                        return 0f;
                    }
                    return garrisonParty.Party.EstimatedStrength;
                }) * 0.7f;
            }
            else
            {
                num3 = faction.Fiefs.Sum(delegate (Town x)
                {
                    MobileParty garrisonParty = x.GarrisonParty;
                    if (garrisonParty == null)
                    {
                        return 0f;
                    }
                    return garrisonParty.Party.EstimatedStrength;
                });
            }
            float num4 = num3;
            if (faction.IsKingdomFaction)
            {
                foreach (Clan clan in ((Kingdom)faction).Clans)
                {
                    if (!clan.IsUnderMercenaryService)
                    {
                        int partyLimitForTier = Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(clan, clan.Tier);
                        num2 += (float)(partyLimitForTier * 64);
                    }
                }
            }
            num += num4 + Math.Max(val, num2);
            float num5 = 0f;
            IEnumerable<IFaction> factionsAtWarWith = faction.FactionsAtWarWith;
            //Func<IFaction, bool> <> 9__3;
            //Func<IFaction, bool> predicate;
            //if ((predicate = <> 9__3) == null)
            //{
            //    predicate = (<> 9__3 = ((IFaction x) => x != targetFaction));
            //}
            //foreach (IFaction faction2 in factionsAtWarWith.Where(predicate))
            //{
            //    float num6 = 0f;
            //    if (!faction2.IsBanditFaction && (!faction2.IsMinorFaction || faction2.Leader == Hero.MainHero) && faction2.IsKingdomFaction)
            //    {
            //        int num7 = 0;
            //        foreach (Clan clan2 in from x in ((Kingdom)faction2).Clans
            //                               where !x.IsUnderMercenaryService
            //                               select x)
            //        {
            //            num7 += Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(clan2, clan2.Tier);
            //        }
            //        num6 += (float)(num7 * 64);
            //        float val2 = faction2.WarPartyComponents.Sum((WarPartyComponent x) => x.Party.EstimatedStrength);
            //        num5 += Math.Max(val2, num6);
            //    }
            //}
            var otherEnemies = faction.FactionsAtWarWith.Where(x => x != targetFaction);

            foreach (IFaction enemyFaction in otherEnemies)
            {
                float num6 = 0f;
                // 只考虑有效的王国势力（非土匪、非无关小势力）
                if (!enemyFaction.IsBanditFaction &&
                    (!enemyFaction.IsMinorFaction || enemyFaction.Leader == Hero.MainHero) &&
                    enemyFaction.IsKingdomFaction)
                {
                    int num7 = 0;
                    foreach (Clan clan2 in ((Kingdom)enemyFaction).Clans.Where(x => !x.IsUnderMercenaryService))
                    {
                        num7 += Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(clan2, clan2.Tier);
                    }
                    num6 += (float)(num7 * 64);
                    float enemyFieldStrength = enemyFaction.WarPartyComponents.Sum(x => x.Party.EstimatedStrength);
                    num5 += Math.Max(enemyFieldStrength, num6);
                }
            }

            return new NewDiplomacyModel.WarStats
            {
                Strength = num,
                ValueOfSettlements = Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(faction),
                TotalStrengthOfEnemies = (flag ? (num5 * 0.6f) : num5)
            };
        }

        private static float CalculateRiskScore(NewDiplomacyModel.WarStats faction1Stats, NewDiplomacyModel.WarStats faction2Stats)
        {
            float num = MathF.Clamp(faction1Stats.ValueOfSettlements, 10000f, 10000000f);
            float num2 = faction1Stats.Strength / (faction2Stats.Strength + faction1Stats.TotalStrengthOfEnemies);
            float num3 = MathF.Clamp(1f / (1f + num2 * num2), 0.1f, 0.9f);
            return num * num3;
        }

        private static void GetBenefitAndRiskScoreForPeace(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace, IFaction evaluatingFaction, out float benefitScore, out float riskScore)
        {
            NewDiplomacyModel.WarStats warStats = NewDiplomacyModel.CalculateWarStatsForPeace(factionDeclaresPeace, factionDeclaredPeace, evaluatingFaction);
            NewDiplomacyModel.WarStats warStats2 = NewDiplomacyModel.CalculateWarStatsForPeace(factionDeclaredPeace, factionDeclaresPeace, evaluatingFaction);
            benefitScore = NewDiplomacyModel.CalculateBenefitScore(warStats, warStats2);
            riskScore = NewDiplomacyModel.CalculateRiskScore(warStats, warStats2);
            riskScore = MathF.Min(warStats2.ValueOfSettlements * 0.75f, riskScore);
            benefitScore = MathF.Min(warStats.ValueOfSettlements * 1.5f, benefitScore);
        }

        private static float CalculateBenefitScore(NewDiplomacyModel.WarStats faction1Stats, NewDiplomacyModel.WarStats faction2Stats)
        {
            float num = MathF.Clamp(faction2Stats.ValueOfSettlements, 10000f, 10000000f);
            float num2 = (faction2Stats.Strength + faction1Stats.TotalStrengthOfEnemies) / faction1Stats.Strength;
            float num3 = MathF.Clamp(1f / (1f + num2 * num2), 0.1f, 0.9f);
            return num * num3;
        }

        private static float ApplyWarProgressToRiskScore(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace, float riskScore)
        {
            float resultNumber = Campaign.Current.Models.DiplomacyModel.GetWarProgressScore(factionDeclaresPeace, factionDeclaredPeace, false).ResultNumber;
            float resultNumber2 = Campaign.Current.Models.DiplomacyModel.GetWarProgressScore(factionDeclaredPeace, factionDeclaresPeace, false).ResultNumber;
            float num = MathF.Abs(resultNumber2 - resultNumber);
            if (num < 75f)
            {
                riskScore *= MBMath.Map(num, 0f, 75f, 0.5f, 1f);
            }
            else if (resultNumber2 > resultNumber)
            {
                float num2 = (resultNumber2 - resultNumber + 650f) / 650f;
                riskScore *= num2;
            }
            return riskScore;
        }

        private static float GetExposureScoreToOtherFaction(IFaction factionDeclaresWar, IFaction factionDeclaredWar)
        {
            HashSet<Settlement> hashSet = new HashSet<Settlement>();
            float num = 0f;
            float num2 = 0f;
            if (factionDeclaresWar.Fiefs.Count == 0 || factionDeclaredWar.Fiefs.Count == 0)
            {
                return 1f;
            }
            foreach (Town town in factionDeclaresWar.Fiefs)
            {
                foreach (Settlement settlement in town.GetNeighborFortifications(MobileParty.NavigationType.All))
                {
                    if (settlement.MapFaction != factionDeclaresWar && !hashSet.Contains(settlement))
                    {
                        if (settlement.MapFaction == factionDeclaredWar)
                        {
                            num2 += 1f;
                        }
                        num += 1f;
                        hashSet.Add(settlement);
                    }
                }
            }
            HashSet<Settlement> hashSet2 = new HashSet<Settlement>();
            foreach (Settlement settlement2 in hashSet)
            {
                foreach (Settlement settlement3 in settlement2.Town.GetNeighborFortifications(MobileParty.NavigationType.All))
                {
                    if (settlement3.MapFaction != factionDeclaresWar && !hashSet.Contains(settlement3) && !hashSet2.Contains(settlement3))
                    {
                        if (settlement3.MapFaction == factionDeclaredWar)
                        {
                            num2 += 0.2f;
                        }
                        num += 0.2f;
                        hashSet2.Add(settlement3);
                    }
                }
            }
            if (num2 < 0.2f)
            {
                return float.MinValue;
            }
            //return 0.8f + num2 / num;
            return CommonConstants.BASE_EXPOSURE_SCORE + num2 / num;
        }

        private TextObject GetReasonForDeclaringPeace(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace, Clan evaluatingClan)
        {
            if (NewDiplomacyModel.GetExposureScoreToOtherFaction(factionDeclaresPeace, factionDeclaredPeace).ApproximatelyEqualsTo(-3.4028235E+38f, 1E-05f))
            {
                return new TextObject("{=i0h0LKa0}Our borders are far from those of the enemy. It is too arduous to pursue this war.", null);
            }
            NewDiplomacyModel.WarStats warStats = NewDiplomacyModel.CalculateWarStatsForPeace(factionDeclaresPeace, factionDeclaredPeace, evaluatingClan);
            NewDiplomacyModel.WarStats warStats2 = NewDiplomacyModel.CalculateWarStatsForPeace(factionDeclaredPeace, factionDeclaresPeace, evaluatingClan);
            float num;
            float num2;
            NewDiplomacyModel.GetBenefitAndRiskScoreForPeace(factionDeclaresPeace, factionDeclaredPeace, evaluatingClan, out num, out num2);
            float num3 = NewDiplomacyModel.ApplyWarProgressToRiskScore(factionDeclaresPeace, factionDeclaredPeace, num2);
            TextObject textObject;
            if (num - num2 > 0f)
            {
                if (num - num3 < 0f)
                {
                    textObject = new TextObject("{=QQtJobYP}We need time to recover from the hardships of war.", null);
                }
                else
                {
                    textObject = new TextObject("{=vwjs6EjJ}On balance, the gains we stand to make are not worth the costs and risks.", null);
                }
            }
            else if (warStats.Strength < warStats2.Strength)
            {
                textObject = new TextObject("{=JOe3BC41}The {ENEMY_KINGDOM_INFORMAL_NAME} is currently more powerful than us. We need time to build up our strength.", null);
            }
            else if (warStats.Strength > warStats2.Strength && warStats.Strength < warStats2.Strength + warStats.TotalStrengthOfEnemies)
            {
                textObject = new TextObject("{=vwjs6EjJ}On balance, the gains we stand to make are not worth the costs and risks.", null);
            }
            else if (warStats.Strength < warStats2.Strength + warStats.TotalStrengthOfEnemies)
            {
                textObject = new TextObject("{=nuqv4GAA}We have too many enemies. We need to make peace with at least some of them.", null);
            }
            else
            {
                textObject = new TextObject("{=HqJSNG3M}Our realm is currently doing well, but we stand to lose this wealth if we go on fighting.", null);
            }
            if (!TextObject.IsNullOrEmpty(textObject))
            {
                textObject.SetTextVariable("ENEMY_KINGDOM_INFORMAL_NAME", factionDeclaredPeace.InformalName);
            }
            return textObject;
        }

        private static void UpdateOurBenefitMinusOurRiskBasedOnEvaluatingFaction(IFaction evaluatingFaction, ref float ourBenefit, ref float ourRisk)
        {
            if (ourBenefit.ApproximatelyEqualsTo(ourRisk, 1E-05f))
            {
                return;
            }
            if (!evaluatingFaction.IsKingdomFaction && evaluatingFaction.Leader != evaluatingFaction.MapFaction.Leader)
            {
                bool flag = ourBenefit > ourRisk;
                if (flag && evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Valor) != 0)
                {
                    ourBenefit *= 1f - 0.05f * (float)MathF.Min(2, MathF.Max(-2, evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Valor)));
                    return;
                }
                if (!flag && evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Calculating) != 0)
                {
                    ourRisk *= 1f + 0.05f * (float)MathF.Min(2, MathF.Max(-2, evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Calculating)));
                }
            }
        }

        private static float GetWarScale(IFaction factionDeclaresWar, IFaction factionDeclaredWar)
        {
            StanceLink stanceWith = factionDeclaresWar.GetStanceWith(factionDeclaredWar);
            if (!stanceWith.IsAtWar)
            {
                return 1f;
            }
            int casualties = stanceWith.GetCasualties(factionDeclaredWar);
            int casualties2 = stanceWith.GetCasualties(factionDeclaresWar);
            int num = MathF.Max(1, (int)stanceWith.WarStartDate.ElapsedDaysUntilNow);
            if (num <= 20)
            {
                return 1f;
            }
            float num2 = (float)MathF.Max(casualties + casualties2, 1) / (20f * MathF.Pow((float)num, 1.5f));
            if (num2 >= 1f || num2 <= 0f)
            {
                return 1f;
            }
            return num2;
        }

        private static float GetRelationScore(IFaction factionDeclaresWar, IFaction factionDeclaredWar, IFaction evaluatingFaction)
        {
            float relationWithClan = (float)factionDeclaresWar.Leader.Clan.GetRelationWithClan(factionDeclaredWar.Leader.Clan);
            int relationWithClan2 = evaluatingFaction.Leader.Clan.GetRelationWithClan(factionDeclaredWar.Leader.Clan);
            float num = (relationWithClan + (float)relationWithClan2) / 2f;
            float result = 0f;
            if (num < 0f)
            {
                if (factionDeclaresWar.CurrentTotalStrength > factionDeclaredWar.CurrentTotalStrength * 2f)
                {
                    result = -250f * num;
                }
                else
                {
                    float num2 = factionDeclaresWar.CurrentTotalStrength / (2f * factionDeclaredWar.CurrentTotalStrength);
                    result = -250f * (num2 * num2) * num;
                }
            }
            return result;
        }

        private static float GetSameCultureTownScore(IFaction factionDeclaresWar, IFaction factionDeclaredWar)
        {
            float b = factionDeclaredWar.Settlements.Sum(delegate (Settlement s)
            {
                if (s.Culture != factionDeclaresWar.Culture || !s.IsFortification)
                {
                    return 0f;
                }
                return s.Town.Prosperity * 0.5f * 50f;
            });
            float num = MathF.Min(100000f, b);
            return 0.3f * num;
        }

        /// <summary>
        /// 计算指定氏族（Clan）基于其封地繁荣度的战争倾向系数
        /// 返回值范围大致为 [-0.4, +0.6]
        /// 负值表示倾向和平，正值表示倾向战争
        /// </summary>
        private float GetClanFiefWarPropensity(Clan clan)
        {
            if (clan.Fiefs.Count == 0)
            {
                ModLogger.Debug($"[Diplomacy] Clan '{clan.Name}' has no fiefs. Using landless war propensity: {CommonConstants.LANDLESS_CLAN_WAR_PROPENSITY:F2}.");
                return CommonConstants.LANDLESS_CLAN_WAR_PROPENSITY; // 无封地 → 根据设计，此处应为负值（倾向和平）
            }

            float totalScore = 0f;
            float totalWeight = 0f;
            StringBuilder debugDetails = new StringBuilder();

            foreach (Town town in clan.Fiefs)
            {
                float prosperity = town.Prosperity;
                bool isCastle = town.IsCastle;
                float weight = isCastle ? CommonConstants.CASTLE_WEIGHT : CommonConstants.TOWN_WEIGHT;
                float score;

                if (isCastle)
                {
                    if (prosperity < CommonConstants.CASTLE_POOR_THRESHOLD)
                        score = CommonConstants.PROSPERITY_SCORE_POOR;
                    else if (prosperity < CommonConstants.CASTLE_AVERAGE_THRESHOLD)
                        score = CommonConstants.PROSPERITY_SCORE_AVERAGE;
                    else if (prosperity < CommonConstants.CASTLE_RICH_THRESHOLD)
                        score = CommonConstants.PROSPERITY_SCORE_RICH;
                    else
                        score = CommonConstants.PROSPERITY_SCORE_VERY_RICH;
                }
                else
                {
                    if (prosperity < CommonConstants.TOWN_POOR_THRESHOLD)
                        score = CommonConstants.PROSPERITY_SCORE_POOR;
                    else if (prosperity < CommonConstants.TOWN_AVERAGE_THRESHOLD)
                        score = CommonConstants.PROSPERITY_SCORE_AVERAGE;
                    else if (prosperity < CommonConstants.TOWN_RICH_THRESHOLD)
                        score = CommonConstants.PROSPERITY_SCORE_RICH;
                    else
                        score = CommonConstants.PROSPERITY_SCORE_VERY_RICH;
                }

                totalScore += score * weight;
                totalWeight += weight;
                debugDetails.AppendLine($"  - {town.Name} (Prosperity: {prosperity:F0}, IsCastle: {isCastle}) -> Score: {score:F2}, Weight: {weight:F1}");
            }

            float finalPropensity = totalWeight > 0f ? totalScore / totalWeight : 0f;
            ModLogger.Debug(
                $"[Diplomacy] Calculated war propensity for Clan '{clan.Name}':\n" +
                $"{debugDetails}" +
                $"Total Score: {totalScore:F2}, Total Weight: {totalWeight:F2} -> Final Propensity: {finalPropensity:F3}"
            );
            return finalPropensity;
        }

        /// <summary>
        /// 应用评估方阵营（通常是王国）的战争潜力（由其领地繁荣度体现）来最终调整风险分数。
        /// 贫穷的王国会放大风险（更怕打），富有的王国会缩小风险（更能打）。
        /// 此函数应在 ApplyWarProgressToRiskScore 之后调用，作为风险计算的最后一步战略修正。
        /// </summary>
        private static float ApplyWarPotentialToRiskScore(
            IFaction factionDeclaresPeace, 
            float riskScore)
        {
            // 我们只关心提出议和的一方（即评估方）的国力
            if (factionDeclaresPeace is not Kingdom evaluatingKingdom || evaluatingKingdom.Fiefs.Count == 0)
            {
                return riskScore; // 非王国或无领地，不调整
            }

            ModLogger.Debug($"[外交分析] 开始应用战争潜力修正风险分 (评估方: {evaluatingKingdom.Name})");
            ModLogger.Debug($"[外交分析] - 修正前风险分: {riskScore:F0}");

            float totalWeightedProsperity = 0f;
            float totalWeight = 0f;

            foreach (var fief in evaluatingKingdom.Fiefs)
            {
                if (fief is Town town)
                {
                    bool isCastle = town.IsCastle;
                    float weight = isCastle ? CommonConstants.CASTLE_WEIGHT : CommonConstants.TOWN_WEIGHT;
                    totalWeightedProsperity += town.Prosperity * weight;
                    totalWeight += weight;
                }
            }

            float riskMultiplier = 1.0f;
            if (totalWeight > 0f)
            {
                float avgProsperity = totalWeightedProsperity / totalWeight;

                // 使用 common.cs 中的阈值进行分级
                if (avgProsperity < CommonConstants.TOWN_POOR_THRESHOLD) { riskMultiplier = 1.8f; } // 极度贫困
                else if (avgProsperity < CommonConstants.TOWN_AVERAGE_THRESHOLD) { riskMultiplier = 1.4f; } // 贫困
                else if (avgProsperity < CommonConstants.TOWN_RICH_THRESHOLD) { riskMultiplier = 0.8f; } // 一般/富有
                else { riskMultiplier = 0.5f; } // 非常富有
            }

            float originalRisk = riskScore;
            riskScore *= riskMultiplier;

            ModLogger.Debug($"[外交分析] - 王国加权平均繁荣度: {totalWeightedProsperity / totalWeight:F0}");
            ModLogger.Debug($"[外交分析] - 战争潜力风险系数: {riskMultiplier:F2}");
            ModLogger.Debug($"[外交分析] - 修正后风险分: {originalRisk:F0} → {riskScore:F0}");
            ModLogger.Debug($"[外交分析] 战争潜力最终修正完成\n");

            return riskScore;
        }

        ///// <summary>
        ///// 重写：评估某个氏族是否支持对另一势力宣战
        ///// </summary>
        //public override float GetScoreOfDeclaringWar(
        //    IFaction factionDeclaresWar,
        //    IFaction factionDeclaredWar,
        //    Clan evaluatingClan,
        //    out TextObject reason,
        //    bool includeReason = false)
        //{
        //    float baseScore = base.GetScoreOfDeclaringWar(
        //        factionDeclaresWar, factionDeclaredWar, evaluatingClan, out reason, includeReason);
        //    float finalScore = baseScore;

        //    if (evaluatingClan != null)
        //    {
        //        float warPropensity = this.GetClanFiefWarPropensity(evaluatingClan);
        //        float adjustment = warPropensity * CommonConstants.WAR_PROPENSITY_SCORE_MULTIPLIER;

        //        finalScore += adjustment;

        //        ModLogger.Debug(
        //            $"[Diplomacy] GetScoreOfDeclaringWar (Clan Level)\n" +
        //            $"- Evaluating Clan: {evaluatingClan.Name}\n" +
        //            $"- Declaring War: {factionDeclaresWar.Name} vs {factionDeclaredWar.Name}\n" +
        //            $"- Base Score: {baseScore:F2}\n" +
        //            $"- War Propensity: {warPropensity:F3}\n" +
        //            $"- Adjustment ({warPropensity:F3} * {CommonConstants.WAR_PROPENSITY_SCORE_MULTIPLIER:F0}): {adjustment:F2}\n" +
        //            $"- Final Score: {finalScore:F2}"
        //        );
        //    }

        //    return finalScore;
        //}

        /// <summary>
        /// 重写：评估某个氏族是否支持与另一势力议和
        /// 采用侵入式修改，直接复用并扩展原始逻辑。
        /// </summary>
        public override float GetScoreOfDeclaringPeaceForClan(
            IFaction factionDeclaresPeace,
            IFaction factionDeclaredPeace,
            Clan evaluatingClan,
            out TextObject reason,
            bool includeReason = false)
        {
            bool shouldLog = false;
            if (evaluatingClan != null && Clan.PlayerClan.Kingdom != null)
            {
                shouldLog = (evaluatingClan.Kingdom == Clan.PlayerClan.Kingdom);
            }

            if (shouldLog)
            {
                ModLogger.Debug($"[外交分析] 开始计算议和倾向");
                ModLogger.Debug($"[外交分析] - 评估氏族: '{evaluatingClan.Name}'");
                ModLogger.Debug($"[外交分析] - 议和双方: '{factionDeclaresPeace.Name}' 与 '{factionDeclaredPeace.Name}'");
            }

            // --- 1. 复制 DefaultDiplomacyModel 的原始逻辑 ---
            reason = null;
            if (includeReason)
            {
                reason = this.GetReasonForDeclaringPeace(factionDeclaresPeace, factionDeclaredPeace, evaluatingClan);
            }

            float exposureScore = NewDiplomacyModel.GetExposureScoreToOtherFaction(factionDeclaresPeace, factionDeclaredPeace);
            if (exposureScore.ApproximatelyEqualsTo(-3.4028235E+38f, 1E-05f))
            {
                if (shouldLog)
                {
                    ModLogger.Debug($"[外交分析] 双方距离过远，返回最大和平倾向值。");
                }
                return 10000000f;
            }
            //exposureScore = MathF.Min(exposureScore * 1.4f, NewDiplomacyModel.GetExposureScoreToOtherFaction(factionDeclaredPeace, factionDeclaresPeace));
            exposureScore = MathF.Min(exposureScore, NewDiplomacyModel.GetExposureScoreToOtherFaction(factionDeclaredPeace, factionDeclaresPeace));
            exposureScore += GetClanFiefWarPropensity(evaluatingClan);

            float benefitScore;
            float riskScore;
            NewDiplomacyModel.GetBenefitAndRiskScoreForPeace(factionDeclaresPeace, factionDeclaredPeace, evaluatingClan, out benefitScore, out riskScore);
            NewDiplomacyModel.UpdateOurBenefitMinusOurRiskBasedOnEvaluatingFaction(evaluatingClan, ref benefitScore, ref riskScore);
            riskScore = NewDiplomacyModel.ApplyWarProgressToRiskScore(factionDeclaresPeace, factionDeclaredPeace, riskScore);
            benefitScore *= NewDiplomacyModel.GetWarScale(factionDeclaresPeace, factionDeclaredPeace);

            float relationScore = NewDiplomacyModel.GetRelationScore(factionDeclaresPeace, factionDeclaredPeace, evaluatingClan);
            float sameCultureTownScore = NewDiplomacyModel.GetSameCultureTownScore(factionDeclaresPeace, factionDeclaredPeace);



            // 计算原始的基础分数 (注意符号: 原始逻辑返回的是负值表示支持和平)
            float baseScore = (sameCultureTownScore + benefitScore * exposureScore - riskScore + relationScore) * -1f;

            float finalScore = baseScore;

            if (shouldLog)
            {
                ModLogger.Debug($"[外交分析] 中间计算结果:");
                ModLogger.Debug($"[外交分析]   - 边境接触程度: {exposureScore:F2}");
                ModLogger.Debug($"[外交分析]   - 战争收益: {benefitScore:F2}");
                ModLogger.Debug($"[外交分析]   - 战争风险: {riskScore:F2}");
                ModLogger.Debug($"[外交分析]   - 双方关系: {relationScore:F2}");
                ModLogger.Debug($"[外交分析]   - 同文化加成: {sameCultureTownScore:F2}");
                ModLogger.Debug($"[外交分析]   - 基础和平倾向分: {baseScore:F2}");
                ModLogger.Debug($"[外交分析] 最终得分: {finalScore:F2}");
                ModLogger.Debug($"[外交分析] 结束\n");
            }

            return finalScore;
        }

        /// <summary>
        /// 【关键】王国层面是否应议和？——直接影响 AI 是否生成和平提案
        /// </summary>
        public override float GetScoreOfDeclaringPeace(
            IFaction factionDeclaresPeace, 
            IFaction factionDeclaredPeace)
        {
            string name1 = factionDeclaresPeace?.Name?.ToString() ?? "NULL";
            string name2 = factionDeclaredPeace?.Name?.ToString() ?? "NULL";
            ModLogger.Debug($"[外交分析] 开始计算王国 '{name1}' 向 '{name2}' 提议议和的基础倾向分");

            float exposureScore = NewDiplomacyModel.GetExposureScoreToOtherFaction(factionDeclaresPeace, factionDeclaredPeace);

            ModLogger.Debug($"[外交分析] - 步骤1: 计算边境接触度");
            ModLogger.Debug($"[外交分析]   - {name1} 与 {name2} 的边境接触分: {exposureScore:F6}");
            if (exposureScore.ApproximatelyEqualsTo(-3.4028235E+38f, 1E-05f))
            {
                ModLogger.Debug($"[外交分析] - 双方距离过远，无法有效交战。返回最大和平倾向值。");
                return 10000000f;
            }

            // exposureScore = MathF.Min(exposureScore * 1.4f, NewDiplomacyModel.GetExposureScoreToOtherFaction(factionDeclaredPeace, factionDeclaresPeace));
            exposureScore = MathF.Min(exposureScore, NewDiplomacyModel.GetExposureScoreToOtherFaction(factionDeclaredPeace, factionDeclaresPeace));
            ModLogger.Debug($"[外交分析] - 调整后最终边境接触分 (取双向最小值): {exposureScore:F6}");

            float benefitScore;
            float riskScore;

            NewDiplomacyModel.GetBenefitAndRiskScoreForPeace(
                factionDeclaresPeace, 
                factionDeclaredPeace, 
                factionDeclaresPeace.Leader.Clan, 
                out benefitScore, out riskScore);
            ModLogger.Debug($"[外交分析] - 步骤2: 计算战争收益与风险");
            ModLogger.Debug($"[外交分析]   - 战争收益分 (Benefit): {benefitScore:F0}");
            ModLogger.Debug($"[外交分析]   - 战争风险分 (Risk): {riskScore:F0}");

            riskScore = NewDiplomacyModel.ApplyWarProgressToRiskScore(
                factionDeclaresPeace, 
                factionDeclaredPeace, 
                riskScore);
            ModLogger.Debug($"[外交分析] - 步骤3.1: 根据战争进程调整风险分");
            ModLogger.Debug($"[外交分析]   - 调整后战争风险分: {riskScore:F0}");

            riskScore = ApplyWarPotentialToRiskScore(factionDeclaresPeace, riskScore);
            ModLogger.Debug($"[外交分析] - 步骤3.2: 根据战争潜力调整风险分");
            ModLogger.Debug($"[外交分析]   - 调整后战争风险分: {riskScore:F0}");

            float warScale = NewDiplomacyModel.GetWarScale(factionDeclaresPeace, factionDeclaredPeace);
            ModLogger.Debug($"[外交分析] - 步骤4: 计算战争规模因子");
            ModLogger.Debug($"[外交分析]   - 战争规模因子: {warScale:F3}");

            benefitScore *= warScale;
            ModLogger.Debug($"[外交分析]   - 应用规模因子后的战争收益分: {benefitScore:F0}");

            // 如果目标阵营有己方文化的城镇，会增加议和的吸引力（可能为了收复失地）。
            float sameCultureTownScore = NewDiplomacyModel.GetSameCultureTownScore(factionDeclaresPeace, factionDeclaredPeace);
            ModLogger.Debug($"[外交分析] - 步骤5: 计算同文化领地加成");
            ModLogger.Debug($"[外交分析]   - 同文化领地加成分: {sameCultureTownScore:F0}");

            // --- Step 6: 计算最终分数 ---
            // 原始公式: (同文化加成 + 收益*暴露度 - 风险) * -1
            // 乘以 -1 是因为：在原版设计中，这个函数返回的是“和平倾向分”。
            // 所以，当 (收益 - 风险) 为负（即风险大于收益）时，最终结果为正，表示支持和平。
            float finalScore = (sameCultureTownScore + benefitScore * exposureScore - riskScore) * -1f;
            
            ModLogger.Debug($"[外交分析] - 步骤6: 计算最终和平倾向分");
            ModLogger.Debug($"[外交分析]   - 计算式: ({sameCultureTownScore:F0} + {benefitScore:F0} * {exposureScore:F3} - {riskScore:F0}) * -1");
            ModLogger.Debug($"[外交分析]   - 最终和平倾向分: {finalScore:F2}");
            ModLogger.Debug($"[外交分析] 结束\n");

            return finalScore;
        }

        ///// <summary>
        ///// 重写：判断和平是否“合适”。
        ///// 复制 DefaultDiplomacyModel 的原始逻辑，并添加详细日志。
        ///// </summary>
        //public override bool IsPeaceSuitable(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)
        //{
        //    // 安全获取名称（避免空引用）
        //    string name1 = factionDeclaresPeace?.Name?.ToString() ?? "NULL";
        //    string name2 = factionDeclaredPeace?.Name?.ToString() ?? "NULL";

        //    // 检查势力是否被消灭
        //    if (factionDeclaresPeace.IsEliminated || factionDeclaredPeace.IsEliminated)
        //    {
        //        ModLogger.Debug(
        //            $"[Mod] IsPeaceSuitable: Peace NOT suitable.\n" +
        //            $"- Reason: One faction is eliminated.\n" +
        //            $"- {name1} eliminated: {factionDeclaresPeace.IsEliminated}\n" +
        //            $"- {name2} eliminated: {factionDeclaredPeace.IsEliminated}"
        //        );
        //        return false;
        //    }

        //    // 调用分数计算（这些方法可能已被我们重写，会触发我们的日志）
        //    float score1 = Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringPeace(factionDeclaresPeace, factionDeclaredPeace);
        //    float score2 = Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringPeace(factionDeclaredPeace, factionDeclaresPeace);
        //    float settlementValue = Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(factionDeclaresPeace);
        //    float threshold = Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(factionDeclaredPeace);

        //    float num;
        //    if (score2 > 0f)
        //    {
        //        num = score2 - score1;
        //    }
        //    else
        //    {
        //        num = threshold - score1;
        //    }

        //    StanceLink stance = factionDeclaresPeace.GetStanceWith(factionDeclaredPeace);
        //    float warDays = stance.WarStartDate.ElapsedDaysUntilNow;

        //    bool isSuitable = num <= settlementValue || warDays >= 150;

        //    ModLogger.Debug(
        //        $"[Mod] IsPeaceSuitable evaluation:\n" +
        //        $"- Factions: {name1} <-> {name2}\n" +
        //        $"- Score({name1} wants peace): {score1:F2}\n" +
        //        $"- Score({name2} wants peace): {score2:F2}\n" +
        //        $"- Settlement Value ({name1}): {settlementValue:F2}\n" +
        //        $"- Decision Threshold ({name2}): {threshold:F2}\n" +
        //        $"- Calculated 'num': {num:F2}\n" +
        //        $"- War Duration: {warDays} days\n" +
        //        $"- Condition 1 (num <= settlementValue): {num <= settlementValue}\n" +
        //        $"- Condition 2 (war >= 150 days): {warDays >= 150}\n" +
        //        $"- FINAL RESULT: {(isSuitable ? "SUITABLE" : "NOT SUITABLE")}"
        //    );

        //    return isSuitable;
        //}


        public override float GetScoreOfClanToJoinKingdom(Clan clan, Kingdom kingdom)
        {
            if (clan.Kingdom != null && clan.Kingdom.RulingClan == clan)
            {
                return -100000000f;
            }
            int relationBetweenClans = FactionManager.GetRelationBetweenClans(kingdom.RulingClan, clan);
            int num = 0;
            int num2 = 0;
            foreach (Clan clan2 in kingdom.Clans)
            {
                int relationBetweenClans2 = FactionManager.GetRelationBetweenClans(clan, clan2);
                num += relationBetweenClans2;
                num2++;
            }
            float num3 = (num2 > 0) ? ((float)num / (float)num2) : 0f;
            float num4 = MathF.Max(-100f, MathF.Min(100f, (float)relationBetweenClans + num3));
            float num5 = MathF.Min(2f, MathF.Max(0.33f, 1f + MathF.Sqrt(MathF.Abs(num4)) * ((num4 < 0f) ? -0.067f : 0.1f)));
            float num6 = 1f;
            if (kingdom.Culture == clan.Culture)
            {
                num6 += 0.15f;
            }
            else if (kingdom.Leader != Hero.MainHero)
            {
                num6 -= 0.15f;
            }
            float num7 = clan.CalculateTotalSettlementBaseValue();
            float num8 = clan.CalculateTotalSettlementValueForFaction(kingdom);
            int commanderLimit = clan.CommanderLimit;
            float num9 = 0f;
            float num10 = 0f;
            if (!clan.IsMinorFaction)
            {
                float num11 = 0f;
                foreach (Town town in kingdom.Fiefs)
                {
                    num11 += town.Settlement.GetSettlementValueForFaction(kingdom);
                }
                int num12 = 0;
                foreach (Clan clan3 in kingdom.Clans)
                {
                    if (!clan3.IsUnderMercenaryService && clan3 != clan)
                    {
                        num12 += clan3.CommanderLimit;
                    }
                }
                num9 = num11 / (float)(num12 + commanderLimit);
                //num10 = -((float)(num12 * num12) * 100f) + 10000f;
                // 提升10000f为100000f，提高接纳独立clan的概率
                //num10 = -((float)(num12 * num12) * 100f) + 50000f;
                num10 = ((float)(num12 * num12) * 100f) - 50000f;
            }
            float num13 = num9 * MathF.Sqrt((float)commanderLimit) * 0.15f * 0.2f;
            num13 *= num5 * num6;
            num13 += (clan.MapFaction.IsAtWarWith(kingdom) ? (num8 - num7) : 0f);
            num13 += num10;
            if (clan.Kingdom != null && clan.Kingdom.Leader == Hero.MainHero && num13 > 0f)
            {
                num13 *= 0.2f;
            }
            return num13;
        }


    }

    //[HarmonyPatch(typeof(KingdomDecisionProposalBehavior), "ConsiderPeace")]
    //public static class ConsiderPeacePatch
    //{
    //    /// <summary>
    //    /// Prefix 补丁：完全替代原 ConsiderPeace 逻辑，并注入详细日志。
    //    /// </summary>
    //    public static bool Prefix(
    //        Clan clan,
    //        Clan otherClan,
    //        IFaction otherFaction,
    //        out MakePeaceKingdomDecision decision)
    //    {
    //        decision = null; // 初始化输出参数

    //        string clanName = clan.Name.ToString() ?? "NULL";
    //        string otherFactionName = otherFaction?.Name?.ToString() ?? "NULL";

    //        ModLogger.Debug($"[Mod] [ConsiderPeace] START - Evaluating peace proposal from {clanName} to {otherFactionName}");

    //        // --- Step 1: Check if peace is suitable ---
    //        if (!Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(clan.MapFaction, otherFaction))
    //        {
    //            ModLogger.Debug($"[Mod] [ConsiderPeace] REJECTED - Peace not suitable (IsPeaceSuitable returned false).");
    //            return false; // 告诉 Harmony 不要执行原方法，并使用我们的返回值
    //        }

    //        // --- Step 2: Check score threshold ---
    //        float peaceScore = Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringPeace(clan.MapFaction, otherFaction);
    //        float threshold = Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(clan.Kingdom);

    //        if (peaceScore < threshold)
    //        {
    //            ModLogger.Debug(
    //                $"[Mod] [ConsiderPeace] REJECTED - Peace score below threshold.\n" +
    //                $"  Score: {peaceScore:F2}, Threshold: {threshold:F2}"
    //            );
    //            return false;
    //        }

    //        // --- Step 3: Calculate tribute ---
    //        int dailyTributeDurationInDays;
    //        int dailyTributeToPay = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(clan, otherClan, out dailyTributeDurationInDays);

    //        if (dailyTributeToPay < 0)
    //        {
    //            ModLogger.Debug($"[Mod] [ConsiderPeace] REJECTED - Invalid tribute amount ({dailyTributeToPay}).");
    //            return false;
    //        }

    //        // --- Step 4: Create and validate decision ---
    //        MakePeaceKingdomDecision makePeaceDecision = new MakePeaceKingdomDecision(
    //            clan, otherFaction, dailyTributeToPay, dailyTributeDurationInDays, true, false);

    //        TextObject failureReason;
    //        if (!makePeaceDecision.CanMakeDecision(out failureReason))
    //        {
    //            string reasonText = failureReason?.ToString() ?? "Unknown reason";
    //            ModLogger.Debug($"[Mod] [ConsiderPeace] REJECTED - Decision failed validation: {reasonText}");
    //            return false;
    //        }

    //        // --- Step 5: Simulate election and check support ---
    //        var possibleOutcomes = makePeaceDecision.DetermineInitialCandidates();
    //        var peaceOutcome = possibleOutcomes.FirstOrDefault(x => x is MakePeaceKingdomDecision.MakePeaceDecisionOutcome outcome && outcome.ShouldPeaceBeDeclared)
    //            as MakePeaceKingdomDecision.MakePeaceDecisionOutcome;

    //        if (peaceOutcome == null)
    //        {
    //            ModLogger.Debug($"[Mod] [ConsiderPeace] REJECTED - No valid 'ShouldPeaceBeDeclared' outcome found.");
    //            return false;
    //        }

    //        float support = makePeaceDecision.DetermineSupport(clan, peaceOutcome);
    //        if (support <= 0f)
    //        {
    //            ModLogger.Debug($"[Mod] [ConsiderPeace] REJECTED - Support for peace is zero or negative ({support:F2}).");
    //            return false;
    //        }

    //        // --- SUCCESS ---
    //        decision = makePeaceDecision;
    //        ModLogger.Debug(
    //            $"[Mod] [ConsiderPeace] ACCEPTED! Peace proposal created.\n" +
    //            $"  Proposer: {clanName}\n" +
    //            $"  Target: {otherFactionName}\n" +
    //            $"  Tribute: {dailyTributeToPay}/day for {dailyTributeDurationInDays} days\n" +
    //            $"  Support Level: {support:F2}"
    //        );

    //        // 返回 false 表示“不要运行原始方法”，我们已经设置了 `decision` 并决定了返回值（通过 output 参数）
    //        return false;
    //    }
    //}
}
