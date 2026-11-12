using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Diagnostics;
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
        private sealed class InitialFiefTroopQuota
        {
            public TroopProductionEntry Entry;
            public int Count;
            public long Remainder;
        }

        /// <summary>
        /// Builds a new-game composition from the complete template. Locked
        /// entries retain their proportional share as intentionally unused
        /// capacity instead of donating it to the currently eligible entries.
        /// </summary>
        internal int InitializeFiefTroopsByTemplateWeights()
        {
            if (!IsRuntimeReady || _fiefPartyTemplate == null)
                return 0;

            List<TroopProductionEntry> templateEntries = _fiefPartyTemplate
                .TroopEntries
                .Where(entry => entry?.Troop != null && entry.Weight > 0)
                .OrderBy(entry => entry.Troop.StringId,
                    StringComparer.Ordinal)
                .ToList();
            if (templateEntries.Count == 0) return 0;

            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            long totalWeight = templateEntries.Sum(entry =>
                (long)entry.Weight);
            long weightedSupplyCost = templateEntries.Sum(entry =>
                (long)entry.Weight *
                doctrine.GetFeudalSupplyCost(entry.Troop));
            if (totalWeight <= 0 || weightedSupplyCost <= 0) return 0;

            int barracksLevel = SettlementManpowerProductionBehavior
                .GetBarracksLevel(_settlement);
            int produced = 0;
            foreach (VillageFiefData village in OrderedVillages(null)
                .Where(village => !village.RecoveryRequired &&
                    village.GetReadyRoster() != null))
            {
                int budget = Math.Max(0,
                    village.GetFeudalBudgetRemainingHearth(
                        _fiefPartyTemplate, doctrine));
                int targetCount = (int)Math.Min(int.MaxValue,
                    (long)budget * totalWeight / weightedSupplyCost);
                if (targetCount <= 0) continue;

                List<InitialFiefTroopQuota> quotas = templateEntries
                    .Select(entry =>
                    {
                        long weighted = (long)targetCount * entry.Weight;
                        return new InitialFiefTroopQuota
                        {
                            Entry = entry,
                            Count = (int)(weighted / totalWeight),
                            Remainder = weighted % totalWeight
                        };
                    })
                    .ToList();
                int undistributed = targetCount - quotas.Sum(item =>
                    item.Count);
                foreach (InitialFiefTroopQuota quota in quotas
                    .OrderByDescending(item => item.Remainder)
                    .ThenBy(item => item.Entry.Troop.StringId,
                        StringComparer.Ordinal)
                    .Take(undistributed))
                    quota.Count++;

                TroopRoster roster = village.GetReadyRoster();
                foreach (InitialFiefTroopQuota quota in quotas
                    .Where(item => item.Count > 0 &&
                        item.Entry.RequiredBarracksLevel <= barracksLevel)
                    .OrderBy(item => item.Entry.Troop.StringId,
                        StringComparer.Ordinal))
                {
                    for (int i = 0; i < quota.Count; i++)
                    {
                        if (!village.CanIncreaseSupply(_fiefPartyTemplate,
                            doctrine, quota.Entry.Troop, 1))
                            break;
                        roster.AddToCounts(quota.Entry.Troop, 1, false,
                            0, 0, true, -1);
                        produced++;
                    }
                }
            }
            SyncManpowerCounters();
            return produced;
        }

        /// <summary>
        /// Selects concrete XML troops for an already approved manpower amount.
        /// No saved troop inventory is consulted or consumed.
        /// </summary>
        private Dictionary<CharacterObject, int>
        WithdrawFiefTroopEntries(
            int maximumCount)
        {
            Dictionary<CharacterObject, int> result = new();
            if (_fiefPartyTemplate == null || maximumCount <= 0)
            {
                return result;
            }

            int barracksLevel = SettlementManpowerProductionBehavior
                .GetBarracksLevel(_settlement);
            List<TroopProductionEntry> eligible = _fiefPartyTemplate
                .TroopEntries
                .Where(entry => entry?.Troop != null &&
                    entry.Weight > 0 &&
                    entry.RequiredBarracksLevel <= barracksLevel)
                .ToList();
            int remainingTotal = maximumCount;
            while (remainingTotal > 0)
            {
                // 每名新兵直接按具体兵种权重抽取。这样即使每天只生产
                // 1～3 人，长期分布也不会被整数分桶永久截断。
                int totalWeight = eligible.Sum(entry => entry.Weight);
                int roll = totalWeight > 0 ? MBRandom.RandomInt(totalWeight) : -1;
                TroopProductionEntry selected = null;
                foreach (TroopProductionEntry entry in eligible)
                {
                    roll -= entry.Weight;
                    if (roll < 0)
                    {
                        selected = entry;
                        break;
                    }
                }
                if (selected == null) break;

                result[selected.Troop] = result.TryGetValue(
                    selected.Troop,
                    out int currentCount) ? currentCount + 1 : 1;
                remainingTotal--;
            }

            return result;
        }


        /// <summary>
        /// 从给定的 TroopRoster 中提取所有采邑类型士兵，返回一个新的仅含采邑部队的 TroopRoster。
        /// </summary>
        /// <param name="roster">源 TroopRoster（例如来自 MemberRoster.GetTroopRoster()）</param>
        /// <returns>新的 TroopRoster，仅包含当前封邑模板认可的士兵</returns>
        public TroopRoster GetFiefTroopRoster() => GetFiefTroopRoster(null);

        internal TroopRoster GetFiefTroopRoster(Settlement villageFilter,
            bool excludeRecovery = false)
        {
            return QueryFeudalTroops(villageFilter,
                FiefTroopState.Ready, excludeRecovery).ToTroopRoster();
        }


        /// <summary>
        /// 执行封邑部队的补员操作：根据可补充人数，按兵种权重分配、生成新兵、更新计数并记录日志。
        /// </summary>
        /// <param name="maxReinforcements">本次每日产能批准的最大补员人数</param>
        private List<FiefTroopProductionResult> PerformReinforcement(
            int maxReinforcements)
        {
            var produced = new List<FiefTroopProductionResult>();
            int allowed = Math.Max(0, maxReinforcements);
            if (allowed <= 0) return produced;
            var tmpNewTroops = WithdrawFiefTroopEntries(allowed);

            List<VillageFiefData> targets = OrderedVillages(null)
                .Where(v => !v.RecoveryRequired && v.GetReadyRoster() != null)
                .ToList();
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            bool shouldLog = RecruitmentLogFilter.ShouldLog(
                _settlement?.OwnerClan);
            Dictionary<VillageFiefData, int> occupiedBefore = null;
            if (shouldLog)
            {
                try
                {
                    occupiedBefore = OrderedVillages(null).ToDictionary(
                        village => village,
                        village => village.GetOccupiedSupplyHearth(
                            _fiefPartyTemplate, doctrine));
                }
                catch (Exception exception)
                {
                    shouldLog = false;
                    ModLogger.Debug(string.Format(
                        "Fief production diagnostics unavailable: fief={0}, phase=Before, reason={1}.",
                        _settlement?.StringId ?? "unknown",
                        exception.GetType().Name));
                }
            }
            int recruited = 0;
            foreach (var kvp in tmpNewTroops.OrderBy(p => p.Key.StringId))
            for (int i = 0; i < kvp.Value; i++)
            {
                VillageFiefData target;
                try
                {
                    target = targets
                        .Where(village => village.CanIncreaseSupply(
                            _fiefPartyTemplate, doctrine, kvp.Key, 1))
                        .OrderByDescending(village => village
                            .GetFeudalBudgetRemainingHearth(
                                _fiefPartyTemplate, doctrine))
                        .ThenBy(village => village.VillageSettlement.StringId)
                        .FirstOrDefault();
                }
                catch (Exception exception)
                {
                    ModLogger.Error(string.Format(
                        "Fief production rejected: fief={0}, troop={1}, tier={2}, reason={3}.",
                        _settlement?.StringId ?? "unknown",
                        kvp.Key?.StringId ?? "unknown", kvp.Key?.Tier ?? -1,
                        exception.Message));
                    break;
                }
                if (target == null) break;
                target.GetReadyRoster().AddToCounts(
                    kvp.Key, 1, false, 0, 0, true, -1);
                FiefTroopProductionResult entry = produced.FirstOrDefault(
                    item => item.Village == target.VillageSettlement &&
                        item.Troop == kvp.Key);
                if (entry == null)
                    produced.Add(new FiefTroopProductionResult(
                        target.VillageSettlement, kvp.Key, 1));
                else entry.Add(1);
                recruited++;
            }
            SyncManpowerCounters();

            if (produced.Sum(item => item.Count) <= 0)
            {
                if (shouldLog)
                {
                    try
                    {
                        TextObject empty = GameTexts.FindText(
                            "str_modifiedarmy_fief_production_no_commit");
                        empty.SetTextVariable("SETTLEMENT_ID",
                            _settlement?.StringId ?? "unknown");
                        empty.SetTextVariable("APPROVED", allowed);
                        empty.SetTextVariable("REASON", "NoCommittedTroops");
                        ModLogger.Debug(empty.ToString());
                    }
                    catch (Exception exception)
                    {
                        ModLogger.Debug(string.Format(
                            "Fief production diagnostics unavailable: fief={0}, phase=ZeroCommit, reason={1}.",
                            _settlement?.StringId ?? "unknown",
                            exception.GetType().Name));
                    }
                }
                return produced;
            }

            if (!shouldLog) return produced;

            try
            {
            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_weekly_reinforcement");
            msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msg.SetTextVariable("MANIFEST", FormatTroopManifest(produced
                .GroupBy(item => item.Troop)
                .ToDictionary(group => group.Key,
                    group => group.Sum(item => item.Count))));
            msg.SetTextVariable("TROOP_COUNT", GetTotalTroopCount());
            msg.SetTextVariable("OCCUPIED_HEARTH",
                GetTotalOccupiedSupplyHearth());
            msg.SetTextVariable("FEUDAL_BUDGET_HEARTH",
                GetTotalFeudalBudgetHearth());
            msg.SetTextVariable("VILLAGE_DETAILS", string.Join(
                GameTexts.FindText("str_modifiedarmy_log_list_separator")
                    .ToString(),
                OrderedVillages(null)
                    .OrderBy(village => village.VillageSettlement?.StringId,
                        StringComparer.Ordinal)
                    .Select(village =>
                    {
                        TextObject detail = GameTexts.FindText(
                            "str_modifiedarmy_fief_production_village_detail");
                        int before = occupiedBefore != null &&
                            occupiedBefore.TryGetValue(village, out int value)
                                ? value
                                : village.GetOccupiedSupplyHearth(
                                    _fiefPartyTemplate, doctrine);
                        int after = village.GetOccupiedSupplyHearth(
                            _fiefPartyTemplate, doctrine);
                        detail.SetTextVariable("VILLAGE_ID",
                            village.VillageSettlement?.StringId ?? "unknown");
                        detail.SetTextVariable("VILLAGE_NAME",
                            village.VillageSettlement?.Name ??
                                new TextObject("unknown"));
                        detail.SetTextVariable("PRODUCED", produced
                            .Where(item => item.Village ==
                                village.VillageSettlement)
                            .Sum(item => item.Count));
                        detail.SetTextVariable("MANIFEST",
                            FormatTroopManifest(produced
                                .Where(item => item.Village ==
                                    village.VillageSettlement)
                                .GroupBy(item => item.Troop)
                                .ToDictionary(group => group.Key,
                                    group => group.Sum(item => item.Count))));
                        detail.SetTextVariable("EFFECTIVE_HEARTH",
                            village.GetEffectiveSupplyHearth());
                        detail.SetTextVariable("OCCUPIED_BEFORE", before);
                        detail.SetTextVariable("OCCUPIED_AFTER", after);
                        detail.SetTextVariable("BUDGET",
                            village.GetFeudalBudgetHearth(doctrine));
                        detail.SetTextVariable("REMAINING",
                            village.GetFeudalBudgetRemainingHearth(
                                _fiefPartyTemplate, doctrine));
                        detail.SetTextVariable("RECOVERY",
                            village.RecoveryRequired ? "true" : "false");
                        return detail.ToString();
                    })));


            if (_settlement.OwnerClan == Clan.PlayerClan)
            {
                ModLogger.Info(msg.ToString());
            }
            else
            {
                ModLogger.Debug(msg.ToString());
            }
            }
            catch (Exception exception)
            {
                ModLogger.Debug(string.Format(
                    "Fief production diagnostics unavailable: fief={0}, phase=After, reason={1}.",
                    _settlement?.StringId ?? "unknown",
                    exception.GetType().Name));
            }
            return produced;
        }

        public int ProduceDailyFiefTroops(int approvedCount)
        {
            if (!IsRuntimeReady || approvedCount <= 0)
                return 0;
            return ProduceDailyFiefTroopsDetailed(approvedCount)
                .Sum(item => item.Count);
        }

        internal IReadOnlyList<FiefTroopProductionResult>
            ProduceDailyFiefTroopsDetailed(int approvedCount)
        {
            if (!IsRuntimeReady || approvedCount <= 0)
                return Array.Empty<FiefTroopProductionResult>();
            return PerformReinforcement(approvedCount);
        }

        /// <summary>
        /// 获取兵种类型的升级权重（用于加权随机）
        /// 步兵 > 骑兵 > 射手
        /// </summary>
        private int GetUpgradeWeight(CharacterObject troop)
        {
            if (troop.IsInfantry)
                return 5; // 最高偏好
            if (troop.IsRanged)
                return 2; // 最低偏好
            return 3; 
        }

        /// <summary>
        /// 从候选列表中按权重随机选择一个目标
        /// </summary>
        private CharacterObject WeightedRandomChoice(CharacterObject[] candidates)
        {
            if (candidates.Length == 0)
                return null;
            if (candidates.Length == 1)
                return candidates[0];

            int totalWeight = 0;
            var weights = new int[candidates.Length];
            for (int i = 0; i < candidates.Length; i++)
            {
                weights[i] = GetUpgradeWeight(candidates[i]);
                totalWeight += weights[i];
            }

            if (totalWeight <= 0)
                return candidates[MBRandom.RandomInt(candidates.Length)];

            int roll = MBRandom.RandomInt(totalWeight);
            int cumulative = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                cumulative += weights[i];
                if (roll < cumulative)
                    return candidates[i];
            }
            return candidates[0]; // fallback
        }

        /// <summary>
        /// 计算封邑部队每周升级概率 [0.05, 0.20]
        /// - 繁荣度提供基础晋升可能（即使无训练场）
        /// - 训练场与繁荣度协同放大效果（主通道）
        /// - 无训练场时：p ∈ [0.05, 0.09]
        /// - 有满级训练场+高繁荣：p = 0.20
        /// </summary>
        private float CalculateFiefUpgradeChance()
        {
            if (_settlement == null)
                return 0.05f;

            // 1. 归一化繁荣度 [0, 1]
            float normP;
            if (_settlement.IsCastle)
                normP = MathF.Min(1f, _settlement.Town.Prosperity / CommonConstants.CASTLE_VERY_RICH_THRESHOLD);
            else
                normP = MathF.Min(1f, _settlement.Town.Prosperity / CommonConstants.TOWN_VERY_RICH_THRESHOLD);

            // 2. 归一化训练场等级 [0, 1]
            float normT = 0f;
            foreach (Building building in _settlement.Town.Buildings)
            {
                if (building.BuildingType == DefaultBuildingTypes.CastleTrainingFields ||
                    building.BuildingType == DefaultBuildingTypes.SettlementTrainingFields)
                {
                    normT = MathF.Min(1f, building.CurrentLevel / 3f);
                    break;
                }
            }
            // 3. 双通道模型
            float baseFromProsperity = 0.05f + 0.04f * normP;     // [0.05, 0.09]
            float bonusFromSynergy = 0.11f * normP * normT;      // [0.00, 0.11]

            float upgradeChance = baseFromProsperity + bonusFromSynergy;
            return MathF.Clamp(upgradeChance, 0.05f, 0.20f);
        }


        /// <summary>
        /// 每周自动尝试将低Tier封邑士兵升级为直接下一阶的同类型高Tier士兵。
        /// - 仅处理 Tier < 4 的健康士兵
        /// - 使用 CalculateVeteranMilitiaSpawnChance 返回的概率作为单兵升级率
        /// </summary>
        private void PerformAutoUpgrade()
        {
            // 获取升级基础概率
            float upgradeChance = CalculateFiefUpgradeChance();

            if (upgradeChance <= 0f)
                return;

            // 对每个士兵独立判定是否升级
            int upgradedCount = 0;
            var upgraded = new Dictionary<
                (CharacterObject From, CharacterObject To), int>();

            foreach (VillageFiefData village in OrderedVillages(null))
            {
              if (village.RecoveryRequired) continue;
              CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                  .RequireActiveConfig().GetDoctrine(_settlement);
              TroopRoster roster = village.GetReadyRoster();
              if (roster == null) continue;
              // Upgrades mutate this roster. Enumerate a value snapshot so
              // RemoveTroop/AddToCounts cannot invalidate the live roster's
              // List<TroopRosterElement> enumerator. Snapshot counts also
              // prevent newly upgraded troops from upgrading again this week.
              foreach (var element in roster.GetTroopRoster().ToList())
              {
                var troop = element.Character;
                int count = element.Number - element.WoundedNumber;

                if (troop == null || count <= 0)
                    continue;

                // 跳过非采邑兵 或 Tier >= 4
                if (!_fiefPartyTemplate.IsEnableTroop(troop) || troop.Tier >= 4)
                    continue;

                if (troop.UpgradeTargets == null || troop.UpgradeTargets.Length == 0)
                    continue;

                // 筛选合法升级目标：非 null + 属于采邑模板
                var validTargets = troop.UpgradeTargets
                    .Where(t => t != null && _fiefPartyTemplate.IsEnableTroop(t))
                    .ToArray();

                if (validTargets.Length == 0)
                    continue;

                for (int i = 0; i < count; i++)
                {
                    if (MBRandom.RandomFloat < upgradeChance)
                    {
                        CharacterObject newTroop = WeightedRandomChoice(validTargets);

                        if (newTroop == null)
                            continue;

                        int delta;
                        try
                        {
                            delta = doctrine.GetFeudalSupplyCost(newTroop) -
                                doctrine.GetFeudalSupplyCost(troop);
                            if (delta > 0 &&
                                (village.GetFreeSupplyHearth(
                                    _fiefPartyTemplate, doctrine) < delta ||
                                 village.GetFeudalBudgetRemainingHearth(
                                    _fiefPartyTemplate, doctrine) < delta))
                                continue;
                        }
                        catch (Exception exception)
                        {
                            ModLogger.Error(string.Format(
                                "Fief upgrade rejected: village={0}, from={1}, to={2}, reason={3}.",
                                village.VillageSettlement?.StringId ?? "unknown",
                                troop.StringId, newTroop.StringId,
                                exception.Message));
                            continue;
                        }

                        roster.RemoveTroop(troop, 1, default(UniqueTroopDescriptor), 0);
                        roster.AddToCounts(newTroop, 1, false, 0, 0, true, -1);

                        upgradedCount++;
                        var route = (troop, newTroop);
                        upgraded[route] = upgraded.TryGetValue(
                            route, out int routeCount)
                                ? routeCount + 1
                                : 1;
                    }
                }
              }
            }

            if (upgradedCount > 0)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_fief_auto_upgrade");
                message.SetTextVariable("MANIFEST",
                    FormatUpgradeManifest(upgraded));
                ModLogger.Debug(message.ToString());
            }
        }

        private static string FormatTroopManifest(
            IReadOnlyDictionary<CharacterObject, int> troops)
        {
            return string.Join(GameTexts.FindText(
                    "str_modifiedarmy_log_list_separator").ToString(),
                troops.Where(item => item.Key != null && item.Value > 0)
                    .OrderBy(item => item.Key.StringId)
                    .Select(item =>
                    {
                        TextObject entry = GameTexts.FindText(
                            "str_modifiedarmy_manpower_manifest_entry");
                        entry.SetTextVariable("COUNT", item.Value);
                        entry.SetTextVariable("TROOP_NAME", item.Key.Name);
                        return entry.ToString();
                    }));
        }

        private static string FormatUpgradeManifest(
            IReadOnlyDictionary<
                (CharacterObject From, CharacterObject To), int> upgrades)
        {
            return string.Join(GameTexts.FindText(
                    "str_modifiedarmy_log_list_separator").ToString(),
                upgrades.Where(item => item.Key.From != null &&
                        item.Key.To != null && item.Value > 0)
                    .OrderBy(item => item.Key.From.StringId)
                    .ThenBy(item => item.Key.To.StringId)
                    .Select(item =>
                    {
                        TextObject entry = GameTexts.FindText(
                            "str_modifiedarmy_fief_auto_upgrade_entry");
                        entry.SetTextVariable("COUNT", item.Value);
                        entry.SetTextVariable("FROM_NAME", item.Key.From.Name);
                        entry.SetTextVariable("TO_NAME", item.Key.To.Name);
                        return entry.ToString();
                    }));
        }


        /// <summary>
        /// 每周调用一次，处理封邑军队的自动恢复与补员。
        /// </summary>
        public void WeeklyUpdate()
        {
            if (!IsRuntimeReady)
            {
                LogRuntimeCoolingUnavailable();
                return;
            }

            // 升级封邑士兵
            PerformAutoUpgrade();

            // Process returned soldiers whose cooldown has completed.
            foreach (VillageFiefData village in OrderedVillages())
                ProcessVillageCooling(village, true, "WeeklyTick");

            // Active service has no fixed expiry. Daily reconciliation owns
            // losses and wage eligibility; weekly processing only advances
            // returned detachments and upgrades ready troops.

            SyncManpowerCounters();

            // Daily manpower production is the only path that adds new fief
            // troops. Weekly updates retain expiry, cooldown and upgrades only.
        }

        internal void ProcessVillageCooling(VillageFiefData village,
            bool advanceWeeklyClock, string trigger)
        {
            if (village?.ReturnBatches == null ||
                village.ReturnBatches.Count == 0)
                return;

            bool trace = PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(
                _settlement, village.VillageSettlement);
            int campaignDay = (int)CampaignTime.Now.ToDays;
            if (village.RecoveryRequired)
            {
                if (trace)
                    foreach (var item in village.ReturnBatches
                        .Select((batch, index) => new { batch, index }))
                        LogCooling(item.batch, item.index, campaignDay,
                            item.batch?.WaitWeeks ?? 0,
                            item.batch?.WaitWeeks ?? 0,
                            "BlockedByRecovery", trigger, village,
                            " recoveryReason=" +
                            (village.RecoveryReason ?? "missing"));
                return;
            }

            TroopRoster roster = village.GetReadyRoster();
            for (int index = village.ReturnBatches.Count - 1;
                index >= 0; index--)
            {
                FiefReturnBatch batch = village.ReturnBatches[index];
                if (batch == null || batch.IsEmpty()) continue;
                int before = batch.WaitWeeks;
                if (advanceWeeklyClock) batch.Tick();
                int after = batch.WaitWeeks;
                if (!batch.IsReady())
                {
                    if (trace)
                        LogCooling(batch, index, campaignDay, before, after,
                            "CoolingProgress", trigger, village);
                    continue;
                }
                if (roster == null)
                {
                    if (trace)
                        LogCooling(batch, index, campaignDay, before, after,
                            "MaturedWaitingForRoster", trigger, village,
                            " rosterActive=false militiaComponent=" +
                            (village.VillageSettlement?.MilitiaPartyComponent !=
                                null ? "present" : "missing"));
                    continue;
                }
                CommitMaturedBatch(village, batch, index, roster, campaignDay,
                    before, after, trigger, trace);
            }
        }

        private void CommitMaturedBatch(VillageFiefData village,
            FiefReturnBatch batch, int batchIndex, TroopRoster roster,
            int campaignDay, int waitBefore, int waitAfter, string trigger,
            bool trace)
        {
            Dictionary<CharacterObject, int> troops = batch.Troops
                .Where(pair => pair.Key != null && pair.Value > 0)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<CharacterObject, int> readyBefore = troops.Keys
                .ToDictionary(troop => troop,
                    troop => roster.GetTroopCount(troop));
            string operationId = string.Format("fief-cooling:{0}:{1}:{2}",
                village.VillageSettlement?.StringId ?? "missing-village",
                campaignDay, Guid.NewGuid().ToString("N"));
            try
            {
                if (troops.Count == 0 || troops.Values.Sum() !=
                    batch.GetTotalCount())
                    throw new InvalidOperationException(
                        "InvalidCoolingBatchTroops");
                foreach (var pair in troops)
                {
                    roster.AddToCounts(pair.Key, pair.Value, false, 0, 0,
                        true, -1);
                    if (roster.GetTroopCount(pair.Key) !=
                        readyBefore[pair.Key] + pair.Value)
                        throw new InvalidOperationException(
                            "CoolingReadyDeltaMismatch");
                }
                if (!village.ReturnBatches.Remove(batch))
                    throw new InvalidOperationException(
                        "CoolingBatchRemovalFailed");
                RefreshManpowerCounters();
                if (trace)
                    LogCooling(batch, batchIndex, campaignDay, waitBefore,
                        waitAfter, "MovedToReady", trigger, village,
                        string.Format(" operation={0} readyBefore={1} " +
                            "readyAfter={2} coolingAfter={3}", operationId,
                            readyBefore.Values.Sum(), troops.Keys.Sum(troop =>
                                roster.GetTroopCount(troop)),
                            village.ReturnBatches.Sum(item =>
                                item?.GetTotalCount() ?? 0)));

                if (trace)
                {
                    TextObject textObject = new TextObject(
                        "{=FiefTroopsReady}{SETTLEMENT_NAME}'s feudal troops are ready!",
                        null);
                    textObject.SetTextVariable("SETTLEMENT_NAME",
                        _settlement.Name);
                    ModLogger.Notice(textObject.ToString());
                }
            }
            catch (Exception exception)
            {
                bool restored = true;
                foreach (var pair in readyBefore)
                {
                    try
                    {
                        int current = roster.GetTroopCount(pair.Key);
                        roster.AddToCounts(pair.Key, pair.Value - current,
                            false, 0, 0, true, -1);
                    }
                    catch { restored = false; }
                }
                if (!village.ReturnBatches.Contains(batch))
                {
                    try { village.ReturnBatches.Insert(
                        Math.Min(batchIndex, village.ReturnBatches.Count),
                        batch); }
                    catch { restored = false; }
                }
                restored = restored && readyBefore.All(pair =>
                    roster.GetTroopCount(pair.Key) == pair.Value) &&
                    village.ReturnBatches.Contains(batch);
                if (!restored)
                    village.RequireRecovery("CoolingRollbackUnverified",
                        operationId);
                if (trace)
                    ModLogger.FileError(string.Format(
                        "[PlayerFiefCooling] fief={0} fiefId={1} " +
                        "village={2} villageId={3} day={4} batchIndex={5} " +
                        "troops={6} waitBefore={7} waitAfter={8} " +
                        "operation={9} result=CoolingCommitFailed " +
                        "errorType={10} rollback={11}.", _settlement?.Name,
                        _settlement?.StringId,
                        village.VillageSettlement?.Name,
                        village.VillageSettlement?.StringId, campaignDay,
                        batchIndex, batch.GetTotalCount(), waitBefore,
                        waitAfter, operationId, exception.GetType().Name,
                        restored ? "Verified" : "RecoveryRequired"));
            }
        }

        private void LogRuntimeCoolingUnavailable()
        {
            if (!PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(_settlement))
                return;
            foreach (VillageFiefData village in OrderedVillages()
                .Where(item => item.ReturnBatches?.Count > 0))
                ModLogger.FileDebug(string.Format(
                    "[PlayerFiefCooling] fief={0} fiefId={1} village={2} " +
                    "villageId={3} day={4} troops={5} result=FiefRuntimeNotReady " +
                    "reason={6}.", _settlement?.Name, _settlement?.StringId,
                    village.VillageSettlement?.Name,
                    village.VillageSettlement?.StringId,
                    (int)CampaignTime.Now.ToDays,
                    village.ReturnBatches.Sum(batch =>
                        batch?.GetTotalCount() ?? 0),
                    RuntimeInitializationFailureReason ?? "missing"));
        }

        private void LogCooling(FiefReturnBatch batch, int batchIndex,
            int campaignDay, int waitBefore, int waitAfter, string result,
            string trigger, VillageFiefData village, string extra = null)
        {
            ModLogger.FileDebug(string.Format(
                "[PlayerFiefCooling] fief={0} fiefId={1} village={2} " +
                "villageId={3} day={4} trigger={5} batchIndex={6} " +
                "troops={7} waitBefore={8} waitAfter={9} result={10}{11}.",
                _settlement?.Name, _settlement?.StringId,
                village.VillageSettlement?.Name,
                village.VillageSettlement?.StringId, campaignDay,
                trigger ?? "unknown", batchIndex,
                batch?.GetTotalCount() ?? 0, waitBefore, waitAfter, result,
                extra ?? string.Empty));
        }
    }
}
