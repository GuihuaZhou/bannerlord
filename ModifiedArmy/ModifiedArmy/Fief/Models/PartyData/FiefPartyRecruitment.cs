using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.PartyFinance.Models;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;
using static System.Collections.Specialized.BitVector32;
using static ModifiedArmy.common.CommonConstants;

namespace ModifiedArmy.Models.Fief
{
    public partial class FiefPartyData
    {
        private void RemoveTroopsFromParty(TroopRoster roster, CharacterObject troop, int countToRemove)
        {
            if (roster == null || troop == null || countToRemove <= 0) return;
            int currentCount = roster.GetElementNumber(troop);
            if (currentCount <= 0) return;
            int actualRemove = Math.Min(countToRemove, currentCount);
            if (actualRemove > 0)
            {
                roster.RemoveTroop(troop, actualRemove, default(UniqueTroopDescriptor), 0);
            }
        }

        /// <summary>
        /// 从封邑军队中按比例招募士兵到目标部队。
        /// - 从 _fiefParty 移除已征召士兵（因为他们已离营）
        /// - Records the transferred troops in a new FiefServiceBatch.
        /// - 总兵力仍归来源封邑，只从就绪转为在役批次
        ///
        /// 经济规则：
        /// - 玩家征召：产生 Prosperity / Hearth 动员成本
        /// - AI 征召：不产生 Prosperity / Hearth 成本
        /// </summary>
        /// <param name="targetParty">目标部队</param>
        /// <returns>总招募人数（必为 10 的倍数）</returns>
        public int RecruitTroopsToParty(
            MobileParty targetParty,
            int maximumCount = int.MaxValue)
        {
            if (targetParty == null || _fiefParty == null)
                return 0;

            if (targetParty.LeaderHero == null)
                return 0;

            if (_settlement.OwnerClan != targetParty.LeaderHero.Clan)
                return 0;

            int currentMembers = targetParty.Party.NumberOfAllMembers;
            int partySizeLimit = targetParty.Party.PartySizeLimit;
            int remainSize = Math.Min(
                partySizeLimit - currentMembers,
                Math.Max(0, maximumCount));

            // A full party cannot receive another fief detachment.
            if (remainSize <= 0)
                return 0;

            // 记录招募的士兵和数量
            Dictionary<CharacterObject, int> tmpRecruitTroops = new();

            float prosperityCost = 0;
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取
            //
            // 若无模板（无 fallback），则不扣除繁荣度/户数。
            // ============================================================
            int tmpProsperityCostPerTroop =
                GetFiefProductionConfig()?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                GetFiefProductionConfig()?.HearthCostPerTroop ?? 0;

            List<RecruitmentCandidate> candidates =
                BuildAvailableRecruitmentCandidates(remainSize);

            RecruitmentPlan aiPlan = null;
            Dictionary<CharacterObject, int> approvedTroops =
                targetParty.LeaderHero.Clan == Clan.PlayerClan
                    ? BuildPlayerFiefRecruitment(
                        targetParty,
                        candidates)
                    : BuildAiFiefRecruitment(
                        targetParty,
                        candidates,
                        out aiPlan);

            int approvedCount = approvedTroops.Values.Sum();
            int minimumAiBatch = MilitaryConfigManager.Instance
                .RequireActiveConfig()
                .AiRecruitment.ForceManagement.MinimumFiefRecruitBatch;
            if (targetParty.LeaderHero.Clan != Clan.PlayerClan &&
                approvedCount > 0 && approvedCount < minimumAiBatch)
            {
                LogAiFiefRecruitment(
                    targetParty,
                    aiPlan,
                    candidates.Sum(candidate => candidate.AvailableCount),
                    0);
                return 0;
            }
            FiefPartyManager manager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            if (approvedCount <= 0 || manager == null ||
                !manager.TryMobilize(_settlement, approvedCount, 1))
            {
                LogAiFiefRecruitment(
                    targetParty,
                    aiPlan,
                    candidates.Sum(candidate => candidate.AvailableCount),
                    0);
                return 0;
            }

            // Apply the completed decision only after all candidates have
            // been evaluated. This prevents earlier roster mutations from
            // changing the model inputs for later candidates.
            foreach (var approval in approvedTroops)
            {
                CharacterObject troop = approval.Key;
                int taken = approval.Value;

                if (troop == null || taken <= 0)
                {
                    continue;
                }

                // 审批与实际提交之间名册可能变化，因此提交时再次收紧数量。
                taken = Math.Min(taken, _fiefParty.GetElementNumber(troop));
                if (taken <= 0 || _fiefPartyTemplate?.IsEnableTroop(troop) != true)
                    continue;
                RemoveTroopsFromParty(_fiefParty, troop, taken);
                targetParty.MemberRoster.AddToCounts(
                    troop,
                    taken,
                    false,
                    0,
                    0,
                    true,
                    -1);

                // Fief troops have no immediate denar purchase price, but
                // their post-exemption wage still consumes the clan's shared
                // thirty-day recruitment commitment.
                if (ClanRecruitmentBudgetManager
                    .GetSupportingClan(targetParty) != Clan.PlayerClan)
                {
                    ClanRecruitmentBudgetManager.CommitRecruitment(
                        targetParty,
                        taken,
                        0f,
                        AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                            targetParty,
                            troop));
                }

                tmpRecruitTroops[troop] = taken;
                hearthCost += taken;
            }

            int totalRecruited =
                tmpRecruitTroops.Values.Sum();

            if (totalRecruited <= 0)
            {
                LogAiFiefRecruitment(
                    targetParty,
                    aiPlan,
                    candidates.Sum(candidate => candidate.AvailableCount),
                    0);
                return 0;
            }

            LogAiFiefRecruitment(
                targetParty,
                aiPlan,
                candidates.Sum(candidate => candidate.AvailableCount),
                totalRecruited);

            // Rural mobilization has already been reserved atomically.

            CreateServingDetachment(targetParty, tmpRecruitTroops);

            TextObject msg =
                GameTexts.FindText(
                    "str_modifiedarmy_fief_recruit_to_party");

            msg.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(targetParty));

            msg.SetTextVariable(
                "SETTLEMENT_NAME",
                _settlement.Name.ToString());

            msg.SetTextVariable("RECRUITED", totalRecruited);
            msg.SetTextVariable("TROOP_COUNT", _totalTroopCount);
            msg.SetTextVariable("TOTAL_LIMIT", _totalLimit);

            // 玩家显示实际成本；AI 显示 0
            msg.SetTextVariable(
                "PROSPERITY_COST",
                prosperityCost);

            msg.SetTextVariable(
                "HEARTH_COST",
                hearthCost);

            if (targetParty.LeaderHero.Clan == Clan.PlayerClan)
            {
                ModLogger.Notice(msg.ToString());
            }
            else if (RecruitmentLogFilter.ShouldLog(targetParty))
            {
                ModLogger.Info(msg.ToString());
            }

            return totalRecruited;
        }

        /// <summary>
        /// Builds the same template-weighted offers used by recruitment,
        /// without changing the fief, party, budget, or service records.
        /// </summary>
        internal IReadOnlyList<RecruitmentCandidate>
            PreviewFiefRecruitmentCandidates(
                MobileParty targetParty,
                int maximumCount)
        {
            var candidates = new List<RecruitmentCandidate>();
            if (targetParty == null || _fiefParty == null)
                return candidates;
            int remainSize = Math.Min(
                targetParty.Party.PartySizeLimit -
                    targetParty.Party.NumberOfAllMembers,
                Math.Max(0, maximumCount));
            if (remainSize <= 0) return candidates;

            return BuildAvailableRecruitmentCandidates(remainSize);
        }

        /// <summary>
        /// 为预览和最终征召建立同一份只读候选。候选只来自当前真实就绪名册，
        /// 并由当前封邑模板判定身份；XML生产权重不限制已有士兵的抽调。
        /// </summary>
        private List<RecruitmentCandidate> BuildAvailableRecruitmentCandidates(int maximumCount)
        {
            List<RecruitmentCandidate> candidates = new();
            int remaining = Math.Max(0, maximumCount);
            foreach (TroopRosterElement element in _fiefParty.GetTroopRoster())
            {
                if (remaining <= 0) break;
                CharacterObject troop = element.Character;
                // Preview and commit must expose the same troop universe.
                // Otherwise the planner can approve a professional troop that
                // GetFiefTroopRoster intentionally filtered out.
                if (troop == null || element.Number <= 0 ||
                    _fiefPartyTemplate?.IsEnableTroop(troop) != true)
                    continue;
                int count = Math.Min(element.Number, remaining);
                if (count <= 0) continue;
                candidates.Add(new RecruitmentCandidate(
                    troop,
                    count,
                    RecruitmentSource.Fief));
                remaining -= count;
            }
            return candidates;
        }

        /// <summary>
        /// Evaluates the exact fief manifest that the authoritative commit
        /// path would use, including player/AI finance and minimum-batch rules.
        /// </summary>
        internal IReadOnlyDictionary<CharacterObject, int>
            PreviewFiefRecruitmentApprovals(
                MobileParty targetParty,
                int maximumCount)
        {
            IReadOnlyList<RecruitmentCandidate> candidates =
                PreviewFiefRecruitmentCandidates(targetParty, maximumCount);
            if (targetParty?.LeaderHero?.Clan == null || candidates.Count == 0)
                return new Dictionary<CharacterObject, int>();
            RecruitmentPlan ignored;
            Dictionary<CharacterObject, int> result =
                targetParty.LeaderHero.Clan == Clan.PlayerClan
                    ? BuildPlayerFiefRecruitment(targetParty, candidates)
                    : BuildAiFiefRecruitment(targetParty, candidates, out ignored);
            int minimumBatch = MilitaryConfigManager.Instance
                .RequireActiveConfig()
                .AiRecruitment.ForceManagement.MinimumFiefRecruitBatch;
            return targetParty.LeaderHero.Clan != Clan.PlayerClan &&
                result.Values.Sum() > 0 && result.Values.Sum() < minimumBatch
                    ? new Dictionary<CharacterObject, int>()
                    : result;
        }

        /// <summary>
        /// Keeps the legacy automatic player recruitment behavior separate
        /// from the AI composition model. The player remains constrained by
        /// party space and the existing additional-wage allowance only.
        /// </summary>
        private static Dictionary<CharacterObject, int>
            BuildPlayerFiefRecruitment(
                MobileParty targetParty,
                IReadOnlyList<RecruitmentCandidate> candidates)
        {
            Dictionary<CharacterObject, int> result = new();
            float remainingDailyWageBudget =
                AiRecruitmentFinancialModel
                    .GetAvailableAdditionalDailyWage(targetParty);

            foreach (RecruitmentCandidate candidate in candidates)
            {
                float unitDailyWage =
                    AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                        targetParty,
                        candidate.Troop);
                int approved = candidate.AvailableCount;

                if (unitDailyWage > 0f)
                {
                    approved = Math.Min(
                        approved,
                        (int)Math.Floor(
                            remainingDailyWageBudget / unitDailyWage));
                }

                if (approved <= 0)
                {
                    continue;
                }

                result[candidate.Troop] = approved;
                remainingDailyWageBudget = Math.Max(
                    0f,
                    remainingDailyWageBudget - approved * unitDailyWage);
            }

            return result;
        }

        /// <summary>
        /// Evaluates all fief offers together so role, quality, wage and
        /// thirty-day affordability limits see one consistent party state.
        /// </summary>
        private static Dictionary<CharacterObject, int>
            BuildAiFiefRecruitment(
                MobileParty targetParty,
                IReadOnlyList<RecruitmentCandidate> candidates,
                out RecruitmentPlan plan)
        {
            Dictionary<CharacterObject, int> result = new();
            plan = RecruitmentModelManager.Model.BuildPlan(
                targetParty,
                candidates);

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.Troop == null ||
                    evaluation.RecruitableCount <= 0)
                {
                    continue;
                }

                result[evaluation.Troop] = evaluation.RecruitableCount;
            }

            return result;
        }

        /// <summary>
        /// Emits one compact record for an AI fief recruitment attempt.
        /// Player recruitment already has its existing result notification.
        /// </summary>
        private void LogAiFiefRecruitment(
            MobileParty targetParty,
            RecruitmentPlan plan,
            int offeredCount,
            int approvedCount)
        {
            if (plan == null)
            {
                return;
            }

            if (!RecruitmentLogFilter.ShouldLog(targetParty))
            {
                return;
            }

            RecruitmentLimitReason mainLimit = RecruitmentLimitReason.None;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.RecruitableCount < evaluation.RequestedCount)
                {
                    mainLimit = evaluation.PrimaryLimit;
                    break;
                }
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_ai_recruitment_fief_plan");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(targetParty));
            message.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
            message.SetTextVariable("CULTURE_ID", plan.CultureId);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", approvedCount);
            message.SetTextVariable(
                "LIMIT",
                GetRecruitmentLimitText(mainLimit));
            if (approvedCount > 0)
            {
                ModLogger.Info(message.ToString());
            }
            else
            {
                ModLogger.Info(message.ToString());
            }
        }

        /// <summary>
        /// Resolves the same stable localization keys used by the other
        /// unified recruitment integrations.
        /// </summary>
        private static TextObject GetRecruitmentLimitText(
            RecruitmentLimitReason reason)
        {
            switch (reason)
            {
                case RecruitmentLimitReason.InvalidParty:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_invalid_party");
                case RecruitmentLimitReason.InvalidTroop:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_invalid_troop");
                case RecruitmentLimitReason.PartySize:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_party_size");
                case RecruitmentLimitReason.WageLimit:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_wage");
                case RecruitmentLimitReason.RecruitmentCost:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_cost");
                case RecruitmentLimitReason.MaintenanceFunds:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_maintenance");
                case RecruitmentLimitReason.BanditTroopExcluded:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_bandit");
                default:
                    return GameTexts.FindText("str_modifiedarmy_recruit_limit_none");
            }
        }


        /// <summary>
        /// 从封邑就绪部队中手动招募指定的士兵，并记录到征召列表。
        /// 前提：selectedRoster 来自 FiefTroops，所有 troop 和数量均有效。
        /// </summary>
        /// <param name="selectedRoster">玩家从 _fiefParty 中选择的士兵</param>
        /// <returns>实际招募的总人数（用于工资豁免）</returns>
        public int RecruitManualSelection(TroopRoster selectedRoster, MobileParty targetParty)
        {
            if (selectedRoster == null || selectedRoster.TotalManCount <= 0 ||
                _fiefParty == null || targetParty == null)
                return 0;

            var tmpRecruitTroops = new Dictionary<CharacterObject, int>();
            float prosperityCost = 0;
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取（与 RecruitTroopsToParty 一致）
            // ============================================================
            int tmpProsperityCostPerTroop =
                GetFiefProductionConfig()?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                GetFiefProductionConfig()?.HearthCostPerTroop ?? 0;

            int remainingPartySpace = Math.Max(0,
                targetParty.Party.PartySizeLimit - targetParty.Party.NumberOfAllMembers);
            Dictionary<CharacterObject, int> validatedSelection = new();
            foreach (var element in selectedRoster.GetTroopRoster())
            {
                if (remainingPartySpace <= 0) break;
                CharacterObject troop = element.Character;
                if (troop == null || element.Number <= 0 ||
                    _fiefPartyTemplate?.IsEnableTroop(troop) != true) continue;
                int available = _fiefParty.GetElementNumber(troop);
                int approved = Math.Min(remainingPartySpace,
                    Math.Min(element.Number, available));
                if (approved <= 0) continue;
                validatedSelection[troop] = approved;
                remainingPartySpace -= approved;
            }
            int requestedCount = validatedSelection.Values.Sum();
            FiefPartyManager manager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            if (requestedCount <= 0 || manager == null ||
                !manager.TryMobilize(_settlement, requestedCount, 1))
            {
                return 0;
            }

            foreach (var element in selectedRoster.GetTroopRoster())
            {
                var troop = element.Character;
                int count = troop != null && validatedSelection.TryGetValue(troop, out int approved)
                    ? approved
                    : 0;
                if (troop == null || count <= 0) continue;

                // A selected troop is recruitable only when it belongs to this
                // exact fief template, not merely to a globally classified
                // militia/sergeant/retinue category.
                if (_fiefPartyTemplate?.IsEnableTroop(troop) != true)
                    continue;

                // 记录招募的士兵
                tmpRecruitTroops[troop] = count;
                // 从封邑party移除士兵
                RemoveTroopsFromParty(_fiefParty, troop, count);

                // ====================================================
                // Hearth 成本
                //
                // 每征召 1 名士兵，消耗 1 Hearth。
                // Prosperity 不在这里计算，统一在本次征召完成后计算。
                // ====================================================
                hearthCost += count;
            }

            int totalRecruited =
                tmpRecruitTroops.Values.Sum();

            if (totalRecruited <= 0)
                return 0;

            // Rural mobilization has already been reserved atomically.

            CreateServingDetachment(targetParty, tmpRecruitTroops);

            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_recruit_to_party");
            msg.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(targetParty));
            msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msg.SetTextVariable("RECRUITED", totalRecruited);
            msg.SetTextVariable("TROOP_COUNT", _totalTroopCount);
            msg.SetTextVariable("TOTAL_LIMIT", _totalLimit);
            msg.SetTextVariable("PROSPERITY_COST", prosperityCost);
            msg.SetTextVariable("HEARTH_COST", hearthCost);

            if (targetParty.LeaderHero.Clan == Clan.PlayerClan)
            {
                ModLogger.Notice(msg.ToString());
            }
            else
            {
                ModLogger.Debug(msg.ToString());
            }

            // Add exactly the roster that was validated and removed from the
            // fief, never the unchecked UI selection object.
            foreach (var recruited in tmpRecruitTroops)
                targetParty.MemberRoster.AddToCounts(recruited.Key, recruited.Value);

            return totalRecruited;
        }


        /// <summary>
        /// 从定居点俘虏中直接招募指定数量的士兵到封建部队（无需冷却）。
        /// </summary>
        /// <param name="troop">要招募的士兵类型</param>
        /// <param name="num">要招募的数量</param>
        /// <returns>是否成功招募</returns>
        public bool RecruitFromPrisoners(CharacterObject troop, int count)
        {
            // 参数校验
            if (troop == null || count <= 0)
                return false;

            if (_fiefParty == null)
                return false;

            // 1. 检查是否为封建部队允许的兵种
            if (_fiefPartyTemplate == null || !_fiefPartyTemplate.IsEnableTroop(troop))
                return false;

            // 2. 俘虏转入与生产共用封邑总体容量，不再使用类别子上限。
            SyncManpowerCounters();
            int availableCapacity = Math.Max(0, _totalLimit - _totalTroopCount);

            if (availableCapacity < count)
                return false;


            // Prisoner conversion does not consume or restore rural capacity.
            _fiefParty.AddToCounts(troop, count, false, 0, 0, true, -1);

            // 消耗的繁荣度
            float prosperityCost = 0;
            // 消耗的户数
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取（与 RecruitTroopsToParty 一致）
            // ============================================================
            int tmpProsperityCostPerTroop =
                GetFiefProductionConfig()?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                GetFiefProductionConfig()?.HearthCostPerTroop ?? 0;

            hearthCost += count * tmpHearthCostPerTroop;
            prosperityCost += count * tmpProsperityCostPerTroop * troop.Tier;

            // 4. 更新派生总数
            SyncManpowerCounters();

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_fief_prisoner_recruited");
            message.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
            message.SetTextVariable("COUNT", count);
            message.SetTextVariable("TROOP_NAME", troop.Name);
            ModLogger.Notice(message.ToString());

            return true;
        }
    }
}
