using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Population.Contracts;
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
        /// <summary>
        /// 计算封建军征召期间每日造成的 Prosperity 损失。
        ///
        /// 每名处于征召状态的士兵每天产生固定 Prosperity 损失。
        ///
        /// Town   = 0.100 Prosperity / 人 / 天
        /// Castle = 0.050 Prosperity / 人 / 天
        ///
        /// 允许产生小数。
        /// </summary>
        private float CalculateDailyRecruitmentProsperityLoss(
            int recruitedTroopCount)
        {
            if (recruitedTroopCount <= 0)
                return 0f;

            float prosperityLossPerTroop = 0f;

            if (_settlement.IsTown)
            {
                prosperityLossPerTroop = 0.100f;
            }
            else if (_settlement.IsCastle)
            {
                prosperityLossPerTroop = 0.050f;
            }

            if (prosperityLossPerTroop <= 0f)
                return 0f;

            return recruitedTroopCount *
                prosperityLossPerTroop;
        }

        /// <summary>
        /// 每天调用一次，处理封邑军队征召期间的每日 Prosperity 损失。
        ///
        /// 规则：
        /// - Only counts soldiers in the current service batches.
        /// - 玩家和 AI 暂时使用相同的每日损失。
        /// - Prosperity 可以产生小数。
        /// - Hearth 不受 Daily Debuff 影响。
        /// </summary>
        public void DailyUpdate()
        {
            // Mobilization now affects village production capacity instead of
            // applying a second, unrelated prosperity penalty every day.
        }

        /// <summary>
        /// 更新定居点繁荣度。
        ///
        /// 玩家：
        ///     100% Prosperity 影响
        ///
        /// AI：
        ///     10% Prosperity 影响
        ///
        /// Hearth 不在此函数中处理。
        /// </summary>
        public float UpdateProsperity(
            float prosperityCost,
            bool isAdd)
        {
            if (prosperityCost <= 0f)
                return prosperityCost;

            // 玩家 100%，AI 10%
            float prosperityMultiplier =
                _settlement.OwnerClan == Clan.PlayerClan
                    ? 1.0f
                    : CommonConstants.AI_FIEF_PROSPERITY_IMPACT_MULTIPLIER;

            float actualProsperityCost =
                prosperityCost * prosperityMultiplier;

            if (isAdd)
            {
                _settlement.Town.Prosperity =
                    Math.Max(
                        0f,
                        _settlement.Town.Prosperity +
                        actualProsperityCost);
            }
            else
            {
                if (_settlement.IsCastle)
                {
                    _settlement.Town.Prosperity =
                        Math.Max(
                            CommonConstants.CASTLE_POOR_THRESHOLD,
                            _settlement.Town.Prosperity -
                            actualProsperityCost);
                }
                else if (_settlement.IsTown)
                {
                    _settlement.Town.Prosperity =
                        Math.Max(
                            CommonConstants.TOWN_POOR_THRESHOLD,
                            _settlement.Town.Prosperity -
                            actualProsperityCost);
                }
            }

            return actualProsperityCost;
        }

        /// <summary>
        /// 更新封邑所属村庄的 Hearth。
        ///
        /// 玩家：
        ///     100% Hearth 影响。
        ///
        /// AI：
        ///     按 AI_FIEF_PROSPERITY_IMPACT_MULTIPLIER
        ///     计算实际 Hearth 影响。
        ///
        /// Hearth 总变化会均匀分配到所属村庄。
        /// </summary>
        public float UpdateHearth(
            float hearthCost,
            bool isAdd)
        {
            if (hearthCost <= 0)
                return 0;

            int villageCount =
                _settlement.BoundVillages.Count;

            if (villageCount <= 0)
                return 0;

            // ============================================================
            // 玩家 / AI Hearth 影响倍率
            //
            // 玩家：100%
            // AI：使用 AI 系数
            // ============================================================

            float hearthMultiplier =
                _settlement.OwnerClan == Clan.PlayerClan
                    ? 1.0f
                    : CommonConstants.AI_FIEF_HEARTH_IMPACT_MULTIPLIER;

            // ============================================================
            // 计算实际 Hearth 变化量
            //
            // Hearth 最终需要以整数形式均匀分配给村庄，
            // 因此在应用倍率后进行四舍五入。
            // ============================================================

            float actualHearthCost =
                Math.Max(
                    0,
                    MathF.Round(
                        hearthCost *
                        hearthMultiplier));

            if (actualHearthCost <= 0)
                return 0;

            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (population == null)
            {
                ModLogger.FileWarn(string.Format(
                    "Fief Hearth update rejected: fief={0}, id={1}, " +
                    "amount={2:F2}, reason=PopulationServiceUnavailable.",
                    _settlement.Name,
                    _settlement.StringId,
                    actualHearthCost));
                return 0;
            }
            var committedOperations =
                new List<(Settlement Settlement, string OperationId)>();
            float committedAmount = 0f;

            // ============================================================
            // 计算总变化量
            // ============================================================

            int sign =
                isAdd ? 1 : -1;

            float totalChange =
                actualHearthCost * sign;

            // ============================================================
            // 均匀分配：
            // 基础值 + 余数
            // ============================================================

            float baseChange =
                totalChange / villageCount;

            float remainder =
                totalChange % villageCount;

            // ============================================================
            // 修正负余数
            // ============================================================

            if (remainder < 0)
            {
                baseChange--;

                remainder +=
                    villageCount;
            }

            // ============================================================
            // 应用基础分配
            // ============================================================

            for (int i = 0;
                i < villageCount;
                i++)
            {
                Village village =
                    _settlement.BoundVillages[i];

                float newHearth =
                    village.Hearth +
                    baseChange;

                if (!isAdd)
                {
                    newHearth =
                        Math.Max(
                            CommonConstants.VillageMinHearthThreshold,
                            newHearth);
                }

                float applied = newHearth - village.Hearth;
                if (!TryApplyPopulationChange(
                        population,
                        village.Settlement,
                        applied,
                        committedOperations,
                        ref committedAmount))
                {
                    RollbackPopulationChanges(
                        population,
                        committedOperations);
                    return 0f;
                }
            }

            // ============================================================
            // 分配余数
            //
            // remainder 始终为非负数。
            //
            // 对于扣除操作，
            // 前 remainder 个村庄额外 -1。
            //
            // 对于恢复操作，
            // 前 remainder 个村庄额外 +1。
            // ============================================================

            for (int i = 0;
                i < remainder;
                i++)
            {
                Village village =
                    _settlement.BoundVillages[i];

                float remainderChange =
                    isAdd
                        ? 1f
                        : -1f;

                float newHearth =
                    village.Hearth +
                    remainderChange;

                if (!isAdd)
                {
                    newHearth =
                        Math.Max(
                            CommonConstants.VillageMinHearthThreshold,
                            newHearth);
                }

                float applied = newHearth - village.Hearth;
                if (!TryApplyPopulationChange(
                        population,
                        village.Settlement,
                        applied,
                        committedOperations,
                        ref committedAmount))
                {
                    RollbackPopulationChanges(
                        population,
                        committedOperations);
                    return 0f;
                }
            }

            return committedAmount;
        }

        private bool TryApplyPopulationChange(
            ISettlementPopulationService population,
            Settlement village,
            float change,
            ICollection<(Settlement Settlement, string OperationId)> commits,
            ref float committedAmount)
        {
            if (Math.Abs(change) <= 0.0001f) return true;
            string operationId = "fief-economy:" +
                Guid.NewGuid().ToString("N");
            PopulationTransactionResult result = change > 0f
                ? population.TryRestore(village, change, operationId)
                : population.TryConsume(village, -change, operationId);
            if (result?.IsCommitted == true)
            {
                commits.Add((village, operationId));
                committedAmount += Math.Abs(change);
                return true;
            }

            ModLogger.FileWarn(string.Format(
                "Fief Hearth update rejected: fief={0}, village={1}, " +
                "operation={2}, amount={3:F2}, reason={4}.",
                _settlement.Name,
                village?.Name,
                operationId,
                Math.Abs(change),
                result?.ReasonCode ?? "Unknown"));
            return false;
        }

        private void RollbackPopulationChanges(
            ISettlementPopulationService population,
            IEnumerable<(Settlement Settlement, string OperationId)> commits)
        {
            foreach (var commit in commits.Reverse())
            {
                PopulationTransactionResult result = population.TryCompensate(
                    commit.Settlement,
                    commit.OperationId);
                if (result?.Status ==
                    PopulationTransactionStatus.Compensated)
                {
                    continue;
                }

                if (Villages != null &&
                    Villages.TryGetValue(
                        commit.Settlement,
                        out VillageFiefData village))
                {
                    village.RequireRecovery(
                        "PopulationCompensationFailed",
                        commit.OperationId);
                }
                ModLogger.FileError(string.Format(
                    "Fief Hearth rollback failed: fief={0}, village={1}, " +
                    "operation={2}, reason={3}.",
                    _settlement.Name,
                    commit.Settlement?.Name,
                    commit.OperationId,
                    result?.ReasonCode ?? "Unknown"));
            }
        }
    }
}
