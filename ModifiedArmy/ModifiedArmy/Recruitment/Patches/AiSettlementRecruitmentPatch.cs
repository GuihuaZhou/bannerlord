using HarmonyLib;
using Helpers;
using ModifiedArmy.common;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Classification;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// 替换原版 RecruitmentCampaignBehavior.CheckRecruiting
    /// 按 AI 决策权重依次执行封邑征召、驻军调拨和雇佣兵招募。
    /// </summary>
    /// 

    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "CheckRecruiting")]
    public static class AiRecruitmentPatch
    {
        /// <summary>
        /// 完全接管 CheckRecruiting，按 AI 决策权重降序尝试各兵源
        /// </summary>
        private static bool Prefix(
            RecruitmentCampaignBehavior __instance, 
            MobileParty mobileParty, 
            Settlement settlement)
        {
            // ==========================================
            // 1. 商队雇佣兵逻辑（原版 if 分支保留）
            // ==========================================
            if (settlement.IsTown && mobileParty.IsCaravan)
            {
                TryRecruitCaravanMercenary(__instance, mobileParty, settlement);
                return false; // 商队分支处理完毕，跳过原版
            }

            // ==========================================
            // 2. 领主队伍前置条件检查（原版 else if 条件保留）
            // ==========================================
            bool isLordEligible =
                mobileParty.IsLordParty &&
                !mobileParty.IsDisbanding &&
                mobileParty.LeaderHero != null &&
                !mobileParty.Party.IsStarving &&
                mobileParty.Party.LeaderHero.IsAlive &&
                (float)mobileParty.PartyTradeGold > HeroHelper.StartRecruitingMoneyLimit(mobileParty.LeaderHero) &&
                (mobileParty.LeaderHero == mobileParty.LeaderHero.Clan.Leader ||
                    (float)mobileParty.LeaderHero.Clan.Gold > HeroHelper.StartRecruitingMoneyLimitForClanLeader(mobileParty.LeaderHero)) &&
                mobileParty.Party.PartySizeLimit > 0 &&
                (long)mobileParty.Party.NumberOfAllMembers * 100L <=
                    (long)mobileParty.Party.PartySizeLimit * 95L;

            if (!isLordEligible)
                return false; // 不满足领主条件，跳过原版

            // ==========================================
            // 3. 获取 AI 招募决策
            // ==========================================
            var aiBehavior = Campaign.Current.GetCampaignBehavior<AiRecruitmentBehavior>();
            if (aiBehavior == null)
                return false;

            RecruitmentDecision decision = aiBehavior.GetRecruitmentDecision(mobileParty);
            if (!decision.ShouldRecruit)
                return false;

            // ==========================================
            // 4. 按权重降序遍历候选兵源并路由执行
            // ==========================================
            var candidates = new List<(RecruitSource source, float weight)>
            {
                (RecruitSource.Fief, decision.FiefWeight),
                (RecruitSource.GarrisonTransfer, decision.GarrisonTransferWeight),
                (RecruitSource.Mercenary, decision.MercenaryWeight)
            };
            // 按权重从高到低排序
            candidates.Sort((a, b) => b.weight.CompareTo(a.weight));
            
            // 衰减因子：每次成功招募后，后续兵权的概率乘以此系数
            const float DecayFactor = 0.4f; 
            // 当前概率乘数（初始为 1.0，成功招募后衰减）
            float currentProbabilityMultiplier = 1.0f;

            foreach (var (source, weight) in candidates)
            {
                if (weight <= 0.01f) continue;

                // 将原始权重转换为 [0, 1] 之间的浮点数
                float recruitProbability = MathF.Clamp(weight, 0f, 1f);
                // 应用衰减后的最终概率
                float finalProbability = recruitProbability * currentProbabilityMultiplier;
                if (RecruitmentLogFilter.ShouldLog(mobileParty))
                {
                    ModLogger.Debug(
                        $"[AIRecruitment] {PartyLogFormatter.GetDisplayName(mobileParty)} is trying to recruit from {source} at {settlement.Name}. " +
                        $"The success chance is {finalProbability:P2}.");
                }

                // 生成随机数，小于权重（概率）则招募
                if (MBRandom.RandomFloat < finalProbability)
                {
                    bool recruited = false;
                    switch (source)
                    {
                        case RecruitSource.Fief:
                            recruited = TryRecruitFief(mobileParty, settlement);
                            break;
                        case RecruitSource.GarrisonTransfer:
                            recruited = TryTransferFromGarrison(__instance, mobileParty, settlement);
                            break;
                        case RecruitSource.Mercenary:
                            recruited = TryRecruitLordMercenary(__instance, mobileParty, settlement, finalProbability);
                            break;
                    }

                    // 一旦前面执行了招募，后面的招募概率继续降低
                    if (recruited)
                    {
                        currentProbabilityMultiplier *= DecayFactor;
                    }
                }
            }

            return false; // 拦截原版逻辑
        }

        private static void ApplyRecruitMercenary(
            RecruitmentCampaignBehavior instance,
            MobileParty party,
            Settlement settlement,
            CharacterObject troop,
            int number)
        {
            int cost = Campaign.Current.Models.PartyWageModel
                .GetTroopRecruitmentCost(
                    troop,
                    party.LeaderHero,
                    false)
                .RoundedResultNumber;
            if (party.IsCaravan)
            {
                party.PartyTradeGold -= number * cost;
            }
            else
            {
                GiveGoldAction.ApplyBetweenCharacters(
                    party.LeaderHero,
                    null,
                    number * cost,
                    true);
            }
            instance.GetMercenaryData(settlement.Town)
                .ChangeMercenaryCount(-number);
            party.AddElementToMemberRoster(troop, number, false);
            CampaignEventDispatcher.Instance.OnTroopRecruited(
                party.LeaderHero,
                settlement,
                null,
                troop,
                number);
        }

        // ==========================================
        // 商队雇佣兵招募（原版 if 分支逻辑提取）
        // ==========================================
        private static void TryRecruitCaravanMercenary(
            RecruitmentCampaignBehavior instance, MobileParty party, Settlement settlement)
        {
            var mercenaryData = instance.GetMercenaryData(settlement.Town);
            if (!mercenaryData.HasAvailableMercenary(Occupation.CaravanGuard) &&
                !mercenaryData.HasAvailableMercenary(Occupation.Mercenary))
                return;

            int partySizeLimit = party.Party.PartySizeLimit;
            if (party.Party.NumberOfAllMembers >= partySizeLimit)
                return;

            CharacterObject troopType = mercenaryData.TroopType;
            int cost = Campaign.Current.Models.PartyWageModel
                .GetTroopRecruitmentCost(troopType, party.LeaderHero, false).RoundedResultNumber;
            int extraCost = party.IsCaravan ? 2000 : 0;

            if (party.PartyTradeGold <= cost + extraCost)
                return;

            bool flag = true;
            double probability = 0.0;
            for (int i = 0; i < mercenaryData.Number; i++)
            {
                if (flag)
                {
                    int remainingGold = party.PartyTradeGold - (cost + extraCost);
                    double goldFactor = Math.Sqrt(MathF.Min(1f, (float)remainingGold / (100f * cost)));
                    float fillRatio = (float)party.Party.NumberOfAllMembers / partySizeLimit;
                    float sizeFactor = (MathF.Min(10f, 1f / fillRatio) * MathF.Min(10f, 1f / fillRatio) - 1f)
                                     * ((party.IsCaravan && party.Party.Owner == Hero.MainHero) ? 0.4f : 0.1f);
                    probability = goldFactor * sizeFactor;
                }

                if (MBRandom.RandomFloat < probability)
                {
                    ApplyRecruitMercenary(instance, party, settlement, troopType, 1);
                    flag = true;
                }
                else
                {
                    flag = false;
                }
            }
        }

        // ==========================================
        // 封邑兵招募
        // ==========================================
        private static bool TryRecruitFief(MobileParty party, Settlement settlement)
        {
            var fiefManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (fiefManager == null) return false;

            int available = fiefManager.GetAvailableTroopCount(settlement);
            if (available <= 0) return false;

            return fiefManager.RecruitFiefTroopsFromSettlement(
                settlement,
                party) > 0;
        }

        // ==========================================
        // AI lord garrison transfer
        // ==========================================
        private static bool TryTransferFromGarrison(
            RecruitmentCampaignBehavior instance,
            MobileParty party,
            Settlement settlement)
        {
            if (party.Party.NumberOfAllMembers >= party.Party.PartySizeLimit)
            {
                return false;
            }

            if (!settlement.IsFortification)
            {
                return false;
            }
            if (party?.ActualClan == null ||
                party.ActualClan != settlement?.OwnerClan)
            {
                return false;
            }
            int freeSlots = Math.Max(
                0,
                party.Party.PartySizeLimit -
                    party.Party.NumberOfAllMembers);
            return GarrisonTransferPolicyModel
                .TransferApprovedTroopsFromGarrison(
                    party,
                    settlement,
                    freeSlots) > 0;
        }

        // ==========================================
        // 领主雇佣兵招募（原版 else if 内 mercenary 逻辑提取）
        // ==========================================

        private static bool TryRecruitLordMercenary(
            RecruitmentCampaignBehavior instance, 
            MobileParty party, 
            Settlement settlement,
            float mercenaryWeight)
        {
            if (!settlement.IsTown) return false;

            var mercenaryData = instance.GetMercenaryData(settlement.Town);
            if (!mercenaryData.HasAvailableMercenary(Occupation.Mercenary)) return false;

            CharacterObject troopType = mercenaryData.TroopType;
            if (troopType == null) return false;

            float recruitProbability = MathF.Clamp(mercenaryWeight, 0f, 1f);

            int recruited = 0;
            int maxAvailable = mercenaryData.Number;

            // 对每个可用雇佣兵掷骰子
            for (int j = 0; j < maxAvailable; j++)
            {
                if (MBRandom.RandomFloat < recruitProbability)
                    recruited++;
            }

            if (recruited <= 0)
            {
                return false;
            }

            // The probability roll decides demand. The unified model then
            // applies composition, capacity, wage and thirty-day affordability
            // constraints to the requested quantity.
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                party,
                new List<RecruitmentCandidate>
                {
                    new RecruitmentCandidate(
                        troopType,
                        recruited,
                        RecruitmentSource.Mercenary)
                });
            RecruitmentEvaluationResult evaluation =
                plan.Evaluations.Count > 0
                    ? plan.Evaluations[0]
                    : null;

            if (evaluation == null)
            {
                return false;
            }

            LogMercenaryEvaluation(
                party,
                settlement,
                plan,
                evaluation);

            if (evaluation.RecruitableCount <= 0)
            {
                return false;
            }

            ApplyRecruitMercenary(
                instance,
                party,
                settlement,
                troopType,
                evaluation.RecruitableCount);
            ClanRecruitmentBudgetManager.CommitRecruitment(
                party,
                evaluation.RecruitableCount,
                evaluation.UnitRecruitmentCost,
                evaluation.UnitDailyWage);

            return true;
        }

        /// <summary>
        /// Emits one localized diagnostic record for each mercenary request
        /// evaluated by the unified recruitment model.
        /// </summary>
        private static void LogMercenaryEvaluation(
            MobileParty party,
            Settlement settlement,
            RecruitmentPlan plan,
            RecruitmentEvaluationResult evaluation)
        {
            if (!RecruitmentLogFilter.ShouldLog(party))
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_ai_recruitment_mercenary_evaluation");

            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
            message.SetTextVariable("CULTURE_ID", plan.CultureId);
            message.SetTextVariable("TROOP_NAME", evaluation.Troop.Name);
            message.SetTextVariable("TIER", evaluation.Troop.Tier);
            message.SetTextVariable(
                "ROLE",
                GetCombatRoleText(evaluation.CombatRole));
            message.SetTextVariable(
                "QUALITY",
                GetQualityText(evaluation.Quality));
            message.SetTextVariable("REQUESTED", evaluation.RequestedCount);
            message.SetTextVariable("APPROVED", evaluation.RecruitableCount);
            message.SetTextVariable(
                "LIMIT",
                GetLimitReasonText(evaluation.PrimaryLimit));
            message.SetTextVariable(
                "UNIT_COST",
                evaluation.UnitRecruitmentCost);
            message.SetTextVariable("UNIT_WAGE", evaluation.UnitDailyWage);
            message.SetTextVariable("DAYS", evaluation.SustainableDays);
            if (evaluation.RecruitableCount > 0)
            {
                ModLogger.Info(message.ToString());
            }
            else
            {
                ModLogger.Info(message.ToString());
            }
        }

        /// <summary>
        /// Converts internal diagnostic enums to localized player-facing text.
        /// </summary>
        private static TextObject GetCombatRoleText(
            CombatRole role)
        {
            switch (role)
            {
                case CombatRole.Ranged:
                    return GameTexts.FindText("str_modifiedarmy_recruit_role_ranged");
                case CombatRole.Cavalry:
                    return GameTexts.FindText("str_modifiedarmy_recruit_role_cavalry");
                case CombatRole.HorseArcher:
                    return GameTexts.FindText("str_modifiedarmy_recruit_role_horse_archer");
                default:
                    return GameTexts.FindText("str_modifiedarmy_recruit_role_infantry");
            }
        }

        /// <summary>
        /// Converts a Tier-based quality group to localized diagnostic text.
        /// </summary>
        private static TextObject GetQualityText(
            TroopQuality quality)
        {
            switch (quality)
            {
                case TroopQuality.MiddleTier:
                    return GameTexts.FindText("str_modifiedarmy_recruit_quality_middle");
                case TroopQuality.TopTier:
                    return GameTexts.FindText("str_modifiedarmy_recruit_quality_top");
                default:
                    return GameTexts.FindText("str_modifiedarmy_recruit_quality_low");
            }
        }

        /// <summary>
        /// Uses a stable localization key for every recruitment limit reason.
        /// </summary>
        private static TextObject GetLimitReasonText(
            RecruitmentLimitReason reason)
        {
            return GameTexts.FindText(GetLimitReasonTextId(reason));
        }

        private static string GetLimitReasonTextId(
            RecruitmentLimitReason reason)
        {
            switch (reason)
            {
                case RecruitmentLimitReason.InvalidParty:
                    return "str_modifiedarmy_recruit_limit_invalid_party";
                case RecruitmentLimitReason.InvalidTroop:
                    return "str_modifiedarmy_recruit_limit_invalid_troop";
                case RecruitmentLimitReason.PartySize:
                    return "str_modifiedarmy_recruit_limit_party_size";
                case RecruitmentLimitReason.CombatRole:
                    return "str_modifiedarmy_recruit_limit_combat_role";
                case RecruitmentLimitReason.Quality:
                    return "str_modifiedarmy_recruit_limit_quality";
                case RecruitmentLimitReason.WageLimit:
                    return "str_modifiedarmy_recruit_limit_wage";
                case RecruitmentLimitReason.RecruitmentCost:
                    return "str_modifiedarmy_recruit_limit_cost";
                case RecruitmentLimitReason.MaintenanceFunds:
                    return "str_modifiedarmy_recruit_limit_maintenance";
                default:
                    return "str_modifiedarmy_recruit_limit_none";
            }
        }
    }
}
