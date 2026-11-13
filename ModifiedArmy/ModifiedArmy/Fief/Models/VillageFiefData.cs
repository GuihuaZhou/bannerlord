using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedArmy.common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Tool;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>Save-owned lifecycle state for feudal troops from one village.</summary>
    [SaveableRootClass(12)]
    public sealed class VillageFiefData
    {
        [SaveableProperty(1)] public Settlement VillageSettlement { get; private set; }
        [SaveableProperty(2)] public List<FiefServiceBatch> ServiceBatches { get; private set; }
        [SaveableProperty(3)] public List<FiefReturnBatch> ReturnBatches { get; private set; }
        [SaveableProperty(4)] public int MobilizedHearth { get; private set; }
        [SaveableProperty(5)] public bool RecoveryRequired { get; private set; }
        [SaveableProperty(6)] public string RecoveryReason { get; private set; }
        [SaveableProperty(7)] public string RecoveryOperationId { get; private set; }
        [SaveableProperty(8)] private string LastMobilizationOperationId { get; set; }
        [SaveableProperty(9)] private int LastMobilizationOperationAmount { get; set; }
        [SaveableProperty(10)] private string LastReturnOperationId { get; set; }
        [SaveableProperty(11)] private int LastReturnOperationAmount { get; set; }

        public VillageFiefData()
        {
            ServiceBatches = new List<FiefServiceBatch>();
            ReturnBatches = new List<FiefReturnBatch>();
        }

        public VillageFiefData(Settlement village) : this() => VillageSettlement = village;

        internal void Repair(Settlement village)
        {
            VillageSettlement = village ?? VillageSettlement;
            ServiceBatches ??= new List<FiefServiceBatch>();
            ReturnBatches ??= new List<FiefReturnBatch>();
            MobilizedHearth = Math.Max(0, MobilizedHearth);
        }

        public TroopRoster GetReadyRoster()
        {
            var component = VillageSettlement?.MilitiaPartyComponent;
            return component?.MobileParty?.IsActive == true
                ? component.MobileParty.MemberRoster
                : null;
        }

        public FiefTroopSnapshot QueryFeudalTroops(
            FiefPartyTemplate template,
            FiefTroopState states = FiefTroopState.All) =>
            FiefTroopQuery.Query(this, template, states);

        public int GetReadyFiefTroopCount(FiefPartyTemplate template) =>
            QueryFeudalTroops(template, FiefTroopState.Ready).TotalCount;

        public int GetServingFiefTroopCount(FiefPartyTemplate template) =>
            QueryFeudalTroops(template, FiefTroopState.Serving).TotalCount;

        internal bool TryGetRawServingCount(out int count,
            out string reason)
        {
            count = 0;
            reason = null;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (FiefServiceBatch batch in ServiceBatches ??
                    Enumerable.Empty<FiefServiceBatch>())
                {
                    if (batch == null) { reason = "NullServiceBatch"; return false; }
                    batch.ResolveServingParty();
                    if (string.IsNullOrEmpty(batch.BatchId))
                    { reason = "MissingServiceBatchId"; return false; }
                    if (!ids.Add(batch.BatchId))
                    { reason = "DuplicateVillageServiceBatchId"; return false; }
                    if (batch.SourceVillage != VillageSettlement ||
                        batch.SourceFief != VillageSettlement?.Village?.Bound)
                    { reason = "InvalidServiceBatchSource"; return false; }
                    if (batch.ServingParty == null)
                    { reason = "MissingServingParty"; return false; }
                    if (batch.Troops == null || batch.Troops.Count == 0 ||
                        batch.Troops.Any(item => item.Key == null ||
                            item.Value <= 0))
                    { reason = "InvalidServiceBatchTroops"; return false; }
                    count = checked(count + batch.Troops.Values.Sum());
                }
                return true;
            }
            catch (OverflowException)
            {
                reason = "ServingCountOverflow";
                return false;
            }
        }

        public int GetCoolingFiefTroopCount(FiefPartyTemplate template) =>
            QueryFeudalTroops(template, FiefTroopState.Cooling).TotalCount;

        public int GetTotalFiefTroopCount(FiefPartyTemplate template) =>
            QueryFeudalTroops(template).TotalCount;

        public int GetEffectiveSupplyHearth() => Math.Max(0,
            (int)Math.Floor((VillageSettlement?.Village?.Hearth ?? 0f) +
                MobilizedHearth));

        public int GetOccupiedSupplyHearth(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine)
        {
            if (template == null || doctrine == null)
                throw new InvalidOperationException(
                    "Fief supply calculation requires template and doctrine.");
            int occupied = 0;
            checked
            {
                foreach (var pair in QueryFeudalTroops(template).Troops)
                    AddOccupied(pair.Key, pair.Value.Total);
            }
            return occupied;

            void AddOccupied(CharacterObject troop, int count)
            {
                if (count <= 0) return;
                if (template.IsEnableTroop(troop) &&
                    doctrine.TryGetFeudalSupplyCost(troop, out int cost))
                    occupied = checked(occupied + checked(count * cost));
            }
        }

        public int GetFreeSupplyHearth(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine) => checked(
                GetEffectiveSupplyHearth() -
                GetOccupiedSupplyHearth(template, doctrine));

        public int GetSupplyDeficitHearth(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine) => Math.Max(0,
                -GetFreeSupplyHearth(template, doctrine));

        public int GetFeudalBudgetHearth(
            CulturalMilitaryDoctrine doctrine)
        {
            if (doctrine == null)
                throw new InvalidOperationException(
                    "Fief budget calculation requires a doctrine.");
            // Doctrine weights allocate the effective Hearth pool between
            // feudal support policy and the professional branch. They no
            // longer scale either branch's daily production rate.
            int allocatable = GetEffectiveSupplyHearth();
            return checked((int)Math.Floor(
                (double)allocatable * doctrine.FiefWeight /
                (doctrine.FiefWeight + doctrine.ProfessionalWeight)));
        }

        public int GetFeudalBudgetRemainingHearth(
            FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine) => checked(
                GetFeudalBudgetHearth(doctrine) -
                GetOccupiedSupplyHearth(template, doctrine));

        public bool CanIncreaseSupply(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine, CharacterObject troop,
            int count)
        {
            if (troop == null || count <= 0) return false;
            int required = checked(
                doctrine.GetFeudalSupplyCost(troop) * count);
            return GetFreeSupplyHearth(template, doctrine) >= required &&
                GetFeudalBudgetRemainingHearth(template, doctrine) >= required;
        }

        public TroopRoster GetReadyFiefTroopRoster(FiefPartyTemplate template)
        {
            return QueryFeudalTroops(template,
                FiefTroopState.Ready).ToTroopRoster();
        }

        internal bool TryMobilize(int count)
        {
            Village village = VillageSettlement?.Village;
            // Mobilization may consume all current Hearth, but never more
            // than the village actually has at commit time.
            if (RecoveryRequired || village == null || count <= 0 ||
                Math.Max(0, (int)Math.Floor(village.Hearth)) < count)
                return false;
            string operationId =
                "feudal-mobilize:" + Guid.NewGuid().ToString("N");
            var result = SettlementPopulationServiceRegistry.Current?.TryConsume(
                VillageSettlement, count, operationId);
            if (result?.IsCommitted != true) return false;
            MobilizedHearth += count;
            LastMobilizationOperationId = operationId;
            LastMobilizationOperationAmount = count;
            return true;
        }

        internal bool TryReleaseMobilization(int count, bool restoreHearth)
        {
            if (RecoveryRequired || count <= 0 || MobilizedHearth < count)
                return false;
            if (restoreHearth)
            {
                string operationId =
                    "feudal-return:" + Guid.NewGuid().ToString("N");
                PopulationTransactionResult result =
                    SettlementPopulationServiceRegistry.Current?.TryRestore(
                        VillageSettlement, count, operationId);
                if (result?.IsCommitted != true) return false;
                LastReturnOperationId = operationId;
                LastReturnOperationAmount = count;
            }
            MobilizedHearth -= count;
            return true;
        }

        internal int RemoveInvalidMobilization(int count,
            bool restoreHearth)
        {
            int released = Math.Min(Math.Max(0, count), MobilizedHearth);
            if (restoreHearth && released > 0 && SettlementPopulationServiceRegistry.Current?.TryRestore(
                VillageSettlement, released,
                "feudal-reconcile-return:" + Guid.NewGuid().ToString("N"))?.IsCommitted != true)
                return 0;
            MobilizedHearth -= released;
            return released;
        }

        internal void RollbackMobilization(int count, bool hearthWasRestored)
        {
            if (count <= 0) return;
            if (hearthWasRestored)
            {
                PopulationTransactionResult result =
                    LastReturnOperationAmount == count &&
                    !string.IsNullOrEmpty(LastReturnOperationId)
                        ? SettlementPopulationServiceRegistry.Current?
                            .TryCompensate(
                                VillageSettlement,
                                LastReturnOperationId)
                        : null;
                if (result?.Status !=
                    PopulationTransactionStatus.Compensated)
                {
                    RequireRecovery(
                        "PopulationRollbackFailed",
                        LastReturnOperationId);
                    return;
                }
                LastReturnOperationId = null;
                LastReturnOperationAmount = 0;
            }
            MobilizedHearth += count;
        }

        internal bool RollbackMobilizationReservation(int count)
        {
            if (count <= 0 || MobilizedHearth < count ||
                VillageSettlement?.Village == null) return false;
            PopulationTransactionResult result =
                LastMobilizationOperationAmount == count &&
                !string.IsNullOrEmpty(LastMobilizationOperationId)
                    ? SettlementPopulationServiceRegistry.Current?
                        .TryCompensate(
                            VillageSettlement,
                            LastMobilizationOperationId)
                    : null;
            if (result?.Status != PopulationTransactionStatus.Compensated)
            {
                RequireRecovery(
                    "PopulationRollbackFailed",
                    LastMobilizationOperationId);
                return false;
            }
            MobilizedHearth -= count;
            LastMobilizationOperationId = null;
            LastMobilizationOperationAmount = 0;
            return true;
        }

        internal void RequireRecovery(string reason = "InvariantMismatch",
            string operationId = null)
        {
            bool stateChanged = !RecoveryRequired ||
                RecoveryReason != reason ||
                RecoveryOperationId != operationId;
            RecoveryRequired = true;
            RecoveryReason = string.IsNullOrEmpty(reason)
                ? "InvariantMismatch" : reason;
            RecoveryOperationId = operationId;
            if (stateChanged)
                ModLogger.FileError(string.Format(
                    "Fief population workflow frozen: village={0}, id={1}, " +
                    "operation={2}, mobilizedHearth={3}, reason={4}.",
                    VillageSettlement?.Name,
                    VillageSettlement?.StringId,
                    operationId ?? "missing",
                    MobilizedHearth,
                    RecoveryReason));
        }

        internal void CompleteRecovery(bool writeLog = true)
        {
            if (RecoveryRequired && writeLog)
                ModLogger.FileInfo(string.Format(
                    "Fief population workflow recovery completed: " +
                    "village={0}, id={1}, operation={2}, " +
                    "mobilizedHearth={3}, previousReason={4}.",
                    VillageSettlement?.Name,
                    VillageSettlement?.StringId,
                    RecoveryOperationId ?? "missing",
                    MobilizedHearth,
                    RecoveryReason ?? "missing"));
            RecoveryRequired = false;
            RecoveryReason = null;
            RecoveryOperationId = null;
        }

    }
}
