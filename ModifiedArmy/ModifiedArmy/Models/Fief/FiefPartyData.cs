using HarmonyLib;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.SaveSystem;

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
    /// 提供采邑部队容量相关的计算与分析功能。
    /// 包括：基于定居点属性计算最大兵力上限，
    /// 以及从部队名单中统计当前三类采邑士兵的实际数量。
    /// </summary>
    public static class FiefTroopCapacity
    {
        /// <summary>
        /// 计算指定定居点的采邑部队最大兵力上限（Total Limit）。
        /// 上限基于定居点类型、附属村庄数量、繁荣度等因素综合确定。
        /// </summary>
        /// <param name="settlement">目标定居点（城镇或城堡）。</param>
        /// <returns>该定居点可维持的采邑部队最大人数；若输入无效则返回 0。</returns>
        public static int CalculateSizeLimit(Settlement settlement)
        {
            if (settlement == null)
                return 0;

            int baseLimit = settlement.IsTown ? 250 : 200;
            int boundVillageCount = settlement.BoundVillages?.Count ?? 0;
            return baseLimit + (boundVillageCount * 30);
        }

        /// <summary>
        /// 从给定的部队名单中统计三类采邑士兵的实际数量（含健康兵与伤员）。
        /// </summary>
        /// <param name="roster">要分析的部队名单，通常来自 MilitiaParty 的 MemberRoster。</param>
        /// <returns>
        /// 包含总兵力及 Retinue、Sergeant、Militia 三类兵种数量的结构体。
        /// 若 roster 为 null，则返回全零计数。
        /// </returns>
        /// <seealso cref="FiefTroopCounts"/>
        public static FiefTroopCounts AnalyzeCurrentManpower(TroopRoster roster)
        {
            if (roster == null)
                return new FiefTroopCounts(0, 0, 0);

            int retinue = 0, sergeant = 0, militia = 0;

            foreach (var element in roster.GetTroopRoster())
            {
                var troop = element.Character;
                var count = element.Number + element.WoundedNumber; // 健康 + 伤员
                if (troop == null || count <= 0)
                    continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                switch (type)
                {
                    case FiefTroopType.Fief_Retinue:
                        retinue += count;
                        break;
                    case FiefTroopType.Fief_Sergeant:
                        sergeant += count;
                        break;
                    case FiefTroopType.Fief_Militia:
                        militia += count;
                        break;
                }
            }

            return new FiefTroopCounts(retinue, sergeant, militia);
        }
    }
    /// <summary>
    /// 封邑部队组成配置：根据 Settlement 类型初始化兵种权重。
    /// 每个小队总人数为 10，权重用于计算理想兵种数量。
    /// </summary>
    public class FiefTroopComposition
    {
        private readonly float _retinueWeight;
        private readonly float _sergeantWeight;
        private readonly float _militiaWeight;

        // 只读属性（公开访问）
        public float RetinueWeight => _retinueWeight;
        public float SergeantWeight => _sergeantWeight;
        public float MilitiaWeight => _militiaWeight;

        public float TotalWeight = 10f; // 始终为 10

        /// <summary>
        /// 根据封邑类型初始化兵种权重（总权重 = 10）
        /// </summary>
        public FiefTroopComposition(Settlement settlement)
        {
            if (settlement?.IsTown == true)
            {
                _retinueWeight = 0.5f;
                _sergeantWeight = 4.5f;
            }
            else if (settlement?.IsCastle == true)
            {
                _retinueWeight = 1.0f;
                _sergeantWeight = 2.0f;
            }
            else
            {
                _retinueWeight = 1.0f;
                _sergeantWeight = 2.0f;
            }

            // 自动计算民兵权重：确保总和为 10
            _militiaWeight = TotalWeight - _retinueWeight - _sergeantWeight;

            // 安全检查：防止负数（可选）
            if (_militiaWeight < 0f)
            {
                // 如果你担心配置错误导致 militia 为负，可以加日志或修正
                // 例如：throw new InvalidOperationException("权重配置错误：民兵权重不能为负");
                // 或强制归零并调整其他值（但通常你控制输入，不会发生）
            }
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
        [SaveableField(1)] private Settlement _settlement;
        /// <summary>
        /// 封邑就绪军队容器
        /// </summary>
        public TroopRoster FiefTroops { get; private set; }
        /// <summary>
        /// 已被征召的封邑军队
        /// </summary>
        [SaveableProperty(2)] public FiefTroopDetachment RecruitedTroops { get; private set; }
        /// <summary>
        /// 处于解散期的封邑军队
        /// </summary>
        [SaveableProperty(3)] public List<FiefTroopDetachment> ReturnedTroopDetachmentList { get; private set; }

        [SaveableProperty(4)] public int RetinueCount { get; private set; } = 0;
        [SaveableProperty(5)] public int SergeantCount { get; private set; } = 0;
        [SaveableProperty(6)] public int MilitiaCount { get; private set; } = 0;
        public int TotalTroopCount;

        private FiefTroopComposition _troopComposition;

        /// <summary>
        /// 封邑军队最大人数限制
        /// </summary>
        public int TotalLimit { get; private set; }

        // === 兵种上限（由封邑决定）===
        public int MaxRetinue { get; private set; }
        public int MaxSergeant { get; private set; }
        public int MaxMilitia { get; private set; }

        // Pre-categorized candidate pools (initialized once per settlement)
        private List<BasicTroopEntry> RetinueCandidates;
        private List<BasicTroopEntry> SergeantCandidates;
        private List<BasicTroopEntry> MilitiaCandidates;


        public int GetReadyFiefTroopCount()
        {
            var counts = FiefTroopCapacity.AnalyzeCurrentManpower(FiefTroops);
            return counts.Retinue + counts.Sergeant + counts.Militia;
            //return TotalTroopCount;
        }

        /// <summary>
        /// 从 FiefTroops 同步当前三类兵种的实际数量到计数器。
        /// 此方法应在初始化、加载存档或部队变动后调用。
        /// </summary>
        private void SyncManpowerCounters()
        {
            if (FiefTroops == null)
            {
                TotalTroopCount = RetinueCount = SergeantCount = MilitiaCount = 0;
                return;
            }

            var counts = FiefTroopCapacity.AnalyzeCurrentManpower(FiefTroops);
            RetinueCount = counts.Retinue;
            SergeantCount = counts.Sergeant;
            MilitiaCount = counts.Militia;

            if (RecruitedTroops != null && !RecruitedTroops.IsEmpty())
            {
                foreach (var kvp in RecruitedTroops.Troops)
                {
                    var troop = kvp.Key;
                    int count = kvp.Value;
                    if (troop == null || count <= 0) continue;

                    var type = SoldierTypeClassifier.GetSoldierType(troop);
                    switch (type)
                    {
                        case FiefTroopType.Fief_Retinue:
                            RetinueCount += count;
                            break;
                        case FiefTroopType.Fief_Sergeant:
                            SergeantCount += count;
                            break;
                        case FiefTroopType.Fief_Militia:
                            MilitiaCount += count;
                            break;
                    }
                }
            }

            if (ReturnedTroopDetachmentList != null)
            {
                foreach (var detachment in ReturnedTroopDetachmentList)
                {
                    if (detachment?.IsEmpty() == false)
                    {
                        foreach (var kvp in detachment.Troops)
                        {
                            var troop = kvp.Key;
                            int count = kvp.Value;
                            if (troop == null || count <= 0) continue;

                            var type = SoldierTypeClassifier.GetSoldierType(troop);
                            switch (type)
                            {
                                case FiefTroopType.Fief_Retinue:
                                    RetinueCount += count;
                                    break;
                                case FiefTroopType.Fief_Sergeant:
                                    SergeantCount += count;
                                    break;
                                case FiefTroopType.Fief_Militia:
                                    MilitiaCount += count;
                                    break;
                            }
                        }
                    }
                }
            }

            TotalTroopCount = RetinueCount + SergeantCount + MilitiaCount;

            //ModLogger.Debug($"[SyncCounters] Settlement: {_settlement?.Name}, " +
            //               $"R={RetinueCount}/{MaxRetinue}, " +
            //               $"S={SergeantCount}/{MaxSergeant}, " +
            //               $"M={MilitiaCount}/{MaxMilitia}");
        }

        /// <summary>
        /// 重置计数器
        /// </summary>
        private void ResetManpowerCounters()
        {
            RetinueCount = SergeantCount = MilitiaCount = 0;
        }

        private void CalculateLimit()
        {
            TotalLimit = FiefTroopCapacity.CalculateSizeLimit(_settlement);

            MaxRetinue = Math.Max(0, (int)MathF.Floor((TotalLimit * _troopComposition.RetinueWeight) / _troopComposition.TotalWeight));
            MaxSergeant = Math.Max(0, (int)MathF.Floor((TotalLimit * _troopComposition.SergeantWeight) / _troopComposition.TotalWeight));
            MaxMilitia = TotalLimit - MaxRetinue - MaxSergeant;
            MaxMilitia = Math.Max(0, MaxMilitia);

            //ModLogger.Debug($"[FiefSettlementData] Calculated for '{_settlement.Name}' with limits: " +
            //               $"R≤{MaxRetinue}, S≤{MaxSergeant}, M≤{MaxMilitia}, T≤{TotalLimit}");
        }
        public bool Reflush(bool first_flag = false)
        {
            if (_settlement == null)
            {
                ModLogger.Error("[Reflush] _settlement is NULL! Skipping initialization.");
                TotalLimit = 0;
                return false;
            }

            if (_troopComposition == null)
                _troopComposition = new FiefTroopComposition(_settlement);

            if (ReturnedTroopDetachmentList == null)
                ReturnedTroopDetachmentList = new List<FiefTroopDetachment>();

            if (RecruitedTroops == null)
                RecruitedTroops = new FiefTroopDetachment(-1);

            CalculateLimit();

            var group = BasicTroopGroupManager.GetGroupForCulture(_settlement.Culture);

            if (RetinueCandidates == null)
                RetinueCandidates = group.TroopsByType[FiefTroopType.Fief_Retinue] ?? new List<BasicTroopEntry>();

            if (SergeantCandidates == null)
                SergeantCandidates = group.TroopsByType[FiefTroopType.Fief_Sergeant] ?? new List<BasicTroopEntry>();

            if (MilitiaCandidates == null)
                MilitiaCandidates = group.TroopsByType[FiefTroopType.Fief_Militia] ?? new List<BasicTroopEntry>();

            //if (FiefTroops  == null)
            //{
            //    FiefTroops = TroopRoster.CreateDummyTroopRoster();
            //    ResetManpowerCounters();
            //}

            if (FiefTroops == null)
            {
                if (_settlement.MilitiaPartyComponent != null
                    && _settlement.MilitiaPartyComponent.MobileParty.IsActive)
                {
                    FiefTroops = _settlement.MilitiaPartyComponent.MobileParty.MemberRoster;
                    SyncManpowerCounters();
                }
                else
                {
                    ResetManpowerCounters();
                    return false;
                }
            }
            else if (first_flag)
            {
                SyncManpowerCounters();
            }

            return true;
        }
        public FiefPartyData() {}
        public FiefPartyData(Settlement settlement)
        {
            _settlement = settlement;
            Reflush();
        }

        public void AfterLoad()
        {
            Reflush(true);
        }

        private CharacterObject WeightedRandomSelectFromBasicTroopEntries(List<BasicTroopEntry> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            int totalWeight = candidates.Sum(c => c.Weight);
            if (totalWeight <= 0)
                return candidates[0].Troop;

            int rand = MBRandom.RandomInt(totalWeight); // 使用整数随机更高效且避免浮点误差
            int sum = 0;

            foreach (var candidate in candidates)
            {
                sum += candidate.Weight;
                if (rand < sum)
                    return candidate.Troop;
            }

            return candidates[candidates.Count - 1].Troop;
        }

        private (Dictionary<CharacterObject, int> retinue,
                 Dictionary<CharacterObject, int> sergeant,
                 Dictionary<CharacterObject, int> militia)
        GenerateRecruitTemplates(int retinueCount, int sergeantCount, int militiaCount)
        {
            var retinueDict = new Dictionary<CharacterObject, int>();
            for (int i = 0; i < retinueCount; i++)
            {
                var troop = WeightedRandomSelectFromBasicTroopEntries(RetinueCandidates);
                if (troop != null)
                {
                    if (retinueDict.ContainsKey(troop))
                        retinueDict[troop]++;
                    else
                        retinueDict[troop] = 1;
                }
            }

            var sergeantDict = new Dictionary<CharacterObject, int>();
            for (int i = 0; i < sergeantCount; i++)
            {
                var troop = WeightedRandomSelectFromBasicTroopEntries(SergeantCandidates);
                if (troop != null)
                {
                    if (sergeantDict.ContainsKey(troop))
                        sergeantDict[troop]++;
                    else
                        sergeantDict[troop] = 1;
                }
            }

            var militiaDict = new Dictionary<CharacterObject, int>();
            for (int i = 0; i < militiaCount; i++)
            {
                var troop = WeightedRandomSelectFromBasicTroopEntries(MilitiaCandidates);
                if (troop != null)
                {
                    if (militiaDict.ContainsKey(troop))
                        militiaDict[troop]++;
                    else
                        militiaDict[troop] = 1;
                }
            }

            return (retinueDict, sergeantDict, militiaDict);
        }


        /// <summary>
        /// 从给定的 TroopRoster 中提取所有采邑类型士兵，返回一个新的仅含采邑部队的 TroopRoster。
        /// </summary>
        /// <param name="roster">源 TroopRoster（例如来自 MemberRoster.GetTroopRoster()）</param>
        /// <returns>新的 TroopRoster，仅包含 Fief_Retinue / Fief_Sergeant / Fief_Militia 类型的士兵</returns>
        public TroopRoster GetFiefTroopRoster()
        {
            if (FiefTroops == null)
                return TroopRoster.CreateDummyTroopRoster();

            var fiefRoster = TroopRoster.CreateDummyTroopRoster();

            foreach (var element in FiefTroops.GetTroopRoster())
            {
                var troop = element.Character;
                var count = element.Number + element.WoundedNumber;
                if (troop == null || count <= 0)
                    continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                if (type is FiefTroopType.Fief_Retinue or
                                    FiefTroopType.Fief_Sergeant or
                                    FiefTroopType.Fief_Militia)
                {
                    if (element.Number >= 0)
                        fiefRoster.AddToCounts(troop, element.Number);
                    if (element.WoundedNumber >= 0)
                        fiefRoster.AddToCounts(troop, 0, false, element.WoundedNumber);
                }
            }

            return fiefRoster;
        }

        /// <summary>
        /// 获取每周自动补员的小队数量。
        /// 默认返回 2，后期可基于繁荣度、领主能力等动态计算。
        /// </summary>
        private int GetWeeklyUpdateCount()
        {
            int baseCount = 0;

            if (_settlement != null)
            {
                float prosperity = _settlement.Town.Prosperity;
                if (_settlement.IsTown)
                {
                    baseCount = Math.Min(40, ((int)(prosperity / 2500f) + 1) * 10);
                }
                else if (_settlement.IsCastle)
                {
                    baseCount = Math.Min(30, ((int)(prosperity / 500f) + 1) * 10);
                }

            }
            return baseCount;
        }

        /// <summary>
        /// 每周调用一次，处理封邑军队的自动恢复与补员。
        /// </summary>
        public void WeeklyUpdate()
        {
            if (!Reflush())
                return;

            // Step 1: 处理 ReturnedTroopDetachmentList 的回归
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
                            FiefTroops.AddToCounts(kvp.Key, kvp.Value, false, 0, 0, true, -1);
                        }

                        // 移除该分遣队
                        ReturnedTroopDetachmentList.RemoveAt(i);
                    }
                }
            }

            // Step 2: 检查是否已达总兵力上限
            int currentTotal = TotalTroopCount;
            //ModLogger.Debug($"[WeeklyUpdate] currentTotal={currentTotal}, TotalLimit={TotalLimit}, Settlement={_settlement}");
            if (currentTotal >= TotalLimit)
                return;

            // Step 3: 计算本周最多可补充人数
            int totalCanAdd = Math.Min(GetWeeklyUpdateCount(), TotalLimit - currentTotal);
            //ModLogger.Debug($"[WeeklyUpdate] totalCanAdd={totalCanAdd}");
            if (totalCanAdd <= 0)
                return;

            // Step 4: 按权重分配理想补充数量
            int idealRetinue = (int)MathF.Floor(totalCanAdd * _troopComposition.RetinueWeight / _troopComposition.TotalWeight);
            int idealSergeant = (int)MathF.Floor(totalCanAdd * _troopComposition.SergeantWeight / _troopComposition.TotalWeight);
            int idealMilitia = totalCanAdd - idealRetinue - idealSergeant;

            // Step 5: 受限于各兵种容量上限
            int actualRetinue = Math.Min(idealRetinue, MaxRetinue - RetinueCount);
            int actualSergeant = Math.Min(idealSergeant, MaxSergeant - SergeantCount);
            int actualMilitia = Math.Min(idealMilitia, MaxMilitia - MilitiaCount);
            //ModLogger.Debug($"[WeeklyUpdate] Ideal: R={idealRetinue}, S={idealSergeant}, M={idealMilitia}");
            //ModLogger.Debug($"[WeeklyUpdate] Capacity left: R={MaxRetinue - RetinueCount}, S={MaxSergeant - SergeantCount}, M={MaxMilitia - MilitiaCount}");
            //ModLogger.Debug($"[WeeklyUpdate] Actual to add: R={actualRetinue}, S={actualSergeant}, M={actualMilitia}");

            // 若无可补充兵员，直接退出
            if (actualRetinue + actualSergeant + actualMilitia <= 0)
            {
                ModLogger.Debug("[WeeklyUpdate] No troops can be recruited this week (capacity full or ideal=0). Skipping.");
                return;
            }

            // Step 6: 生成招募列表
            var (retinueDict, sergeantDict, militiaDict) = GenerateRecruitTemplates(
                actualRetinue,
                actualSergeant,
                actualMilitia
            );

            // Step 7: 批量添加部队
            foreach (var kvp in retinueDict)
            {
                FiefTroops.AddToCounts(kvp.Key, kvp.Value, false, 0, 0, true, -1);
                RetinueCount += kvp.Value;
            }
            foreach (var kvp in sergeantDict)
            {
                FiefTroops.AddToCounts(kvp.Key, kvp.Value, false, 0, 0, true, -1);
                SergeantCount += kvp.Value;
            }
            foreach (var kvp in militiaDict)
            {
                FiefTroops.AddToCounts(kvp.Key, kvp.Value, false, 0, 0, true, -1);
                MilitiaCount += kvp.Value;
            }

            TotalTroopCount = RetinueCount + SergeantCount + MilitiaCount;

            //ModLogger.Debug($"[WeeklyUpdate] Added {actualRetinue}R+{actualSergeant}S+{actualMilitia}M to '{_settlement?.Name}'" +
            //            $"Counts: R={RetinueCount}/{MaxRetinue}, S={SergeantCount}/{MaxSergeant}, " +
            //            $"M={MilitiaCount}/{MaxMilitia}, T={TotalTroopCount}/{TotalLimit}.");
        }

        
        /// <summary>
        /// 玩家将部队中的采邑士兵（含伤员）归还至封邑军队。
        /// - 所有归还士兵（健康+伤员）均作为健康兵加入 FiefTroops（通过冷却分遣队）
        /// - 使用 RemoveTroop 自动处理健康/伤员混合移除
        /// - 仅处理 Occupation.Soldier
        /// - 先清空 RecruitedTroops
        /// - 按兵种剩余容量归还（不再循环权重）
        /// </summary>
        public int ReturnTroopsToSettlement(MobileParty sourceParty)
        {
            if (sourceParty == null || FiefTroops == null)
                return 0;

            // Step 1: 清空 RecruitedTroops 并扣减计数器
            if (RecruitedTroops != null && !RecruitedTroops.IsEmpty())
            {
                int pendingRetinue = 0, pendingSergeant = 0, pendingMilitia = 0;
                foreach (var kvp in RecruitedTroops.Troops)
                {
                    var troop = kvp.Key;
                    int count = kvp.Value;
                    if (troop == null) continue;
                    var type = SoldierTypeClassifier.GetSoldierType(troop);
                    switch (type)
                    {
                        case FiefTroopType.Fief_Retinue: pendingRetinue += count; break;
                        case FiefTroopType.Fief_Sergeant: pendingSergeant += count; break;
                        case FiefTroopType.Fief_Militia: pendingMilitia += count; break;
                    }
                }
                RetinueCount = Math.Max(0, RetinueCount - pendingRetinue);
                SergeantCount = Math.Max(0, SergeantCount - pendingSergeant);
                MilitiaCount = Math.Max(0, MilitiaCount - pendingMilitia);
                TotalTroopCount = RetinueCount + MilitiaCount + SergeantCount;
                RecruitedTroops.Clear();
            }

            // Step 2: 分类当前 party 中的采邑士兵
            var retinuePool = new List<(CharacterObject troop, int totalCount)>();
            var sergeantPool = new List<(CharacterObject troop, int totalCount)>();
            var militiaPool = new List<(CharacterObject troop, int totalCount)>();

            foreach (var element in sourceParty.MemberRoster.GetTroopRoster())
            {
                var troop = element.Character;
                if (troop == null || troop.Occupation != Occupation.Soldier)
                    continue;
                int total = element.Number + element.WoundedNumber;
                if (total <= 0)
                    continue;
                var type = SoldierTypeClassifier.GetSoldierType(troop);
                switch (type)
                {
                    case FiefTroopType.Fief_Retinue: retinuePool.Add((troop, total)); break;
                    case FiefTroopType.Fief_Sergeant: sergeantPool.Add((troop, total)); break;
                    case FiefTroopType.Fief_Militia: militiaPool.Add((troop, total)); break;
                }
            }

            // Step 3: 计算各兵种可接收的剩余容量
            int retinueSpace = Math.Max(0, MaxRetinue - RetinueCount);
            int sergeantSpace = Math.Max(0, MaxSergeant - SergeantCount);
            int militiaSpace = Math.Max(0, MaxMilitia - MilitiaCount);

            // 若无空间，直接返回
            if (retinueSpace + sergeantSpace + militiaSpace <= 0)
                return 0;

            // Step 4: 从各 pool 中取兵（不超过 space），构建归还字典
            var allReturned = new Dictionary<CharacterObject, int>();

            // 扈从
            int takenRetinue = 0;
            foreach (var (troop, available) in retinuePool)
            {
                if (takenRetinue >= retinueSpace) break;
                int take = Math.Min(available, retinueSpace - takenRetinue);
                if (take > 0)
                {
                    if (allReturned.ContainsKey(troop))
                        allReturned[troop] += take;
                    else
                        allReturned[troop] = take;
                    takenRetinue += take;
                }
            }

            // 军士
            int takenSergeant = 0;
            foreach (var (troop, available) in sergeantPool)
            {
                if (takenSergeant >= sergeantSpace) break;
                int take = Math.Min(available, sergeantSpace - takenSergeant);
                if (take > 0)
                {
                    if (allReturned.ContainsKey(troop))
                        allReturned[troop] += take;
                    else
                        allReturned[troop] = take;
                    takenSergeant += take;
                }
            }

            // 民兵
            int takenMilitia = 0;
            foreach (var (troop, available) in militiaPool)
            {
                if (takenMilitia >= militiaSpace) break;
                int take = Math.Min(available, militiaSpace - takenMilitia);
                if (take > 0)
                {
                    if (allReturned.ContainsKey(troop))
                        allReturned[troop] += take;
                    else
                        allReturned[troop] = take;
                    takenMilitia += take;
                }
            }

            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();

            int totalReturned = takenRetinue + takenSergeant + takenMilitia;
            if (totalReturned <= 0)
            {
                _fiefWageExemptionManager.ClearAllExemptions(sourceParty);

                ModLogger.Notice($"[Return] Returned {totalReturned} troops to fief '{_settlement?.Name}'. " +
                           $"Counts: R={RetinueCount}/{MaxRetinue}, S={SergeantCount}/{MaxSergeant}, " +
                           $"M={MilitiaCount}/{MaxMilitia}, T={TotalTroopCount}/{TotalLimit}.");
                return 0;
            }

            // Step 5: 从 sourceParty 移除士兵
            foreach (var kvp in allReturned)
            {
                RemoveTroopsFromParty(sourceParty.MemberRoster, kvp.Key, kvp.Value);
            }

            // Step 6: 创建冷却分遣队（2周后自动回归 FiefTroops）
            var detachment = new FiefTroopDetachment(2);
            detachment.AddTroops(allReturned);
            if (ReturnedTroopDetachmentList == null)
                ReturnedTroopDetachmentList = new List<FiefTroopDetachment>();
            ReturnedTroopDetachmentList.Add(detachment);

            // Step 7: 更新计数器
            RetinueCount += takenRetinue;
            SergeantCount += takenSergeant;
            MilitiaCount += takenMilitia;

            TotalTroopCount = RetinueCount + SergeantCount + MilitiaCount;

            _fiefWageExemptionManager.ConsumeExemption(sourceParty, totalReturned);

            ModLogger.Notice($"[Return] Returned {totalReturned} troops to fief '{_settlement?.Name}'. " +
                           $"Counts: R={RetinueCount}/{MaxRetinue}, S={SergeantCount}/{MaxSergeant}, " +
                           $"M={MilitiaCount}/{MaxMilitia}, T={TotalTroopCount}/{TotalLimit}.");
            return totalReturned;
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
        /// - 从 FiefTroops 移除已征召士兵（因为他们已离营）
        /// - 添加到 RecruitedTroops（标记为已征召）
        /// - 不修改 RetinueCount/SergeantCount/MilitiaCount（兵力仍属封邑）
        /// </summary>
        /// <param name="targetParty">目标部队</param>
        /// <returns>总招募人数（必为 10 的倍数）</returns>
        public int RecruitTroopsToParty(MobileParty targetParty)
        {
            if (targetParty == null || FiefTroops == null)
                return 0;

            if (_settlement.OwnerClan != targetParty.LeaderHero.Clan)
                return 0;

            int currentMembers = targetParty.Party.NumberOfAllMembers;
            int partySizeLimit = targetParty.Party.PartySizeLimit;
            if (currentMembers >= partySizeLimit)
                return 0;

            // Step 1: 分类士兵
            var retinuePool = new List<(CharacterObject troop, int count)>();
            var sergeantPool = new List<(CharacterObject troop, int count)>();
            var militiaPool = new List<(CharacterObject troop, int count)>();

            foreach (var element in FiefTroops.GetTroopRoster())
            {
                var troop = element.Character;
                var count = element.Number;
                if (troop == null || count <= 0) continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                switch (type)
                {
                    case FiefTroopType.Fief_Retinue:
                        retinuePool.Add((troop, count));
                        break;
                    case FiefTroopType.Fief_Sergeant:
                        sergeantPool.Add((troop, count));
                        break;
                    case FiefTroopType.Fief_Militia:
                        militiaPool.Add((troop, count));
                        break;
                }
            }

            // 按 Tier 降序（优先高阶兵）
            retinuePool.Sort((a, b) => (b.troop?.Tier ?? 0).CompareTo(a.troop?.Tier ?? 0));
            sergeantPool.Sort((a, b) => (b.troop?.Tier ?? 0).CompareTo(a.troop?.Tier ?? 0));
            militiaPool.Sort((a, b) => (b.troop?.Tier ?? 0).CompareTo(a.troop?.Tier ?? 0));

            // Step 2: 计算 targetParty 可接受的最大人数
            int remainingSlots = partySizeLimit - currentMembers;
            if (remainingSlots <= 0)
                return 0;

            // Step 3: 按权重分配理想招募数量
            int idealRetinue = (int)MathF.Floor(remainingSlots * _troopComposition.RetinueWeight / _troopComposition.TotalWeight);
            int idealSergeant = (int)MathF.Floor(remainingSlots * _troopComposition.SergeantWeight / _troopComposition.TotalWeight);
            int idealMilitia = remainingSlots - idealRetinue - idealSergeant; // 补足至 remainingSlots

            // Step 4: 直接从 pool 取兵并执行三处操作
            int totalRecruited = 0;
            var allRecruitedTroops = new Dictionary<CharacterObject, int>(); // 用于 RecruitedTroops.AddTroops

            // 扈从
            int takenRetinue = 0;
            foreach (var (troop, available) in retinuePool)
            {
                if (takenRetinue >= idealRetinue) break;
                int take = Math.Min(available, idealRetinue - takenRetinue);
                if (take > 0)
                {
                    RemoveTroopsFromParty(FiefTroops, troop, take);
                    targetParty.MemberRoster.AddToCounts(troop, take, false, 0, 0, true, -1);
                    if (allRecruitedTroops.ContainsKey(troop))
                        allRecruitedTroops[troop] += take;
                    else
                        allRecruitedTroops[troop] = take;

                    takenRetinue += take;
                }
            }

            // 军士
            int takenSergeant = 0;
            foreach (var (troop, available) in sergeantPool)
            {
                if (takenSergeant >= idealSergeant) break;
                int take = Math.Min(available, idealSergeant - takenSergeant);
                if (take > 0)
                {
                    RemoveTroopsFromParty(FiefTroops, troop, take);
                    targetParty.MemberRoster.AddToCounts(troop, take, false, 0, 0, true, -1);
                    if (allRecruitedTroops.ContainsKey(troop))
                        allRecruitedTroops[troop] += take;
                    else
                        allRecruitedTroops[troop] = take;

                    takenSergeant += take;
                }
            }

            // 民兵
            int takenMilitia = 0;
            foreach (var (troop, available) in militiaPool)
            {
                if (takenMilitia >= idealMilitia) break;
                int take = Math.Min(available, idealMilitia - takenMilitia);
                if (take > 0)
                {
                    RemoveTroopsFromParty(FiefTroops, troop, take);
                    targetParty.MemberRoster.AddToCounts(troop, take, false, 0, 0, true, -1);
                    if (allRecruitedTroops.ContainsKey(troop))
                        allRecruitedTroops[troop] += take;
                    else
                        allRecruitedTroops[troop] = take;

                    takenMilitia += take;
                }
            }

            totalRecruited = takenRetinue + takenSergeant + takenMilitia;
            if (totalRecruited <= 0)
                return 0;

            /// 
            /// 非玩家家族的采邑部队不受限制，招募后可以马上补充
            /// 
            if (_settlement.OwnerClan == Clan.PlayerClan)
                RecruitedTroops.AddTroops(allRecruitedTroops);
            else
            {
                RetinueCount -= takenRetinue;
                SergeantCount -= takenSergeant;
                MilitiaCount -= takenMilitia;
            }

            TotalTroopCount = RetinueCount + SergeantCount + MilitiaCount;

            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager.AddExemption(targetParty, totalRecruited);

            ModLogger.Notice($"[Fief] Recruited {totalRecruited} troops (R={takenRetinue}, S={takenSergeant}, M={takenMilitia}).");
            return totalRecruited;
        }

        /// <summary>
        /// 返回税收裁剪系数：1 - (当前人数/最大容量) * 0.9
        /// 范围锁定在 [0.1, 1.0]
        /// </summary>
        public float GetTaxationMultiplier()
        {
            //ModLogger.Debug($"[Tax Debug] TotalTroopCount={TotalTroopCount}, TotalLimit={TotalLimit}");

            if (TotalLimit <= 0)
                return 1.0f;

            float ratio = MathF.Clamp((float)TotalTroopCount / TotalLimit, 0f, 1f);
            float multiplier = 1f - ratio * 0.9f;

            //ModLogger.Debug($"[Tax Debug] ratio={ratio:F3} → multiplier={multiplier:F3}");
            return MathF.Clamp(multiplier, 0.1f, 1.0f);
        }
    }


    [HarmonyPatch(typeof(Settlement), "RemoveMilitiasFromParty")]
    public static class Settlement_RemoveMilitiasFromParty_Patch
    {
        // 判断是否为采邑部队
        private static bool IsFiefTroop(CharacterObject troop)
        {
            if (troop == null) return false;
            var type = SoldierTypeClassifier.GetSoldierType(troop);
            return type is FiefTroopType.Fief_Retinue or
                             FiefTroopType.Fief_Sergeant or
                             FiefTroopType.Fief_Militia;
        }

        // Prefix：完全接管函数逻辑
        public static bool Prefix(MobileParty militiaParty, int numberToRemove)
        {
            if (militiaParty == null || militiaParty.MemberRoster == null)
                return false; // skip original

            var roster = militiaParty.MemberRoster;

            // 统计 **非采邑** 民兵总数
            int nonFiefCount = 0;
            var nonFiefIndices = new List<int>();
            for (int i = 0; i < roster.Count; i++)
            {
                var troop = roster.GetCharacterAtIndex(i);
                int count = roster.GetElementNumber(i);
                if (count <= 0) continue;

                if (!IsFiefTroop(troop))
                {
                    nonFiefCount += count;
                    nonFiefIndices.Add(i);
                }
            }

            // 如果没有非采邑部队，或要移除数量 ≤ 0，则什么都不做
            if (nonFiefCount <= 0 || numberToRemove <= 0)
                return false; // skip original

            // 如果要移除的数量 ≥ 非采邑总数，则清空所有非采邑
            if (numberToRemove >= nonFiefCount)
            {
                foreach (int i in nonFiefIndices)
                {
                    roster.AddToCountsAtIndex(i, -roster.GetElementNumber(i), 0, 0, false);
                }
                roster.RemoveZeroCounts();
                return false; // done
            }

            // 按比例移除非采邑部队（模仿原版逻辑）
            float ratio = (float)numberToRemove / nonFiefCount;
            int remainingToRemove = numberToRemove;

            foreach (int i in nonFiefIndices)
            {
                if (remainingToRemove <= 0) break;

                int currentCount = roster.GetElementNumber(i);
                if (currentCount <= 0) continue;

                int toRemove = MBRandom.RoundRandomized(currentCount * ratio);
                if (toRemove > remainingToRemove)
                    toRemove = remainingToRemove;

                roster.AddToCountsAtIndex(i, -toRemove, 0, 0, false);
                remainingToRemove -= toRemove;
            }

            roster.RemoveZeroCounts();
            return false; // skip original implementation
        }
    }

}
