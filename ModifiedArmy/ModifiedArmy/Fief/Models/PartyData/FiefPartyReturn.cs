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
    }
}
