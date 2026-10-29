using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.PartyManagement.Bindings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    
    /// <summary>
    /// Saveable type definer for Fief Squad system.
    /// Ensures proper serialization of settlement and roster lists.
    /// </summary>
    public class FiefSaveDefiner : SaveableTypeDefiner
    {
        public FiefSaveDefiner() : base(20251116) { }

        protected override void DefineClassTypes() 
        {
            base.AddClassDefinition(typeof(FiefTroopDetachment), 1);
            base.AddClassDefinition(typeof(FiefPartyData), 2);
            base.AddClassDefinition(typeof(FiefWageExemption), 3);
            base.AddClassDefinition(typeof(FiefWageExemptionManager), 4);
            base.AddClassDefinition(
                typeof(SettlementManpowerProductionData),
                5);
            base.AddClassDefinition(typeof(FiefServiceBatch), 6);
            base.AddClassDefinition(typeof(FiefReturnBatch), 7);
            base.AddClassDefinition(typeof(ReinforcementMission), 8);
            base.AddClassDefinition(typeof(ReinforcementPartyComponent), 9);
            base.AddClassDefinition(typeof(PartySettlementBindingRecord), 10);
        }

        protected override void DefineContainerDefinitions()
        {
            base.ConstructContainerDefinition(typeof(Dictionary<CharacterObject, int>));
            base.ConstructContainerDefinition(typeof(List<FiefTroopDetachment>));
            base.ConstructContainerDefinition(typeof(List<FiefServiceBatch>));
            base.ConstructContainerDefinition(typeof(List<FiefReturnBatch>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, FiefPartyData>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, int>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, Settlement>));
            base.ConstructContainerDefinition(
                typeof(Dictionary<Settlement, SettlementManpowerProductionData>));

            base.ConstructContainerDefinition(typeof(List<FiefWageExemption>));
            base.ConstructContainerDefinition(typeof(Dictionary<String, List<FiefWageExemption>>));
            base.ConstructContainerDefinition(typeof(List<ReinforcementMission>));
            base.ConstructContainerDefinition(
                typeof(Dictionary<string, PartySettlementBindingRecord>));
        }
    }

    /// <summary>
    /// Campaign behavior that manages fief squads across all player-owned settlements.
    /// Handles initialization, daily updates, saving/loading, and public APIs for recruitment/disbanding.
    /// </summary>
    public class FiefPartyManager : CampaignBehaviorBase
    {
        [SaveableField(1)]
        private Dictionary<Settlement, FiefPartyData> _fiefDataMap = new Dictionary<Settlement, FiefPartyData>();

        [SaveableField(2)]
        private Dictionary<Settlement, int> _mobilizedHearth = new Dictionary<Settlement, int>();

        [SaveableField(3)]
        private Dictionary<Settlement, Settlement> _mobilizationOwners = new Dictionary<Settlement, Settlement>();

        [SaveableField(4)]
        private bool _mobilizationMigrationComplete;

        [SaveableField(5)]
        private Dictionary<Settlement, int> _legacyUntrackedMobilization = new Dictionary<Settlement, int>();

        private readonly Dictionary<MobileParty, List<ServingDetachmentRef>>
            _servingDetachmentIndex = new Dictionary<MobileParty, List<ServingDetachmentRef>>();

        private FiefManpowerLedger ManpowerLedger => new FiefManpowerLedger(
            _mobilizedHearth, _mobilizationOwners, _legacyUntrackedMobilization);

        private FiefServiceLedger ServiceLedger =>
            new FiefServiceLedger(_servingDetachmentIndex);

        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, MapEvent.BattleTypes>(this.OnAfterSiegeCompleted));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, new Action<Hero, Settlement, Hero, CharacterObject, int>(this.OnTroopRecruited));
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<Dictionary<Settlement, FiefPartyData>>("_fiefDataMap", ref this._fiefDataMap);
            dataStore.SyncData("_mobilizedHearth", ref _mobilizedHearth);
            dataStore.SyncData("_mobilizationOwners", ref _mobilizationOwners);
            dataStore.SyncData("_mobilizationMigrationComplete", ref _mobilizationMigrationComplete);
            dataStore.SyncData("_legacyUntrackedMobilization", ref _legacyUntrackedMobilization);

            if (_fiefDataMap == null)
            {
                _fiefDataMap = new Dictionary<Settlement, FiefPartyData>();
            }
            _mobilizedHearth ??= new Dictionary<Settlement, int>();
            _mobilizationOwners ??= new Dictionary<Settlement, Settlement>();
            _legacyUntrackedMobilization ??= new Dictionary<Settlement, int>();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            FiefPartyTemplateManager.Instance.InitAllEnableTroops();
            InitializeFiefData();
            RebuildServingDetachmentIndex();
            ReconcileMobilization(!_mobilizationMigrationComplete);
            _mobilizationMigrationComplete = true;
        }

        /// <summary>
        /// Initializes fief data for all player-owned villages, castles, and towns.
        /// Also restores saved squad compositions from loaded data.
        /// </summary>
        public void InitializeFiefData()
        {
            foreach (var settlement in Settlement.All)
            {
                //if (settlement.OwnerClan != Clan.PlayerClan) continue;
                if (!settlement.IsCastle && !settlement.IsTown) continue;
                if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
                {
                    data.InitialFeifPartyData();
                }
                else
                {
                    _fiefDataMap[settlement] = new FiefPartyData(settlement);
                }
            }
            TextObject msgComplete = GameTexts.FindText("str_modifiedarmy_fief_init_complete");
            msgComplete.SetTextVariable("COUNT", _fiefDataMap.Count);
            ModLogger.Info(msgComplete.ToString());
        }

        private void OnWeeklyTick()
        {
            int updatedCount = 0;
            foreach (var data in _fiefDataMap.Values)
            {
                if (data == null)
                {
                    continue;
                }

                data.WeeklyUpdate();
                updatedCount++;
            }
            TextObject weekly = GameTexts.FindText(
                "str_modifiedarmy_fief_weekly_update");
            weekly.SetTextVariable("COUNT", updatedCount);
            ModLogger.Debug(weekly.ToString());
        }

        private void OnDailyTick()
        {
            foreach (var data in _fiefDataMap.Values)
            {
                data?.DailyUpdate();
            }
            ReconcileMobilization(false);
            foreach (MobileParty party in _servingDetachmentIndex.Keys.ToList())
            {
                AdvanceServiceDays(party);
                ReconcilePartyFiefTroops(party);
            }
        }

        private void ReconcileMobilization(bool preserveLegacyDeficits)
        {
            foreach (Settlement fief in _fiefDataMap.Keys.ToList())
            {
                int totalRecords = _fiefDataMap[fief]?.GetRecruitedFiefTroopCount() ?? 0;
                int tracked = _mobilizationOwners
                    .Where(p => p.Value == fief)
                    .Sum(p => _mobilizedHearth.TryGetValue(p.Key, out int n) ? Math.Max(0, n) : 0);
                if (preserveLegacyDeficits && tracked < totalRecords)
                    _legacyUntrackedMobilization[fief] = totalRecords - tracked;
                int legacy = _legacyUntrackedMobilization.TryGetValue(fief, out int oldCount)
                    ? Math.Min(totalRecords, Math.Max(0, oldCount))
                    : 0;
                if (legacy > 0) _legacyUntrackedMobilization[fief] = legacy;
                else _legacyUntrackedMobilization.Remove(fief);
                int expected = totalRecords - legacy;
                if (tracked > expected)
                {
                    ReleaseMobilization(fief, tracked - expected, false);
                }
                else if (!preserveLegacyDeficits && tracked < expected)
                {
                    AddMobilizationMarkers(fief, expected - tracked);
                }
            }

            foreach (Settlement orphan in _mobilizationOwners.Values
                .Where(owner => owner == null || !_fiefDataMap.ContainsKey(owner))
                .Distinct()
                .ToList())
            {
                if (orphan != null)
                    ReleaseMobilization(orphan, int.MaxValue, false);
            }
            foreach (Settlement village in _mobilizationOwners
                .Where(p => p.Value == null)
                .Select(p => p.Key)
                .ToList())
            {
                _mobilizationOwners.Remove(village);
                _mobilizedHearth.Remove(village);
            }
        }

        private void AddMobilizationMarkers(Settlement fief, int count)
        {
            List<Village> villages = fief?.BoundVillages?
                .Where(v => v?.Settlement != null)
                .OrderBy(v => v.Settlement.StringId)
                .ToList();
            if (villages == null || villages.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                Settlement village = villages[i % villages.Count].Settlement;
                int current = _mobilizedHearth.TryGetValue(village, out int value) ? value : 0;
                _mobilizedHearth[village] = current + 1;
                _mobilizationOwners[village] = fief;
            }
        }

        public int GetMobilizedHearth(Village village)
        {
            return ManpowerLedger.GetMobilizedHearth(village);
        }

        /// <summary>
        /// Reserves rural production capacity before any troop roster is changed.
        /// The deduction is distributed evenly while preserving the recruitment floor.
        /// </summary>
        public bool TryMobilize(Settlement fief, int count, int hearthPerTroop)
        {
            return ManpowerLedger.TryMobilize(fief, count, hearthPerTroop);
        }

        public bool TryConsumeProfessionalHearth(Settlement source, int count)
        {
            return TryConsumeProfessionalHearth(
                source, count, out Dictionary<Settlement, int> _);
        }

        internal bool TryConsumeProfessionalHearth(
            Settlement source,
            int count,
            out Dictionary<Settlement, int> consumedByVillage)
        {
            consumedByVillage = new Dictionary<Settlement, int>();
            if (source == null || count <= 0) return false;
            List<Village> villages = source.IsVillage
                ? new List<Village> { source.Village }
                : source.BoundVillages.Where(v => v?.Settlement != null).ToList();
            villages = villages
                .Where(v => v?.VillageState == Village.VillageStates.Normal)
                .OrderBy(v => v.Settlement.StringId)
                .ToList();
            if (villages.Count == 0) return false;
            int required = checked(count * 2);
            if (villages.Sum(v => Math.Max(0, (int)Math.Floor(v.Hearth) - 100)) < required)
                return false;
            int remaining = required;
            while (remaining > 0)
            {
                foreach (Village village in villages)
                {
                    if (remaining <= 0) break;
                    if (village.Hearth <= 100f) continue;
                    village.Hearth -= 1f;
                    consumedByVillage[village.Settlement] =
                        consumedByVillage.TryGetValue(
                            village.Settlement, out int consumed)
                                ? consumed + 1
                                : 1;
                    remaining--;
                }
            }
            return true;
        }

        internal void RollbackProfessionalHearth(
            IReadOnlyDictionary<Settlement, int> consumedByVillage)
        {
            if (consumedByVillage == null) return;
            foreach (var pair in consumedByVillage)
                if (pair.Key?.Village != null && pair.Value > 0)
                    pair.Key.Village.Hearth += pair.Value;
        }

        /// <summary>
        /// Releases an exact amount of mobilized capacity. Returning soldiers restore
        /// hearth; expired, captured, or reconciled records only release the marker.
        /// </summary>
        public int ReleaseMobilization(Settlement fief, int count, bool restoreHearth)
        {
            return ManpowerLedger.Release(fief, count, restoreHearth);
        }

        public int ReleaseFiefService(Settlement fief, int count, bool restoreHearth)
        {
            return ManpowerLedger.ReleaseService(fief, count, restoreHearth);
        }

        /// <summary>
        /// Counts only mobilization backed by concrete village Hearth markers.
        /// Legacy untracked deficits are deliberately excluded because they
        /// cannot safely restore Hearth during a normal return.
        /// </summary>
        internal int GetTrackedMobilizationCount(Settlement fief)
        {
            return ManpowerLedger.GetTrackedCount(fief);
        }

        /// <summary>
        /// Releases exactly <paramref name="count"/> tracked Hearth markers
        /// and restores the corresponding village Hearth. The per-village
        /// result is a rollback token for the surrounding return transaction.
        /// No state changes are made when the tracked balance is insufficient.
        /// </summary>
        internal bool TryReleaseMobilizationExact(
            Settlement fief,
            int count,
            out Dictionary<Settlement, int> releasedByVillage)
        {
            return ManpowerLedger.TryReleaseExact(
                fief, count, out releasedByVillage);
        }

        /// <summary>
        /// Reverses a successful exact mobilization release after a later step
        /// in the same return transaction fails.
        /// </summary>
        internal void RollbackMobilizationRelease(
            Settlement fief,
            IReadOnlyDictionary<Settlement, int> releasedByVillage)
        {
            ManpowerLedger.RollbackRelease(fief, releasedByVillage);
        }

        public void RebuildServingDetachmentIndex()
        {
            ServiceLedger.Rebuild(_fiefDataMap);
        }

        public void RegisterServingDetachment(
            Settlement source,
            FiefServiceBatch batch)
        {
            ServiceLedger.Register(source, batch);
        }

        public void UnregisterServingDetachment(
            Settlement source,
            FiefServiceBatch batch)
        {
            ServiceLedger.Unregister(source, batch);
        }

        internal List<ServingDetachmentRef> GetServingDetachments(MobileParty party)
        {
            return ServiceLedger.Get(party);
        }

        public void AdvanceServiceDays(MobileParty party)
        {
            foreach (ServingDetachmentRef item in GetServingDetachments(party))
                item.Detachment.AdvanceToCurrentDay();
        }

        public FiefReconciliationResult ReconcilePartyFiefTroops(MobileParty party)
        {
            return FiefServiceReconciler.Reconcile(this, party);
        }

        /// <summary>
        /// Attempts a controlled partial return from one identified service
        /// batch to its real source fief. The party is reconciled first, so
        /// upgraded troops and confirmed battle losses are reflected before
        /// the requested limit is evaluated.
        /// </summary>
        /// <param name="servingParty">Party currently carrying the batch.</param>
        /// <param name="sourceFief">Authoritative source recorded for it.</param>
        /// <param name="batchId">Stable ID of the batch to reduce.</param>
        /// <param name="maximumCount">Maximum soldiers allowed to return.</param>
        /// <returns>Actual committed result; rejected calls have no return side effects.</returns>
        public FiefReturnResult TryReturnFiefTroops(
            MobileParty servingParty,
            Settlement sourceFief,
            string batchId,
            int maximumCount)
        {
            return FiefReturnService.TryReturn(
                this, servingParty, sourceFief, batchId, maximumCount);
        }

        /// <summary>
        /// Writes one auditable result for both successful and rejected return
        /// attempts without displaying a diagnostic UI message.
        /// </summary>
        internal static void LogFiefReturn(
            MobileParty party,
            FiefReturnResult result,
            int remainingBatch,
            int cooldownWeeks)
        {
            ModLogger.Notice(string.Format(
                "Fief return: party={0}, source={1}, batch={2}, " +
                "requested={3}, returned={4}, remainingBatch={5}, " +
                "restoredHearth={6}, cooldownWeeks={7}, reason={8}",
                party?.StringId ?? "unknown",
                result?.SourceFief?.StringId ?? "unknown",
                result?.BatchId ?? "unknown",
                result?.RequestedCount ?? 0,
                result?.ActualReturnedCount ?? 0,
                Math.Max(0, remainingBatch),
                result?.ActualRestoredHearth ?? 0,
                Math.Max(0, cooldownWeeks),
                result?.ReasonCode ?? "unknown"));
        }

        public void ReconcileServingPartiesForClan(Clan clan)
        {
            if (clan == null) return;
            foreach (MobileParty party in _servingDetachmentIndex.Keys
                .Where(p => p != null && p.ActualClan == clan).ToList())
            {
                AdvanceServiceDays(party);
                ReconcilePartyFiefTroops(party);
            }
        }

        public Dictionary<CharacterObject, int> GetExemptTroopCounts(MobileParty party)
        {
            Dictionary<CharacterObject, int> result = new();
            foreach (ServingDetachmentRef item in GetServingDetachments(party))
            {
                FiefServiceBatch batch = item.Detachment;
                if (batch.ServiceDays >= batch.FreeWageDays) continue;
                foreach (var troop in batch.Troops)
                {
                    if (troop.Key == null || troop.Value <= 0) continue;
                    result[troop.Key] = result.TryGetValue(troop.Key, out int old)
                        ? old + troop.Value
                        : troop.Value;
                }
            }
            return result;
        }

        /// <summary>
        /// Returns every currently serving fief troop by character type,
        /// regardless of whether its temporary wage exemption has expired.
        /// </summary>
        public Dictionary<CharacterObject, int> GetServingTroopCounts(
            MobileParty party)
        {
            Dictionary<CharacterObject, int> result = new();
            foreach (ServingDetachmentRef item in GetServingDetachments(party))
            {
                foreach (var troop in item.Detachment.Troops)
                {
                    if (troop.Key == null || troop.Value <= 0)
                    {
                        continue;
                    }
                    result[troop.Key] = result.TryGetValue(
                        troop.Key,
                        out int existing)
                            ? existing + troop.Value
                            : troop.Value;
                }
            }
            return result;
        }

        public void TransferFiefService(
            MobileParty sourceParty,
            MobileParty targetParty,
            IReadOnlyDictionary<CharacterObject, int> transferredTroops)
        {
            if (sourceParty == null || targetParty == null || transferredTroops == null) return;
            Dictionary<CharacterObject, int> remaining = transferredTroops
                .Where(p => p.Key != null && p.Value > 0)
                .ToDictionary(p => p.Key, p => p.Value);
            foreach (ServingDetachmentRef item in GetServingDetachments(sourceParty)
                .OrderBy(p => p.Detachment.BatchId, StringComparer.Ordinal).ToList())
            {
                Dictionary<CharacterObject, int> moved = new();
                foreach (CharacterObject troop in item.Detachment.Troops.Keys.ToList())
                {
                    if (!remaining.TryGetValue(troop, out int wanted) || wanted <= 0) continue;
                    int count = Math.Min(wanted, item.Detachment.Troops[troop]);
                    if (count <= 0) continue;
                    item.Detachment.Troops[troop] -= count;
                    if (item.Detachment.Troops[troop] <= 0) item.Detachment.Troops.Remove(troop);
                    remaining[troop] -= count;
                    moved[troop] = count;
                }
                if (moved.Count > 0)
                {
                    FiefServiceBatch split = item.Detachment.SplitTo(targetParty, moved);
                    _fiefDataMap[item.SourceFief].ServiceBatches.Add(split);
                    RegisterServingDetachment(item.SourceFief, split);
                }
                if (item.Detachment.IsEmpty())
                {
                    _fiefDataMap[item.SourceFief].ServiceBatches.Remove(item.Detachment);
                    UnregisterServingDetachment(item.SourceFief, item.Detachment);
                }
                _fiefDataMap[item.SourceFief].RefreshManpowerCounters();
            }
        }

        /// <summary>
        /// Finalizes every service identity still attached to a party that is
        /// about to be irreversibly destroyed. These soldiers are recorded as
        /// losses: mobilization markers are released without restoring Hearth.
        /// Normal returns must always use <see cref="TryReturnFiefTroops"/>.
        /// </summary>
        public int SettleRemainingFiefServiceAsLoss(MobileParty party)
        {
            if (party == null) return 0;
            int lostTotal = 0;
            foreach (ServingDetachmentRef item in GetServingDetachments(party)
                .ToList())
            {
                int lost = item.Detachment?.GetTotalCount() ?? 0;
                if (lost > 0)
                {
                    ReleaseFiefService(item.SourceFief, lost, false);
                    lostTotal += lost;
                }
                GetFiefData(item.SourceFief)?.ServiceBatches
                    .Remove(item.Detachment);
                GetFiefData(item.SourceFief)?.RefreshManpowerCounters();
                UnregisterServingDetachment(item.SourceFief, item.Detachment);
            }
            return lostTotal;
        }

        private void OnPartyRemoved(PartyBase partyBase)
        {
            MobileParty party = partyBase?.IsMobile == true
                ? partyBase.MobileParty
                : null;
            if (party == null) return;
            foreach (ServingDetachmentRef item in GetServingDetachments(party))
            {
                int lost = item.Detachment.GetTotalCount();
                if (lost > 0) ReleaseFiefService(item.SourceFief, lost, false);
                GetFiefData(item.SourceFief)?.ServiceBatches
                    .Remove(item.Detachment);
                GetFiefData(item.SourceFief)?.RefreshManpowerCounters();
                UnregisterServingDetachment(item.SourceFief, item.Detachment);
            }
        }

        /// <summary>
        /// Retrieves fief data for a specific settlement.
        /// </summary>
        public FiefPartyData GetFiefData(Settlement settlement)
        {
            return _fiefDataMap.TryGetValue(settlement, out var data) ? data : null;
        }

        /// <summary>
        /// Gets all fief data mappings.
        /// </summary>
        public Dictionary<Settlement, FiefPartyData> GetAllFiefData()
        {
            return _fiefDataMap;
        }

        /// <summary>
        /// Returns the combined troop roster of all fief squads in a settlement.
        /// </summary>
        public TroopRoster GetFiefTroopRoster(Settlement settlement)
        {
            if (settlement == null) return TroopRoster.CreateDummyTroopRoster();
            if (_fiefDataMap.TryGetValue(settlement, out FiefPartyData data) && data != null)
            {
                return data.GetFiefTroopRoster();
            }
            return TroopRoster.CreateDummyTroopRoster();
        }

        /// <summary>
        /// Recruits all available fief squads from a settlement into the target mobile party.
        /// </summary>
        public int RecruitFiefTroopsFromSettlement(
            Settlement settlement,
            MobileParty targetParty,
            int maximumCount = int.MaxValue)
        {
            if (settlement == null || targetParty == null)
            {
                ModLogger.Error("[Fief] Recruitment failed: invalid parameters.");
                return 0;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.RecruitTroopsToParty(targetParty, maximumCount);
            }
            return 0;
        }

        /// <summary>
        /// Returns template-weighted fief offers for a pure exchange preview.
        /// </summary>
        public IReadOnlyList<RecruitmentCandidate>
            PreviewFiefRecruitmentCandidates(
                Settlement settlement,
                MobileParty targetParty,
                int maximumCount)
        {
            return settlement != null && targetParty != null &&
                _fiefDataMap.TryGetValue(settlement, out FiefPartyData data) &&
                data != null
                    ? data.PreviewFiefRecruitmentCandidates(
                        targetParty,
                        maximumCount)
                    : Array.Empty<RecruitmentCandidate>();
        }

        /// <summary>Returns the exact approved manifest without committing it.</summary>
        public IReadOnlyDictionary<CharacterObject, int>
            PreviewFiefRecruitmentApprovals(
                Settlement settlement,
                MobileParty targetParty,
                int maximumCount)
        {
            return settlement != null && targetParty != null &&
                _fiefDataMap.TryGetValue(settlement, out FiefPartyData data) &&
                data != null
                    ? data.PreviewFiefRecruitmentApprovals(
                        targetParty,
                        maximumCount)
                    : new Dictionary<CharacterObject, int>();
        }

        /// <summary>
        /// Disbands all fief squads in a settlement.
        /// Optionally recovers troops from a mobile party to refill squads.
        /// </summary>
        public void ReturnTroopsToSettlement(Settlement settlement, MobileParty mobileParty = null)
        {
            if (settlement == null)
            {
                ModLogger.Error("[Fief] Disband failed: settlement is null.");
                return;
            }
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                data.ReturnTroopsToSettlement(mobileParty);
            }
        }

        public int ReturnTroopsToSettlementWithResult(
            Settlement settlement,
            MobileParty mobileParty = null)
        {
            if (settlement == null ||
                !_fiefDataMap.TryGetValue(settlement, out var data) ||
                data == null)
            {
                return 0;
            }

            return data.ReturnTroopsToSettlement(mobileParty);
        }

        /// <summary>
        /// 获取指定封邑的部队最大人数。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>
        public int GetFiefTroopLimit(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetFiefTroopLimit();
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑当前可招募的军队人数（即就绪状态的健康士兵总数）。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>可招募人数，若封邑无效或非玩家所有则返回 0</returns>
        public int GetAvailableTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetReadyFiefTroopCount();
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑当前已被征召的士兵人数。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>已被征召人数，若无征召或封邑无效则返回 0</returns>
        public int GetRecruitedTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                return data.GetRecruitedFiefTroopCount();
            }
            return 0;
        }

        /// <summary>
        /// Gets the number of returned soldiers still waiting for cooldown.
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>解散期士兵总数，若无或封邑无效则返回 0</returns>
        public int GetWaitCycleTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
            {
                int total = 0;
                if (data.ReturnBatches != null)
                {
                    foreach (var detachment in data.ReturnBatches)
                    {
                        total += detachment.GetTotalCount();
                    }
                }
                return total;
            }
            return 0;
        }

        /// <summary>
        /// 获取指定封邑士兵的实际数量
        /// </summary>
        public int GetFiefTroopCount(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (_fiefDataMap.TryGetValue(settlement, out var data) && data != null)
                return data.GetTotalTroopCount();
            return 0;
        }

        /// <summary>
        /// 监听围城结束事件
        /// </summary>
        private void OnAfterSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool isWin, MapEvent.BattleTypes battleType)
        {
            if (battleType != MapEvent.BattleTypes.Siege && battleType != MapEvent.BattleTypes.SallyOut)
                return;

            if (!_fiefDataMap.TryGetValue(siegeSettlement, out var fiefData) || fiefData == null)
                return;

            fiefData.OnSiegeCompleted(isWin);
        }

        /// <summary>
        /// 监听定居点所有者家族变化事件
        /// </summary>
        private void OnSettlementOwnerChanged(Settlement settlement, bool openToClaim, Hero newOwner, Hero previousOwner, Hero capturerHero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement == null) return;

            if (!_fiefDataMap.TryGetValue(settlement, out var fiefData) || fiefData == null)
                return;

            fiefData.OnSettlementOwnerChanged();
        }

        public void ManualRecruitFromFief(Settlement settlement, TroopRoster selectedRoster, MobileParty targetParty)
        {
            if (settlement == null || selectedRoster == null)
                return;

            if (_fiefDataMap.TryGetValue(settlement, out var fiefData) && fiefData != null)
            {
                fiefData.RecruitManualSelection(selectedRoster, targetParty);
            }
        }

        /// <summary>
        /// 监听招募事件，扣除定居点繁荣度/户数。
        ///
        /// 优先使用对应文化的 FiefPartyTemplate 中的
        /// ProsperityCostPerTroop / HearthCostPerTroop。
        ///
        /// 若无模板则使用默认 fallback 值。
        /// </summary>
        ///
        private void OnTroopRecruited(
            Hero recruiter,
            Settlement settlement,
            Hero recruitmentSource,
            CharacterObject troop,
            int count)
        {
            // 安全检查
            if (settlement == null || count <= 0)
                return;

            if (troop.Occupation != Occupation.Soldier)
                return;

            // ============================================================
            // 从已有的 FiefPartyData 获取 template，
            // 以读取 ProsperityCostPerTroop / HearthCostPerTroop。
            //
            // 若无模板，则不扣除繁荣度/户数。
            // ============================================================
            // Professional village recruits permanently consume two hearths.
            // They do not create a mobilized-hearth liability.
            if (settlement.IsVillage || settlement.IsTown || settlement.IsCastle)
            {
                bool accepted = TryConsumeProfessionalHearth(settlement, count);
                int rejected = accepted ? 0 : count;

                // The native event is raised after recruitment. Roll back only
                // unsupported recruitment so the 100-hearth floor remains real.
                if (rejected > 0 && recruiter?.PartyBelongedTo != null)
                {
                    TroopRoster roster = recruiter.PartyBelongedTo.MemberRoster;
                    int removable = Math.Min(rejected, roster.GetElementNumber(troop));
                    if (removable > 0)
                        roster.RemoveTroop(troop, removable, default(UniqueTroopDescriptor), 0);
                }
            }
        }
    }
}
