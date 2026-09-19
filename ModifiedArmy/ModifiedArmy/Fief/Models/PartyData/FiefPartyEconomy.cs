using Bannerlord.UIExtenderEx;
using HarmonyLib;
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
        /// - 只计算当前处于 RecruitedTroopDetachmentList 的士兵。
        /// - 玩家和 AI 暂时使用相同的每日损失。
        /// - Prosperity 可以产生小数。
        /// - Hearth 不受 Daily Debuff 影响。
        /// </summary>
        public void DailyUpdate()
        {
            int recruitedTroopCount =
                GetRecruitedFiefTroopCount();

            if (recruitedTroopCount <= 0)
                return;

            float dailyProsperityLoss =
                CalculateDailyRecruitmentProsperityLoss(
                    recruitedTroopCount);

            if (dailyProsperityLoss <= 0f)
                return;

            dailyProsperityLoss= UpdateProsperity(
                dailyProsperityLoss,
                false);

            // 只有玩家领地打印 Daily Debuff 日志
            if (_settlement.OwnerClan == Clan.PlayerClan)
            {
                ModLogger.Notice(
                    $"[DailyDebuff] Fief '{_settlement?.Name}' lost {dailyProsperityLoss:F2} Prosperity from {recruitedTroopCount} recruited troops.");
            }
        }

        /// <summary>
        /// 计算玩家征召封建军所需支付的 Prosperity。
        ///
        /// 计算规则：
        /// Castle BaseCost = 1
        /// Town   BaseCost = 3
        ///
        /// 动员比例：
        /// 0% ~ 25%      -> x1.0
        /// >25% ~ 50%    -> x1.5
        /// >50% ~ 75%    -> x2.0
        /// >75% ~ 100%   -> x2.5
        ///
        /// 如果一次征召跨越多个档位，则分段计算。
        /// </summary>
        public float CalculateRecruitmentProsperityCost(int recruitCount)
        {
            if (recruitCount <= 0)
                return 0;

            // ============================================================
            // 征召基础繁荣度成本：从模板读取
            //
            // 旧逻辑：
            //     Town   = 3
            //     Castle = 1
            //
            // 新逻辑：
            //     由 _fiefPartyTemplate.ProsperityCostPerTroop 决定，
            //     允许每个文化/定居点类型独立配置。
            // ============================================================
            float baseProsperityCost = _fiefPartyTemplate?.ProsperityCostPerTroop ?? 0f;

            if (baseProsperityCost <= 0f)
                return 0;

            int currentMobilized =
                GetRecruitedFiefTroopCount();

            int maxMobilized =
                Math.Max(1, _totalLimit);

            int remainingToCalculate =
                recruitCount;

            float totalProsperityCost = 0f;

            while (remainingToCalculate > 0 &&
                currentMobilized < maxMobilized)
            {
                float mobilizationRatio =
                    currentMobilized / (float)maxMobilized;

                float tierMultiplier;
                int tierLimit;

                if (mobilizationRatio < 0.25f)
                {
                    tierMultiplier = 1.0f;
                    tierLimit =
                        (int)MathF.Ceiling(maxMobilized * 0.25f);
                }
                else if (mobilizationRatio < 0.50f)
                {
                    tierMultiplier = 1.5f;
                    tierLimit =
                        (int)MathF.Ceiling(maxMobilized * 0.50f);
                }
                else if (mobilizationRatio < 0.75f)
                {
                    tierMultiplier = 2.0f;
                    tierLimit =
                        (int)MathF.Ceiling(maxMobilized * 0.75f);
                }
                else
                {
                    tierMultiplier = 2.5f;
                    tierLimit = maxMobilized;
                }

                int troopsInThisTier =
                    Math.Min(
                        remainingToCalculate,
                        Math.Max(
                            0,
                            tierLimit - currentMobilized));

                if (troopsInThisTier <= 0)
                    break;

                totalProsperityCost +=
                    troopsInThisTier *
                    baseProsperityCost *
                    tierMultiplier;

                currentMobilized +=
                    troopsInThisTier;

                remainingToCalculate -=
                    troopsInThisTier;
            }

            return MathF.Round(
                totalProsperityCost);
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

                village.Hearth =
                    newHearth;
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

                village.Hearth =
                    newHearth;
            }

            return actualHearthCost;
        }
    }
}
