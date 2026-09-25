using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
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
        /// Withdraws saved fief manpower for the legacy Fief Party roster.
        /// Pool production already applies barracks unlocks and XML weights,
        /// while this transfer applies the separate Fief Party establishment.
        /// </summary>
        private Dictionary<CharacterObject, int>
        WithdrawFiefPoolTroops(
            Dictionary<SoldierType, int> remainingTypeCapacity,
            int maximumCount)
        {
            Dictionary<CharacterObject, int> result = new();
            SettlementRecruitmentPoolBehavior pools = Campaign.Current
                .GetCampaignBehavior<SettlementRecruitmentPoolBehavior>();
            if (pools == null || maximumCount <= 0)
            {
                return result;
            }

            IReadOnlyDictionary<CharacterObject, int> available =
                pools.GetAvailableTroops(
                    _settlement,
                    RecruitmentPoolKind.Fief);
            int remainingTotal = maximumCount;

            // The pool already maintains the XML troop weights. Transfer its
            // largest stocks first while respecting each legacy Fief Party
            // SoldierType establishment limit.
            foreach (KeyValuePair<CharacterObject, int> entry in available
                .Where(entry => entry.Key != null && entry.Value > 0)
                .OrderByDescending(entry => entry.Value)
                .ThenBy(entry => entry.Key.StringId))
            {
                if (remainingTotal <= 0 ||
                    !_fiefPartyTemplate.IsEnableTroop(entry.Key))
                {
                    continue;
                }

                SoldierType type = SoldierTypeClassifier.GetSoldierType(
                    entry.Key);
                if (!remainingTypeCapacity.TryGetValue(
                        type,
                        out int typeCapacity) ||
                    typeCapacity <= 0)
                {
                    continue;
                }

                int count = Math.Min(
                    entry.Value,
                    Math.Min(typeCapacity, remainingTotal));
                if (count <= 0 || !pools.TryConsume(
                        _settlement,
                        RecruitmentPoolKind.Fief,
                        entry.Key,
                        count))
                {
                    continue;
                }

                result[entry.Key] = count;
                remainingTypeCapacity[type] -= count;
                remainingTotal -= count;
            }

            return result;
        }


        /// <summary>
        /// 从给定的 TroopRoster 中提取所有采邑类型士兵，返回一个新的仅含采邑部队的 TroopRoster。
        /// </summary>
        /// <param name="roster">源 TroopRoster（例如来自 MemberRoster.GetTroopRoster()）</param>
        /// <returns>新的 TroopRoster，仅包含 Retinue / Sergeant / Militia 类型的士兵</returns>
        public TroopRoster GetFiefTroopRoster()
        {
            if (_fiefParty == null)
                return TroopRoster.CreateDummyTroopRoster();

            var fiefRoster = TroopRoster.CreateDummyTroopRoster();

            foreach (var element in _fiefParty.GetTroopRoster())
            {
                var troop = element.Character;
                if (!_fiefPartyTemplate.IsEnableTroop(troop))
                {
                    continue;
                }

                // Number already includes wounded troops in Bannerlord rosters.
                var count = element.Number;
                if (troop == null || count <= 0)
                    continue;

                fiefRoster.AddToCounts(
                    troop,
                    element.Number,
                    false,
                    element.WoundedNumber);
            }

            return fiefRoster;
        }


        /// <summary>
        /// 执行封邑部队的补员操作：根据可补充人数，按兵种权重分配、生成新兵、更新计数并记录日志。
        /// </summary>
        /// <param name="maxReinforcements">最多可补充的总人数（已扣除总容量限制）</param>
        private void PerformReinforcement(int maxReinforcements)
        {
            Dictionary<SoldierType, int> tmpSoldierTypeSize = new();
            int tmpTotalWeight = 0;
            foreach (var kvp in _soldierTypeWeights)
            {
                tmpSoldierTypeSize[kvp.Key] = Math.Max(0, _soldierTypeMaxCounts[kvp.Key] - _soldierTypeCounts[kvp.Key]);
                if (tmpSoldierTypeSize[kvp.Key] > 0)
                {
                    tmpTotalWeight += kvp.Value;
                }
            }

            if (tmpTotalWeight <= 0)
                return;

            // 按权重分配可补充人数
            foreach (var kvp in _soldierTypeWeights)
            {
                if (tmpSoldierTypeSize[kvp.Key] > 0)
                {
                    int idealSize = (maxReinforcements * _soldierTypeWeights[kvp.Key]) / tmpTotalWeight;
                    // 需要考虑兵种类型的最大容量
                    tmpSoldierTypeSize[kvp.Key] = Math.Min(idealSize, tmpSoldierTypeSize[kvp.Key]);
                }
            }

            // 若无可补充兵员，直接退出
            if (tmpSoldierTypeSize.Values.Sum() <= 0)
            {
                ModLogger.Debug($"[WeeklyUpdate] No troops can be recruited this week in {_settlement.Name} (capacity full or ideal=0). Skipping.");
                return;
            }

            // New fief soldiers must come from the settlement's saved fief
            // manpower pool. No troop is created by this legacy weekly path.
            Dictionary<SoldierType, int> remainingTypeCapacity =
                new Dictionary<SoldierType, int>(tmpSoldierTypeSize);
            var tmpNewTroops = WithdrawFiefPoolTroops(
                remainingTypeCapacity,
                maxReinforcements);

            // Reuse the existing message fields, but report actual transfers
            // instead of the desired allocation calculated above.
            foreach (SoldierType type in tmpSoldierTypeSize.Keys.ToList())
            {
                tmpSoldierTypeSize[type] = 0;
            }

            // 批量添加
            foreach (var kvp in tmpNewTroops)
            {
                _fiefParty.AddToCounts(kvp.Key, kvp.Value, false, 0, 0, true, -1);

                var type = SoldierTypeClassifier.GetSoldierType(kvp.Key);
                _soldierTypeCounts[type] += kvp.Value;
                tmpSoldierTypeSize[type] += kvp.Value;
            }

            _totalTroopCount = _soldierTypeCounts.Values.Sum();;

            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_weekly_reinforcement");
            msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msg.SetTextVariable("RETINUE", tmpSoldierTypeSize[SoldierType.Retinue]);
            msg.SetTextVariable("SERGEANT", tmpSoldierTypeSize[SoldierType.Sergeant]);
            msg.SetTextVariable("MARINE", tmpSoldierTypeSize[SoldierType.Marine]);
            msg.SetTextVariable("SLAVE", tmpSoldierTypeSize[SoldierType.Slave]);
            msg.SetTextVariable("MILITIA", tmpSoldierTypeSize[SoldierType.Militia]);

            msg.SetTextVariable("RETINUE_COUNT", _soldierTypeCounts[SoldierType.Retinue]);
            msg.SetTextVariable("MAX_RETINUE", _soldierTypeMaxCounts[SoldierType.Retinue]);
            msg.SetTextVariable("SERGEANT_COUNT", _soldierTypeCounts[SoldierType.Sergeant]);
            msg.SetTextVariable("MAX_SERGEANT", _soldierTypeMaxCounts[SoldierType.Sergeant]);
            msg.SetTextVariable("MARINE_COUNT", _soldierTypeCounts[SoldierType.Marine]);
            msg.SetTextVariable("MAX_MARINE", _soldierTypeMaxCounts[SoldierType.Marine]);
            msg.SetTextVariable("SLAVE_COUNT", _soldierTypeCounts[SoldierType.Slave]);
            msg.SetTextVariable("MAX_SLAVE", _soldierTypeMaxCounts[SoldierType.Slave]);
            msg.SetTextVariable("MILITIA_COUNT", _soldierTypeCounts[SoldierType.Militia]);
            msg.SetTextVariable("MAX_MILITIA", _soldierTypeMaxCounts[SoldierType.Militia]);
            msg.SetTextVariable("TROOP_COUNT", _totalTroopCount);
            msg.SetTextVariable("TOTAL_LIMIT", _totalLimit);


            if (_settlement.OwnerClan == Clan.PlayerClan)
            {
                ModLogger.Info(msg.ToString());
            }
            else
            {
                ModLogger.Debug(msg.ToString());
            }
        }

        /// <summary>
        /// 获取当前繁荣度和户数对补员的影响
        /// </summary>
        /// 
        public float CalculateReinforcementMultiplier()
        {
            // === 1. 获取繁荣度 ===
            float prosperity = _settlement.Town.Prosperity; // 使用 Settlement 的 Prosperity 属性
            float prosperityRatio = 0f;

            if (_settlement.IsTown)
            {
                prosperityRatio = MathF.Min(1f, prosperity / CommonConstants.TOWN_VERY_RICH_THRESHOLD); 
            }
            else if (_settlement.IsCastle)
            {
                prosperityRatio = MathF.Min(1f, prosperity / CommonConstants.CASTLE_VERY_RICH_THRESHOLD); 
            }

            // === 2. 获取附属村庄总户数 ===
            float totalHearth = 0;
            int villageCount = 0;
            foreach (var village in _settlement.BoundVillages)
            {
                if (!village.IsDeserted)
                    totalHearth += village.Hearth;
                
                villageCount += 1;
            }
            float hearthRatio = MathF.Min(1f, totalHearth / (CommonConstants.VillageMaxReinforcementHearthThreshold * villageCount)); 

            // === 3. 计算加权综合比例 ===
            float prosperityWeight = CommonConstants.ProsperityWeight; // 繁荣度权重
            float hearthWeight = CommonConstants.HearthsWeight;    // 村庄户数权重
            float combinedRatio = prosperityRatio * prosperityWeight + hearthRatio * hearthWeight;
            combinedRatio = MathF.Clamp(combinedRatio, 0f, 1f);

            return combinedRatio;
        }

        /// <summary>
        /// 获取每周自动补员的数量
        /// </summary>
        public int GetWeeklyUpdateCount()
        {
            if (_settlement == null || _fiefPartyTemplate == null) 
                return 0;

            int minReinforcements = _fiefPartyTemplate.MinWeeklySupplement;
            int maxReinforcements = _fiefPartyTemplate.MaxWeeklySupplement;

            // 如果 min >= max，直接返回 min（避免无效区间）
            if (minReinforcements >= maxReinforcements)
                return minReinforcements;


            float combinedRatio = CalculateReinforcementMultiplier();

            // 映射到 [min, max] 区间 ===
            float reinforcements = minReinforcements + combinedRatio * (maxReinforcements - minReinforcements);
            
            return (int)MathF.Round(reinforcements);
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

            ModLogger.Debug($"[AutoUpgrade] Base chance in {_settlement.Name}: {upgradeChance:P1}");

            // 对每个士兵独立判定是否升级
            int upgradedCount = 0;

            // 遍历所有 troop 
            var rosterSnapshot = _fiefParty.GetTroopRoster();
            foreach (var element in rosterSnapshot)
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

                        _fiefParty.RemoveTroop(troop, 1, default(UniqueTroopDescriptor), 0);
                        _fiefParty.AddToCounts(newTroop, 1, false, 0, 0, true, -1);

                        upgradedCount++;
                    }
                }
            }

            if (upgradedCount > 0)
            {
                ModLogger.Debug($"[AutoUpgrade] Upgraded {upgradedCount} troops in {_settlement.Name}.");
            }
        }


        /// <summary>
        /// 每周调用一次，处理封邑军队的自动恢复与补员。
        /// </summary>
        public void WeeklyUpdate()
        {
            if (!InitialFeifPartyData())
                return;

            // 升级封邑士兵
            PerformAutoUpgrade();

            // 处理 ReturnedTroopDetachmentList, 即解散士兵冷却结束
            if (ReturnedTroopDetachmentList != null)
            {
                for (int i = ReturnedTroopDetachmentList.Count - 1; i >= 0; i--)
                {
                    var detachment = ReturnedTroopDetachmentList[i];
                    detachment.Tick();

                    if (detachment.IsReadyToReturn())
                    {
                        foreach (var kvp in detachment.Troops)
                        {
                            _fiefParty.AddToCounts(kvp.Key, kvp.Value, false, 0, 0, true, -1);
                        }

                        // 移除该分遣队
                        ReturnedTroopDetachmentList.RemoveAt(i);

                        if (_settlement.OwnerClan == Clan.PlayerClan)
                        {
                            TextObject textObject = new TextObject("{=FiefTroopsReady}{SETTLEMENT_NAME}'s feudal troops are ready!", null);
                            textObject.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
                            MBInformationManager.AddQuickInformation(textObject, 5000, null, null, "");
                        }
                    }
                }
            }

            // 处理已经征召的士兵
            if (RecruitedTroopDetachmentList != null)
            {
                for (int i = RecruitedTroopDetachmentList.Count - 1; i >= 0; i--)
                {
                    var detachment = RecruitedTroopDetachmentList[i];
                    detachment.Tick();

                    if (detachment.IsLastCycle() && _settlement.OwnerClan == Clan.PlayerClan)
                    {
                        TextObject textObject = new TextObject("{=FiefTroopsServiceEnding}The service period of {SETTLEMENT_NAME}'s feudal troops is about to end!", null);
                        textObject.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
                        MBInformationManager.AddQuickInformation(textObject, 5000, null, null, "");
                    }
                    else if (detachment.IsReadyToReturn())
                    {
                        foreach (var kvp in detachment.Troops)
                        {
                            var type = SoldierTypeClassifier.GetSoldierType(kvp.Key);
                            _soldierTypeCounts[type] = Math.Max(0, _soldierTypeCounts[type] - kvp.Value);
                        }
                        // 移除该分遣队
                        RecruitedTroopDetachmentList.RemoveAt(i);
                    }
                }
            }

            // 检查是否已达总兵力上限
            _totalTroopCount = _soldierTypeCounts.Values.Sum();
            if (_totalTroopCount >= _totalLimit)
                return;

            // 计算本周最多可补充人数
            int tmpWeeklyUpdateCount = Math.Min(GetWeeklyUpdateCount(), _totalLimit - _totalTroopCount);
            if (tmpWeeklyUpdateCount <= 0)
                return;

            PerformReinforcement(tmpWeeklyUpdateCount);
        }
    }
}
