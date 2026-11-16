// Feudal village supply domain. Saved service/return batches are the troop
// lifecycle authority; daily branch quotas are derived, campaign-scoped views
// and never become a second persistent Hearth ledger.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ModifiedArmy.common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>Save-owned lifecycle state for feudal troops from one village.</summary>
    [SaveableRootClass(12)]
    public sealed class VillageFiefData
    {
        // Campaign-scoped derived quota snapshot. It is intentionally not
        // saveable: the authoritative inputs remain village Hearth, doctrine
        // weights and the XML template, and the snapshot is rebuilt on load.
        private int _supplyQuotaCampaignDay = int.MinValue;
        private FiefPartyTemplate _supplyQuotaTemplate;
        private IReadOnlyDictionary<TroopProductionEntry, int>
            _dailySupplyBranchQuotas;

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
            InvalidateDailySupplyBranchQuotas();
        }

        /// <summary>
        /// Ensures the village has one deterministic branch-quota snapshot for
        /// the current campaign day. Repeated calls on the same day are reads.
        /// </summary>
        internal void RefreshDailySupplyBranchQuotas(
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine) =>
            EnsureDailySupplyBranchQuotas(template, doctrine);

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
            int denominator = checked(doctrine.FiefWeight +
                doctrine.ProfessionalWeight);
            if (doctrine.FiefWeight < 0 || doctrine.ProfessionalWeight < 0 ||
                denominator <= 0)
                throw new InvalidOperationException(
                    "Invalid doctrine Hearth allocation weights.");
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
            if (!template.TryGetSupplyBranch(troop, out var branch))
                return false;
            int required = checked(
                doctrine.GetFeudalSupplyCost(troop) * count);
            return GetFreeSupplyHearth(template, doctrine) >= required &&
                GetFeudalBudgetRemainingHearth(template, doctrine) >= required &&
                GetSupplyBranchRemainingHearth(template, doctrine,
                    branch) >= required;
        }

        /// <summary>
        /// Returns today's immutable branch quotas. The calculation first
        /// derives the current feudal Hearth budget, then distributes that
        /// budget by XML weight with stable largest-remainder rounding.
        /// </summary>
        public IReadOnlyDictionary<TroopProductionEntry, int>
            GetSupplyBranchQuotas(FiefPartyTemplate template,
                CulturalMilitaryDoctrine doctrine)
        {
            EnsureDailySupplyBranchQuotas(template, doctrine);
            return _dailySupplyBranchQuotas;
        }

        /// <summary>
        /// Builds the non-persistent daily quota view once. Locked branches
        /// remain in the denominator, and allocation never borrows across
        /// branches or villages.
        /// </summary>
        private void EnsureDailySupplyBranchQuotas(
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine)
        {
            if (template == null || doctrine == null)
                throw new InvalidOperationException(
                    "Branch quota calculation requires template and doctrine.");
            int campaignDay = (int)CampaignTime.Now.ToDays;
            if (_dailySupplyBranchQuotas != null &&
                _supplyQuotaCampaignDay == campaignDay &&
                _supplyQuotaTemplate == template)
                return;
            List<TroopProductionEntry> branches = template.SupplyBranches
                .OrderBy(entry => entry.Troop.StringId,
                    StringComparer.Ordinal).ToList();
            long totalWeight = branches.Sum(entry => (long)entry.Weight);
            if (totalWeight <= 0)
                throw new InvalidOperationException(
                    "Fief template has no positive supply branch weight.");
            int budget = GetFeudalBudgetHearth(doctrine);
            var quotas = branches.ToDictionary(entry => entry,
                entry => (int)((long)budget * entry.Weight / totalWeight));
            int remainder = budget - quotas.Values.Sum();
            foreach (TroopProductionEntry entry in branches
                .OrderByDescending(entry =>
                    (long)budget * entry.Weight % totalWeight)
                .ThenBy(entry => entry.Troop.StringId,
                    StringComparer.Ordinal).Take(remainder))
                quotas[entry]++;
            _dailySupplyBranchQuotas = new ReadOnlyDictionary<
                TroopProductionEntry, int>(quotas);
            _supplyQuotaCampaignDay = campaignDay;
            _supplyQuotaTemplate = template;
            LogDailySupplyBranchQuotas(template, doctrine, budget, quotas);
        }

        /// <summary>Clears only the derived daily view during runtime repair.</summary>
        private void InvalidateDailySupplyBranchQuotas()
        {
            _supplyQuotaCampaignDay = int.MinValue;
            _supplyQuotaTemplate = null;
            _dailySupplyBranchQuotas = null;
        }

        /// <summary>
        /// Sums current-tier Hearth for Ready, Serving and Cooling troops that
        /// uniquely belong to one XML root branch. Serving therefore freezes
        /// its actual supply cost until death or permanent departure is reconciled.
        /// </summary>
        public int GetOccupiedSupplyBranchHearth(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine, TroopProductionEntry branch)
        {
            if (branch?.Troop == null) return 0;
            int occupied = 0;
            checked
            {
                foreach (var pair in QueryFeudalTroops(template).Troops)
                    if (template.TryGetSupplyBranch(pair.Key, out var owner) &&
                        owner == branch)
                        occupied += pair.Value.Total *
                            doctrine.GetFeudalSupplyCost(pair.Key);
            }
            return occupied;
        }

        /// <summary>Returns today's branch quota minus live three-state use.</summary>
        public int GetSupplyBranchRemainingHearth(
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine,
            TroopProductionEntry branch) => checked(
                GetSupplyBranchQuotas(template, doctrine)[branch] -
                GetOccupiedSupplyBranchHearth(template, doctrine, branch));

        private void LogDailySupplyBranchQuotas(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine, int budget,
            IReadOnlyDictionary<TroopProductionEntry, int> quotas)
        {
            if (!RecruitmentLogFilter.ShouldLog(
                    VillageSettlement?.OwnerClan)) return;
            string separator = GameTexts.FindText(
                "str_modifiedarmy_log_list_separator").ToString();
            string branches = string.Join(separator, quotas.OrderBy(pair =>
                pair.Key.Troop.StringId, StringComparer.Ordinal).Select(pair =>
            {
                TextObject entry = GameTexts.FindText(
                    "str_modifiedarmy_fief_supply_quota_entry");
                int occupied = GetOccupiedSupplyBranchHearth(template,
                    doctrine, pair.Key);
                entry.SetTextVariable("TROOP_NAME", pair.Key.Troop.Name);
                entry.SetTextVariable("WEIGHT", pair.Key.Weight);
                entry.SetTextVariable("QUOTA", pair.Value);
                entry.SetTextVariable("OCCUPIED", occupied);
                entry.SetTextVariable("REMAINING",
                    Math.Max(0, pair.Value - occupied));
                return entry.ToString();
            }));
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_fief_supply_quota_daily");
            message.SetTextVariable("VILLAGE_NAME", VillageSettlement.Name);
            message.SetTextVariable("FIEF_NAME",
                VillageSettlement.Village?.Bound?.Name ??
                    new TextObject("{=ModifiedArmy_UnknownFief}Unknown fief"));
            message.SetTextVariable("EFFECTIVE_HEARTH",
                GetEffectiveSupplyHearth());
            message.SetTextVariable("FEUDAL_BUDGET", budget);
            message.SetTextVariable("BRANCHES", branches);
            ModLogger.Debug(message.ToString());
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
