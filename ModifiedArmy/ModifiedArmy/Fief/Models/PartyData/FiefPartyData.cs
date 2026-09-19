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
    [SaveableRootClass(2)]
    public partial class FiefPartyData
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

        /// <summary>
        /// 获取当前定居点使用的部队模板。
        /// </summary>
        public FiefPartyTemplate GetFiefPartyTemplate() => _fiefPartyTemplate;

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
                // Number already includes wounded troops in Bannerlord rosters.
                var count = element.Number;
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

                //// 清除驻军的士气惩罚
                //if (_settlement.Town != null && _settlement.Town.GarrisonParty != null)
                //{
                //    _settlement.Town.GarrisonParty.RecentEventsMorale = 0f;
                //}
            }

            SyncManpowerCounters();
        }

        public void OnSettlementOwnerChanged()
        {
            RecruitedTroopDetachmentList.Clear();
            SyncManpowerCounters();
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
    }
}
