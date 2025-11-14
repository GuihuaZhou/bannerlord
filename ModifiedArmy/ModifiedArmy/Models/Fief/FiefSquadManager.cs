using ModifiedArmy.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// 定义一个采邑小队的组成：1个贵族兵，3个职业军，6个征召兵。
    /// 使用 List<CharacterObject> 来分别存储这三类兵种。
    /// </summary>
    public class FiefSquad
    {
        public List<CharacterObject> NobleTroops { get; private set; }
        public List<CharacterObject> ProfessionalTroops { get; private set; }
        public List<CharacterObject> LevyTroops { get; private set; }

        // 修正：判断小队是否按 1-3-6 满员
        public bool IsFilled => NobleTroops.Count == 1 && ProfessionalTroops.Count == 3 && LevyTroops.Count == 6;

        public FiefSquad()
        {
            NobleTroops = new List<CharacterObject>();
            ProfessionalTroops = new List<CharacterObject>();
            LevyTroops = new List<CharacterObject>();
        }

        public FiefSquad(CharacterObject noble, List<CharacterObject> professionals, List<CharacterObject> levies)
        {
            // 确保数量符合 1-3-6 的结构
            if (professionals.Count != 3 || levies.Count != 6)
            {
                throw new ArgumentException("ProfessionalTroops must have 3 members, LevyTroops must have 6 members.");
            }
            // 注意：根据您的纠正，CharacterObject 没有 IsTroop 属性，此处不再进行检查。
            // 假设传入的 CharacterObject 都是有效的 Troop 类型。
            NobleTroops = new List<CharacterObject> { noble };
            ProfessionalTroops = professionals;
            LevyTroops = levies;
        }

        /// <summary>
        /// 获取小队总人数 (1 + 3 + 6 = 10)
        /// </summary>
        public int TotalTroopCount => NobleTroops.Count + ProfessionalTroops.Count + LevyTroops.Count;

        /// <summary>
        /// 填充小队。如果小队已满员，则直接返回。
        /// </summary>
        public void FillSquad(CharacterObject noble, List<CharacterObject> professionals, List<CharacterObject> levies)
        {
            // 修正：如果小队已满员，直接返回
            if (IsFilled)
            {
                InformationManager.DisplayMessage(new InformationMessage("Squad already filled, skipping FillSquad."));
                return;
            }
            if (professionals.Count != 3 || levies.Count != 6)
            {
                throw new ArgumentException("ProfessionalTroops must have 3 members, LevyTroops must have 6 members.");
            }
            NobleTroops.Add(noble);
            ProfessionalTroops.AddRange(professionals);
            LevyTroops.AddRange(levies);
        }

        /// <summary>
        /// 清空小队。
        /// </summary>
        public void ClearSquad()
        {
            NobleTroops.Clear();
            ProfessionalTroops.Clear();
            LevyTroops.Clear();
        }
    }


    /// <summary>
    /// 管理特定 Settlement 的采邑小队数据。
    /// </summary>
    public class FiefSettlementData
    {
        public Settlement Settlement { get; private set; }
        public List<FiefSquad> Squads { get; private set; }
        public int MaxSquadCount { get; private set; }

        public FiefSettlementData(Settlement settlement)
        {
            Settlement = settlement;
            Squads = new List<FiefSquad>();
            // 根据定居点类型设置最大小队数 (使用正确的属性)
            MaxSquadCount = settlement.IsVillage ? 2 : (settlement.IsCastle ? 5 : 10); // town

            // 初始化小队列表，确保有 MaxSquadCount 个空的 FiefSquad 对象
            for (int i = 0; i < MaxSquadCount; i++)
            {
                Squads.Add(new FiefSquad());
            }
        }

        /// <summary>
        /// 获取当前已填充的小队数量。
        /// </summary>
        public int CurrentFilledSquadCount => Squads.Count(s => s.IsFilled);

        /// <summary>
        /// 获取当前未填充的小队数量。
        /// </summary>
        public int CurrentEmptySquadCount => Squads.Count(s => !s.IsFilled);
    }

    /// <summary>
    /// 负责管理所有 Settlement 的采邑小队数据的 CampaignBehavior。
    /// </summary>
    public class FiefSquadManager : CampaignBehaviorBase
    {
        // 存储所有 Settlement 与对应 FiefSettlementData 的映射
        private Dictionary<Settlement, FiefSettlementData> _fiefDataMap = new Dictionary<Settlement, FiefSettlementData>();

        public override void RegisterEvents()
        {
            // 注册会话启动事件，用于初始化数据
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            // 注册每周事件
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // TODO: 实现存档/读档同步逻辑 (ISyncable)
            // 例如: dataStore.SyncData("_fiefDataMap", ref _fiefDataMap);
            // 这对于保存小队状态至关重要，但实现起来较为复杂，涉及 ISyncable 接口。
            // 为简化，此处不实现，这意味着存档/读档后数据会丢失或需要重新初始化。
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            // 初始化所有定居点的采邑数据
            InitializeFiefData();
        }

        private void InitializeFiefData()
        {
            _fiefDataMap.Clear(); // 清空旧数据（以防万一）

            int villageCount = 0, castleCount = 0, townCount = 0;

            // 遍历游戏世界中的所有定居点
            foreach (var settlement in Settlement.All)
            {
                // 使用正确的属性来判断定居点类型
                if (settlement.IsVillage || settlement.IsCastle || settlement.IsTown)
                {
                    // 为每个定居点创建 FiefSettlementData 实例
                    var fiefData = new FiefSettlementData(settlement);
                    _fiefDataMap[settlement] = fiefData;

                    // 统计定居点类型
                    if (settlement.IsVillage) villageCount++;
                    else if (settlement.IsCastle) castleCount++;
                    else if (settlement.IsTown) townCount++;

                    // Debug 输出，确认初始化
                    //InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] Initialized Fief data for {settlement.Name} (Type: {(settlement.IsVillage ? "Village" : (settlement.IsCastle ? "Castle" : "Town"))}, Max Squads: {fiefData.MaxSquadCount})"));
                }
            }

            // 输出初始化完成的统计信息
            InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] Initialization complete. Processed Villages: {villageCount}, Castles: {castleCount}, Towns: {townCount}."));
        }

        private void OnWeeklyTick()
        {
            InformationManager.DisplayMessage(new InformationMessage("[FiefSquadMod] Weekly tick event triggered. Starting squad processing."));

            // 遍历所有定居点的采邑数据
            foreach (var kvp in _fiefDataMap)
            {
                var settlement = kvp.Key;
                var fiefData = kvp.Value;

                // 2.1 遍历 FiefSettlementData 中的 FiefSquad
                foreach (var squad in fiefData.Squads)
                {
                    // 2.1.1 如果 Squad 已填充，则有一定概率升级
                    if (squad.IsFilled)
                    {
                        if (ShouldUpgradeSquad())
                        {
                            // 这里可以实现升级逻辑，例如增加单位等级或替换为高级单位
                            // 为了示例，我们只打印日志
                            InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] Squad in {settlement.Name} checked for upgrade, but upgrade logic not implemented yet."));
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] Squad in {settlement.Name} checked for upgrade, but did not upgrade."));
                        }
                    }
                    // 2.1.2 如果 Squad 没有填充，则有一定概率填充
                    else
                    {
                        if (ShouldFillSquad())
                        {
                            // 尝试填充小队
                            if (TryFillSquad(settlement, squad))
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] Squad in {settlement.Name} filled."));
                            }
                            else
                            {
                                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] Squad in {settlement.Name} failed to fill (insufficient volunteers or other reasons)."));
                            }
                        }
                        else
                        {
                            InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] Squad in {settlement.Name} checked for filling, but did not fill."));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 封装升级概率判断函数 (50%)
        /// </summary>
        /// <returns>True 表示应该升级，False 表示不应该升级。</returns>
        private bool ShouldUpgradeSquad()
        {
            //return MBRandom.RandomFloat <= 0.5f;
            return true;
        }

        /// <summary>
        /// 封装填充概率判断函数 (50%)
        /// </summary>
        /// <returns>True 表示应该填充，False 表示不应该填充。</returns>
        private bool ShouldFillSquad()
        {
            return true;
        }

        /// <summary>
        /// 尝试为指定的 Squad 填充单位。
        /// </summary>
        /// <param name="settlement">目标定居点。</param>
        /// <param name="squad">目标小队。</param>
        /// <returns>如果成功填充则返回 true，否则返回 false。</returns>
        private bool TryFillSquad(Settlement settlement, FiefSquad squad)
        {
            // 获取该定居点的 Culture 对应的志愿兵候选缓存
            var volunteerCandidates = CultureVolunteerGroupsCache.Instance.GetVolunteerCandidateCache(settlement.Culture);
            if (volunteerCandidates == null || volunteerCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No volunteer candidates found for {settlement.Name} (Culture: {settlement.Culture.Name})."));
                return false;
            }

            // 从候选池中筛选出对应类型的单位
            // 注意：这里需要确保 TroopType 枚举在当前命名空间下是可访问的。
            // 如果编译报错找不到 TroopType，请检查 VolunteerPatches.cs 是否已编译且包含该枚举定义。
            var nobleCandidates = volunteerCandidates.Where(c => c.Type == TroopType.EliteBasic).ToList();
            var professionalCandidates = volunteerCandidates.Where(c => c.Type == TroopType.Professional).ToList();
            var levyCandidates = volunteerCandidates.Where(c => c.Type == TroopType.Basic).ToList();

            // 检查是否有对应类型的单位存在 (即使只有一种单位)
            if (nobleCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No noble candidates found for {settlement.Name}."));
                return false; // 没有贵族兵类型，无法组成小队
            }
            if (professionalCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No professional candidates found for {settlement.Name}."));
                return false; // 没有职业军类型，无法组成小队
            }
            if (levyCandidates.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[FiefSquadMod] No levy candidates found for {settlement.Name}."));
                return false; // 没有征召兵类型，无法组成小队
            }

            // 随机选择单位 (使用加权随机选择)
            // 贵族兵 (随机选一个，从 nobleCandidates 中)
            var noble = WeightedRandomSelectFromCharacterWeightPairs(nobleCandidates);
            if (noble == null) return false; // 如果选择失败

            // 职业军 (随机选三个，从 professionalCandidates 中，允许重复选择)
            var professionals = new List<CharacterObject>();
            for (int i = 0; i < 3; i++)
            {
                var pro = WeightedRandomSelectFromCharacterWeightPairs(professionalCandidates);
                if (pro == null) return false; // 如果选择失败
                professionals.Add(pro);
            }

            // 征召兵 (随机选六个，从 levyCandidates 中，允许重复选择)
            var levies = new List<CharacterObject>();
            for (int i = 0; i < 6; i++)
            {
                var levy = WeightedRandomSelectFromCharacterWeightPairs(levyCandidates);
                if (levy == null) return false; // 如果选择失败
                levies.Add(levy);
            }

            // 填充小队
            squad.FillSquad(noble, professionals, levies);
            return true;
        }

        /// <summary>
        /// 从 CharacterWeightPair 列表中根据权重随机选择一个 CharacterObject。
        /// </summary>
        /// <param name="candidates">候选列表。</param>
        /// <returns>选中的 CharacterObject，如果列表为空或选择失败则返回 null。</returns>
        private CharacterObject WeightedRandomSelectFromCharacterWeightPairs(List<CharacterWeightPair> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            // 计算总权重
            float totalWeight = candidates.Sum(c => c.Weight);
            if (totalWeight <= 0)
            {
                // 如果总权重 <= 0，返回列表中的第一个单位，或者 null
                return candidates[0].Character; // 或者 return null;
            }

            // 生成随机数
            float rand = MBRandom.RandomFloat * totalWeight;
            float sum = 0;

            // 遍历列表，根据权重选择
            foreach (var candidate in candidates)
            {
                sum += candidate.Weight;
                if (rand < sum)
                {
                    return candidate.Character;
                }
            }

            // 理论上不应该到达这里，但如果到达了，返回最后一个单位
            return candidates[candidates.Count - 1].Character;
        }


        /// <summary>
        /// 获取指定 Settlement 的 FiefSettlementData。
        /// </summary>
        /// <param name="settlement">目标 Settlement。</param>
        /// <returns>对应的 FiefSettlementData，如果不存在则返回 null。</returns>
        public FiefSettlementData GetFiefData(Settlement settlement)
        {
            return _fiefDataMap.TryGetValue(settlement, out var data) ? data : null;
        }

        /// <summary>
        /// 获取所有定居点的采邑数据。
        /// </summary>
        /// <returns>包含所有 FiefSettlementData 的字典。</returns>
        public Dictionary<Settlement, FiefSettlementData> GetAllFiefData()
        {
            return _fiefDataMap;
        }

        /// <summary>
        /// 获取指定定居点的所有采邑士兵（展开为单个 CharacterObject 列表）。
        /// </summary>
        /// <param name="settlement">目标定居点</param>
        /// <returns>包含所有采邑士兵的列表；若无数据则返回空列表</returns>
        public List<CharacterObject> GetAllFiefTroopsForSettlement(Settlement settlement)
        {
            if (settlement == null)
                return new List<CharacterObject>();

            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData))
                return new List<CharacterObject>();

            var allTroops = new List<CharacterObject>();
            foreach (var squad in fiefData.Squads)
            {
                if (squad.IsFilled) // 只收集已填充的小队（也可改为收集所有非空，按需调整）
                {
                    allTroops.AddRange(squad.NobleTroops);
                    allTroops.AddRange(squad.ProfessionalTroops);
                    allTroops.AddRange(squad.LevyTroops);
                }
            }

            return allTroops;
        }
    }
}
