using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.PartyManagement.Finance.Models;
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
        /// <returns>实际招募人数</returns>
        public int RecruitTroopsToParty(
            MobileParty targetParty,
            int maximumCount = int.MaxValue,
            IReadOnlyDictionary<CharacterObject, int> expectedApprovals = null,
            Settlement villageFilter = null)
        {
            if (targetParty == null || _fiefParty == null)
                return 0;

            if (targetParty.LeaderHero == null)
                return 0;

            if (_settlement.OwnerClan != targetParty.LeaderHero.Clan)
                return 0;
            bool useTemplateWeights =
                targetParty.LeaderHero.Clan != Clan.PlayerClan;

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
            List<RecruitmentCandidate> candidates =
                BuildAvailableRecruitmentCandidates(
                    remainSize,
                    useTemplateWeights,
                    villageFilter);
            LogWeightedCandidateManifest(targetParty, remainSize, candidates);

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
            if (expectedApprovals != null &&
                !RecruitmentManifestEquals(approvedTroops, expectedApprovals))
            {
                return 0;
            }
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
            if (approvedCount <= 0)
            {
                LogAiFiefRecruitment(
                    targetParty,
                    aiPlan,
                    candidates.Sum(candidate => candidate.AvailableCount),
                    0);
                return 0;
            }

            if (!TryCommitVillageRecruitment(targetParty, approvedTroops,
                useTemplateWeights, villageFilter, out tmpRecruitTroops))
                return 0;
            hearthCost = tmpRecruitTroops.Values.Sum();

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

            foreach (var recruited in tmpRecruitTroops)
                if (ClanRecruitmentBudgetManager.GetSupportingClan(targetParty) != Clan.PlayerClan)
                    ClanRecruitmentBudgetManager.CommitRecruitment(targetParty,
                        recruited.Value, 0f,
                        AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                            targetParty, recruited.Key));

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
            msg.SetTextVariable("TROOP_COUNT", GetTotalTroopCount());
            msg.SetTextVariable("OCCUPIED_HEARTH",
                GetTotalOccupiedSupplyHearth());
            msg.SetTextVariable("FEUDAL_BUDGET_HEARTH",
                GetTotalFeudalBudgetHearth());

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

        private static bool RecruitmentManifestEquals(
            IReadOnlyDictionary<CharacterObject, int> left,
            IReadOnlyDictionary<CharacterObject, int> right)
        {
            if (left == null || right == null || left.Count != right.Count)
                return false;
            foreach (var pair in left)
            {
                if (pair.Key == null || pair.Value <= 0 ||
                    !right.TryGetValue(pair.Key, out int count) ||
                    count != pair.Value)
                    return false;
            }
            return right.All(pair => pair.Key != null && pair.Value > 0);
        }

        private void LogWeightedCandidateManifest(
            MobileParty targetParty,
            int requestedCount,
            IReadOnlyList<RecruitmentCandidate> candidates)
        {
            if (targetParty?.LeaderHero?.Clan == null ||
                targetParty.LeaderHero.Clan == Clan.PlayerClan ||
                !RecruitmentLogFilter.ShouldLog(targetParty))
                return;
            string manifest = string.Join(
                ",",
                candidates
                    .OrderBy(candidate => candidate.Troop?.StringId,
                        StringComparer.Ordinal)
                    .Select(candidate => string.Format(
                        "{0}:{1}",
                        candidate.Troop?.StringId ?? "unknown",
                        candidate.AvailableCount)));
            ModLogger.Debug(string.Format(
                "AI fief weighted candidates: Template={0}; Settlement={1}; " +
                "Requested={2}; Offered={3}; Manifest={4}",
                _fiefPartyTemplate?.TemplateId ?? "unknown",
                _settlement?.StringId ?? "unknown",
                requestedCount,
                candidates.Sum(candidate => candidate.AvailableCount),
                manifest));
        }

        /// <summary>
        /// Builds the same AI-weighted or legacy-player offers used by
        /// recruitment without changing fief, party, budget, or service records.
        /// </summary>
        internal IReadOnlyList<RecruitmentCandidate>
            PreviewFiefRecruitmentCandidates(
                MobileParty targetParty,
                int maximumCount,
                Settlement villageFilter = null)
        {
            var candidates = new List<RecruitmentCandidate>();
            if (targetParty == null || _fiefParty == null)
                return candidates;
            int remainSize = Math.Min(
                targetParty.Party.PartySizeLimit -
                    targetParty.Party.NumberOfAllMembers,
                Math.Max(0, maximumCount));
            if (remainSize <= 0) return candidates;

            bool useTemplateWeights = targetParty.LeaderHero?.Clan != null &&
                targetParty.LeaderHero.Clan != Clan.PlayerClan;
            return BuildAvailableRecruitmentCandidates(
                remainSize,
                useTemplateWeights,
                villageFilter);
        }

        /// <summary>
        /// 为预览和最终征召建立同一份只读候选。候选只来自当前真实就绪名册，
        /// 并由当前封邑模板判定身份；XML生产权重不限制已有士兵的抽调。
        /// </summary>
        private List<RecruitmentCandidate> BuildAvailableRecruitmentCandidates(
            int maximumCount,
            bool useTemplateWeights,
            Settlement villageFilter)
        {
            return useTemplateWeights
                ? BuildTemplateWeightedRecruitmentCandidates(
                    maximumCount, villageFilter)
                : BuildLegacyRecruitmentCandidates(
                    maximumCount, villageFilter);
        }

        /// <summary>
        /// Preserves the existing automatic player ordering and limits.
        /// </summary>
        private List<RecruitmentCandidate> BuildLegacyRecruitmentCandidates(
            int maximumCount,
            Settlement villageFilter)
        {
            List<RecruitmentCandidate> candidates = new();
            int remaining = Math.Max(0, maximumCount);
            foreach (TroopRosterElement element in GetFiefTroopRoster(
                villageFilter, true).GetTroopRoster())
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
        /// Builds deterministic AI offers from the template's positive-weight
        /// roots. Existing healthy troops are assigned to the nearest matching
        /// root and scarce quota is redistributed without mutating game state.
        /// </summary>
        private List<RecruitmentCandidate>
            BuildTemplateWeightedRecruitmentCandidates(int maximumCount,
                Settlement villageFilter)
        {
            var candidates = new List<RecruitmentCandidate>();
            if (_fiefParty == null || _fiefPartyTemplate == null ||
                maximumCount <= 0)
                return candidates;

            List<WeightedRecruitmentRoot> roots = BuildWeightedRoots();
            if (roots.Count == 0) return candidates;

            var healthyStock = new Dictionary<CharacterObject, int>();
            foreach (TroopRosterElement element in GetFiefTroopRoster(
                villageFilter, true).GetTroopRoster())
            {
                CharacterObject troop = element.Character;
                int healthy = Math.Max(0, element.Number - element.WoundedNumber);
                if (troop == null || troop.IsHero || healthy <= 0 ||
                    !_fiefPartyTemplate.IsEnableTroop(troop))
                    continue;
                healthyStock[troop] = healthyStock.TryGetValue(troop, out int old)
                    ? old + healthy
                    : healthy;
            }

            foreach (var stock in healthyStock)
            {
                WeightedRecruitmentRoot selected = null;
                int selectedDistance = int.MaxValue;
                foreach (WeightedRecruitmentRoot root in roots)
                {
                    int distance = GetUpgradeDistance(root.Troop, stock.Key);
                    if (distance < 0 || distance > selectedDistance) continue;
                    if (distance < selectedDistance || selected == null ||
                        root.Ordinal < selected.Ordinal ||
                        root.Ordinal == selected.Ordinal &&
                        string.CompareOrdinal(
                            root.Troop.StringId,
                            selected.Troop.StringId) < 0)
                    {
                        selected = root;
                        selectedDistance = distance;
                    }
                }
                if (selected != null)
                    selected.Stock[stock.Key] = stock.Value;
            }

            int target = Math.Min(
                maximumCount,
                roots.Sum(root => root.Stock.Values.Sum()));
            AllocateWeightedQuota(roots, target);

            foreach (WeightedRecruitmentRoot root in roots
                .OrderBy(item => item.Ordinal)
                .ThenBy(item => item.Troop.StringId, StringComparer.Ordinal))
            {
                int quota = root.Allocation;
                foreach (var stock in root.Stock
                    .OrderByDescending(item => item.Key.Tier)
                    .ThenBy(item => item.Key.StringId, StringComparer.Ordinal))
                {
                    if (quota <= 0) break;
                    int taken = Math.Min(quota, stock.Value);
                    if (taken <= 0) continue;
                    candidates.Add(new RecruitmentCandidate(
                        stock.Key,
                        taken,
                        RecruitmentSource.Fief));
                    quota -= taken;
                }
            }
            return candidates;
        }

        private List<WeightedRecruitmentRoot> BuildWeightedRoots()
        {
            var roots = new List<WeightedRecruitmentRoot>();
            var byTroop = new Dictionary<CharacterObject, WeightedRecruitmentRoot>();
            int ordinal = 0;
            foreach (var entry in _fiefPartyTemplate.TroopEntries)
            {
                CharacterObject troop = entry?.Troop;
                int weight = entry?.Weight ?? 0;
                if (troop == null || weight <= 0)
                {
                    ordinal++;
                    continue;
                }
                if (byTroop.TryGetValue(troop, out WeightedRecruitmentRoot existing))
                {
                    existing.Weight += weight;
                }
                else
                {
                    var root = new WeightedRecruitmentRoot(troop, weight, ordinal);
                    roots.Add(root);
                    byTroop[troop] = root;
                }
                ordinal++;
            }
            return roots;
        }

        private static int GetUpgradeDistance(
            CharacterObject root,
            CharacterObject target)
        {
            if (root == null || target == null) return -1;
            if (root == target) return 0;
            var visited = new HashSet<CharacterObject> { root };
            var queue = new Queue<KeyValuePair<CharacterObject, int>>();
            queue.Enqueue(new KeyValuePair<CharacterObject, int>(root, 0));
            while (queue.Count > 0)
            {
                KeyValuePair<CharacterObject, int> current = queue.Dequeue();
                CharacterObject[] upgrades = current.Key.UpgradeTargets;
                if (upgrades == null) continue;
                foreach (CharacterObject upgrade in upgrades)
                {
                    if (upgrade == null || !visited.Add(upgrade)) continue;
                    int distance = current.Value + 1;
                    if (upgrade == target) return distance;
                    queue.Enqueue(new KeyValuePair<CharacterObject, int>(
                        upgrade,
                        distance));
                }
            }
            return -1;
        }

        private static void AllocateWeightedQuota(
            IReadOnlyList<WeightedRecruitmentRoot> roots,
            int requestedCount)
        {
            int remaining = Math.Max(0, requestedCount);
            while (remaining > 0)
            {
                List<WeightedRecruitmentRoot> active = roots
                    .Where(root => root.Weight > 0 && root.RemainingCapacity > 0)
                    .OrderBy(root => root.Ordinal)
                    .ThenBy(root => root.Troop.StringId, StringComparer.Ordinal)
                    .ToList();
                if (active.Count == 0) break;
                long totalWeight = active.Sum(root => (long)root.Weight);
                var quotas = new Dictionary<WeightedRecruitmentRoot, int>();
                var remainders = new Dictionary<WeightedRecruitmentRoot, long>();
                int assigned = 0;
                foreach (WeightedRecruitmentRoot root in active)
                {
                    long weighted = (long)remaining * root.Weight;
                    int quota = (int)(weighted / totalWeight);
                    quotas[root] = quota;
                    remainders[root] = weighted % totalWeight;
                    assigned += quota;
                }
                int remainderSeats = remaining - assigned;
                foreach (WeightedRecruitmentRoot root in active
                    .OrderByDescending(root => remainders[root])
                    .ThenBy(root => root.Ordinal)
                    .ThenBy(root => root.Troop.StringId, StringComparer.Ordinal)
                    .Take(remainderSeats))
                {
                    quotas[root]++;
                }

                int progressed = 0;
                foreach (WeightedRecruitmentRoot root in active)
                {
                    int taken = Math.Min(quotas[root], root.RemainingCapacity);
                    root.Allocation += taken;
                    progressed += taken;
                }
                if (progressed <= 0) break;
                remaining -= progressed;
            }
        }

        private sealed class WeightedRecruitmentRoot
        {
            public WeightedRecruitmentRoot(
                CharacterObject troop,
                int weight,
                int ordinal)
            {
                Troop = troop;
                Weight = weight;
                Ordinal = ordinal;
            }

            public CharacterObject Troop { get; }
            public int Weight { get; set; }
            public int Ordinal { get; }
            public int Allocation { get; set; }
            public Dictionary<CharacterObject, int> Stock { get; } = new();
            public int RemainingCapacity =>
                Math.Max(0, Stock.Values.Sum() - Allocation);
        }

        /// <summary>
        /// Evaluates the exact fief manifest that the authoritative commit
        /// path would use, including player/AI finance and minimum-batch rules.
        /// </summary>
        internal IReadOnlyDictionary<CharacterObject, int>
            PreviewFiefRecruitmentApprovals(
                MobileParty targetParty,
                int maximumCount,
                Settlement villageFilter = null)
        {
            IReadOnlyList<RecruitmentCandidate> candidates =
                PreviewFiefRecruitmentCandidates(
                    targetParty, maximumCount, villageFilter);
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
        public int RecruitManualSelection(TroopRoster selectedRoster,
            MobileParty targetParty, Settlement villageFilter = null)
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
            int remainingPartySpace = Math.Max(0,
                targetParty.Party.PartySizeLimit - targetParty.Party.NumberOfAllMembers);
            Dictionary<CharacterObject, int> validatedSelection = new();
            foreach (var element in selectedRoster.GetTroopRoster())
            {
                if (remainingPartySpace <= 0) break;
                CharacterObject troop = element.Character;
                if (troop == null || element.Number <= 0 ||
                    _fiefPartyTemplate?.IsEnableTroop(troop) != true) continue;
                TroopRosterElement availableElement = GetFiefTroopRoster(
                    villageFilter, true).GetTroopRoster()
                    .FirstOrDefault(item => item.Character == troop);
                int available = Math.Max(0, availableElement.Number -
                    availableElement.WoundedNumber);
                int approved = Math.Min(remainingPartySpace,
                    Math.Min(element.Number, available));
                if (approved <= 0) continue;
                validatedSelection[troop] = approved;
                remainingPartySpace -= approved;
            }
            int requestedCount = validatedSelection.Values.Sum();
            if (requestedCount <= 0)
            {
                return 0;
            }

            if (!TryCommitVillageRecruitment(targetParty, validatedSelection,
                true, villageFilter, out tmpRecruitTroops)) return 0;
            hearthCost = tmpRecruitTroops.Values.Sum();

            int totalRecruited =
                tmpRecruitTroops.Values.Sum();

            if (totalRecruited <= 0)
                return 0;


            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_recruit_to_party");
            msg.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(targetParty));
            msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msg.SetTextVariable("RECRUITED", totalRecruited);
            msg.SetTextVariable("TROOP_COUNT", GetTotalTroopCount());
            msg.SetTextVariable("OCCUPIED_HEARTH",
                GetTotalOccupiedSupplyHearth());
            msg.SetTextVariable("FEUDAL_BUDGET_HEARTH",
                GetTotalFeudalBudgetHearth());
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

            return totalRecruited;
        }


        private sealed class VillageRecruitmentSource
        {
            public VillageFiefData Village;
            public CharacterObject Troop;
            public int Count;
            public int Wounded;
            public TroopRoster SourceRoster;
            public int BeforeCount;
            public int BeforeWounded;
        }

        private enum FiefRecruitmentStage
        {
            Prepare,
            ValidateSource,
            Snapshot,
            RemoveReadyTroops,
            MobilizeHearth,
            CreateServiceBatch,
            RegisterServiceBatch,
            AddPartyTroops,
            ValidateCommit,
            SyncCounters,
            CommitCompleted,
            Rollback
        }

        private bool TryCommitVillageRecruitment(MobileParty targetParty,
            IReadOnlyDictionary<CharacterObject, int> approvals,
            bool healthyOnly,
            Settlement villageFilter,
            out Dictionary<CharacterObject, int> committed)
        {
            committed = new Dictionary<CharacterObject, int>();
            string operationId = Guid.NewGuid().ToString("N");
            FiefRecruitmentStage stage = FiefRecruitmentStage.Prepare;
            List<VillageRecruitmentSource> plan = new();
            Dictionary<CharacterObject, (int Count, int Wounded)> targetBefore =
                new();
            Dictionary<VillageFiefData, (float Hearth, int Mobilized)>
                villageBefore = new();
            Dictionary<VillageFiefData, HashSet<FiefServiceBatch>>
                serviceBefore = new();
            List<(VillageFiefData Village, FiefServiceBatch Batch)> created = new();
            List<(VillageFiefData Village, int Count)> mobilized = new();
            FiefPartyManager manager = null;
            try
            {
                if (targetParty?.Party == null ||
                    targetParty.MemberRoster == null || approvals == null ||
                    approvals.Count == 0 || _settlement?.OwnerClan == null ||
                    targetParty.LeaderHero?.Clan != _settlement.OwnerClan)
                    return RejectRecruitmentTransaction(operationId,
                        targetParty, approvals, "InvalidRequest");
                manager = Campaign.Current?
                    .GetCampaignBehavior<FiefPartyManager>();
                if (manager == null || manager.IsServicePartyBlocked(targetParty))
                    return RejectRecruitmentTransaction(operationId,
                        targetParty, approvals, manager == null
                            ? "ManagerUnavailable"
                            : "ServicePartyBlocked");
                MilitaryConfigManager.Instance.RequireActiveConfig()
                    .GetDoctrine(_settlement);

                stage = FiefRecruitmentStage.ValidateSource;
                foreach (var approval in approvals
                    .Where(p => p.Key != null && p.Value > 0)
                    .OrderBy(p => p.Key.StringId))
                {
                    int remaining = approval.Value;
                    foreach (VillageFiefData village in
                        OrderedVillages(villageFilter))
                    {
                        if (village.RecoveryRequired ||
                            !IsCurrentVillage(village.VillageSettlement))
                            continue;
                        TroopRoster roster = village.GetReadyRoster();
                        if (roster == null) continue;
                        TroopRosterElement element = roster.GetTroopRoster()
                            .FirstOrDefault(e => e.Character == approval.Key);
                        int available = healthyOnly
                            ? Math.Max(0,
                                element.Number - element.WoundedNumber)
                            : Math.Max(0, element.Number);
                        int count = Math.Min(remaining, available);
                        if (count <= 0) continue;
                        int wounded = healthyOnly ? 0 : Math.Max(0,
                            count - Math.Max(0,
                                element.Number - element.WoundedNumber));
                        plan.Add(new VillageRecruitmentSource
                        {
                            Village = village,
                            Troop = approval.Key,
                            Count = count,
                            Wounded = wounded,
                            SourceRoster = roster,
                            BeforeCount = element.Number,
                            BeforeWounded = element.WoundedNumber
                        });
                        remaining -= count;
                        if (remaining == 0) break;
                    }
                    if (remaining != 0)
                        return RejectRecruitmentTransaction(operationId,
                            targetParty, approvals,
                            "InsufficientReadySource");
                }
                int total = plan.Sum(p => p.Count);
                if (total <= 0 || targetParty.Party.PartySizeLimit -
                    targetParty.Party.NumberOfAllMembers < total)
                    return RejectRecruitmentTransaction(operationId,
                        targetParty, approvals,
                        total <= 0 ? "EmptyPlan" :
                            "PartyCapacityInsufficient");
                foreach (var group in plan.GroupBy(p => p.Village))
                {
                    Village village = group.Key.VillageSettlement?.Village;
                    if (village == null || Math.Max(0,
                        (int)Math.Floor(village.Hearth) - 100) <
                        group.Sum(p => p.Count))
                        return RejectRecruitmentTransaction(operationId,
                            targetParty, approvals,
                            "VillageHearthInsufficient");
                }

                stage = FiefRecruitmentStage.Snapshot;
                targetBefore = plan.Select(p => p.Troop).Distinct()
                    .ToDictionary(troop => troop, troop =>
                        GetRosterCounts(targetParty.MemberRoster, troop));
                villageBefore = plan.Select(p => p.Village).Distinct()
                    .ToDictionary(village => village, village =>
                        (village.VillageSettlement.Village.Hearth,
                            village.MobilizedHearth));
                serviceBefore = plan.Select(p => p.Village).Distinct()
                    .ToDictionary(village => village, village =>
                        new HashSet<FiefServiceBatch>(
                            village.ServiceBatches ??
                            Enumerable.Empty<FiefServiceBatch>()));

                stage = FiefRecruitmentStage.RemoveReadyTroops;
                foreach (var entry in plan)
                {
                    if (entry.Village.GetReadyRoster() != entry.SourceRoster)
                        throw new InvalidOperationException(
                            "VillageRosterReferenceChanged");
                    entry.SourceRoster.AddToCounts(entry.Troop,
                        -entry.Count, false, -entry.Wounded, 0, true, -1);
                }
                foreach (var group in plan.GroupBy(p => p.Village))
                {
                    stage = FiefRecruitmentStage.MobilizeHearth;
                    int count = group.Sum(p => p.Count);
                    if (!group.Key.TryMobilize(count))
                        throw new InvalidOperationException("VillageHearthChanged");
                    mobilized.Add((group.Key, count));
                    Dictionary<CharacterObject, int> troops = group
                        .GroupBy(p => p.Troop)
                        .ToDictionary(g => g.Key, g => g.Sum(p => p.Count));
                    stage = FiefRecruitmentStage.CreateServiceBatch;
                    FiefServiceBatch batch = CreateServingDetachment(
                        group.Key, targetParty, troops);
                    if (batch == null)
                        throw new InvalidOperationException("BatchCreationFailed");
                    created.Add((group.Key, batch));
                    stage = FiefRecruitmentStage.RegisterServiceBatch;
                    if (!manager.IsServingDetachmentRegistered(
                        _settlement, group.Key.VillageSettlement, batch))
                        throw new InvalidOperationException(
                            "BatchRegistrationFailed");
                }
                stage = FiefRecruitmentStage.AddPartyTroops;
                foreach (var entry in plan)
                {
                    targetParty.MemberRoster.AddToCounts(entry.Troop,
                        entry.Count, false, entry.Wounded, 0, true, -1);
                    committed[entry.Troop] = committed.TryGetValue(
                        entry.Troop, out int old) ? old + entry.Count : entry.Count;
                }
                stage = FiefRecruitmentStage.ValidateCommit;
                if (!ValidateRecruitmentCommit(plan, targetParty,
                    targetBefore, villageBefore, created, manager,
                    _settlement))
                    throw new InvalidOperationException(
                        "RecruitmentPostCommitValidationFailed");
                stage = FiefRecruitmentStage.SyncCounters;
                SyncManpowerCounters();
                stage = FiefRecruitmentStage.CommitCompleted;
                return true;
            }
            catch (Exception exception)
            {
                FiefRecruitmentStage failedStage = stage;
                stage = FiefRecruitmentStage.Rollback;
                bool restored = true;
                var rollbackFailures = new List<string>();
                foreach (CharacterObject troop in targetBefore.Keys)
                {
                    try
                    {
                        RestoreRosterEntry(targetParty.MemberRoster, troop,
                            targetBefore[troop]);
                    }
                    catch (Exception rollbackException)
                    {
                        restored = false;
                        rollbackFailures.Add("TargetRoster:" +
                            rollbackException);
                    }
                }
                foreach (var entry in plan)
                {
                    try
                    {
                        if (entry.Village.GetReadyRoster() != entry.SourceRoster)
                            throw new InvalidOperationException(
                                "VillageRosterReferenceChanged");
                        RestoreRosterEntry(entry.SourceRoster, entry.Troop,
                            (entry.BeforeCount, entry.BeforeWounded));
                    }
                    catch (Exception rollbackException)
                    {
                        restored = false;
                        rollbackFailures.Add("SourceRoster:" +
                            (entry.Village?.VillageSettlement?.StringId ??
                                "unknown") + ":" + rollbackException);
                    }
                }
                foreach (var pair in serviceBefore)
                {
                    foreach (FiefServiceBatch batch in
                        (pair.Key.ServiceBatches ??
                            new List<FiefServiceBatch>()).Where(batch =>
                                !pair.Value.Contains(batch)).ToList())
                    {
                        try
                        {
                            pair.Key.ServiceBatches.Remove(batch);
                            manager?.UnregisterServingDetachment(_settlement,
                                pair.Key.VillageSettlement, batch);
                        }
                        catch (Exception rollbackException)
                        {
                            restored = false;
                            rollbackFailures.Add("ServiceBatch:" +
                                (pair.Key.VillageSettlement?.StringId ??
                                    "unknown") + ":" + rollbackException);
                        }
                    }
                }
                foreach (var item in mobilized)
                {
                    try
                    {
                        if (!item.Village.RollbackMobilizationReservation(
                            item.Count)) restored = false;
                    }
                    catch (Exception rollbackException)
                    {
                        restored = false;
                        rollbackFailures.Add("Mobilization:" +
                            (item.Village?.VillageSettlement?.StringId ??
                                "unknown") + ":" + rollbackException);
                    }
                }
                try
                {
                    manager?.RebuildServingDetachmentIndex();
                }
                catch (Exception rollbackException)
                {
                    restored = false;
                    rollbackFailures.Add("LedgerRebuild:" +
                        rollbackException);
                }
                restored = restored && VerifyRecruitmentRollback(plan,
                    targetParty, targetBefore, villageBefore, serviceBefore,
                    manager, _settlement);
                if (!restored)
                    foreach (VillageFiefData village in plan
                        .Select(p => p.Village).Distinct())
                        village.RequireRecovery(
                            "RecruitmentRollbackUnverified", operationId);
                LogRecruitmentTransactionFailure(operationId, failedStage,
                    targetParty, plan, exception, restored,
                    rollbackFailures);
                committed.Clear();
                try { SyncManpowerCounters(); }
                catch (Exception counterException)
                {
                    ModLogger.Error(string.Format(
                        "Fief recruitment counter recovery failed: operation={0}, exception={1}",
                        operationId, counterException));
                }
                return false;
            }
        }

        private static (int Count, int Wounded) GetRosterCounts(
            TroopRoster roster, CharacterObject troop)
        {
            if (roster == null || troop == null) return (0, 0);
            TroopRosterElement element = roster.GetTroopRoster()
                .FirstOrDefault(item => item.Character == troop);
            return (Math.Max(0, element.Number),
                Math.Max(0, element.WoundedNumber));
        }

        private static void RestoreRosterEntry(TroopRoster roster,
            CharacterObject troop, (int Count, int Wounded) expected)
        {
            if (roster == null || troop == null)
                throw new InvalidOperationException("RosterUnavailable");
            var actual = GetRosterCounts(roster, troop);
            roster.AddToCounts(troop, expected.Count - actual.Count, false,
                expected.Wounded - actual.Wounded, 0, true, -1);
            if (GetRosterCounts(roster, troop) != expected)
                throw new InvalidOperationException("RosterRestoreMismatch");
        }

        private static bool ValidateRecruitmentCommit(
            IReadOnlyList<VillageRecruitmentSource> plan,
            MobileParty targetParty,
            IReadOnlyDictionary<CharacterObject, (int Count, int Wounded)> targetBefore,
            IReadOnlyDictionary<VillageFiefData, (float Hearth, int Mobilized)> villageBefore,
            IReadOnlyList<(VillageFiefData Village, FiefServiceBatch Batch)> created,
            FiefPartyManager manager,
            Settlement sourceFief)
        {
            foreach (VillageRecruitmentSource entry in plan)
            {
                var actual = GetRosterCounts(entry.SourceRoster, entry.Troop);
                if (actual.Count != entry.BeforeCount - entry.Count ||
                    actual.Wounded != entry.BeforeWounded - entry.Wounded)
                    return false;
            }
            foreach (var pair in targetBefore)
            {
                int added = plan.Where(p => p.Troop == pair.Key)
                    .Sum(p => p.Count);
                int wounded = plan.Where(p => p.Troop == pair.Key)
                    .Sum(p => p.Wounded);
                var actual = GetRosterCounts(targetParty.MemberRoster, pair.Key);
                if (actual.Count != pair.Value.Count + added ||
                    actual.Wounded != pair.Value.Wounded + wounded)
                    return false;
            }
            foreach (var pair in villageBefore)
            {
                int count = plan.Where(p => p.Village == pair.Key)
                    .Sum(p => p.Count);
                if (pair.Key.MobilizedHearth != pair.Value.Mobilized + count ||
                    Math.Abs(pair.Key.VillageSettlement.Village.Hearth -
                        (pair.Value.Hearth - count)) > 0.01f)
                    return false;
            }
            return created.All(item =>
                item.Village.ServiceBatches.Contains(item.Batch) &&
                item.Batch.GetTotalCount() == plan
                    .Where(p => p.Village == item.Village).Sum(p => p.Count) &&
                manager?.IsServingDetachmentRegistered(sourceFief,
                    item.Village.VillageSettlement, item.Batch) == true);
        }

        private static bool VerifyRecruitmentRollback(
            IReadOnlyList<VillageRecruitmentSource> plan,
            MobileParty targetParty,
            IReadOnlyDictionary<CharacterObject, (int Count, int Wounded)> targetBefore,
            IReadOnlyDictionary<VillageFiefData, (float Hearth, int Mobilized)> villageBefore,
            IReadOnlyDictionary<VillageFiefData, HashSet<FiefServiceBatch>>
                serviceBefore,
            FiefPartyManager manager,
            Settlement sourceFief)
        {
            return plan.All(entry => GetRosterCounts(
                    entry.SourceRoster, entry.Troop) ==
                    (entry.BeforeCount, entry.BeforeWounded)) &&
                targetBefore.All(pair => GetRosterCounts(
                    targetParty.MemberRoster, pair.Key) == pair.Value) &&
                villageBefore.All(pair =>
                    pair.Key.MobilizedHearth == pair.Value.Mobilized &&
                    Math.Abs(pair.Key.VillageSettlement.Village.Hearth -
                        pair.Value.Hearth) <= 0.01f) &&
                serviceBefore.All(pair =>
                    pair.Key.ServiceBatches.Count == pair.Value.Count &&
                    pair.Value.All(batch =>
                        pair.Key.ServiceBatches.Contains(batch)) &&
                    pair.Key.ServiceBatches.All(batch =>
                        manager?.IsServingDetachmentRegistered(sourceFief,
                            pair.Key.VillageSettlement, batch) == true));
        }

        private void LogRecruitmentTransactionFailure(
            string operationId,
            FiefRecruitmentStage failedStage,
            MobileParty targetParty,
            IReadOnlyCollection<VillageRecruitmentSource> plan,
            Exception exception,
            bool restored,
            IReadOnlyCollection<string> rollbackFailures)
        {
            string villages = string.Join(",", (plan ??
                Array.Empty<VillageRecruitmentSource>())
                .Select(item => item.Village?.VillageSettlement?.StringId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .OrderBy(id => id));
            string rollbackDetail = rollbackFailures == null ||
                rollbackFailures.Count == 0
                    ? "none"
                    : string.Join(" | ", rollbackFailures);
            try
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_fief_recruitment_transaction_failed");
                message.SetTextVariable("OPERATION_ID", operationId);
                message.SetTextVariable("FIEF_ID",
                    _settlement?.StringId ?? "unknown");
                message.SetTextVariable("VILLAGE_IDS",
                    string.IsNullOrEmpty(villages) ? "unknown" : villages);
                message.SetTextVariable("PARTY_ID",
                    targetParty?.StringId ?? "unknown");
                message.SetTextVariable("STAGE", failedStage.ToString());
                message.SetTextVariable("REQUESTED",
                    plan?.Sum(item => item.Count) ?? 0);
                message.SetTextVariable("ROLLBACK",
                    restored ? "Verified" : "RecoveryRequired");
                message.SetTextVariable("ROLLBACK_DETAIL", rollbackDetail);
                message.SetTextVariable("EXCEPTION", exception?.ToString() ??
                    "unknown");
                ModLogger.Error(message.ToString());
            }
            catch (Exception loggingException)
            {
                ModLogger.Error(string.Format(
                    "Fief recruitment transaction failed: operation={0}, fief={1}, villages={2}, party={3}, stage={4}, rollback={5}, rollbackDetail={6}, exception={7}, loggingException={8}",
                    operationId, _settlement?.StringId ?? "unknown",
                    villages, targetParty?.StringId ?? "unknown",
                    failedStage, restored ? "Verified" : "RecoveryRequired",
                    rollbackDetail, exception, loggingException));
            }
        }

        private bool RejectRecruitmentTransaction(
            string operationId,
            MobileParty targetParty,
            IReadOnlyDictionary<CharacterObject, int> approvals,
            string reasonCode)
        {
            if (targetParty?.LeaderHero?.Clan != Clan.PlayerClan &&
                !RecruitmentLogFilter.ShouldLog(targetParty))
                return false;
            try
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_fief_recruitment_rejected");
                message.SetTextVariable("OPERATION_ID", operationId);
                message.SetTextVariable("FIEF_ID",
                    _settlement?.StringId ?? "unknown");
                message.SetTextVariable("PARTY_ID",
                    targetParty?.StringId ?? "unknown");
                message.SetTextVariable("REQUESTED", approvals?.Values
                    .Where(value => value > 0).Sum() ?? 0);
                message.SetTextVariable("REASON_CODE", reasonCode);
                ModLogger.Debug(message.ToString());
            }
            catch (Exception exception)
            {
                ModLogger.Debug(string.Format(
                    "Fief recruitment rejected: operation={0}, fief={1}, party={2}, reason={3}, loggingException={4}",
                    operationId, _settlement?.StringId ?? "unknown",
                    targetParty?.StringId ?? "unknown", reasonCode,
                    exception.GetType().Name));
            }
            return false;
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

            // 1. 检查是否为封建部队允许的兵种
            if (_fiefPartyTemplate == null || !_fiefPartyTemplate.IsEnableTroop(troop))
                return false;

            // Conversion adds a feudal soldier, so it must fit one village's
            // supply capacity even though it does not permanently deduct Hearth.
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            VillageFiefData target = OrderedVillages(null)
                .FirstOrDefault(v => !v.RecoveryRequired &&
                    v.GetReadyRoster() != null &&
                    v.CanIncreaseSupply(_fiefPartyTemplate, doctrine,
                        troop, count));
            if (target == null) return false;
            target.GetReadyRoster().AddToCounts(
                troop, count, false, 0, 0, true, -1);

            // 消耗的繁荣度
            float prosperityCost = 0;
            // ============================================================
            // 征召成本：从模板读取（与 RecruitTroopsToParty 一致）
            // ============================================================
            int tmpProsperityCostPerTroop =
                GetFiefProductionConfig()?.ProsperityCostPerTroop ?? 0;

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
