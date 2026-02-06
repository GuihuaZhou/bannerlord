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
    /// <summary>
    /// 表示封邑部队中三类兵种的数量统计结果。
    /// </summary>
    public readonly struct FiefTroopCounts
    {
        /// <summary>
        /// 采邑部队总人数。
        /// </summary>
        public readonly int Total;

        /// <summary>
        /// 扈从（Retinue）数量。
        /// </summary>
        public readonly int Retinue;

        /// <summary>
        /// 军士（Sergeant）数量。
        /// </summary>
        public readonly int Sergeant;

        /// <summary>
        /// 民兵（Militia）数量。
        /// </summary>
        public readonly int Militia;

        /// <summary>
        /// 初始化一个新的 <see cref="FiefTroopCounts"/> 实例。
        /// </summary>
        /// <param name="retinue">扈从数量。</param>
        /// <param name="sergeant">军士数量。</param>
        /// <param name="militia">民兵数量。</param>
        public FiefTroopCounts(int retinue, int sergeant, int militia)
        {
            Retinue = retinue;
            Sergeant = sergeant;
            Militia = militia;
            Total = retinue + sergeant + militia;
        }
    }

    /// <summary>
    /// 表示一个封邑部队分遣队。
    /// 可用于：
    /// - 征召状态的分遣队（WaitCycle = -1，表示永久在外服役，不会自动回归）
    /// - 遣返状态的分遣队（WaitCycle > 0，表示正在等待回归封邑，倒计时结束后可重新征召）
    /// </summary>
    /// 
    [SaveableRootClass(1)]
    public class FiefTroopDetachment
    {
        /// <summary>
        /// 分遣队中的部队组成：CharacterObject -> 数量
        /// </summary>
        [SaveableProperty(1)]
        public Dictionary<CharacterObject, int> Troops { get; private set; }

        /// <summary>
        /// 等待周期（倒计时）：
        /// - >0：还需等待若干周才能回归封邑
        /// - <=0：已满足回归条件（仅对遣返分遣队有效）
        /// - -1：特殊值，表示该分遣队处于征召状态（如在玩家军队中）
        /// </summary>
        [SaveableProperty(2)]
        public int WaitCycle { get; set; }

        /// <summary>
        /// 构造函数：创建一个新的封邑部队分遣队，并设置初始等待周期
        /// </summary>
        /// <param name="initialWaitCycle">
        /// 初始等待周期。
        /// - 若用于遣返分遣队，传入正整数（如 2 表示 2 周后回归）
        /// - 若用于征召分遣队，应传入 -1
        /// </param>
        public FiefTroopDetachment() { }
        public FiefTroopDetachment(int initialWaitCycle)
        {
            WaitCycle = initialWaitCycle;
            Troops = new Dictionary<CharacterObject, int>();
        }

        /// <summary>
        /// 向当前分遣队中添加部队
        /// </summary>
        /// <param name="newTroops">要添加的部队字典</param>
        public void AddTroops(Dictionary<CharacterObject, int> newTroops)
        {
            if (newTroops == null || newTroops.Count == 0)
            {
                ModLogger.Debug("[AddTroops] Input is null or empty. Skipping.");
                return;
            }

            foreach (var kvp in newTroops)
            {
                var troop = kvp.Key;
                var count = kvp.Value;

                if (count <= 0)
                {
                    continue;
                }

                if (Troops.ContainsKey(troop))
                {
                    Troops[troop] += count;
                }
                else
                {
                    Troops[troop] = count;
                }
            }
        }

        /// <summary>
        /// 获取该分遣队的总人数
        /// </summary>
        /// <returns>总人数</returns>
        public int GetTotalCount()
        {
            int total = 0;
            foreach (var count in Troops.Values)
            {
                total += count;
            }
            return total;
        }

        /// <summary>
        /// 判断该分遣队是否为空（无有效部队）
        /// </summary>
        public bool IsEmpty()
        {
            return Troops.Count == 0 || GetTotalCount() == 0;
        }

        /// <summary>
        /// 每周调用一次：若等待周期大于 0，则减 1
        /// </summary>
        public void Tick()
        {
            if (WaitCycle > 0)
            {
                WaitCycle--;
            }
        }

        /// <summary>
        /// 判断该分遣队是否已准备好回归封邑
        /// 条件：等待周期 ≤ 0 且分遣队非空
        /// </summary>
        public bool IsReadyToReturn()
        {
            return WaitCycle <= 0 && !IsEmpty();
        }


        /// <summary>
        /// 判断是否为最后一个周期
        /// 
        /// </summary>
        /// 
        public bool IsLastCycle()
        {
            if (WaitCycle == 1)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 清空分遣队中的所有部队。
        /// </summary>
        public void Clear()
        {
            Troops.Clear();
        }

        /// <summary>
        /// 返回该分遣队的简要描述（用于调试）
        /// </summary>
        public override string ToString()
        {
            if (IsEmpty())
                return "[Empty]";

            return $"[WaitCycle={WaitCycle}, Total={GetTotalCount()}, Types={Troops.Count}]";
        }
    }

    [SaveableRootClass(3)]
    public class FiefPartyData
    {
        [SaveableField(1)] 
        private Settlement _settlement;
        /// <summary>
        /// 封邑就绪军队容器
        /// </summary>
        public TroopRoster _fiefParty { get; private set; }
        /// <summary>
        /// 已被征召的封邑军队
        /// </summary>
        [SaveableProperty(1)] 
        public List<FiefTroopDetachment> RecruitedTroopDetachmentList { get; private set; }
        /// <summary>
        /// 处于解散期的封邑军队
        /// </summary>
        [SaveableProperty(2)] 
        public List<FiefTroopDetachment> ReturnedTroopDetachmentList { get; private set; }

        [SaveableProperty(3)] 
        public int RetinueCount { get; private set; } = 0;
        [SaveableProperty(4)] 
        public int SergeantCount { get; private set; } = 0;
        [SaveableProperty(5)] 
        public int MilitiaCount { get; private set; } = 0;

        /// <summary>
        /// 封邑士兵类型和实际数量
        /// <summary>
        public Dictionary<SoldierType, int> _soldierTypeCounts { get; private set; }

        /// <summary>
        /// 当前封邑使用的部队模板
        /// </summary>
        private FiefPartyTemplate _fiefPartyTemplate;

        /// <summary>
        /// 封邑士兵类型和最大数量
        /// <summary>
        public Dictionary<SoldierType, int> _soldierTypeMaxCounts { get; private set; }

        /// <summary>
        /// 封邑士兵类型和权重
        /// <summary>
        public Dictionary<SoldierType, int> _soldierTypeWeights { get; private set; }

        /// <summary>
        /// 封邑士兵总权重
        /// <summary>
        private int _totalWeight;

        /// <summary>
        /// 封邑士兵总人数
        /// <summary>
        public int _totalTroopCount { get; private set; }

        /// <summary>
        /// 封邑军队最大人数限制
        /// </summary>
        public int _totalLimit { get; private set; }


        public FiefPartyData() { }
        public FiefPartyData(Settlement settlement)
        {
            _settlement = settlement;
            InitialFeifPartyData();
            InitialFeifParty();
        }

        /// <summary>
        /// 获取采邑部队的最大数量。
        /// </summary>
        /// 
        public int GetFiefTroopLimit() => _totalLimit;

        /// <summary>
        /// 获取封邑士兵的总数（包括就绪、征召、冷却）
        /// </summary>
        public Dictionary<SoldierType, int> GetTroopCounts() => _soldierTypeCounts;

        /// <summary>
        /// 获取封邑士兵的最大数量
        /// </summary>
        public Dictionary<SoldierType, int> GetMaxTroopCounts() => _soldierTypeMaxCounts;


        /// <summary>
        /// 从给定的部队名单中统计采邑士兵的实际数量（含健康兵与伤员）。
        /// </summary>
        public void AnalyzeCurrentManpower(
            TroopRoster roster, 
            Dictionary<SoldierType, int> tmpSoldierTypeCounts)
        {
            if (roster == null)
                return;

            foreach (var element in roster.GetTroopRoster())
            {
                var troop = element.Character;
                var count = element.Number + element.WoundedNumber; // 健康 + 伤员
                if (troop == null || count <= 0)
                    continue;

                if (!_fiefPartyTemplate.IsEnableTroop(troop))
                    continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                if (tmpSoldierTypeCounts.ContainsKey(type))
                    tmpSoldierTypeCounts[type] += count;
                else
                    tmpSoldierTypeCounts[type] = count;
            }
        }

        /// <summary>
        /// 将指定分遣队列表中的兵力按兵种类型累加到给定的计数器。
        /// </summary>
        /// <param name="detachments">分遣队列表，可为 null</param>
        /// 
        public void AccumulateDetachments(
            List<FiefTroopDetachment> detachments,
            Dictionary<SoldierType, int> tmpSoldierTypeCounts)
        {
            if (detachments != null)
            {
                foreach (var detachment in detachments)
                {
                    if (detachment?.IsEmpty() == false)
                    {
                        foreach (var kvp in detachment.Troops)
                        {
                            var troop = kvp.Key;
                            int count = kvp.Value;
                            if (troop == null || count <= 0) continue;

                            var type = SoldierTypeClassifier.GetSoldierType(troop);
                            if (tmpSoldierTypeCounts.ContainsKey(type))
                                tmpSoldierTypeCounts[type] += count;
                            else
                                tmpSoldierTypeCounts[type] = count;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取就绪的封邑士兵数量
        /// </summary>
        public int GetReadyFiefTroopCount()
        {
            Dictionary<SoldierType, int> tmpSoldierTypeCounts = new();
            AnalyzeCurrentManpower(_fiefParty, tmpSoldierTypeCounts);
            return tmpSoldierTypeCounts.Values.Sum();
        }

        /// <summary>
        /// 获取征召的封邑士兵数量
        /// </summary>
        public int GetRecruitedFiefTroopCount()
        {
            Dictionary<SoldierType, int> tmpSoldierTypeCounts = new();
            AccumulateDetachments(RecruitedTroopDetachmentList, tmpSoldierTypeCounts);
            return tmpSoldierTypeCounts.Values.Sum();
        }

        /// <summary>
        /// 从 _fiefParty 同步当前三类兵种的实际数量到计数器。
        /// 此方法应在初始化、加载存档或部队变动后调用。
        /// </summary>
        private void SyncManpowerCounters()
        {
            foreach (SoldierType type in Enum.GetValues(typeof(SoldierType)))
            {
                _soldierTypeCounts[type] = 0;
            }

            // 1. 就绪部队
            AnalyzeCurrentManpower(_fiefParty, _soldierTypeCounts);
            // 2. 征召中部队
            AccumulateDetachments(RecruitedTroopDetachmentList, _soldierTypeCounts);

            // 3. 归还冷却中部队
            AccumulateDetachments(ReturnedTroopDetachmentList, _soldierTypeCounts);

            _totalTroopCount = _soldierTypeCounts.Values.Sum();
        }


        /// <summary>
        /// 计算指定定居点的采邑部队最大兵力上限（Total Limit）。
        /// 上限基于定居点类型、附属村庄数量、繁荣度等因素综合确定。
        /// </summary>
        /// <returns>该定居点可维持的采邑部队最大人数；若输入无效则返回 0。</returns>
        public int CalculateSizeLimit()
        {
            if (_settlement == null || _fiefPartyTemplate == null)
                return 0;

            int boundVillageCount = _settlement.BoundVillages?.Count ?? 0;
            return _fiefPartyTemplate.BaseLimit + (boundVillageCount * _fiefPartyTemplate.VillageBonus);
        }

        /// <summary>
        /// 重置计数器
        /// </summary>
        private void ResetManpowerCounters()
        {
            RetinueCount = SergeantCount = MilitiaCount = 0;
        }


        /// <summary>
        /// 初始化封邑部队，最多补充1/2满编
        /// <summary>
        /// 
        private void InitialFeifParty()
        {
            int deficit = _totalLimit - _totalTroopCount;
            if (deficit <= 0)
                return;

            float multiplier = CalculateReinforcementMultiplier();
            int count = (int)(deficit * 0.5f * multiplier);

            if (count <= 0)
                return;

            // 执行补员
            PerformReinforcement(count);
        }

        /// <summary>
        /// 围攻结束后调用，用于同步当前兵力并处理定居点陷落逻辑。
        /// </summary>
        /// <param name="settlementWasCaptured">是否被攻击方攻占成功（即原领主失去控制权）</param>
        /// 
        public void OnSiegeCompleted(bool settlementWasCaptured)
        {
            if (settlementWasCaptured)
            {
                RecruitedTroopDetachmentList.Clear();
                ModLogger.Debug($"Settlement {_settlement.Name} captured - cleared all recruited troop detachments.");

                // 立即补充一部分士兵
                _totalTroopCount = 0;
                InitialFeifParty();

                // 清除驻军的士气惩罚
                if (_settlement.Town != null && _settlement.Town.GarrisonParty != null)
                {
                    _settlement.Town.GarrisonParty.RecentEventsMorale = 0f;
                }
            }

            SyncManpowerCounters();
        }

        public void OnSettlementOwnerChanged()
        {
            RecruitedTroopDetachmentList.Clear();
            SyncManpowerCounters();
        }


        /// <summary>
        /// 更新定居点繁荣度和户数
        /// </summary>
        /// 
        private void updateProsperity(float prosperityCost, int hearthCost, bool isAdd)
        {
            // 分配繁荣度
            if (isAdd)
                _settlement.Town.Prosperity = Math.Max(0f, _settlement.Town.Prosperity + prosperityCost);
            else
            {
                if (_settlement.IsCastle)
                    _settlement.Town.Prosperity = Math.Max(CommonConstants.CASTLE_POOR_THRESHOLD, _settlement.Town.Prosperity - prosperityCost);
                else if (_settlement.IsTown)
                    _settlement.Town.Prosperity = Math.Max(CommonConstants.TOWN_POOR_THRESHOLD, _settlement.Town.Prosperity - prosperityCost);
            }

            // 分配户数

            if (hearthCost <= 0)
                return;

            int villageCount = _settlement.BoundVillages.Count;
            int sign = isAdd ? 1 : -1;
            int totalChange = hearthCost * sign;

            // 均匀分配：基础值 + 余数
            int baseChange = totalChange / villageCount;
            int remainder = totalChange % villageCount;

            // 修正负余数
            if (remainder < 0)
            {
                baseChange--;
                remainder += villageCount;
            }

            // === 应用基础分配 ===
            for (int i = 0; i < villageCount; i++)
            {
                Village village = _settlement.BoundVillages[i];
                float newHearth = village.Hearth + baseChange;

                if (!isAdd)
                {
                    newHearth = Math.Max(Settings.Instance.VillageMinHearthThreshold, newHearth);
                }

                village.Hearth = newHearth;
            }

            // === 分配余数
            for (int i = 0; i < remainder; i++)
            {
                Village village = _settlement.BoundVillages[i];
                float newHearth = village.Hearth + 1f;

                if (!isAdd)
                {
                    newHearth = Math.Max(Settings.Instance.VillageMinHearthThreshold, newHearth);
                }

                village.Hearth = newHearth;
            }
        }

        private void CalculateLimit()
        {
            if (_totalLimit == 0)
                _totalLimit = CalculateSizeLimit();

            _soldierTypeWeights = _fiefPartyTemplate.GetSoldierTypeWeights();
            _totalWeight = _soldierTypeWeights.Values.Sum();
            if (_totalWeight <= 0)
            {
                _totalWeight = 1; // 避免除零
                ModLogger.Warn($"[FiefSettlementData] Total weight is zero for '{_settlement.Name}', clamped to 1.");
                return;
            }

            _soldierTypeMaxCounts ??= new Dictionary<SoldierType, int>();
            _soldierTypeCounts ??= new Dictionary<SoldierType, int>();
            foreach (SoldierType type in Enum.GetValues(typeof(SoldierType)))
            {
                _soldierTypeMaxCounts[type] = 0;
                _soldierTypeCounts[type] = 0;
            }

            // 按权重分配上限
            foreach (var kvp in _soldierTypeWeights)
            {
                if (kvp.Value <= 0) continue;
                int max = (int)MathF.Floor((_totalLimit * kvp.Value) / (float)_totalWeight);
                _soldierTypeMaxCounts[kvp.Key] = Math.Max(0, max);
            }

            // 日志
            var nonZero = _soldierTypeMaxCounts
                .Where(kvp => kvp.Value > 0)
                .Select(kvp => $"{kvp.Key}≤{kvp.Value}")
                .ToArray();

            string detail = nonZero.Length > 0 ? string.Join(", ", nonZero) : "no limits";
            ModLogger.Debug($"[FiefSettlementData] Limits for '{_settlement.Name}': {detail}, Total≤{_totalLimit}");
        }


        /// <summary>
        /// 根据定居点解析对应的 FiefPartyTemplate ID 并获取模板。
        /// 命名规则：{culture}_{town|castle}{_port?}
        /// </summary>
        private FiefPartyTemplate ResolveFiefPartyTemplate(Settlement settlement)
        {
            if (settlement == null || settlement.Culture == null)
                return null;

            string culture = settlement.Culture.StringId;
            string type = settlement.IsCastle ? "castle" : "town";
            string portSuffix = "";

            if (settlement.IsTown && settlement.HasPort)
            {
                portSuffix = "_port";
            }

            string templateId = $"{culture}_{type}{portSuffix}";
            ModLogger.Debug($"[Fief] Trying template ID: '{templateId}' for {settlement.Name}");
            var template = FiefPartyTemplateManager.Instance.GetTemplate(templateId);

            if (template == null && !string.IsNullOrEmpty(portSuffix))
            {
                templateId = $"{culture}_{type}";
                ModLogger.Debug($"[Fief] Trying template ID: '{templateId}' for {settlement.Name} again");
                template = FiefPartyTemplateManager.Instance.GetTemplate(templateId);
            }

            return template;
        }

        public bool InitialFeifPartyData()
        {
            if (_settlement == null)
            {
                ModLogger.Error("[InitialFeifPartyData] _settlement is NULL! Skipping initialization.");
                _totalLimit = 0;
                return false;
            }

            if (ReturnedTroopDetachmentList == null)
                ReturnedTroopDetachmentList = new List<FiefTroopDetachment>();

            if (RecruitedTroopDetachmentList == null)
                RecruitedTroopDetachmentList = new List<FiefTroopDetachment>();

            var group = BasicTroopGroupManager.GetGroupForCulture(_settlement.Culture);
            if (group == null)
            {
                ModLogger.Warn($"No baisc troop for {_settlement.Culture.Name}");
                return false;
            }

            if (_fiefPartyTemplate == null)
            {
                _fiefPartyTemplate = ResolveFiefPartyTemplate(_settlement);
            }

            if (_fiefPartyTemplate == null)
            {
                ModLogger.Warn($"[FiefPartyData] No template found for settlement '{_settlement.Name}'. Recruitment disabled.");
                return false;
            }

            CalculateLimit();

            if (_fiefParty == null)
            {
                if (_settlement.MilitiaPartyComponent != null
                    && _settlement.MilitiaPartyComponent.MobileParty.IsActive)
                {
                    _fiefParty = _settlement.MilitiaPartyComponent.MobileParty.MemberRoster;
                }
                else
                {
                    ResetManpowerCounters();
                    return false;
                }
            }

            SyncManpowerCounters();
            return true;
        }


        /// <summary>
        /// 获取当前定居点的兵营等级。
        /// 城镇使用 SettlementBarracks，城堡使用 CastleBarracks。
        /// </summary>
        private int GetCurrentBarracksLevel()
        {
            if (_settlement?.Town == null) 
                return 0;

            var town = _settlement.Town;
            var barracksType = _settlement.IsCastle 
                ? DefaultBuildingTypes.CastleBarracks 
                : DefaultBuildingTypes.SettlementBarracks;

            foreach (var building in town.Buildings)
            {
                if (building.BuildingType == barracksType)
                {
                    return building.CurrentLevel;
                }
            }
            return 0; // 未找到兵营，等级为0
        }


        private CharacterObject WeightedRandomSelectFromBasicTroopEntries(List<BasicTroopEntry> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            int tmpTotalWeight = candidates.Sum(c => c.Weight);
            if (tmpTotalWeight <= 0)
                return candidates[0].Troop;

            int rand = MBRandom.RandomInt(tmpTotalWeight); // 使用整数随机更高效且避免浮点误差
            int sum = 0;

            int currentBarracksLevel = GetCurrentBarracksLevel();
            foreach (var candidate in candidates)
            {
                if (candidate.RequiredBarracksLevel > currentBarracksLevel)
                    continue;

                sum += candidate.Weight;
                if (rand < sum)
                    return candidate.Troop;
            }

            return null;
        }

        // 根据模板生成新troops
        private Dictionary<CharacterObject, int> 
        GenerateNewTroops(Dictionary<SoldierType, int> tmpSoldierTypeSize)
        {
            // 获取基础troops
            Dictionary<SoldierType, List<BasicTroopEntry>> tmpBasicTroops = _fiefPartyTemplate.GetBasicTroops();

            Dictionary<CharacterObject, int> tmpNewTroops = new();
            // 遍历生成新的basic troops
            foreach (var kvp in tmpSoldierTypeSize)
            {
                for (int i = 0; i < kvp.Value; i++)
                {
                    var troop = WeightedRandomSelectFromBasicTroopEntries(tmpBasicTroops[kvp.Key]);
                    if (troop != null)
                    {
                        if (tmpNewTroops.ContainsKey(troop))
                            tmpNewTroops[troop]++;
                        else
                            tmpNewTroops[troop] = 1;
                    }
                }
            }

            return tmpNewTroops;
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

                var count = element.Number + element.WoundedNumber;
                if (troop == null || count <= 0)
                    continue;

                if (element.Number >= 0)
                    fiefRoster.AddToCounts(troop, element.Number);
                if (element.WoundedNumber >= 0)
                    fiefRoster.AddToCounts(troop, 0, false, element.WoundedNumber);
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

            // 生成新troops
            var tmpNewTroops = GenerateNewTroops(tmpSoldierTypeSize);

            // 批量添加
            foreach (var kvp in tmpNewTroops)
            {
                _fiefParty.AddToCounts(kvp.Key, kvp.Value, false, 0, 0, true, -1);

                var type = SoldierTypeClassifier.GetSoldierType(kvp.Key);
                _soldierTypeCounts[type] += kvp.Value;
            }

            _totalTroopCount = _soldierTypeCounts.Values.Sum();;

            TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_weekly_reinforcement");
            msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msg.SetTextVariable("RETINUE", tmpSoldierTypeSize[SoldierType.Retinue]);
            msg.SetTextVariable("SERGEANT", tmpSoldierTypeSize[SoldierType.Sergeant]);
            msg.SetTextVariable("MILITIA", tmpSoldierTypeSize[SoldierType.Militia]);
            msg.SetTextVariable("RETINUE_COUNT", _soldierTypeCounts[SoldierType.Retinue]);
            msg.SetTextVariable("MAX_RETINUE", _soldierTypeMaxCounts[SoldierType.Retinue]);
            msg.SetTextVariable("SERGEANT_COUNT", _soldierTypeCounts[SoldierType.Sergeant]);
            msg.SetTextVariable("MAX_SERGEANT", _soldierTypeMaxCounts[SoldierType.Sergeant]);
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
            float hearthRatio = MathF.Min(1f, totalHearth / (Settings.Instance.VillageMaxReinforcementHearthThreshold * villageCount)); 

            // === 3. 计算加权综合比例 ===
            float prosperityWeight = Settings.Instance.ProsperityWeight; // 繁荣度权重
            float hearthWeight = Settings.Instance.HearthsWeight;    // 村庄户数权重
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
            var rosterSnapshot = _fiefParty.GetTroopRoster().ToList();
            foreach (var element in rosterSnapshot)
            {
                var troop = element.Character;
                int count = element.Number; 

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

                for (int i = 0; i < count; i++)
                {
                    if (MBRandom.RandomFloat < upgradeChance)
                    {
                        _fiefParty.RemoveTroop(troop, 1, default(UniqueTroopDescriptor), 0);

                        CharacterObject newTroop = WeightedRandomChoice(validTargets);
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


        /// <summary>
        /// 玩家将部队中的采邑士兵（含伤员）归还至封邑军队。
        /// - 所有归还士兵（健康+伤员）均作为健康兵加入 FiefTroops（通过冷却分遣队）
        /// - 使用 RemoveTroop 自动处理健康/伤员混合移除
        /// - 仅处理 Occupation.Soldier
        /// - 先清空 RecruitedTroopDetachmentList
        /// - 按兵种剩余容量归还（不再循环权重）
        /// </summary>
        public int ReturnTroopsToSettlement(MobileParty sourceParty)
        {
            if (sourceParty == null || _fiefParty == null)
                return 0;

            // 清空 RecruitedTroopDetachmentList 并扣减计数器
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

                            var type = SoldierTypeClassifier.GetSoldierType(troop);
                            _soldierTypeCounts[type] = Math.Max(0, _soldierTypeCounts[type] - count);
                        }
                        detachment.Clear();
                    }
                }
                RecruitedTroopDetachmentList.Clear();
            }

            // 记录归还的封邑士兵
            Dictionary<CharacterObject, int> tmpReturnTroops = new();
            // 返还的繁荣度
            int prosperityCost = 0;
            // 返还的户数
            int hearthCost = 0;

            int tmpProsperityCostPerTier = 0;

            if (_settlement.IsTown)
                tmpProsperityCostPerTier = Settings.Instance.TownProsperityCostPerTier;
            else if (_settlement.IsCastle)
                tmpProsperityCostPerTier = Settings.Instance.CastleProsperityCostPerTier;

            foreach (var element in sourceParty.MemberRoster.GetTroopRoster())
            {
                var troop = element.Character;
                if (troop == null || troop.Occupation != Occupation.Soldier)
                    continue;

                if (!_fiefPartyTemplate.IsEnableTroop(troop))
                    continue;

                int count = element.Number + element.WoundedNumber;
                if (count <= 0)
                    continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                // 检查剩余容量是否足够
                int taken = Math.Min(count, _soldierTypeMaxCounts[type] - _soldierTypeCounts[type]);
                if (taken > 0)
                {
                    // 从 sourceParty 移除士兵
                    RemoveTroopsFromParty(sourceParty.MemberRoster, troop, taken);
                    // 记录归还的士兵和数量
                    if (tmpReturnTroops.ContainsKey(troop))
                        tmpReturnTroops[troop] += taken;
                    else
                        tmpReturnTroops[troop] = taken;
                    // 修改计数器
                    _soldierTypeCounts[type] += taken;
                    
                    hearthCost += taken * Settings.Instance.VillageHearthCostPer;
                    prosperityCost += taken * tmpProsperityCostPerTier * troop.Tier;
                }
            }

            int tmpReturnTroopCount = tmpReturnTroops.Values.Sum();
            if (tmpReturnTroopCount <= 0)
            {
                ModLogger.Debug($"[Return] Returned {tmpReturnTroopCount} troops to fief '{_settlement?.Name}'. ");
                return 0;
            }
            updateProsperity(prosperityCost, hearthCost, true);

            // 添加到ReturnedTroopDetachmentList，记录处于冷却状态的士兵
            FiefTroopDetachment targetDetachment = ReturnedTroopDetachmentList
                .FirstOrDefault(d => d != null && d.WaitCycle == CommonConstants.RETURN_TROOP_WAIT_CYCLE);

            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(CommonConstants.RETURN_TROOP_WAIT_CYCLE); // WaitCycle = 2
                ReturnedTroopDetachmentList.Add(targetDetachment);
            }
            // 向目标分遣队添加归还的部队
            targetDetachment.AddTroops(tmpReturnTroops);

            // 更新计数器
            _totalTroopCount = _soldierTypeCounts.Values.Sum();

            // 清除工资减免
            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager.ConsumeExemption(sourceParty, tmpReturnTroopCount);

            TextObject msgResult = GameTexts.FindText("str_modifiedarmy_fief_return_result");
            msgResult.SetTextVariable("PARTY_NAME", sourceParty.Name.ToString());
            msgResult.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msgResult.SetTextVariable("RETURNED_COUNT", tmpReturnTroopCount);
            msgResult.SetTextVariable("RETINUE_COUNT", _soldierTypeCounts[SoldierType.Retinue]);
            msgResult.SetTextVariable("MAX_RETINUE", _soldierTypeMaxCounts[SoldierType.Retinue]);
            msgResult.SetTextVariable("SERGEANT_COUNT", _soldierTypeCounts[SoldierType.Sergeant]);
            msgResult.SetTextVariable("MAX_SERGEANT", _soldierTypeMaxCounts[SoldierType.Sergeant]);
            msgResult.SetTextVariable("MARINE_COUNT", _soldierTypeCounts[SoldierType.Marine]);
            msgResult.SetTextVariable("MAX_MARINE", _soldierTypeMaxCounts[SoldierType.Marine]);
            msgResult.SetTextVariable("SLAVE_COUNT", _soldierTypeCounts[SoldierType.Slave]);
            msgResult.SetTextVariable("MAX_SLAVE", _soldierTypeMaxCounts[SoldierType.Slave]);
            msgResult.SetTextVariable("MILITIA_COUNT", _soldierTypeCounts[SoldierType.Militia]);
            msgResult.SetTextVariable("MAX_MILITIA", _soldierTypeMaxCounts[SoldierType.Militia]);
            msgResult.SetTextVariable("TROOP_COUNT", _totalTroopCount);
            msgResult.SetTextVariable("TOTAL_LIMIT", _totalLimit);
            msgResult.SetTextVariable("PROSPERITY_COST", prosperityCost);
            msgResult.SetTextVariable("HEARTH_COST", hearthCost);

            if (sourceParty.LeaderHero.Clan == Clan.PlayerClan)
                ModLogger.Notice(msgResult.ToString());
            else
                ModLogger.Debug(msgResult.ToString());

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
        /// 从封邑军队中按比例招募士兵到目标部队。
        /// - 从 _fiefParty 移除已征召士兵（因为他们已离营）
        /// - 添加到 RecruitedTroops（标记为已征召）
        /// - 不修改 RetinueCount/SergeantCount/MilitiaCount（兵力仍属封邑）
        /// </summary>
        /// <param name="targetParty">目标部队</param>
        /// <returns>总招募人数（必为 10 的倍数）</returns>
        public int RecruitTroopsToParty(MobileParty targetParty)
        {
            if (targetParty == null || _fiefParty == null)
                return 0;

            if (_settlement.OwnerClan != targetParty.LeaderHero.Clan)
                return 0;

            int currentMembers = targetParty.Party.NumberOfAllMembers;
            int partySizeLimit = targetParty.Party.PartySizeLimit;
            int remainSize = partySizeLimit - currentMembers;
            // 目标party没有空间，则停止招募
            if (remainSize <= 0)
                return 0;

            if (_soldierTypeWeights == null || _totalWeight <= 0)
            {
                ModLogger.Error($"[Recruit] CRITICAL: _soldierTypeWeights is null/empty or _totalWeight={_totalWeight}");
                return 0;
            }

            // 士兵类型及可招募的数量
            Dictionary<SoldierType, int> tmpSoldierTypeSize = new();
            // 记录招募的士兵类型及数量
            Dictionary<SoldierType, int> tmpRecruitSoldierTypeSize = new();
            // 按权重分配剩余空间
            foreach (var kvp in _soldierTypeWeights)
            {
                tmpSoldierTypeSize[kvp.Key] = (remainSize * _soldierTypeWeights[kvp.Key]) / _totalWeight;
                tmpRecruitSoldierTypeSize[kvp.Key] = 0;
            }

            // 记录招募的士兵和数量
            Dictionary<CharacterObject, int> tmpRecruitTroops = new();

            // 消耗的繁荣度
            int prosperityCost = 0;
            // 消耗的户数
            int hearthCost = 0;

            int tmpProsperityCostPerTier = 0;

            if (_settlement.IsTown)
                tmpProsperityCostPerTier = Settings.Instance.TownProsperityCostPerTier;
            else if (_settlement.IsCastle)
                tmpProsperityCostPerTier = Settings.Instance.CastleProsperityCostPerTier;

            foreach (var element in _fiefParty.GetTroopRoster())
            {
                var troop = element.Character;
                var count = element.Number;
                if (troop == null || count <= 0) continue;

                if (!SoldierTypeClassifier.IsFiefTroop(troop))
                    continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                // 检查剩余容量是否足够
                int taken = Math.Min(count, tmpSoldierTypeSize[type]);
                if (taken > 0)
                {
                    // 从封邑party移除士兵
                    RemoveTroopsFromParty(_fiefParty, troop, taken);
                    // 向目标party添加士兵
                    targetParty.MemberRoster.AddToCounts(troop, taken, false, 0, 0, true, -1);
                    // 记录招募的士兵
                    if (tmpRecruitTroops.ContainsKey(troop))
                        tmpRecruitTroops[troop] += taken;
                    else
                        tmpRecruitTroops[troop] = taken;
                    // 更新计数器
                    tmpSoldierTypeSize[type] = Math.Min(0, tmpSoldierTypeSize[type] - taken);
                    tmpRecruitSoldierTypeSize[type] += taken;

                    hearthCost += taken * Settings.Instance.VillageHearthCostPer;
                    prosperityCost += taken * tmpProsperityCostPerTier * troop.Tier;
                }
            }

            int totalRecruited = tmpRecruitTroops.Values.Sum();
            if (totalRecruited <= 0)
                return 0;

            updateProsperity(prosperityCost, hearthCost, false);

            // 记录招募的士兵
            FiefTroopDetachment targetDetachment = RecruitedTroopDetachmentList
                .FirstOrDefault(d => d != null && d.WaitCycle == CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE);

            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE);
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

            //if (_settlement.OwnerClan == Clan.PlayerClan)
            if (targetParty.LeaderHero.Clan == Clan.PlayerClan)
            {
                ModLogger.Notice(msg.ToString());
            }
            else
            {
                ModLogger.Info(msg.ToString());
            }

            // 给与工资减免
            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager.AddExemption(targetParty, totalRecruited, CommonConstants.FIEF_WAGE_EXEMPTION_DAYS);

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
            // 消耗的繁荣度
            int prosperityCost = 0;
            // 消耗的户数
            int hearthCost = 0;

            int tmpProsperityCostPerTier = 0;
            if (_settlement.IsTown)
                tmpProsperityCostPerTier = Settings.Instance.TownProsperityCostPerTier;
            else if (_settlement.IsCastle)
                tmpProsperityCostPerTier = Settings.Instance.CastleProsperityCostPerTier;

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

                hearthCost += count * Settings.Instance.VillageHearthCostPer;
                prosperityCost += count * tmpProsperityCostPerTier * troop.Tier;
            }

            int totalRecruited = tmpRecruitTroops.Values.Sum();
            if (totalRecruited <= 0)
                return 0;

            updateProsperity(prosperityCost, hearthCost, false);

            // 添加到 RecruitedTroopDetachmentList
            FiefTroopDetachment targetDetachment = RecruitedTroopDetachmentList
                .FirstOrDefault(d => d != null && d.WaitCycle == CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE);

            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(CommonConstants.FIEF_TROOP_MAX_SERVICE_CYCLE);
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
            _fiefWageExemptionManager.AddExemption(targetParty, totalRecruited, CommonConstants.FIEF_WAGE_EXEMPTION_DAYS);

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
            int prosperityCost = 0;
            // 消耗的户数
            int hearthCost = 0;

            int tmpProsperityCostPerTier = 0;
            if (_settlement.IsTown)
                tmpProsperityCostPerTier = Settings.Instance.TownProsperityCostPerTier;
            else if (_settlement.IsCastle)
                tmpProsperityCostPerTier = Settings.Instance.CastleProsperityCostPerTier;

            hearthCost += count * Settings.Instance.VillageHearthCostPer;
            prosperityCost += count * tmpProsperityCostPerTier * troop.Tier;

            updateProsperity(prosperityCost, hearthCost, true);

            // 4. 更新计数器
            _soldierTypeCounts[type] += count;
            _totalTroopCount = _soldierTypeCounts.Values.Sum();

            ModLogger.Notice($"{_settlement.Name}的封邑部队从俘虏中招募了{count}名{troop.Name}");

            return true;
        }
    }
}
