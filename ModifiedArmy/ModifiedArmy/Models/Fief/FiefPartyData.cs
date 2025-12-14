using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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

        /// <summary>
        /// 将指定分遣队列表中的兵力按兵种类型累加到给定的计数器。
        /// </summary>
        /// <param name="detachments">分遣队列表，可为 null</param>
        /// 
        public static FiefTroopCounts AccumulateDetachments(List<FiefTroopDetachment> detachments)
        {
            int retinue = 0, sergeant = 0, militia = 0;

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
                    }
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
        private float _retinueWeight;
        private float _sergeantWeight;
        private float _militiaWeight;
        private readonly Settlement _settlement;

        // 只读属性（公开访问）
        public float RetinueWeight => _retinueWeight;
        public float SergeantWeight => _sergeantWeight;
        public float MilitiaWeight => _militiaWeight;

        public float TotalWeight = 10f; // 始终为 10

        private void flush()
        {
            float prosperity = _settlement.Town.Prosperity;
            if (_settlement.IsTown)
            {
                int level = (int)(prosperity / 5000f);
                level = Math.Min(level, 2);
                _retinueWeight = 0.5f + level * 0.25f;
                _sergeantWeight = 4.5f + level * 0.25f;
            }
            else if (_settlement.IsCastle)
            {
                int level = (int)(prosperity / 500f);
                level = Math.Min(level, 4);
                _retinueWeight = 1.0f + level * 0.25f;
                _sergeantWeight = 2.0f + level * 0.25f;
            }
            else
            {
                _retinueWeight = 1.0f;
                _sergeantWeight = 2.0f;
            }

            // 自动计算民兵权重：确保总和为 10
            _militiaWeight = TotalWeight - _retinueWeight - _sergeantWeight;
        }

        /// <summary>
        /// 根据封邑类型初始化兵种权重（总权重 = 10）
        /// </summary>
        public FiefTroopComposition(Settlement settlement)
        {
            _settlement = settlement;
            if (_settlement != null)
                flush();
        }

        public void update()
        {
            if (_settlement != null)
                flush();
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

        /// <summary>
        /// 当前封邑使用的部队模板（不保存到存档）
        /// </summary>
        private FiefPartyTemplate _fiefPartyTemplate;

        public FiefPartyData() { }
        public FiefPartyData(Settlement settlement)
        {
            _settlement = settlement;
            Reflush();
        }

        /// <summary>
        /// 获取采邑部队的最大数量。
        /// </summary>
        /// 
        public int GetFiefTroopLimit() => TotalLimit;

        /// <summary>
        /// 获取扈从（Retinue）的当前总数量（包括就绪、征召中、遣返中）。
        /// </summary>
        public int GetRetinueCount() => RetinueCount;

        /// <summary>
        /// 获取扈从（Retinue）的最大允许数量。
        /// </summary>
        public int GetMaxRetinueCount() => MaxRetinue;

        /// <summary>
        /// 获取当前处于征召状态的扈从（Retinue）数量。
        /// </summary>
        public int GetRecruitedRetinueCount()
        {
            var counts = FiefTroopCapacity.AccumulateDetachments(RecruitedTroopDetachmentList);
            return counts.Retinue;
        }

        /// <summary>
        /// 获取军士（Sergeant）的当前总数量（包括就绪、征召中、遣返中）。
        /// </summary>
        public int GetSergeantCount() => SergeantCount;

        /// <summary>
        /// 获取军士（Sergeant）的最大允许数量。
        /// </summary>
        public int GetMaxSergeantCount() => MaxSergeant;

        /// <summary>
        /// 获取当前处于征召状态的军士（Sergeant）数量。
        /// </summary>
        public int GetRecruitedSergeantCount()
        {
            var counts = FiefTroopCapacity.AccumulateDetachments(RecruitedTroopDetachmentList);
            return counts.Sergeant;
        }

        /// <summary>
        /// 获取民兵（Militia）的当前总数量（包括就绪、征召中、遣返中）。
        /// </summary>
        public int GetMilitiaCount() => MilitiaCount;

        /// <summary>
        /// 获取民兵（Militia）的最大允许数量。
        /// </summary>
        public int GetMaxMilitiaCount() => MaxMilitia;

        /// <summary>
        /// 获取当前处于征召状态的民兵（Militia）数量。
        /// </summary>
        public int GetRecruitedMilitiaCount()
        {
            var counts = FiefTroopCapacity.AccumulateDetachments(RecruitedTroopDetachmentList);
            return counts.Militia;
        }

        public int GetReadyFiefTroopCount()
        {
            var counts = FiefTroopCapacity.AnalyzeCurrentManpower(FiefTroops);
            return counts.Retinue + counts.Sergeant + counts.Militia;
        }


        public int GetRecruitedFiefTroopCount()
        {
            var recruitedCounts = FiefTroopCapacity.AccumulateDetachments(RecruitedTroopDetachmentList);
            return recruitedCounts.Retinue + recruitedCounts.Sergeant + recruitedCounts.Militia;
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

            // 1. 就绪部队
            var baseCounts = FiefTroopCapacity.AnalyzeCurrentManpower(FiefTroops);

            // 2. 征召中部队
            var recruitedCounts = FiefTroopCapacity.AccumulateDetachments(RecruitedTroopDetachmentList);

            // 3. 归还冷却中部队
            var returnedCounts = FiefTroopCapacity.AccumulateDetachments(ReturnedTroopDetachmentList);

            // 4. 合并
            RetinueCount = baseCounts.Retinue + recruitedCounts.Retinue + returnedCounts.Retinue;
            SergeantCount = baseCounts.Sergeant + recruitedCounts.Sergeant + returnedCounts.Sergeant;
            MilitiaCount = baseCounts.Militia + recruitedCounts.Militia + returnedCounts.Militia;

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
            }

            SyncManpowerCounters();
        }

        public void OnSettlementOwnerChanged()
        {
            RecruitedTroopDetachmentList.Clear();
            SyncManpowerCounters();
        }

        /// <summary>
        /// 检查封邑部队是否超限。若超限，则将所有兵种按 Tier 升序排序，
        /// 从健康兵中删除（使用 GetTroopCount），直到各类总兵力不超限。
        /// </summary>
        public void EnforceTroopLimits()
        {
            if (FiefTroops == null)
                return;

            // 1. 计算三类兵种的超限数量（基于总兵力 = 健康 + 伤员）
            int excessMilitia = Math.Max(0, MilitiaCount - MaxMilitia);
            int excessSergeant = Math.Max(0, SergeantCount - MaxSergeant);
            int excessRetinue = Math.Max(0, RetinueCount - MaxRetinue);

            if ((excessMilitia | excessSergeant | excessRetinue) == 0)
                return;

            // 2. 收集所有唯一的兵种（CharacterObject）
            var uniqueTroops = new List<CharacterObject>();
            foreach (var element in FiefTroops.GetTroopRoster())
            {
                if (element.Character != null)
                    uniqueTroops.Add(element.Character);
            }

            if (uniqueTroops.Count == 0)
                return;

            // 3. 按 Tier 升序排序（低阶优先）
            uniqueTroops.Sort((a, b) => a.Tier.CompareTo(b.Tier));

            // 4. 遍历排序后的兵种，仅使用 GetTroopCount 查询可删数量
            int removedRetinue = 0, removedSergeant = 0, removedMilitia = 0;
            foreach (var troop in uniqueTroops)
            {
                if ((excessMilitia | excessSergeant | excessRetinue) == 0)
                    break;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                int currentExcess = type switch
                {
                    FiefTroopType.Fief_Militia => excessMilitia,
                    FiefTroopType.Fief_Sergeant => excessSergeant,
                    FiefTroopType.Fief_Retinue => excessRetinue,
                    _ => 0
                };

                if (currentExcess <= 0)
                    continue;

                int troopCount = FiefTroops.GetTroopCount(troop);
                if (troopCount <= 0)
                    continue;

                int removeNow = Math.Min(troopCount, currentExcess);
                if (removeNow > 0)
                {
                    FiefTroops.RemoveTroop(troop, removeNow, default(UniqueTroopDescriptor), 0);

                    // 更新超限计数
                    switch (type)
                    {
                        case FiefTroopType.Fief_Retinue:
                            removedRetinue += removeNow;
                            excessRetinue -= removeNow;
                            break;
                        case FiefTroopType.Fief_Sergeant:
                            removedSergeant += removeNow;
                            excessSergeant -= removeNow;
                            break;
                        case FiefTroopType.Fief_Militia:
                            removedMilitia += removeNow;
                            excessMilitia -= removeNow;
                            break;
                    }
                }
            }

            if (removedRetinue > 0 || removedSergeant > 0 || removedMilitia > 0)
            {
                SyncManpowerCounters();
                if (_settlement.OwnerClan == Clan.PlayerClan)
                {
                    TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_troops_removed_due_to_limit");
                    msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
                    msg.SetTextVariable("RETINUE", removedRetinue);
                    msg.SetTextVariable("SERGEANT", removedSergeant);
                    msg.SetTextVariable("MILITIA", removedMilitia);
                    ModLogger.Notice(msg.ToString());
                }
            }
        }

        private void CalculateLimit()
        {
            _troopComposition.update();
            TotalLimit = FiefTroopCapacity.CalculateSizeLimit(_settlement);

            MaxRetinue = Math.Max(0, (int)MathF.Floor((TotalLimit * _troopComposition.RetinueWeight) / _troopComposition.TotalWeight));
            MaxSergeant = Math.Max(0, (int)MathF.Floor((TotalLimit * _troopComposition.SergeantWeight) / _troopComposition.TotalWeight));
            MaxMilitia = TotalLimit - MaxRetinue - MaxSergeant;
            MaxMilitia = Math.Max(0, MaxMilitia);

            ModLogger.Debug($"[FiefSettlementData] Calculated for '{_settlement.Name}' with limits: " +
                           $"R≤{MaxRetinue}, S≤{MaxSergeant}, M≤{MaxMilitia}, T≤{TotalLimit}");
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

            if (RecruitedTroopDetachmentList == null)
                RecruitedTroopDetachmentList = new List<FiefTroopDetachment>();


            CalculateLimit();

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
            else
            {
                EnforceTroopLimits();
            }

            return true;
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
                var troop = WeightedRandomSelectFromBasicTroopEntries(_fiefPartyTemplate.RetinueTroops);
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
                var troop = WeightedRandomSelectFromBasicTroopEntries(_fiefPartyTemplate.SergeantTroops);
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
                var troop = WeightedRandomSelectFromBasicTroopEntries(_fiefPartyTemplate.MilitiaTroops);
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
            if (_settlement == null) return 0;

            var settings = Main.ModSettings;
            float prosperity = _settlement.Town.Prosperity;

            int baseReinforcements = settings.BaseReinforcements;
            int maxReinforcements = settings.MaxReinforcements;

            if (_settlement.IsTown)
            {
                int divisor = Math.Max(1, settings.TownProsperityPer);
                int extraGroups = (int)(prosperity / divisor);
                baseReinforcements += extraGroups * 10;
            }
            else if (_settlement.IsCastle)
            {
                int divisor = Math.Max(1, settings.CastleProsperityPer);
                int extraGroups = (int)(prosperity / divisor);
                baseReinforcements += extraGroups * 10;
            }

            return Math.Min(maxReinforcements, baseReinforcements);
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

            if (RecruitedTroopDetachmentList != null)
            {
                for (int i = RecruitedTroopDetachmentList.Count - 1; i >= 0; i--)
                {
                    var detachment = RecruitedTroopDetachmentList[i];
                    detachment.Tick();

                    if (detachment.IsReadyToReturn())
                    {
                        foreach (var kvp in detachment.Troops)
                        {
                            switch (SoldierTypeClassifier.GetSoldierType(kvp.Key))
                            {
                                case FiefTroopType.Fief_Retinue:
                                    RetinueCount = Math.Max(0, RetinueCount - kvp.Value);
                                    break;
                                case FiefTroopType.Fief_Sergeant:
                                    SergeantCount = Math.Max(0, SergeantCount - kvp.Value);
                                    break;
                                case FiefTroopType.Fief_Militia:
                                    MilitiaCount = Math.Max(0, MilitiaCount - kvp.Value);
                                    break;
                            }
                        }
                        // 移除该分遣队
                        RecruitedTroopDetachmentList.RemoveAt(i);
                    }
                }
            }

            // Step 2: 检查是否已达总兵力上限
            int currentTotal = TotalTroopCount;
            if (currentTotal >= TotalLimit)
                return;

            // Step 3: 计算本周最多可补充人数
            int totalCanAdd = Math.Min(GetWeeklyUpdateCount(), TotalLimit - currentTotal);
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

            if (_settlement.OwnerClan == Clan.PlayerClan)
            {
                TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_weekly_reinforcement");
                msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
                msg.SetTextVariable("RETINUE", actualRetinue);
                msg.SetTextVariable("SERGEANT", actualSergeant);
                msg.SetTextVariable("MILITIA", actualMilitia);
                msg.SetTextVariable("RETINUE_COUNT", RetinueCount);
                msg.SetTextVariable("MAX_RETINUE", MaxRetinue);
                msg.SetTextVariable("SERGEANT_COUNT", SergeantCount);
                msg.SetTextVariable("MAX_SERGEANT", MaxSergeant);
                msg.SetTextVariable("MILITIA_COUNT", MilitiaCount);
                msg.SetTextVariable("MAX_MILITIA", MaxMilitia);
                msg.SetTextVariable("TROOP_COUNT", TotalTroopCount);
                msg.SetTextVariable("TOTAL_LIMIT", TotalLimit);
                ModLogger.Info(msg.ToString());
            }
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
            if (sourceParty == null || FiefTroops == null)
                return 0;

            // Step 1: 清空 RecruitedTroopDetachmentList 并扣减计数器

            if (RecruitedTroopDetachmentList != null)
            {
                foreach (var detachment in RecruitedTroopDetachmentList)
                {
                    if (detachment?.IsEmpty() == false)
                    {
                        int pendingRetinue = 0, pendingSergeant = 0, pendingMilitia = 0;
                        foreach (var kvp in detachment.Troops)
                        {
                            var troop = kvp.Key;
                            int count = kvp.Value;
                            if (troop == null || count <= 0) continue;

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
                        detachment.Clear();
                    }
                }
                RecruitedTroopDetachmentList.Clear();
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
                    case FiefTroopType.Fief_Retinue: 
                        retinuePool.Add((troop, total)); 
                        break;
                    case FiefTroopType.Fief_Sergeant: 
                        sergeantPool.Add((troop, total)); 
                        break;
                    case FiefTroopType.Fief_Militia: 
                        militiaPool.Add((troop, total)); 
                        break;
                }
            }

            // Step 3: 计算各兵种可接收的剩余容量
            int retinueSpace = Math.Max(0, MaxRetinue - RetinueCount);
            int sergeantSpace = Math.Max(0, MaxSergeant - SergeantCount);
            int militiaSpace = Math.Max(0, MaxMilitia - MilitiaCount);

            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager.ClearAllExemptions(sourceParty);

            ModLogger.Debug($"[Return] Space: R={retinueSpace}/{MaxRetinue}, S={sergeantSpace}/{MaxSergeant}, " +
                           $"M={militiaSpace}/{MaxMilitia}.");

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

            int totalReturned = takenRetinue + takenSergeant + takenMilitia;
            if (totalReturned <= 0)
            {
                ModLogger.Debug($"[Return] Returned {totalReturned} troops to fief '{_settlement?.Name}'. " +
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
            FiefTroopDetachment targetDetachment = null;

            if (ReturnedTroopDetachmentList == null)
                ReturnedTroopDetachmentList = new List<FiefTroopDetachment>();

            // 尝试在 ReturnedTroopDetachmentList 中查找 WaitCycle == 2 的分遣队
            if (ReturnedTroopDetachmentList != null)
            {
                foreach (var detachment in ReturnedTroopDetachmentList)
                {
                    if (detachment != null && detachment.WaitCycle == 2)
                    {
                        targetDetachment = detachment;
                        break;
                    }
                }
            }

            // 如果没找到，则新建一个
            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(2); // WaitCycle = 2
                ReturnedTroopDetachmentList.Add(targetDetachment);
            }

            // 向目标分遣队添加归还的部队
            targetDetachment.AddTroops(allReturned);

            // Step 7: 更新计数器
            RetinueCount += takenRetinue;
            SergeantCount += takenSergeant;
            MilitiaCount += takenMilitia;

            TotalTroopCount = RetinueCount + SergeantCount + MilitiaCount;

            _fiefWageExemptionManager.ConsumeExemption(sourceParty, totalReturned);

            TextObject msgResult = GameTexts.FindText("str_modifiedarmy_fief_return_result");
            msgResult.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
            msgResult.SetTextVariable("RETURNED_COUNT", totalReturned);
            msgResult.SetTextVariable("RETINUE_COUNT", RetinueCount);
            msgResult.SetTextVariable("MAX_RETINUE", MaxRetinue);
            msgResult.SetTextVariable("SERGEANT_COUNT", SergeantCount);
            msgResult.SetTextVariable("MAX_SERGEANT", MaxSergeant);
            msgResult.SetTextVariable("MILITIA_COUNT", MilitiaCount);
            msgResult.SetTextVariable("MAX_MILITIA", MaxMilitia);
            msgResult.SetTextVariable("TROOP_COUNT", TotalTroopCount);
            msgResult.SetTextVariable("TOTAL_LIMIT", TotalLimit);
            ModLogger.Notice(msgResult.ToString());
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

            if (RecruitedTroopDetachmentList == null)
            {
                RecruitedTroopDetachmentList = new List<FiefTroopDetachment>();
            }
            FiefTroopDetachment targetDetachment = null;
            foreach (var detachment in RecruitedTroopDetachmentList)
            {
                if (detachment != null && detachment.WaitCycle == 5)
                {
                    targetDetachment = detachment;
                    break;
                }
            }

            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(4);
                RecruitedTroopDetachmentList.Add(targetDetachment);
            }
            targetDetachment.AddTroops(allRecruitedTroops);

            if (_settlement.OwnerClan == Clan.PlayerClan)
            {
                TextObject msg = GameTexts.FindText("str_modifiedarmy_fief_recruit_to_party");
                msg.SetTextVariable("SETTLEMENT_NAME", _settlement.Name.ToString());
                msg.SetTextVariable("RETINUE", takenRetinue);
                msg.SetTextVariable("SERGEANT", takenSergeant);
                msg.SetTextVariable("MILITIA", takenMilitia);
                ModLogger.Notice(msg.ToString());
            }

            TotalTroopCount = RetinueCount + SergeantCount + MilitiaCount;

            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager.AddExemption(targetParty, totalRecruited, 28);

            return totalRecruited;
        }


        /// <summary>
        /// 从封邑就绪部队中手动招募指定的士兵，并记录到征召列表。
        /// 前提：selectedRoster 来自 FiefTroops，所有 troop 和数量均有效。
        /// </summary>
        /// <param name="selectedRoster">玩家从 FiefTroops 中选择的士兵</param>
        /// <returns>实际招募的总人数（用于工资豁免）</returns>
        public int RecruitManualSelection(TroopRoster selectedRoster, MobileParty targetParty)
        {
            if (selectedRoster == null || selectedRoster.TotalManCount <= 0 || FiefTroops == null)
                return 0;

            var recruitDict = new Dictionary<CharacterObject, int>();
            int totalRecruited = 0;

            foreach (var element in selectedRoster.GetTroopRoster())
            {
                var troop = element.Character;
                int count = element.Number;
                if (troop == null || count <= 0) continue;

                var type = SoldierTypeClassifier.GetSoldierType(troop);
                if (type is not (FiefTroopType.Fief_Retinue or FiefTroopType.Fief_Sergeant or FiefTroopType.Fief_Militia))
                    continue;

                recruitDict[troop] = count;
                totalRecruited += count;
            }

            if (totalRecruited <= 0)
                return 0;

            // 从 FiefTroops 移除
            foreach (var kvp in recruitDict)
            {
                FiefTroops.RemoveTroop(kvp.Key, kvp.Value, default, 0);
            }

            // 添加到 RecruitedTroopDetachmentList（WaitCycle = 5）
            FiefTroopDetachment targetDetachment = RecruitedTroopDetachmentList
                .FirstOrDefault(d => d != null && d.WaitCycle == 5);

            if (targetDetachment == null)
            {
                targetDetachment = new FiefTroopDetachment(5);
                RecruitedTroopDetachmentList.Add(targetDetachment);
            }

            targetDetachment.AddTroops(recruitDict);

            // 同步计数器
            SyncManpowerCounters();

            var _fiefWageExemptionManager = Campaign.Current.GetCampaignBehavior<FiefWageExemptionManager>();
            _fiefWageExemptionManager.AddExemption(targetParty, totalRecruited, 28);

            targetParty.MemberRoster.Add(selectedRoster);

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
