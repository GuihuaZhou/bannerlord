using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using ModifiedArmy.Recruitment.Pools.Behaviors;
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
        // Compatibility-only read snapshot. Mutations must be routed to a village.
        public TroopRoster _fiefParty => GetFiefTroopRoster();
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
        [SaveableProperty(8)]
        public Dictionary<Settlement, VillageFiefData> Villages { get; private set; }

        /// <summary>
        /// 当前封邑使用的部队模板
        /// </summary>
        private FiefPartyTemplate _fiefPartyTemplate;

        /// <summary>
        /// 封邑士兵总人数
        /// <summary>
        public int _totalTroopCount { get; private set; }

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
            VillageFiefData village,
            MobileParty servingParty,
            Dictionary<CharacterObject, int> actualTroops)
        {
            if (servingParty == null || actualTroops == null ||
                actualTroops.Values.Sum() <= 0)
                return null;

            CultureObject culture = _fiefPartyTemplate?.Culture ?? _settlement?.Culture;
            if (_settlement?.IsFortification != true ||
                village?.VillageSettlement?.IsVillage != true ||
                !IsCurrentVillage(village.VillageSettlement))
                return null;
            int freeWageDays = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(culture).Service.FreeWageDays;
            FiefServiceBatch batch = FiefServiceBatch.Create(
                _settlement,
                village.VillageSettlement,
                servingParty,
                freeWageDays,
                actualTroops);
            if (village == null) return null;
            village.ServiceBatches.Add(batch);
            try
            {
                Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                    .RegisterServingDetachment(
                        _settlement, village.VillageSettlement, batch);
            }
            catch
            {
                village.ServiceBatches.Remove(batch);
                throw;
            }
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
        /// 获取封邑士兵的总数（包括就绪、征召、冷却）
        /// </summary>
        public int GetTotalTroopCount() => _totalTroopCount;

        public int GetTotalOccupiedSupplyHearth()
        {
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            return Villages?.Values.Where(village => village != null).Sum(
                village => village.GetOccupiedSupplyHearth(
                    _fiefPartyTemplate, doctrine)) ?? 0;
        }

        public int GetTotalFreeSupplyHearth()
        {
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            return Villages?.Values.Where(village => village != null).Sum(
                village => village.GetFreeSupplyHearth(
                    _fiefPartyTemplate, doctrine)) ?? 0;
        }

        public int GetTotalEffectiveSupplyHearth() => Villages?.Values
            .Where(village => village != null)
            .Sum(village => village.GetEffectiveSupplyHearth()) ?? 0;

        public int GetTotalFeudalBudgetHearth()
        {
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            return Villages?.Values.Where(village => village != null).Sum(
                village => village.GetFeudalBudgetHearth(doctrine)) ?? 0;
        }

        public int GetTotalFeudalBudgetRemainingHearth()
        {
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            return Villages?.Values.Where(village => village != null).Sum(
                village => Math.Max(0,
                    village.GetFeudalBudgetRemainingHearth(
                        _fiefPartyTemplate, doctrine))) ?? 0;
        }

        public int GetTotalSupplyDeficitHearth()
        {
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            return Villages?.Values.Where(village => village != null).Sum(
                village => village.GetSupplyDeficitHearth(
                    _fiefPartyTemplate, doctrine)) ?? 0;
        }

        public bool CanProduceAnyFiefTroop()
        {
            if (_fiefPartyTemplate == null) return false;
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(_settlement);
            int barracksLevel = SettlementManpowerProductionBehavior
                .GetBarracksLevel(_settlement);
            IEnumerable<CharacterObject> troops = _fiefPartyTemplate
                .TroopEntries.Where(entry => entry?.Troop != null &&
                    entry.Weight > 0 &&
                    entry.RequiredBarracksLevel <= barracksLevel)
                .Select(entry => entry.Troop);
            foreach (VillageFiefData village in OrderedVillages(null)
                .Where(village => !village.RecoveryRequired))
            foreach (CharacterObject troop in troops)
            {
                try
                {
                    if (village.CanIncreaseSupply(_fiefPartyTemplate,
                        doctrine, troop, 1)) return true;
                }
                catch (Exception exception)
                {
                    ModLogger.Error(string.Format(
                        "Fief production capacity rejected: village={0}, troop={1}, tier={2}, reason={3}.",
                        village.VillageSettlement?.StringId ?? "unknown",
                        troop.StringId, troop.Tier, exception.Message));
                }
            }
            return false;
        }

        internal float CalculateFeudalTaxReliefAmount(float wageRatio)
        {
            var wageModel = Campaign.Current?.Models?.PartyWageModel;
            if (wageModel == null || _fiefPartyTemplate == null ||
                wageRatio <= 0f) return 0f;

            long dailyWages = 0;
            checked
            {
                foreach (VillageFiefData village in OrderedVillages())
                foreach (var pair in village.GetRegisteredFeudalTroopCounts(
                    _fiefPartyTemplate))
                {
                    int unitWage = Math.Max(0,
                        wageModel.GetCharacterWage(pair.Key));
                    dailyWages += (long)unitWage * pair.Value;
                }
            }
            double relief = dailyWages * (double)wageRatio;
            return relief >= float.MaxValue
                ? float.MaxValue
                : (float)Math.Max(0d, relief);
        }


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
            return Villages?.Values.Sum(v => v?.GetReadyFiefTroopCount(_fiefPartyTemplate) ?? 0) ?? 0;
        }

        /// <summary>
        /// 获取征召的封邑士兵数量
        /// </summary>
        public int GetRecruitedFiefTroopCount()
        {
            return Villages?.Values.Sum(v => v?.GetServingFiefTroopCount() ?? 0) ?? 0;
        }

        /// <summary>
        /// 从 _fiefParty 同步当前三类兵种的实际数量到计数器。
        /// 此方法应在初始化、加载存档或部队变动后调用。
        /// </summary>
        private void SyncManpowerCounters()
        {
            // 在役和冷却批次是历史身份记录，不能因当前模板变化而丢失。
            // 只有共享就绪名册需要按当前模板过滤，避免把原生民兵算入封邑编制。
            _totalTroopCount = Villages?.Values.Sum(v =>
                v?.GetTotalFiefTroopCount(_fiefPartyTemplate) ?? 0) ?? 0;
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
            var template = FiefPartyTemplateManager.Instance.GetTemplate(templateId);

            if (template == null && !string.IsNullOrEmpty(portSuffix))
            {
                templateId = $"{culture}_{type}";
                template = FiefPartyTemplateManager.Instance.GetTemplate(templateId);
            }

            return template;
        }

        public bool InitialFeifPartyData()
        {
            if (_settlement == null)
            {
                ModLogger.Error("[InitialFeifPartyData] _settlement is NULL! Skipping initialization.");
                return false;
            }

            if (ReturnedTroopDetachmentList == null)
                ReturnedTroopDetachmentList = new List<FiefTroopDetachment>();

            if (RecruitedTroopDetachmentList == null)
                RecruitedTroopDetachmentList = new List<FiefTroopDetachment>();

            Villages ??= new Dictionary<Settlement, VillageFiefData>();

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

            CharacterObject[] nativeMilitia =
            {
                _settlement.Culture.MeleeMilitiaTroop,
                _settlement.Culture.MeleeEliteMilitiaTroop,
                _settlement.Culture.RangedMilitiaTroop,
                _settlement.Culture.RangedEliteMilitiaTroop
            };
            CharacterObject overlap = nativeMilitia.FirstOrDefault(t =>
                t != null && _fiefPartyTemplate.IsEnableTroop(t));
            if (overlap != null)
            {
                ModLogger.Error(string.Format(
                    "Fief/native militia identity overlap: fief={0}, troop={1}. Fief operations are disabled because roster identity is ambiguous.",
                    _settlement.StringId, overlap.StringId));
                return false;
            }

            foreach (Village village in (_settlement.BoundVillages ?? new List<Village>())
                .Where(v => v?.Settlement != null)
                .OrderBy(v => v.Settlement.StringId))
            {
                if (!Villages.TryGetValue(village.Settlement, out VillageFiefData data) || data == null)
                    Villages[village.Settlement] = data = new VillageFiefData(village.Settlement);
                data.Repair(village.Settlement);
            }

            SyncManpowerCounters();
            return true;
        }

        internal VillageFiefData GetVillageData(Settlement village) => village != null &&
            Villages != null && Villages.TryGetValue(village, out VillageFiefData data)
                ? data : null;

        internal bool RepairVillageMilitiaBinding(Settlement village)
        {
            if (!IsCurrentVillage(village)) return false;
            Villages ??= new Dictionary<Settlement, VillageFiefData>();
            if (!Villages.TryGetValue(village, out VillageFiefData data) ||
                data == null)
                Villages[village] = data = new VillageFiefData(village);
            data.Repair(village);
            SyncManpowerCounters();
            return true;
        }

        internal IEnumerable<VillageFiefData> OrderedVillages() =>
            (Villages?.Values ?? Enumerable.Empty<VillageFiefData>())
                .Where(v => v?.VillageSettlement != null)
                .OrderBy(v => v.VillageSettlement.StringId);

        internal IEnumerable<VillageFiefData> OrderedVillages(
            Settlement villageFilter) => OrderedVillages()
                .Where(v => villageFilter == null ||
                    v.VillageSettlement == villageFilter)
                .Where(v => IsCurrentVillage(v.VillageSettlement));

        internal bool IsCurrentVillage(Settlement village) =>
            village?.IsVillage == true &&
            village.Village.Bound == _settlement &&
            _settlement.BoundVillages?.Any(v => v?.Settlement == village) == true;
    }
}
