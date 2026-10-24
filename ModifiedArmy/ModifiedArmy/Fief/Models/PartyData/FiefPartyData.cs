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

        // Properties 1 and 2 are retained only so old saves can deserialize.
        // Their contents are discarded and never participate in new games.
        [SaveableProperty(6)]
        public List<FiefServiceBatch> ServiceBatches { get; private set; }

        [SaveableProperty(7)]
        public List<FiefReturnBatch> ReturnBatches { get; private set; }

        [SaveableProperty(3)] 
        public int LegacyRetinueCount { get; private set; } = 0;
        [SaveableProperty(4)] 
        public int LegacySergeantCount { get; private set; } = 0;
        [SaveableProperty(5)] 
        public int LegacyMilitiaCount { get; private set; } = 0;

        /// <summary>
        /// 当前封邑使用的部队模板
        /// </summary>
        private FiefPartyTemplate _fiefPartyTemplate;

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

        public Settlement GetSettlement() => _settlement;

        private FiefProductionConfig GetFiefProductionConfig() =>
            _settlement == null ? null : MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement)
                .GetProduction(_settlement);

        internal void RefreshManpowerCounters() => SyncManpowerCounters();

        internal FiefServiceBatch CreateServingDetachment(
            MobileParty servingParty,
            Dictionary<CharacterObject, int> actualTroops)
        {
            if (servingParty == null || actualTroops == null ||
                actualTroops.Values.Sum() <= 0)
                return null;

            CultureObject culture = _fiefPartyTemplate?.Culture ?? _settlement?.Culture;
            int freeWageDays = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(culture).Service.FreeWageDays;
            FiefServiceBatch batch = FiefServiceBatch.Create(
                servingParty,
                freeWageDays,
                actualTroops);
            ServiceBatches.Add(batch);
            Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                .RegisterServingDetachment(_settlement, batch);
            SyncManpowerCounters();
            return batch;
        }

        public FiefPartyData() { }
        public FiefPartyData(Settlement settlement)
        {
            _settlement = settlement;
            InitialFeifPartyData();
        }

        /// <summary>
        /// 获取采邑部队的最大数量。
        /// </summary>
        /// 
        public int GetFiefTroopLimit() => _totalLimit;

        /// <summary>
        /// 获取封邑士兵的总数（包括就绪、征召、冷却）
        /// </summary>
        public int GetTotalTroopCount() => _totalTroopCount;


        /// <summary>
        /// 从给定的部队名单中统计采邑士兵的实际数量（含健康兵与伤员）。
        /// </summary>
        public int CountCurrentManpower(TroopRoster roster)
        {
            if (roster == null || _fiefPartyTemplate == null) return 0;
            int total = 0;

            foreach (var element in roster.GetTroopRoster())
            {
                var troop = element.Character;
                // Number already includes wounded troops in Bannerlord rosters.
                var count = element.Number;
                if (troop == null || count <= 0)
                    continue;

                if (!_fiefPartyTemplate.IsEnableTroop(troop))
                    continue;

                total += count;
            }
            return total;
        }

        /// <summary>
        /// 将指定分遣队列表中的兵力按兵种类型累加到给定的计数器。
        /// </summary>
        /// <param name="detachments">分遣队列表，可为 null</param>
        /// 
        public int CountDetachments(IEnumerable<FiefServiceBatch> detachments)
        {
            int total = 0;
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

                            total += count;
                        }
                    }
                }
            }
            return total;
        }

        /// <summary>
        /// 获取就绪的封邑士兵数量
        /// </summary>
        public int GetReadyFiefTroopCount()
        {
            return CountCurrentManpower(_fiefParty);
        }

        /// <summary>
        /// 获取征召的封邑士兵数量
        /// </summary>
        public int GetRecruitedFiefTroopCount()
        {
            return CountDetachments(ServiceBatches);
        }

        /// <summary>
        /// 从 _fiefParty 同步当前三类兵种的实际数量到计数器。
        /// 此方法应在初始化、加载存档或部队变动后调用。
        /// </summary>
        private void SyncManpowerCounters()
        {
            // 在役和冷却批次是历史身份记录，不能因当前模板变化而丢失。
            // 只有共享就绪名册需要按当前模板过滤，避免把原生民兵算入封邑编制。
            _totalTroopCount = CountCurrentManpower(_fiefParty) +
                CountDetachments(ServiceBatches) +
                (ReturnBatches?.Where(batch => batch?.Troops != null)
                    .Sum(batch => batch.Troops.Where(pair => pair.Key != null)
                        .Sum(pair => Math.Max(0, pair.Value))) ?? 0);
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
            FiefProductionConfig production = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement)
                .GetProduction(_settlement);
            return production.BaseLimit +
                (boundVillageCount * production.VillageBonus);
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
                // Serving detachments remain authoritative after a capture.
                // They may still be alive in their serving parties and must
                // continue to age and receive the correct wage treatment.
                ModLogger.Info($"[Fief Service] {_settlement.Name} changed hands; active service batches were preserved.");

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
            // Ownership changes do not prove that serving soldiers died or
            // returned. Preserve their records and forbid normal return by
            // the ownership checks in the return flow.
            SyncManpowerCounters();
        }

        
        private void CalculateLimit()
        {
            if (_totalLimit == 0)
                _totalLimit = CalculateSizeLimit();

            ModLogger.Debug($"[FiefSettlementData] Limit for '{_settlement.Name}': Total≤{_totalLimit}");
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

            ServiceBatches ??= new List<FiefServiceBatch>();
            ReturnBatches ??= new List<FiefReturnBatch>();

            // Old detachments cannot prove their serving party and are not
            // migrated. Clear them once the new containers are available.
            RecruitedTroopDetachmentList.Clear();
            ReturnedTroopDetachmentList.Clear();

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
                    return false;
                }
            }

            SyncManpowerCounters();
            return true;
        }
    }
}
