using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.Recruitment.Preferences.Models;
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
            base.AddClassDefinition(typeof(SettlementProfessionalPreferenceRecord), 11);
            base.AddClassDefinition(typeof(VillageFiefData), 12);
        }

        protected override void DefineContainerDefinitions()
        {
            base.ConstructContainerDefinition(typeof(Dictionary<CharacterObject, int>));
            base.ConstructContainerDefinition(typeof(List<FiefTroopDetachment>));
            base.ConstructContainerDefinition(typeof(List<FiefServiceBatch>));
            base.ConstructContainerDefinition(typeof(List<FiefReturnBatch>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, FiefPartyData>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, VillageFiefData>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, int>));
            base.ConstructContainerDefinition(typeof(Dictionary<Settlement, Settlement>));
            base.ConstructContainerDefinition(
                typeof(Dictionary<Settlement, SettlementManpowerProductionData>));

            base.ConstructContainerDefinition(typeof(List<FiefWageExemption>));
            base.ConstructContainerDefinition(typeof(Dictionary<String, List<FiefWageExemption>>));
            base.ConstructContainerDefinition(typeof(List<ReinforcementMission>));
            base.ConstructContainerDefinition(
                typeof(Dictionary<string, PartySettlementBindingRecord>));
            base.ConstructContainerDefinition(
                typeof(Dictionary<string, SettlementProfessionalPreferenceRecord>));
            base.ConstructContainerDefinition(typeof(Dictionary<string, int>));
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

        private readonly Dictionary<MobileParty, List<ServingDetachmentRef>>
            _servingDetachmentIndex = new Dictionary<MobileParty, List<ServingDetachmentRef>>();
        private readonly HashSet<MobileParty> _blockedServiceParties =
            new HashSet<MobileParty>();
        private readonly HashSet<Settlement> _runtimeReadinessFailuresLogged =
            new HashSet<Settlement>();

        public bool IsSessionReady { get; private set; }

        private FiefServiceLedger ServiceLedger =>
            new FiefServiceLedger(
                _servingDetachmentIndex, _blockedServiceParties);

        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, MapEvent.BattleTypes>(this.OnAfterSiegeCompleted));
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, new Action<Settlement, bool, Hero, Hero, Hero, ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail>(this.OnSettlementOwnerChanged));
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, new Action<Hero, Settlement, Hero, CharacterObject, int>(this.OnTroopRecruited));
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(this, OnPartyRemoved);
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(
                this, OnMobilePartyCreated);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(
                this, OnMapEventEnded);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData<Dictionary<Settlement, FiefPartyData>>("_fiefDataMap", ref this._fiefDataMap);

            if (_fiefDataMap == null)
            {
                _fiefDataMap = new Dictionary<Settlement, FiefPartyData>();
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            IsSessionReady = false;
            _runtimeReadinessFailuresLogged.Clear();
            FiefPartyTemplateManager.Instance.InitAllEnableTroops();
            InitializeFiefData();
            RemoveInvalidPersistentFeudalTroops();
            if (CampaignState.IsNewGame)
                InitializeNewGameFiefTroops();
            RebuildServingDetachmentIndex();
            ReconcileMobilization();
            IsSessionReady = true;
            ModLogger.Info(string.Format(
                "Fief runtime session ready: fiefs={0}.",
                _fiefDataMap.Values.Count(data =>
                    data?.IsRuntimeReady == true)));
        }

        private void RemoveInvalidPersistentFeudalTroops()
        {
            int repairedVillages = 0;
            int servingRemoved = 0;
            int coolingRemoved = 0;
            foreach (FiefPartyData fief in _fiefDataMap.Values
                .Where(data => data?.IsRuntimeReady == true))
            {
                FiefPartyTemplate template = fief.GetFiefPartyTemplate();
                CulturalMilitaryDoctrine doctrine = MilitaryConfigManager
                    .Instance.RequireActiveConfig().GetDoctrine(
                        fief.GetSettlement());
                foreach (VillageFiefData village in fief.OrderedVillages())
                {
                    bool IsValid(CharacterObject troop) => troop != null &&
                        template?.IsEnableTroop(troop) == true &&
                        doctrine.TryGetFeudalSupplyCost(troop, out _);
                    List<(FiefServiceBatch Batch, CharacterObject Troop,
                        int Count)> invalidServing = village.ServiceBatches
                        .Where(batch => batch != null)
                        .SelectMany(batch => batch.Troops.Where(pair =>
                            pair.Key != null && pair.Value > 0 &&
                            !IsValid(pair.Key)).Select(pair =>
                                (batch, pair.Key, pair.Value))).ToList();
                    List<(FiefReturnBatch Batch, CharacterObject Troop,
                        int Count)> invalidCooling = village.ReturnBatches
                        .Where(batch => batch != null)
                        .SelectMany(batch => batch.Troops.Where(pair =>
                            pair.Key != null && pair.Value > 0 &&
                            !IsValid(pair.Key)).Select(pair =>
                                (batch, pair.Key, pair.Value))).ToList();
                    bool legacyInvalidRecovery = village.RecoveryRequired &&
                        (village.RecoveryReason == "InvalidFeudalTroopCost" ||
                         village.RecoveryReason ==
                            "InvalidFeudalTroopRepairBlocked" ||
                         village.RecoveryReason ==
                            "InvalidFeudalTroopRepairFailed");
                    if (invalidServing.Count == 0 &&
                        invalidCooling.Count == 0)
                    {
                        if (legacyInvalidRecovery)
                            village.CompleteRecovery();
                        continue;
                    }

                    int servingCount = invalidServing.Sum(item => item.Count);
                    foreach (var group in invalidServing.GroupBy(
                        item => item.Batch))
                    {
                        Dictionary<CharacterObject, int> after = group.Key
                            .Troops.ToDictionary(pair => pair.Key,
                                pair => pair.Value);
                        foreach (var item in group)
                        {
                            ModLogger.Warn(string.Format(
                                "Invalid feudal troop removed: source=Serving villageId={0} troopId={1} tier={2} count={3} batchId={4} servingPartyId={5} reasonCode=InvalidFeudalTroop.",
                                village.VillageSettlement?.StringId ??
                                "unknown", item.Troop.StringId,
                                item.Troop.Tier, item.Count,
                                item.Batch.BatchId ?? "none",
                                item.Batch.ServingParty?.StringId ?? "none"));
                            after.Remove(item.Troop);
                        }
                        group.Key.ReplaceTroops(after);
                        if (group.Key.IsEmpty())
                        {
                            village.ServiceBatches.Remove(group.Key);
                            UnregisterServingDetachment(group.Key.SourceFief,
                                village.VillageSettlement, group.Key);
                        }
                    }
                    foreach (var group in invalidCooling.GroupBy(
                        item => item.Batch))
                    {
                        Dictionary<CharacterObject, int> after = group.Key
                            .Troops.ToDictionary(pair => pair.Key,
                                pair => pair.Value);
                        foreach (var item in group)
                        {
                            ModLogger.Warn(string.Format(
                                "Invalid feudal troop removed: source=Cooling villageId={0} troopId={1} tier={2} count={3} waitWeeks={4} reasonCode=InvalidFeudalTroop.",
                                village.VillageSettlement?.StringId ??
                                "unknown", item.Troop.StringId,
                                item.Troop.Tier, item.Count,
                                item.Batch.WaitWeeks));
                            after.Remove(item.Troop);
                        }
                        group.Key.ReplaceTroops(after);
                        if (group.Key.GetTotalCount() <= 0)
                            village.ReturnBatches.Remove(group.Key);
                    }
                    int released = village.RemoveInvalidMobilization(
                        servingCount, true);
                    if (released != servingCount)
                        ModLogger.Warn(string.Format(
                            "Invalid feudal troop mobilization mismatch: villageId={0} requestedRelease={1} actualRelease={2} reasonCode=InvalidFeudalTroopMobilizationShortfall.",
                            village.VillageSettlement?.StringId ?? "unknown",
                            servingCount, released));
                    if (legacyInvalidRecovery)
                        village.CompleteRecovery();
                    repairedVillages++;
                    servingRemoved += servingCount;
                    coolingRemoved += invalidCooling.Sum(item => item.Count);
                }
            }
            if (repairedVillages > 0)
                ModLogger.Warn(string.Format(
                    "Invalid feudal troop cleanup completed: villages={0}, servingRemoved={1}, coolingRemoved={2}.",
                    repairedVillages, servingRemoved, coolingRemoved));
        }

        private void InitializeNewGameFiefTroops()
        {
            int produced = 0;
            int initialized = 0;
            foreach (FiefPartyData data in _fiefDataMap.Values
                .Where(data => data?.GetSettlement()?.IsFortification == true)
                .OrderBy(data => data.GetSettlement().StringId))
            {
                if (!data.IsRuntimeReady) continue;
                int added = data.InitializeFiefTroopsByTemplateWeights();
                if (added <= 0) continue;
                produced += added;
                initialized++;
            }
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_fief_new_game_initialized");
            message.SetTextVariable("FIEF_COUNT", initialized);
            message.SetTextVariable("TROOP_COUNT", produced);
            ModLogger.Info(message.ToString());
        }

        /// <summary>
        /// Initializes fief data for all player-owned villages, castles, and towns.
        /// Also restores saved squad compositions from loaded data.
        /// </summary>
        public void InitializeFiefData()
        {
            int expected = 0;
            int initialized = 0;
            int failed = 0;
            foreach (var settlement in Settlement.All)
            {
                //if (settlement.OwnerClan != Clan.PlayerClan) continue;
                if (!settlement.IsCastle && !settlement.IsTown) continue;
                expected++;
                FiefPartyData data;
                if (_fiefDataMap.TryGetValue(settlement, out data) && data != null)
                {
                    // Reuse the deserialized authoritative data container.
                }
                else
                {
                    data = new FiefPartyData(settlement);
                    _fiefDataMap[settlement] = data;
                }
                bool ready;
                try
                {
                    ready = data.InitialFeifPartyData();
                }
                catch (Exception exception)
                {
                    string reason = "InitializationException:" +
                        exception.GetType().Name;
                    data.MarkRuntimeInitializationFailed(reason);
                    failed++;
                    ModLogger.Error(string.Format(
                        "Fief runtime initialization failed: fief={0}, culture={1}, expectedTemplate={2}, reason={3}, exception={4}.",
                        settlement.StringId,
                        settlement.Culture?.StringId ?? "unknown",
                        data.ExpectedTemplateId,
                        reason,
                        exception));
                    continue;
                }
                if (ready)
                {
                    initialized++;
                    continue;
                }
                failed++;
                ModLogger.Error(string.Format(
                    "Fief runtime initialization failed: fief={0}, culture={1}, expectedTemplate={2}, reason={3}.",
                    settlement.StringId,
                    settlement.Culture?.StringId ?? "unknown",
                    data.ExpectedTemplateId,
                    data.RuntimeInitializationFailureReason ?? "unknown"));
            }
            TextObject msgComplete = GameTexts.FindText("str_modifiedarmy_fief_init_complete");
            msgComplete.SetTextVariable("COUNT", initialized);
            ModLogger.Info(msgComplete.ToString());
            ModLogger.Info(string.Format(
                "Fief runtime initialization summary: expected={0}, initialized={1}, failed={2}.",
                expected, initialized, failed));
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
            ReconcileMobilization();
            foreach (MobileParty party in _servingDetachmentIndex.Keys.ToList())
            {
                AdvanceServiceDays(party);
                ReconcilePartyFiefTroops(party);
            }
        }

        private void ReconcileMobilization()
        {
            foreach (FiefPartyData fief in _fiefDataMap.Values)
            foreach (VillageFiefData village in fief?.Villages?.Values ??
                Enumerable.Empty<VillageFiefData>())
            {
                int serving = village.GetServingFiefTroopCount(
                    fief.GetFiefPartyTemplate());
                if (village.MobilizedHearth != serving)
                {
                    bool newlyRequired = !village.RecoveryRequired;
                    village.RequireRecovery();
                    if (newlyRequired)
                        ModLogger.Error(string.Format(
                            "Fief mobilization mismatch: village={0}, tracked={1}, serving={2}. Dangerous operations remain rejected.",
                            village.VillageSettlement?.StringId ?? "unknown",
                            village.MobilizedHearth, serving));
                }
            }
        }

        internal bool ValidateVillageFiefState(Settlement settlement,
            out string reason)
        {
            reason = null;
            VillageFiefData village = GetVillageFiefData(settlement);
            Settlement sourceFief = settlement?.Village?.Bound;
            if (village == null || sourceFief == null ||
                village.VillageSettlement != settlement ||
                GetFiefData(sourceFief)?.IsCurrentVillage(settlement) != true)
            {
                reason = "InvalidVillageBinding";
                return false;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (FiefPartyData fief in _fiefDataMap.Values)
            foreach (VillageFiefData owner in fief?.Villages?.Values ??
                Enumerable.Empty<VillageFiefData>())
            foreach (FiefServiceBatch batch in owner?.ServiceBatches ??
                Enumerable.Empty<FiefServiceBatch>())
            {
                if (batch == null || string.IsNullOrEmpty(batch.BatchId) ||
                    !ids.Add(batch.BatchId))
                {
                    reason = "DuplicateOrMissingBatchId";
                    return false;
                }
                if (owner == village && (batch.SourceFief != sourceFief ||
                    batch.SourceVillage != settlement || batch.IsEmpty()))
                {
                    reason = "InvalidBatchSource";
                    return false;
                }
            }
            foreach (FiefServiceBatch batch in village.ServiceBatches)
            {
                batch.ResolveServingParty();
                if (batch.ServingParty == null ||
                    batch.GetTotalCount() <= 0)
                {
                    reason = "MissingServingParty";
                    return false;
                }
                foreach (ServingDetachmentRef shared in
                    GetServingDetachments(batch.ServingParty))
                {
                    VillageFiefData sharedVillage = GetVillageFiefData(
                        shared.SourceVillage);
                    if (sharedVillage != village &&
                        sharedVillage?.RecoveryRequired == true)
                    {
                        reason = "SharedPartyRecoveryRequired";
                        return false;
                    }
                }
            }
            if (village.MobilizedHearth !=
                village.GetServingFiefTroopCount(
                    GetFiefData(sourceFief).GetFiefPartyTemplate()))
            {
                reason = "MobilizationMismatch";
                return false;
            }
            return true;
        }

        internal bool TryCompleteVillageRecovery(Settlement settlement,
            string expectedOperationId, out string reason)
        {
            VillageFiefData village = GetVillageFiefData(settlement);
            if (village == null || !village.RecoveryRequired)
            {
                reason = "RecoveryNotRequired";
                return false;
            }
            if (!string.IsNullOrEmpty(village.RecoveryOperationId) &&
                !string.Equals(village.RecoveryOperationId,
                    expectedOperationId, StringComparison.Ordinal))
            {
                reason = "RecoveryEvidenceMismatch";
                return false;
            }
            RebuildServingDetachmentIndex();
            if (!ValidateVillageFiefState(settlement, out reason))
                return false;
            village.CompleteRecovery();
            RebuildServingDetachmentIndex();
            ModLogger.Info(string.Format(
                "Fief village recovery completed: village={0}, operation={1}.",
                settlement.StringId, expectedOperationId ?? "none"));
            return true;
        }

        public int GetMobilizedHearth(Village village)
        {
            return GetVillageFiefData(village?.Settlement)?.MobilizedHearth ?? 0;
        }

        public int GetOccupiedSupplyHearth(Settlement settlement)
        {
            FiefPartyData fief = GetFiefData(settlement?.IsVillage == true
                ? settlement.Village?.Bound : settlement);
            if (fief == null) return 0;
            return settlement.IsVillage
                ? fief.GetVillageData(settlement)?.GetOccupiedSupplyHearth(
                    fief.GetFiefPartyTemplate(),
                    MilitaryConfigManager.Instance.RequireActiveConfig()
                        .GetDoctrine(settlement.Village.Bound)) ?? 0
                : fief.GetTotalOccupiedSupplyHearth();
        }

        public int GetFreeSupplyHearth(Settlement settlement)
        {
            FiefPartyData fief = GetFiefData(settlement?.IsVillage == true
                ? settlement.Village?.Bound : settlement);
            if (fief == null) return 0;
            return settlement.IsVillage
                ? fief.GetVillageData(settlement)?.GetFreeSupplyHearth(
                    fief.GetFiefPartyTemplate(),
                    MilitaryConfigManager.Instance.RequireActiveConfig()
                        .GetDoctrine(settlement.Village.Bound)) ?? 0
                : fief.GetTotalFreeSupplyHearth();
        }

        public int GetEffectiveSupplyHearth(Settlement settlement)
        {
            FiefPartyData fief = GetFiefData(settlement?.IsVillage == true
                ? settlement.Village?.Bound : settlement);
            if (fief == null) return 0;
            return settlement.IsVillage
                ? fief.GetVillageData(settlement)?
                    .GetEffectiveSupplyHearth() ?? 0
                : fief.GetTotalEffectiveSupplyHearth();
        }

        public int GetSupplyDeficitHearth(Settlement settlement)
        {
            FiefPartyData fief = GetFiefData(settlement?.IsVillage == true
                ? settlement.Village?.Bound : settlement);
            if (fief == null) return 0;
            return settlement.IsVillage
                ? fief.GetVillageData(settlement)?.GetSupplyDeficitHearth(
                    fief.GetFiefPartyTemplate(),
                    MilitaryConfigManager.Instance.RequireActiveConfig()
                        .GetDoctrine(settlement.Village.Bound)) ?? 0
                : fief.GetTotalSupplyDeficitHearth();
        }

        public int GetActualVillageHearth(Settlement settlement)
        {
            if (settlement == null) return 0;
            if (settlement.IsVillage)
                return Math.Max(0, (int)Math.Floor(
                    settlement.Village?.Hearth ?? 0f));
            return settlement.BoundVillages.Where(village =>
                    village?.Settlement != null)
                .Sum(village => Math.Max(0,
                    (int)Math.Floor(village.Hearth)));
        }

        public float GetFeudalTaxReliefAmount(Settlement fief)
        {
            float wageRatio = MilitaryConfigManager.Instance
                .RequireActiveConfig().HearthEconomy
                .FeudalTaxReliefWageRatio;
            if (fief?.IsFortification != true || wageRatio == 0f)
                return 0f;
            FiefPartyData data = GetFiefData(fief);
            return Math.Max(0f,
                data?.CalculateFeudalTaxReliefAmount(wageRatio) ?? 0f);
        }

        public bool TryConvertFiefTroops(Settlement fief,
            Settlement sourceVillage, CharacterObject sourceTroop, int count,
            FiefConversionDirection direction, out int converted,
            out string reason) => FiefTroopConversionService.TryConvert(
                this, fief, sourceVillage, sourceTroop, count, direction,
                out converted, out reason);

        public FiefTroopSnapshot QueryFeudalTroops(Settlement settlement,
            FiefTroopState states = FiefTroopState.All,
            bool excludeRecovery = false)
        {
            Settlement fiefSettlement = settlement?.IsVillage == true
                ? settlement.Village?.Bound : settlement;
            FiefPartyData data = GetFiefData(fiefSettlement);
            return data == null
                ? new FiefTroopSnapshot(states,
                    new Dictionary<CharacterObject, FiefTroopCount>())
                : data.QueryFeudalTroops(
                    settlement?.IsVillage == true ? settlement : null,
                    states, excludeRecovery);
        }

        /// <summary>
        /// Reserves rural production capacity before any troop roster is changed.
        /// The deduction is distributed evenly while preserving the recruitment floor.
        /// </summary>
        internal bool TryMobilizeVillage(Settlement village, int count)
        {
            return GetVillageFiefData(village)?.TryMobilize(count) == true;
        }

        public bool TryConsumeProfessionalHearth(Settlement source,
            CharacterObject troop, int count)
        {
            return TryConsumeProfessionalHearth(
                source, troop, count, out Dictionary<Settlement, int> _);
        }

        internal bool TryConsumeProfessionalHearth(
            Settlement source,
            CharacterObject troop,
            int count,
            out Dictionary<Settlement, int> consumedByVillage)
        {
            consumedByVillage = new Dictionary<Settlement, int>();
            if (source == null || troop == null || count <= 0) return false;
            int costPerTroop;
            int required;
            try
            {
                costPerTroop = MilitaryConfigManager.Instance
                    .RequireActiveConfig().HearthEconomy
                    .GetProfessionalRecruitmentCost(troop);
                required = checked(count * costPerTroop);
            }
            catch (Exception exception)
            {
                ModLogger.Error(string.Format(
                    "Professional Hearth cost rejected: source={0}, troop={1}, tier={2}, count={3}, reason={4}.",
                    source.StringId, troop.StringId, troop.Tier, count,
                    exception.Message));
                return false;
            }
            List<Village> villages = source.IsVillage
                ? new List<Village> { source.Village }
                : source.BoundVillages.Where(v => v?.Settlement != null).ToList();
            villages = villages
                .Where(v => v?.VillageState == Village.VillageStates.Normal)
                .OrderBy(v => v.Settlement.StringId)
                .ToList();
            if (villages.Count == 0) return false;
            var capacities = new Dictionary<Village, int>();
            foreach (Village village in villages)
            {
                VillageFiefData villageData = GetVillageFiefData(
                    village.Settlement);
                FiefPartyData fief = GetFiefData(village.Settlement
                    .Village?.Bound);
                if (villageData == null || fief == null ||
                    villageData.RecoveryRequired) continue;
                try
                {
                    int floorCapacity = Math.Max(0,
                        (int)Math.Floor(village.Hearth - 100f));
                    int supplyCapacity = Math.Max(0,
                        villageData.GetFreeSupplyHearth(
                            fief.GetFiefPartyTemplate(),
                            MilitaryConfigManager.Instance
                                .RequireActiveConfig().GetDoctrine(
                                    village.Settlement.Village.Bound)));
                    capacities[village] = Math.Min(
                        floorCapacity, supplyCapacity);
                }
                catch (Exception exception)
                {
                    ModLogger.Error(string.Format(
                        "Professional Hearth capacity rejected: village={0}, troop={1}, reason={2}.",
                        village.Settlement.StringId, troop.StringId,
                        exception.Message));
                    return false;
                }
            }
            if (capacities.Values.Sum(value => (long)value) < required)
                return false;
            int remaining = required;
            while (remaining > 0)
            {
                foreach (Village village in villages)
                {
                    if (remaining <= 0) break;
                    if (!capacities.TryGetValue(village, out int available) ||
                        available <= 0) continue;
                    consumedByVillage[village.Settlement] =
                        consumedByVillage.TryGetValue(
                            village.Settlement, out int consumed)
                                ? consumed + 1
                                : 1;
                    capacities[village] = available - 1;
                    remaining--;
                }
            }
            var applied = new Dictionary<Settlement, int>();
            try
            {
                foreach (var pair in consumedByVillage)
                {
                    pair.Key.Village.Hearth -= pair.Value;
                    applied[pair.Key] = pair.Value;
                }
                foreach (Settlement village in consumedByVillage.Keys)
                {
                    VillageFiefData data = GetVillageFiefData(village);
                    FiefPartyData fief = GetFiefData(village.Village?.Bound);
                    if (village.Village.Hearth < 100f || data == null ||
                        fief == null || data.GetFreeSupplyHearth(
                            fief.GetFiefPartyTemplate(),
                            MilitaryConfigManager.Instance
                                .RequireActiveConfig().GetDoctrine(
                                    village.Village.Bound)) < 0)
                        throw new InvalidOperationException(
                            "ProfessionalHearthPostCommitValidationFailed");
                }
                if (source.OwnerClan == Clan.PlayerClan)
                    ModLogger.Debug(string.Format(
                        "Professional Hearth committed: source={0}, troop={1}, tier={2}, count={3}, unitCost={4}, totalCost={5}, villages={6}.",
                        source.StringId, troop.StringId, troop.Tier, count,
                        costPerTroop, required,
                        string.Join(",", consumedByVillage.OrderBy(pair =>
                            pair.Key.StringId).Select(pair =>
                            pair.Key.StringId + ":" + pair.Value))));
                return true;
            }
            catch (Exception exception)
            {
                foreach (var pair in applied)
                    if (pair.Key?.Village != null)
                        pair.Key.Village.Hearth += pair.Value;
                consumedByVillage.Clear();
                ModLogger.Error(string.Format(
                    "Professional Hearth transaction rolled back: source={0}, troop={1}, count={2}, reason={3}.",
                    source.StringId, troop.StringId, count,
                    exception.Message));
                return false;
            }
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
        internal int ReleaseVillageFiefService(Settlement village, int count,
            bool restoreHearth)
        {
            return GetVillageFiefData(village)?
                .TryReleaseMobilization(count, restoreHearth) == true ? count : 0;
        }

        /// <summary>
        /// Counts only mobilization backed by concrete village Hearth markers.
        /// Legacy untracked deficits are deliberately excluded because they
        /// cannot safely restore Hearth during a normal return.
        /// </summary>
        internal int GetTrackedMobilizationCount(Settlement village)
        {
            return GetVillageFiefData(village)?.MobilizedHearth ?? 0;
        }

        /// <summary>
        /// Releases exactly <paramref name="count"/> tracked Hearth markers
        /// and restores the corresponding village Hearth. The per-village
        /// result is a rollback token for the surrounding return transaction.
        /// No state changes are made when the tracked balance is insufficient.
        /// </summary>
        internal bool TryReleaseMobilizationExact(
            Settlement village,
            int count,
            out Dictionary<Settlement, int> releasedByVillage)
        {
            releasedByVillage = new Dictionary<Settlement, int>();
            VillageFiefData data = GetVillageFiefData(village);
            if (data?.TryReleaseMobilization(count, true) != true) return false;
            releasedByVillage[village] = count;
            return true;
        }

        /// <summary>
        /// Reverses a successful exact mobilization release after a later step
        /// in the same return transaction fails.
        /// </summary>
        internal void RollbackMobilizationRelease(
            Settlement village,
            IReadOnlyDictionary<Settlement, int> releasedByVillage)
        {
            if (releasedByVillage != null &&
                releasedByVillage.TryGetValue(village, out int count))
                GetVillageFiefData(village)?.RollbackMobilization(count, true);
        }

        public void RebuildServingDetachmentIndex()
        {
            ServiceLedger.Rebuild(_fiefDataMap);
        }

        public void RegisterServingDetachment(
            Settlement source,
            Settlement sourceVillage,
            FiefServiceBatch batch)
        {
            ServiceLedger.Register(source, sourceVillage, batch);
        }

        public void UnregisterServingDetachment(
            Settlement source,
            Settlement sourceVillage,
            FiefServiceBatch batch)
        {
            ServiceLedger.Unregister(source, sourceVillage, batch);
        }

        internal bool IsServingDetachmentRegistered(
            Settlement source,
            Settlement sourceVillage,
            FiefServiceBatch batch) =>
            ServiceLedger.Contains(source, sourceVillage, batch);

        internal List<ServingDetachmentRef> GetServingDetachments(MobileParty party)
        {
            return ServiceLedger.Get(party);
        }

        internal bool IsServicePartyBlocked(MobileParty party) =>
            party != null && _blockedServiceParties.Contains(party);

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
                this, servingParty, servingParty?.CurrentSettlement,
                sourceFief, batchId, maximumCount,
                PartyExchangePurpose.NaturalVisit);
        }

        internal FiefReturnResult TryReturnFiefTroops(
            MobileParty servingParty,
            Settlement interactionSettlement,
            Settlement sourceFief,
            string batchId,
            int maximumCount,
            PartyExchangePurpose purpose = PartyExchangePurpose.NaturalVisit,
            IReadOnlyDictionary<CharacterObject, int> requestedTroops = null)
        {
            return FiefReturnService.TryReturn(
                this, servingParty, interactionSettlement, sourceFief,
                batchId, maximumCount, purpose, requestedTroops);
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
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_fief_return_diagnostic");
            message.SetTextVariable("PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("SOURCE_NAME",
                result?.SourceFief?.Name ?? new TextObject(
                    "{=ModifiedArmy_CommonUnknown}Unknown"));
            message.SetTextVariable("BATCH_ID", result?.BatchId ?? "unknown");
            message.SetTextVariable("REQUESTED", result?.RequestedCount ?? 0);
            message.SetTextVariable("RETURNED",
                result?.ActualReturnedCount ?? 0);
            message.SetTextVariable("REMAINING", Math.Max(0, remainingBatch));
            message.SetTextVariable("RESTORED_HEARTH",
                result?.ActualRestoredHearth ?? 0);
            message.SetTextVariable("COOLDOWN_WEEKS",
                Math.Max(0, cooldownWeeks));
            message.SetTextVariable("RESULT",
                GetFiefReturnReasonText(result?.ReasonCode));
            ModLogger.Notice(message.ToString());
        }

        private static TextObject GetFiefReturnReasonText(string reasonCode)
        {
            string id;
            switch (reasonCode)
            {
                case "Completed": id = "ModifiedArmy_FiefReturnCompleted"; break;
                case "PartiallyCompleted": id = "ModifiedArmy_FiefReturnPartial"; break;
                case "InvalidRequest": id = "ModifiedArmy_FiefReturnInvalidRequest"; break;
                case "InvalidReturnLocation": id = "ModifiedArmy_FiefReturnInvalidLocation"; break;
                case "NoEligibleBatch": id = "ModifiedArmy_FiefReturnNoBatch"; break;
                case "RecoveryRequired": id = "ModifiedArmy_FiefReturnRecoveryRequired"; break;
                case "NoTrackedMobilization": id = "ModifiedArmy_FiefReturnNoMobilization"; break;
                case "StateChangedBeforeCommit": id = "ModifiedArmy_FiefReturnStateChanged"; break;
                default:
                    TextObject other = new TextObject(
                        "{=ModifiedArmy_FiefReturnOther}Other result ({REASON_CODE})");
                    other.SetTextVariable("REASON_CODE",
                        reasonCode ?? "unknown");
                    return other;
            }
            return new TextObject("{=" + id + "}" + reasonCode);
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

        public bool CanTransferFiefService(
            MobileParty sourceParty,
            IReadOnlyDictionary<CharacterObject, int> transferredTroops,
            out string reason)
        {
            reason = null;
            if (sourceParty == null || transferredTroops == null)
            {
                reason = "InvalidTransferRequest";
                return false;
            }
            if (IsServicePartyBlocked(sourceParty))
            {
                reason = "ServicePartyBlocked";
                return false;
            }
            List<ServingDetachmentRef> refs = GetServingDetachments(sourceParty);
            foreach (ServingDetachmentRef item in refs)
            {
                VillageFiefData owner = GetVillageFiefData(item.SourceVillage);
                if (owner == null || owner.RecoveryRequired ||
                    item.Detachment == null || item.Detachment.IsEmpty() ||
                    item.Detachment.ServingParty != sourceParty ||
                    item.Detachment.SourceFief != item.SourceFief ||
                    item.Detachment.SourceVillage != item.SourceVillage ||
                    !owner.ServiceBatches.Contains(item.Detachment))
                {
                    owner?.RequireRecovery("ServiceTransferPrecheckFailed");
                    reason = "InvalidServiceIdentity";
                    return false;
                }
            }
            Dictionary<CharacterObject, int> available = GetServingTroopCounts(
                sourceParty);
            foreach (var pair in transferredTroops.Where(pair =>
                pair.Key != null && pair.Value > 0))
            {
                if (!available.TryGetValue(pair.Key, out int count) ||
                    count < pair.Value)
                {
                    reason = "InsufficientServiceIdentity";
                    return false;
                }
            }
            return true;
        }

        public bool TransferFiefService(
            MobileParty sourceParty,
            MobileParty targetParty,
            IReadOnlyDictionary<CharacterObject, int> transferredTroops)
        {
            string reason = targetParty == null ? "InvalidTarget" : null;
            if (reason != null || !CanTransferFiefService(sourceParty,
                transferredTroops, out reason))
            {
                ModLogger.Warn(string.Format(
                    "Fief service transfer rejected: source={0}, target={1}, reason={2}.",
                    sourceParty?.StringId ?? "unknown",
                    targetParty?.StringId ?? "unknown",
                    reason ?? "InvalidTarget"));
                return false;
            }
            Dictionary<CharacterObject, int> remaining = transferredTroops
                .Where(p => p.Key != null && p.Value > 0)
                .ToDictionary(p => p.Key, p => p.Value);
            List<ServingDetachmentRef> sourceRefs = GetServingDetachments(
                sourceParty).OrderBy(p => p.Detachment.BatchId,
                    StringComparer.Ordinal).ToList();
            Dictionary<FiefServiceBatch, Dictionary<CharacterObject, int>>
                snapshots = sourceRefs.ToDictionary(
                    item => item.Detachment,
                    item => item.Detachment.Troops.ToDictionary(
                        pair => pair.Key, pair => pair.Value));
            List<(VillageFiefData Owner, FiefServiceBatch Batch)> added = new();
            try
            {
                foreach (ServingDetachmentRef item in sourceRefs)
                {
                    VillageFiefData owner = GetVillageFiefData(
                        item.SourceVillage);
                    if (owner == null || owner.RecoveryRequired)
                        throw new InvalidOperationException(
                            "ServiceIdentityChanged");
                    Dictionary<CharacterObject, int> moved = new();
                    foreach (CharacterObject troop in item.Detachment.Troops
                        .Keys.ToList())
                    {
                        if (!remaining.TryGetValue(troop, out int wanted) ||
                            wanted <= 0) continue;
                        int count = Math.Min(wanted,
                            item.Detachment.Troops[troop]);
                        if (count <= 0) continue;
                        item.Detachment.Troops[troop] -= count;
                        if (item.Detachment.Troops[troop] <= 0)
                            item.Detachment.Troops.Remove(troop);
                        remaining[troop] -= count;
                        moved[troop] = count;
                    }
                    if (moved.Count > 0)
                    {
                        FiefServiceBatch split = item.Detachment.SplitTo(
                            targetParty, moved);
                        owner.ServiceBatches.Add(split);
                        added.Add((owner, split));
                        RegisterServingDetachment(item.SourceFief,
                            item.SourceVillage, split);
                    }
                    if (item.Detachment.IsEmpty())
                    {
                        owner.ServiceBatches.Remove(item.Detachment);
                        UnregisterServingDetachment(item.SourceFief,
                            item.SourceVillage, item.Detachment);
                    }
                    _fiefDataMap[item.SourceFief]
                        .RefreshManpowerCounters();
                }
                if (remaining.Any(pair => pair.Value > 0))
                    throw new InvalidOperationException(
                        "ServiceTransferCommitMismatch");
                return true;
            }
            catch (Exception exception)
            {
                bool restored = true;
                foreach (var item in added)
                {
                    try { item.Owner.ServiceBatches.Remove(item.Batch); }
                    catch { restored = false; }
                }
                foreach (ServingDetachmentRef item in sourceRefs)
                {
                    try
                    {
                        VillageFiefData owner = GetVillageFiefData(
                            item.SourceVillage);
                        if (!owner.ServiceBatches.Contains(item.Detachment))
                            owner.ServiceBatches.Add(item.Detachment);
                        item.Detachment.ReplaceTroops(
                            snapshots[item.Detachment]);
                    }
                    catch { restored = false; }
                }
                RebuildServingDetachmentIndex();
                restored = restored && sourceRefs.All(item =>
                    snapshots[item.Detachment].Count ==
                        item.Detachment.Troops.Count &&
                    snapshots[item.Detachment].All(pair =>
                        item.Detachment.Troops.TryGetValue(pair.Key,
                            out int count) && count == pair.Value));
                if (!restored)
                    foreach (ServingDetachmentRef item in sourceRefs)
                        GetVillageFiefData(item.SourceVillage)?
                            .RequireRecovery(
                                "ServiceTransferRollbackUnverified");
                ModLogger.Error(string.Format(
                    "Fief service transfer failed: source={0}, target={1}, reason={2}, rollback={3}.",
                    sourceParty.StringId, targetParty.StringId,
                    exception.GetType().Name,
                    restored ? "Verified" : "RecoveryRequired"));
                return false;
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
                    if (ReleaseVillageFiefService(
                        item.SourceVillage, lost, false) != lost)
                    {
                        GetVillageFiefData(item.SourceVillage)?
                            .RequireRecovery();
                        continue;
                    }
                    lostTotal += lost;
                }
                GetVillageFiefData(item.SourceVillage)?.ServiceBatches
                    .Remove(item.Detachment);
                GetFiefData(item.SourceFief)?.RefreshManpowerCounters();
                UnregisterServingDetachment(item.SourceFief,
                    item.SourceVillage, item.Detachment);
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
                if (lost > 0 && ReleaseVillageFiefService(
                    item.SourceVillage, lost, false) != lost)
                {
                    GetVillageFiefData(item.SourceVillage)?.RequireRecovery();
                    continue;
                }
                GetVillageFiefData(item.SourceVillage)?.ServiceBatches
                    .Remove(item.Detachment);
                GetFiefData(item.SourceFief)?.RefreshManpowerCounters();
                UnregisterServingDetachment(item.SourceFief,
                    item.SourceVillage, item.Detachment);
            }

            CleanupRemovedVillageMilitiaParty(party);
        }

        private void CleanupRemovedVillageMilitiaParty(MobileParty party)
        {
            MilitiaPartyComponent component = party?.PartyComponent as
                MilitiaPartyComponent;
            Settlement village = component?.Settlement;
            if (village?.IsVillage != true) return;

            FiefPartyData data = GetFiefData(village.Village?.Bound);
            if (data == null) return;
            int lostReadyTroops = data.GetVillageData(village)?
                .GetReadyFiefTroopCount(data.GetFiefPartyTemplate()) ?? 0;

            // The ready roster belongs to the native militia party and is
            // destroyed with it. Service, return and mobilized-hearth state
            // belong to VillageFiefData and must survive this lifecycle.
            data.RefreshManpowerCounters();
            if (village.OwnerClan == Clan.PlayerClan)
                ModLogger.Debug(string.Format(
                    "Village militia party removed: village={0}, lostReadyFiefTroops={1}; persistent service and return state preserved.",
                    village.StringId, lostReadyTroops));
        }

        private void OnMobilePartyCreated(MobileParty party)
        {
            MilitiaPartyComponent component = party?.PartyComponent as
                MilitiaPartyComponent;
            Settlement village = component?.Settlement;
            if (village?.IsVillage != true) return;

            FiefPartyData data = GetFiefData(village.Village?.Bound);
            if (data?.RepairVillageMilitiaBinding(village) != true) return;

            // Recreated militia parties resume through the ordinary daily
            // production path. New-game bulk initialization is intentionally
            // not invoked here.
            if (village.OwnerClan == Clan.PlayerClan)
                ModLogger.Debug(string.Format(
                    "Village militia party recreated: village={0}; feudal troops will resume through daily replenishment.",
                    village.StringId));
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            if (mapEvent == null ||
                mapEvent.BattleState != BattleState.AttackerVictory &&
                mapEvent.BattleState != BattleState.DefenderVictory)
                return;

            // MapEventEnded is raised after battle casualties have been
            // committed, but before native end-of-event processing removes
            // parties. Snapshot participants because reconciliation can empty
            // batches and party removal may follow in the same finalization.
            List<MobileParty> parties = mapEvent.AttackerSide.Parties
                .Concat(mapEvent.DefenderSide.Parties)
                .Where(item => item?.Party?.IsMobile == true)
                .Select(item => item.Party.MobileParty)
                .Where(item => item != null &&
                    GetServingDetachments(item).Count > 0)
                .Distinct()
                .ToList();

            foreach (MobileParty party in parties)
            {
                List<ServingDetachmentRef> tracked =
                    GetServingDetachments(party).ToList();
                try
                {
                    FiefReconciliationResult result =
                        ReconcilePartyFiefTroops(party);
                    if (result.LostCount > 0 &&
                        RecruitmentLogFilter.ShouldLog(party))
                        ModLogger.Info(string.Format(
                            "Post-battle fief service reconciled: party={0}, matched={1}, lost={2}, reason={3}.",
                            party.StringId, result.MatchedCount,
                            result.LostCount,
                            result.ReasonCode ?? "unknown"));
                    else if (result.BlockedByRecovery &&
                        RecruitmentLogFilter.ShouldLog(party))
                        ModLogger.Warn(string.Format(
                            "Post-battle fief service reconciliation blocked: party={0}, reason={1}.",
                            party.StringId,
                            result.ReasonCode ?? "unknown"));
                }
                catch (Exception exception)
                {
                    foreach (ServingDetachmentRef item in tracked)
                        GetVillageFiefData(item.SourceVillage)?
                            .RequireRecovery(
                                "BattleReconciliationException");
                    ModLogger.Error(string.Format(
                        "Post-battle fief service reconciliation failed: party={0}, reason={1}, exception={2}.",
                        party.StringId, "BattleReconciliationException",
                        exception));
                }
            }
        }

        /// <summary>
        /// Retrieves fief data for a specific settlement.
        /// </summary>
        public FiefPartyData GetFiefData(Settlement settlement)
        {
            return _fiefDataMap.TryGetValue(settlement, out var data) ? data : null;
        }

        public bool TryGetReadyFiefData(Settlement settlement,
            out FiefPartyData data)
        {
            data = null;
            if (!IsSessionReady || settlement == null) return false;
            FiefPartyData candidate = GetFiefData(settlement);
            if (candidate?.IsRuntimeReady != true)
            {
                if (_runtimeReadinessFailuresLogged.Add(settlement))
                {
                    ModLogger.Error(string.Format(
                        "Fief runtime data unavailable: fief={0}, culture={1}, expectedTemplate={2}, reason={3}.",
                        settlement.StringId,
                        settlement.Culture?.StringId ?? "unknown",
                        candidate?.ExpectedTemplateId ?? "unknown",
                        candidate?.RuntimeInitializationFailureReason ??
                            (candidate == null ? "DataMissing" : "NotReady")));
                }
                return false;
            }
            data = candidate;
            return true;
        }

        internal VillageFiefData GetVillageFiefData(Settlement villageSettlement)
        {
            if (villageSettlement?.Village == null) return null;
            Settlement bound = villageSettlement.Village.Bound;
            VillageFiefData current = GetFiefData(bound)?
                .GetVillageData(villageSettlement);
            return current ?? _fiefDataMap.Values
                .Select(data => data?.GetVillageData(villageSettlement))
                .FirstOrDefault(data => data != null);
        }

        internal bool TryResolveOperationScope(Settlement interaction,
            MobileParty party, bool requireCurrentSettlement,
            out FiefOperationScope scope)
        {
            scope = null;
            if (interaction == null || party == null ||
                requireCurrentSettlement && party.CurrentSettlement != interaction)
                return false;
            Settlement sourceFief = interaction.IsVillage
                ? interaction.Village?.Bound
                : interaction.IsFortification ? interaction : null;
            FiefPartyData data = GetFiefData(sourceFief);
            Settlement villageFilter = interaction.IsVillage ? interaction : null;
            if (data == null || sourceFief?.OwnerClan != party.ActualClan ||
                interaction.OwnerClan != party.ActualClan ||
                party.MapEvent != null || sourceFief.IsUnderSiege ||
                villageFilter != null && !data.IsCurrentVillage(villageFilter))
                return false;
            scope = new FiefOperationScope
            {
                InteractionSettlement = interaction,
                SourceFief = sourceFief,
                VillageFilter = villageFilter,
                FiefData = data
            };
            return true;
        }

        internal string GetFiefSourceFingerprint(Settlement interaction,
            MobileParty party)
        {
            if (!TryResolveOperationScope(interaction, party, true,
                out FiefOperationScope scope)) return null;
            return string.Join("|", scope.FiefData
                .OrderedVillages(scope.VillageFilter)
                .SelectMany(v => FiefTroopRosterFilter.GetEntries(
                    v.GetReadyRoster(),
                    scope.FiefData.GetFiefPartyTemplate())
                    .Select(e => string.Format("{0}:{1}:{2}:{3}",
                        v.VillageSettlement.StringId,
                        e.Character.StringId, e.Number, e.WoundedNumber)))
                .OrderBy(value => value, StringComparer.Ordinal));
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
            Settlement fief = settlement.IsVillage
                ? settlement.Village?.Bound : settlement;
            if (_fiefDataMap.TryGetValue(fief, out FiefPartyData data) &&
                data != null && (!settlement.IsVillage ||
                data.IsCurrentVillage(settlement)))
            {
                return data.GetFiefTroopRoster(
                    settlement.IsVillage ? settlement : null);
            }
            return TroopRoster.CreateDummyTroopRoster();
        }

        public TroopRoster GetFiefManagementRoster(Settlement interaction,
            Settlement villageFilter, FiefManagementState state,
            MobileParty playerParty, bool selectableOnly = false)
        {
            TroopRoster result = TroopRoster.CreateDummyTroopRoster();
            Settlement fief = interaction?.IsVillage == true
                ? interaction.Village?.Bound : interaction;
            FiefPartyData data = GetFiefData(fief);
            if (data == null || interaction == null ||
                interaction.IsVillage && villageFilter != interaction ||
                villageFilter != null && !data.IsCurrentVillage(villageFilter))
                return result;
            foreach (VillageFiefData village in data.OrderedVillages(
                villageFilter))
            {
                if (state == FiefManagementState.Ready)
                {
                    foreach (TroopRosterElement item in village
                        .GetReadyFiefTroopRoster(data.GetFiefPartyTemplate())
                        .GetTroopRoster())
                    {
                        int count = selectableOnly
                            ? Math.Max(0, item.Number - item.WoundedNumber)
                            : item.Number;
                        if (count > 0)
                            result.AddToCounts(item.Character, count, false,
                                selectableOnly ? 0 : item.WoundedNumber);
                    }
                    continue;
                }
                if (state == FiefManagementState.Serving)
                {
                    foreach (FiefServiceBatch batch in village.ServiceBatches
                        .Where(batch => batch != null && !batch.IsEmpty() &&
                            (!selectableOnly ||
                             batch.ServingParty == playerParty)))
                    foreach (var pair in batch.Troops)
                        if (pair.Key != null && pair.Value > 0)
                            result.AddToCounts(pair.Key, pair.Value);
                    continue;
                }
                if (!selectableOnly)
                    foreach (FiefReturnBatch batch in village.ReturnBatches
                        .Where(batch => batch != null && !batch.IsEmpty()))
                    foreach (var pair in batch.Troops)
                        if (pair.Key != null && pair.Value > 0)
                            result.AddToCounts(pair.Key, pair.Value);
            }
            return result;
        }

        public string GetFiefManagementFingerprint(Settlement interaction,
            Settlement villageFilter, FiefManagementState state,
            MobileParty playerParty)
        {
            TroopRoster roster = GetFiefManagementRoster(interaction,
                villageFilter, state, playerParty, state !=
                    FiefManagementState.Cooling);
            return string.Join("|", roster.GetTroopRoster()
                .Where(item => item.Character != null)
                .OrderBy(item => item.Character.StringId)
                .Select(item => string.Format("{0}:{1}:{2}",
                    item.Character.StringId, item.Number,
                    item.WoundedNumber)));
        }

        public int ReturnSelectedFiefTroops(Settlement interaction,
            Settlement villageFilter, MobileParty party,
            TroopRoster selectedRoster)
        {
            if (!TryResolveOperationScope(interaction, party, true,
                out FiefOperationScope scope) || selectedRoster == null ||
                selectedRoster.TotalManCount <= 0 ||
                villageFilter != null &&
                !scope.FiefData.IsCurrentVillage(villageFilter))
                return 0;
            Dictionary<CharacterObject, int> remaining = selectedRoster
                .GetTroopRoster().Where(item => item.Character != null &&
                    item.Number > 0).ToDictionary(item => item.Character,
                    item => item.Number);
            int returned = 0;
            AdvanceServiceDays(party);
            ReconcilePartyFiefTroops(party);
            foreach (ServingDetachmentRef item in GetServingDetachments(party)
                .Where(item => item.SourceFief == scope.SourceFief &&
                    (villageFilter == null ||
                     item.SourceVillage == villageFilter))
                .OrderBy(item => item.SourceVillage?.StringId)
                .ThenBy(item => item.Detachment?.BatchId).ToList())
            {
                Dictionary<CharacterObject, int> request = item.Detachment
                    .Troops.Where(pair => pair.Key != null && pair.Value > 0 &&
                        remaining.TryGetValue(pair.Key, out int wanted) &&
                        wanted > 0).ToDictionary(pair => pair.Key, pair =>
                        Math.Min(pair.Value, remaining[pair.Key]));
                int requested = request.Values.Sum();
                if (requested <= 0) continue;
                FiefReturnResult result = TryReturnFiefTroops(party,
                    interaction, scope.SourceFief,
                    item.Detachment.BatchId, requested,
                    PartyExchangePurpose.NaturalVisit, request);
                returned += result.ActualReturnedCount;
                foreach (var pair in result.ReturnedTroops)
                    remaining[pair.Key] -= pair.Value;
                if (remaining.Values.All(value => value <= 0)) break;
            }
            return returned;
        }

        /// <summary>
        /// Recruits all available fief squads from a settlement into the target mobile party.
        /// </summary>
        public int RecruitFiefTroopsFromSettlement(
            Settlement settlement,
            MobileParty targetParty,
            int maximumCount = int.MaxValue,
            IReadOnlyDictionary<CharacterObject, int> expectedApprovals = null)
        {
            if (settlement == null || targetParty == null)
            {
                ModLogger.Error("[Fief] Recruitment failed: invalid parameters.");
                return 0;
            }
            if (TryResolveOperationScope(settlement, targetParty, true,
                out FiefOperationScope scope))
            {
                return scope.FiefData.RecruitTroopsToParty(
                    targetParty,
                    maximumCount,
                    expectedApprovals,
                    scope.VillageFilter);
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
            return TryResolveOperationScope(settlement, targetParty, false,
                out FiefOperationScope scope)
                    ? scope.FiefData.PreviewFiefRecruitmentCandidates(
                        targetParty,
                        maximumCount,
                        scope.VillageFilter)
                    : Array.Empty<RecruitmentCandidate>();
        }

        /// <summary>Returns the exact approved manifest without committing it.</summary>
        public IReadOnlyDictionary<CharacterObject, int>
            PreviewFiefRecruitmentApprovals(
                Settlement settlement,
                MobileParty targetParty,
                int maximumCount)
        {
            return TryResolveOperationScope(settlement, targetParty, false,
                out FiefOperationScope scope)
                    ? scope.FiefData.PreviewFiefRecruitmentApprovals(
                        targetParty,
                        maximumCount,
                        scope.VillageFilter)
                    : new Dictionary<CharacterObject, int>();
        }

        public IReadOnlyDictionary<CharacterObject, int>
            PreviewFiefRecruitmentApprovals(
                Settlement settlement,
                MobileParty targetParty,
                int maximumCount,
                out IReadOnlyList<RecruitmentCandidate> candidates,
                out RecruitmentPlan plan,
                out int approvedBeforeMinimumBatch,
                out int minimumBatch)
        {
            if (TryResolveOperationScope(settlement, targetParty, false,
                out FiefOperationScope scope))
                return scope.FiefData.PreviewFiefRecruitmentApprovals(
                    targetParty, maximumCount, scope.VillageFilter,
                    out candidates, out plan,
                    out approvedBeforeMinimumBatch, out minimumBatch);
            candidates = Array.Empty<RecruitmentCandidate>();
            plan = null;
            approvedBeforeMinimumBatch = 0;
            minimumBatch = 0;
            return new Dictionary<CharacterObject, int>();
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
            if (TryResolveOperationScope(settlement, mobileParty, true,
                out FiefOperationScope scope))
            {
                scope.FiefData.ReturnTroopsToSettlement(
                    mobileParty, settlement, scope.VillageFilter);
            }
        }

        public int ReturnTroopsToSettlementWithResult(
            Settlement settlement,
            MobileParty mobileParty = null)
        {
            if (!TryResolveOperationScope(settlement, mobileParty, true,
                out FiefOperationScope scope))
            {
                return 0;
            }

            return scope.FiefData.ReturnTroopsToSettlement(
                mobileParty, settlement, scope.VillageFilter);
        }

        /// <summary>
        /// 获取指定封邑当前可招募的军队人数（即就绪状态的健康士兵总数）。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>可招募人数，若封邑无效或非玩家所有则返回 0</returns>
        public int GetAvailableTroopCount(Settlement settlement)
        {
            return QueryFeudalTroops(settlement,
                FiefTroopState.Ready).TotalCount;
        }

        /// <summary>
        /// 获取指定封邑当前已被征召的士兵人数。
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>已被征召人数，若无征召或封邑无效则返回 0</returns>
        public int GetRecruitedTroopCount(Settlement settlement)
        {
            return QueryFeudalTroops(settlement,
                FiefTroopState.Serving).TotalCount;
        }

        /// <summary>
        /// Gets the number of returned soldiers still waiting for cooldown.
        /// </summary>
        /// <param name="settlement">目标封邑</param>
        /// <returns>解散期士兵总数，若无或封邑无效则返回 0</returns>
        public int GetWaitCycleTroopCount(Settlement settlement)
        {
            return QueryFeudalTroops(settlement,
                FiefTroopState.Cooling).TotalCount;
        }

        /// <summary>
        /// 获取指定封邑士兵的实际数量
        /// </summary>
        public int GetFiefTroopCount(Settlement settlement)
        {
            return QueryFeudalTroops(settlement).TotalCount;
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

        public void ManualRecruitFromFief(Settlement settlement,
            TroopRoster selectedRoster, MobileParty targetParty,
            Settlement villageFilter = null)
        {
            if (settlement == null || selectedRoster == null)
                return;

            if (TryResolveOperationScope(settlement, targetParty, true,
                out FiefOperationScope scope))
            {
                Settlement effectiveFilter = settlement.IsVillage
                    ? settlement : villageFilter;
                if (effectiveFilter != null &&
                    !scope.FiefData.IsCurrentVillage(effectiveFilter))
                    return;
                scope.FiefData.RecruitManualSelection(
                    selectedRoster, targetParty, effectiveFilter);
            }
        }

        /// <summary>
        /// 监听招募事件，扣除定居点繁荣度/户数。
        ///
        /// 优先使用对应文化的 FiefPartyTemplate 中的
        /// Professional Hearth cost is resolved globally by troop Tier.
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
            // New professional recruits permanently consume tier-based Hearth.
            // They do not create a mobilized-hearth liability. Prisoner
            // recruitment emits this event with no settlement and is excluded.
            if (settlement.IsVillage || settlement.IsTown || settlement.IsCastle)
            {
                bool accepted = TryConsumeProfessionalHearth(
                    settlement, troop, count);
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
