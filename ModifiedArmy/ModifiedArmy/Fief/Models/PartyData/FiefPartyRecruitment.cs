using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.PartyFinance.Models;
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
            float remainingDailyWageBudget =
                AiRecruitmentFinancialModel
                    .GetAvailableAdditionalDailyWage(targetParty);

            // 目标 party 没有空间，则停止招募
            if (remainSize <= 0
                || remainingDailyWageBudget <= 0f)
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

                float unitDailyWage =
                    AiRecruitmentFinancialModel
                        .EstimateUnitDailyWage(
                            targetParty,
                            troop);

                if (unitDailyWage > 0f)
                {
                    taken = Math.Min(
                        taken,
                        (int)Math.Floor(
                            remainingDailyWageBudget
                            / unitDailyWage));
                }

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

                    remainingDailyWageBudget = Math.Max(
                        0f,
                        remainingDailyWageBudget
                        - taken * unitDailyWage);

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
