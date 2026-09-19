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
        /// 玩家/AI 将部队中的采邑士兵（含伤员）归还至封邑军队。
        /// - 所有归还士兵（健康+伤员）均作为健康兵加入 FiefTroops（通过冷却分遣队）
        /// - 使用 RemoveTroop 自动处理健康/伤员混合移除
        /// - 仅处理 Occupation.Soldier
        /// - 先清空 RecruitedTroopDetachmentList
        /// - 按兵种剩余容量归还（不再循环权重）
        /// - 归还时：
        ///     1. 恢复实际归还士兵对应的 Hearth
        ///     2. 按照征召模型恢复对应的一次性 Prosperity 动员成本
        /// - Daily Debuff 不会因为归还而返还
        /// </summary>
        public int ReturnTroopsToSettlement(MobileParty sourceParty)
        {
            if (sourceParty == null || _fiefParty == null)
                return 0;

            // ============================================================
            // 记录当前处于征召状态的士兵数量
            //
            // 必须在清空 RecruitedTroopDetachmentList 之前获取。
            //
            // 例如：
            //     当前有 60 名采邑士兵被征召
            //     recruitedCountBeforeReturn = 60
            //
            // 这个值目前主要用于日志和调试。
            // Prosperity 返还按照实际归还人数重新使用
            // CalculateRecruitmentProsperityCost() 计算。
            // ============================================================
            int recruitedCountBeforeReturn =
                GetRecruitedFiefTroopCount();

            // ============================================================
            // 清空 RecruitedTroopDetachmentList 并扣减计数器
            //
            // 注意：
            // 这里清空之后，_soldierTypeCounts 表示的是：
            //
            //     当前没有被征召出去的封建军队数量
            //
            // 不能使用这里的封建军队总人数来计算本次
            // Prosperity 返还。
            // ============================================================
            if (RecruitedTroopDetachmentList != null)
            {
                foreach (var detachment in RecruitedTroopDetachmentList)
                {
                    if (detachment?.IsEmpty() == false)
                    {
                        foreach (var kvp in detachment.Troops)
                        {
                            var troop = kvp.Key;
                            int count = kvp.Value;

                            if (troop == null || count <= 0)
                                continue;

                            var type =
                                SoldierTypeClassifier.GetSoldierType(troop);

                            _soldierTypeCounts[type] =
                                Math.Max(
                                    0,
                                    _soldierTypeCounts[type] - count);
                        }

                        detachment.Clear();
                    }
                }

                RecruitedTroopDetachmentList.Clear();
            }

            // ============================================================
            // 记录归还的封邑士兵
            // ============================================================
            Dictionary<CharacterObject, int> tmpReturnTroops = new();

            foreach (var element in sourceParty.MemberRoster.GetTroopRoster())
            {
                var troop = element.Character;

                if (troop == null ||
                    troop.Occupation != Occupation.Soldier)
                    continue;

                if (!_fiefPartyTemplate.IsEnableTroop(troop))
                    continue;

                // Number already includes wounded troops in Bannerlord rosters.
                int count = element.Number;

                if (count <= 0)
                    continue;

                var type =
                    SoldierTypeClassifier.GetSoldierType(troop);

                // ========================================================
                // 检查剩余容量是否足够
                // ========================================================
                int availableCapacity =
                    Math.Max(
                        0,
                        _soldierTypeMaxCounts[type] -
                        _soldierTypeCounts[type]);

                int taken =
                    Math.Min(
                        count,
                        availableCapacity);

                if (taken > 0)
                {
                    // 从 sourceParty 移除士兵
                    RemoveTroopsFromParty(
                        sourceParty.MemberRoster,
                        troop,
                        taken);

                    // 记录归还的士兵和数量
                    if (tmpReturnTroops.ContainsKey(troop))
                    {
                        tmpReturnTroops[troop] += taken;
                    }
                    else
                    {
                        tmpReturnTroops[troop] = taken;
                    }

                    // 修改计数器
                    _soldierTypeCounts[type] += taken;
                }
            }

            int tmpReturnTroopCount =
                tmpReturnTroops.Values.Sum();

            if (tmpReturnTroopCount <= 0)
            {
                ModLogger.Debug(
                    $"[Return] Returned {tmpReturnTroopCount} troops to fief '{_settlement?.Name}'. ");

                return 0;
            }

            // ============================================================
            // 计算本次实际归还士兵对应的 Prosperity
            //
            // 重要：
            //
            // CalculateRecruitmentProsperityCost() 的逻辑是：
            //
            //     当前已征召人数
            //          ↓
            //     再征召 N 人所需要的 Prosperity
            //
            // 由于前面已经清空 RecruitedTroopDetachmentList，
            // 此时：
            //
            //     GetRecruitedFiefTroopCount() = 0
            //
            // 因此：
            //
            //     归还 60 人
            //     → CalculateRecruitmentProsperityCost(60)
            //     → 0 → 60
            //
            // Town：
            //     60 × 3 × 1 = 180
            //
            // 所以：
            //
            //     征召 60 → -180
            //     归还 60 → +180
            //
            // 如果只回来 40 人：
            //
            //     40 × 3 × 1 = 120
            //
            //     征召 60 → -180
            //     归还 40 → +120
            //
            // 剩余 20 人对应的 Prosperity 永久损失。
            //
            // Daily Debuff 不参与返还。
            // ============================================================
            float prosperityToReturn =
                CalculateRecruitmentProsperityCost(
                    (int)tmpReturnTroopCount);

            // ============================================================
            // 返还 Prosperity
            // ============================================================
            if (prosperityToReturn > 0)
            {
                prosperityToReturn = UpdateProsperity(
                    prosperityToReturn,
                    true);
            }

            // ============================================================
            // 归还 Hearth
            //
            // 只恢复实际活着归还的士兵数量。
            //
            // 例如：
            //     征召 100
            //     归还 70
            //
            //     Hearth +70
            //
            // 剩余 30 不恢复。
            // ============================================================
            float hearthCost = UpdateHearth(
                tmpReturnTroopCount,
                true);

            // ============================================================
            // 添加到 ReturnedTroopDetachmentList，
            // 记录处于冷却状态的士兵
            //
            // 冷却周期从模板读取（_fiefPartyTemplate.ReturnCooldownWeeks），
            // 替代原 CommonConstants.RETURN_TROOP_WAIT_CYCLE。
            // ============================================================
            int returnCooldown =
                _fiefPartyTemplate?.ReturnCooldownWeeks
                ?? CommonConstants.RETURN_TROOP_WAIT_CYCLE;

            FiefTroopDetachment targetDetachment =
                ReturnedTroopDetachmentList
                    .FirstOrDefault(
                        d => d != null &&
                            d.WaitCycle ==
                            returnCooldown);

            if (targetDetachment == null)
            {
                targetDetachment =
                    new FiefTroopDetachment(
                        returnCooldown);

                ReturnedTroopDetachmentList.Add(
                    targetDetachment);
            }

            // 向目标分遣队添加归还的部队
            targetDetachment.AddTroops(
                tmpReturnTroops);

            // ============================================================
            // 更新计数器
            // ============================================================
            _totalTroopCount =
                _soldierTypeCounts.Values.Sum();

            // ============================================================
            // 清除工资减免
            // ============================================================
            var _fiefWageExemptionManager =
                Campaign.Current
                    .GetCampaignBehavior<FiefWageExemptionManager>();

            _fiefWageExemptionManager?.ConsumeExemption(
                sourceParty,
                tmpReturnTroopCount);

            // ============================================================
            // 显示归还结果
            // ============================================================
            TextObject msgResult =
                GameTexts.FindText(
                    "str_modifiedarmy_fief_return_result");

            msgResult.SetTextVariable(
                "PARTY_NAME",
                sourceParty.Name.ToString());

            msgResult.SetTextVariable(
                "SETTLEMENT_NAME",
                _settlement.Name.ToString());

            msgResult.SetTextVariable(
                "RETURNED_COUNT",
                tmpReturnTroopCount);

            msgResult.SetTextVariable(
                "PROSPERITY_COST",
                prosperityToReturn);

            msgResult.SetTextVariable(
                "HEARTH_COST",
                tmpReturnTroopCount);

            msgResult.SetTextVariable(
                "RETINUE_COUNT",
                _soldierTypeCounts[SoldierType.Retinue]);

            msgResult.SetTextVariable(
                "MAX_RETINUE",
                _soldierTypeMaxCounts[SoldierType.Retinue]);

            msgResult.SetTextVariable(
                "SERGEANT_COUNT",
                _soldierTypeCounts[SoldierType.Sergeant]);

            msgResult.SetTextVariable(
                "MAX_SERGEANT",
                _soldierTypeMaxCounts[SoldierType.Sergeant]);

            msgResult.SetTextVariable(
                "MARINE_COUNT",
                _soldierTypeCounts[SoldierType.Marine]);

            msgResult.SetTextVariable(
                "MAX_MARINE",
                _soldierTypeMaxCounts[SoldierType.Marine]);

            msgResult.SetTextVariable(
                "SLAVE_COUNT",
                _soldierTypeCounts[SoldierType.Slave]);

            msgResult.SetTextVariable(
                "MAX_SLAVE",
                _soldierTypeMaxCounts[SoldierType.Slave]);

            msgResult.SetTextVariable(
                "MILITIA_COUNT",
                _soldierTypeCounts[SoldierType.Militia]);

            msgResult.SetTextVariable(
                "MAX_MILITIA",
                _soldierTypeMaxCounts[SoldierType.Militia]);

            msgResult.SetTextVariable(
                "TROOP_COUNT",
                _totalTroopCount);

            msgResult.SetTextVariable(
                "TOTAL_LIMIT",
                _totalLimit);

            if (sourceParty.LeaderHero.Clan == Clan.PlayerClan)
                ModLogger.Notice(
                    msgResult.ToString());
            else
                ModLogger.Debug(
                    msgResult.ToString());

            // ============================================================
            // 玩家额外输出 Prosperity 归还日志
            // ============================================================
            if (_settlement.OwnerClan == Clan.PlayerClan)
            {
                ModLogger.Debug(
                    $"[Return] Fief '{_settlement?.Name}' returned " +
                    $"{prosperityToReturn:F2} Prosperity. " +
                    $"Recruited before return: {recruitedCountBeforeReturn}, " +
                    $"returned troops: {tmpReturnTroopCount}.");
            }

            return tmpReturnTroopCount;
        }       
                
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
        /// 从封邑军队中按比例招募士兵到目标部队。
        /// - 从 _fiefParty 移除已征召士兵（因为他们已离营）
        /// - 添加到 RecruitedTroopDetachmentList（标记为已征召）
        /// - 不修改 RetinueCount/SergeantCount/MilitiaCount（兵力仍属封邑）
        ///
        /// 经济规则：
        /// - 玩家征召：产生 Prosperity / Hearth 动员成本
        /// - AI 征召：不产生 Prosperity / Hearth 成本
        /// </summary>
        /// <param name="targetParty">目标部队</param>
        /// <returns>总招募人数（必为 10 的倍数）</returns>
        public int RecruitTroopsToParty(MobileParty targetParty)
        {
            if (targetParty == null || _fiefParty == null)
                return 0;

            if (targetParty.LeaderHero == null)
                return 0;

            if (_settlement.OwnerClan != targetParty.LeaderHero.Clan)
                return 0;

            int currentMembers = targetParty.Party.NumberOfAllMembers;
            int partySizeLimit = targetParty.Party.PartySizeLimit;
            int remainSize = partySizeLimit - currentMembers;

            // 目标 party 没有空间，则停止招募
            if (remainSize <= 0)
                return 0;

            if (_soldierTypeWeights == null || _totalWeight <= 0)
            {
                ModLogger.Error(
                    $"[Recruit] CRITICAL: _soldierTypeWeights is null/empty or _totalWeight={_totalWeight}");

                return 0;
            }

            // 士兵类型及可招募的数量
            Dictionary<SoldierType, int> tmpSoldierTypeSize = new();

            // 记录招募的士兵类型及数量
            Dictionary<SoldierType, int> tmpRecruitSoldierTypeSize = new();

            // 按权重分配剩余空间
            foreach (var kvp in _soldierTypeWeights)
            {
                tmpSoldierTypeSize[kvp.Key] =
                    (remainSize * _soldierTypeWeights[kvp.Key]) / _totalWeight;

                tmpRecruitSoldierTypeSize[kvp.Key] = 0;
            }

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
                _fiefPartyTemplate?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                _fiefPartyTemplate?.HearthCostPerTroop ?? 0;

            foreach (var element in _fiefParty.GetTroopRoster())
            {
                var troop = element.Character;
                var count = element.Number;

                if (troop == null || count <= 0)
                    continue;

                if (!SoldierTypeClassifier.IsFiefTroop(troop))
                    continue;

                var type =
                    SoldierTypeClassifier.GetSoldierType(troop);

                // 检查剩余容量是否足够
                int taken =
                    Math.Min(
                        count,
                        tmpSoldierTypeSize[type]);

                if (taken > 0)
                {
                    // 从封邑 party 移除士兵
                    RemoveTroopsFromParty(
                        _fiefParty,
                        troop,
                        taken);

                    // 向目标 party 添加士兵
                    targetParty.MemberRoster.AddToCounts(
                        troop,
                        taken,
                        false,
                        0,
                        0,
                        true,
                        -1);

                    // 记录招募的士兵
                    if (tmpRecruitTroops.ContainsKey(troop))
                        tmpRecruitTroops[troop] += taken;
                    else
                        tmpRecruitTroops[troop] = taken;

                    // 更新计数器
                    tmpSoldierTypeSize[type] =
                        Math.Max(
                            0,
                            tmpSoldierTypeSize[type] - taken);

                    tmpRecruitSoldierTypeSize[type] += taken;

                    hearthCost +=
                        taken * tmpHearthCostPerTroop;
                }
            }

            int totalRecruited =
                tmpRecruitTroops.Values.Sum();

            if (totalRecruited <= 0)
                return 0;

            prosperityCost =
                CalculateRecruitmentProsperityCost(totalRecruited);

            prosperityCost = UpdateProsperity(
                prosperityCost,
                false);
            hearthCost = UpdateHearth(
                hearthCost,
                false);

            // ============================================================
            // 记录招募的士兵
            //
            // 服役周期从模板读取（_fiefPartyTemplate.MaxServiceWeeks），
            // 替代原 CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE。
            // ============================================================
            int maxServiceWeeks =
                _fiefPartyTemplate?.MaxServiceWeeks
                ?? CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE;

            FiefTroopDetachment targetDetachment =
                RecruitedTroopDetachmentList
                    .FirstOrDefault(
                        d => d != null &&
                            d.WaitCycle ==
                            maxServiceWeeks);

            if (targetDetachment == null)
            {
                targetDetachment =
                    new FiefTroopDetachment(
                        maxServiceWeeks);

                RecruitedTroopDetachmentList.Add(
                    targetDetachment);
            }

            targetDetachment.AddTroops(
                tmpRecruitTroops);

            TextObject msg =
                GameTexts.FindText(
                    "str_modifiedarmy_fief_recruit_to_party");

            msg.SetTextVariable(
                "PARTY_NAME",
                targetParty.Name.ToString());

            msg.SetTextVariable(
                "SETTLEMENT_NAME",
                _settlement.Name.ToString());

            msg.SetTextVariable(
                "RETINUE",
                tmpRecruitSoldierTypeSize[SoldierType.Retinue]);

            msg.SetTextVariable(
                "SERGEANT",
                tmpRecruitSoldierTypeSize[SoldierType.Sergeant]);

            msg.SetTextVariable(
                "MARINE",
                tmpRecruitSoldierTypeSize[SoldierType.Marine]);

            msg.SetTextVariable(
                "SLAVE",
                tmpRecruitSoldierTypeSize[SoldierType.Slave]);

            msg.SetTextVariable(
                "MILITIA",
                tmpRecruitSoldierTypeSize[SoldierType.Militia]);

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
            else
            {
                ModLogger.Info(msg.ToString());
            }

            // ============================================================
            // 给与工资减免
            // ============================================================
            var _fiefWageExemptionManager =
                Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();

            _fiefWageExemptionManager?.AddExemption(
                targetParty,
                totalRecruited,
                CommonConstants.FIEF_WAGE_EXEMPTION_DAYS);

            return totalRecruited;
        }


        /// <summary>
        /// 从封邑就绪部队中手动招募指定的士兵，并记录到征召列表。
        /// 前提：selectedRoster 来自 FiefTroops，所有 troop 和数量均有效。
        /// </summary>
        /// <param name="selectedRoster">玩家从 _fiefParty 中选择的士兵</param>
        /// <returns>实际招募的总人数（用于工资豁免）</returns>
        public int RecruitManualSelection(TroopRoster selectedRoster, MobileParty targetParty)
        {
            if (selectedRoster == null || selectedRoster.TotalManCount <= 0 || _fiefParty == null)
                return 0;

            var tmpRecruitTroops = new Dictionary<CharacterObject, int>();
            // 记录招募的士兵类型及数量
            Dictionary<SoldierType, int> tmpRecruitSoldierTypeSize = new();
            // 按权重分配剩余空间
            foreach (var kvp in _soldierTypeWeights)
            {
                tmpRecruitSoldierTypeSize[kvp.Key] = 0;
            }

            float prosperityCost = 0;
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取（与 RecruitTroopsToParty 一致）
            // ============================================================
            int tmpProsperityCostPerTroop =
                _fiefPartyTemplate?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                _fiefPartyTemplate?.HearthCostPerTroop ?? 0;

            foreach (var element in selectedRoster.GetTroopRoster())
            {
                var troop = element.Character;
                int count = element.Number;
                if (troop == null || count <= 0) continue;

                if (!SoldierTypeClassifier.IsFiefTroop(troop))
                    continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);

                // 记录招募的士兵
                tmpRecruitTroops[troop] = count;
                tmpRecruitSoldierTypeSize[type] += count;
                // 从封邑party移除士兵
                RemoveTroopsFromParty(_fiefParty, troop, count);

                // ====================================================
                // Hearth 成本
                //
                // 每征召 1 名士兵，消耗 1 Hearth。
                // Prosperity 不在这里计算，统一在本次征召完成后计算。
                // ====================================================
                hearthCost +=
                    count * tmpHearthCostPerTroop;
            }

            int totalRecruited =
                tmpRecruitTroops.Values.Sum();

            if (totalRecruited <= 0)
                return 0;

            prosperityCost =
                CalculateRecruitmentProsperityCost(totalRecruited);

            prosperityCost = UpdateProsperity(
                prosperityCost,
                false);
            hearthCost = UpdateHearth(
                hearthCost,
                false);

            // 添加到 RecruitedTroopDetachmentList
            int maxServiceWeeks =
                _fiefPartyTemplate?.MaxServiceWeeks
                ?? CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE;

            FiefTroopDetachment targetDetachment = RecruitedTroopDetachmentList
                .FirstOrDefault(d => d != null && d.WaitCycle == maxServiceWeeks);

            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(maxServiceWeeks);
                RecruitedTroopDetachmentList.Add(targetDetachment);
            }
            targetDetachment.AddTroops(tmpRecruitTroops);

            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_recruit_to_party");
            msg.SetTextVariable("PARTY_NAME", targetParty.Name.ToString());
            msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msg.SetTextVariable("RETINUE", tmpRecruitSoldierTypeSize[SoldierType.Retinue]);
            msg.SetTextVariable("SERGEANT", tmpRecruitSoldierTypeSize[SoldierType.Sergeant]);
            msg.SetTextVariable("MARINE", tmpRecruitSoldierTypeSize[SoldierType.Marine]);
            msg.SetTextVariable("SLAVE", tmpRecruitSoldierTypeSize[SoldierType.Slave]);
            msg.SetTextVariable("MILITIA", tmpRecruitSoldierTypeSize[SoldierType.Militia]);
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

            // 给予工资减免
            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager?.AddExemption(
                targetParty,
                totalRecruited,
                CommonConstants.FIEF_WAGE_EXEMPTION_DAYS);

            // 将士兵添加到目标party
            targetParty.MemberRoster.Add(selectedRoster);

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

            // 2. 获取士兵类型并检查剩余容量
            var type = SoldierTypeClassifier.GetSoldierType(troop);
            int currentCount = _soldierTypeCounts[type];
            int maxCount = _soldierTypeMaxCounts[type];
            int availableCapacity = Math.Max(0, maxCount - currentCount);

            if (availableCapacity < count)
                return false;


            // 3. 直接添加到封建部队并更新繁荣度
            _fiefParty.AddToCounts(troop, count, false, 0, 0, true, -1);

            // 消耗的繁荣度
            float prosperityCost = 0;
            // 消耗的户数
            float hearthCost = 0;

            // ============================================================
            // 征召成本：从模板读取（与 RecruitTroopsToParty 一致）
            // ============================================================
            int tmpProsperityCostPerTroop =
                _fiefPartyTemplate?.ProsperityCostPerTroop ?? 0;

            int tmpHearthCostPerTroop =
                _fiefPartyTemplate?.HearthCostPerTroop ?? 0;

            hearthCost += count * tmpHearthCostPerTroop;
            prosperityCost += count * tmpProsperityCostPerTroop * troop.Tier;

            prosperityCost = UpdateProsperity(prosperityCost, true);
            hearthCost = UpdateHearth(hearthCost, true);

            // 4. 更新计数器
            _soldierTypeCounts[type] += count;
            _totalTroopCount = _soldierTypeCounts.Values.Sum();

            ModLogger.Notice($"{_settlement.Name}的封邑部队从俘虏中招募了{count}名{troop.Name}");

            return true;
        }
    }
}
