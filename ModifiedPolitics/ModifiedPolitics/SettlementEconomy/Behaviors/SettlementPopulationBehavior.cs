// Authoritative Hearth ledger for villages and fortifications. This behavior
// owns persistence and transaction recovery; recruitment callers in
// ModifiedArmy may mutate Hearth only through ISettlementPopulationService.
using System;
using System.Collections.Generic;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.common;
using ModifiedPolitics.SettlementEconomy.Config;
using ModifiedPolitics.SettlementEconomy.Models;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.SettlementEconomy.Behaviors
{
    internal sealed class PopulationDailyDiagnostics
    {
        public int CommittedCount { get; set; }
        public int CompensatedCount { get; set; }
        public int RejectedCount { get; set; }
        public float Consumed { get; set; }
        public float Restored { get; set; }
        public int ProfessionalRecruited { get; set; }
    }

    /// <summary>
    /// Save-compatible population transaction record. Property IDs 1-10 retain
    /// their historical meanings; ID 11 separates professional recruit count
    /// from the fractional Hearth actually consumed by new transactions.
    /// </summary>
    public sealed class PopulationTransactionRecord
    {
        [SaveableProperty(1)] public Settlement Settlement { get; private set; }
        [SaveableProperty(2)] public float Amount { get; private set; }
        [SaveableProperty(3)] public int Status { get; private set; }
        [SaveableProperty(4)] public float HearthBefore { get; private set; }
        [SaveableProperty(5)] public float HearthAfter { get; private set; }
        [SaveableProperty(6)] public int TransactionKind { get; private set; }
        [SaveableProperty(7)] public float BurdenDelta { get; private set; }
        [SaveableProperty(8)] public float BurdenBefore { get; private set; }
        [SaveableProperty(9)] public float BurdenAfter { get; private set; }
        [SaveableProperty(10)] public int CommittedDay { get; private set; }
        [SaveableProperty(11)] public float HearthConsumed { get; private set; }

        public PopulationTransactionRecord() { }

        public PopulationTransactionRecord(
            Settlement settlement,
            float amount,
            float hearthBefore,
            float hearthAfter)
        {
            Settlement = settlement;
            Amount = amount;
            HearthBefore = hearthBefore;
            HearthAfter = hearthAfter;
            Status = (int)PopulationTransactionStatus.Committed;
        }

        /// <summary>
        /// Creates a committed professional transaction without repurposing the
        /// legacy Amount field, which continues to store recruit count.
        /// </summary>
        public static PopulationTransactionRecord ProfessionalRecruitment(
            Settlement settlement, int count, float hearthBefore,
            float hearthAfter, float hearthConsumed,
            int committedDay) => new PopulationTransactionRecord(
                settlement, count, hearthBefore, hearthAfter)
            {
                TransactionKind = 1,
                HearthConsumed = hearthConsumed,
                CommittedDay = committedDay
            };

        public void MarkCompensated(float hearthAfter,
            float burdenAfter = 0f)
        {
            HearthAfter = hearthAfter;
            if (TransactionKind == 1) BurdenAfter = burdenAfter;
            Status = (int)PopulationTransactionStatus.Compensated;
        }

        public void MarkRecoveryRequired()
        {
            Status = (int)PopulationTransactionStatus.RecoveryRequired;
        }
    }

    /// <summary>
    /// Campaign behavior and sole owner of Hearth persistence, mutation,
    /// compensation, and RecoveryRequired freezes.
    /// </summary>
    public sealed class SettlementPopulationBehavior :
        CampaignBehaviorBase,
        ISettlementPopulationService
    {
        private const string HearthKey =
            "ModifiedPolitics.SettlementPopulation.Hearth.v1";
        private const string TransactionKey =
            "ModifiedPolitics.SettlementPopulation.Transactions.v1";
        private const string RecoveryKey =
            "ModifiedPolitics.SettlementPopulation.Recovery.v1";
        private const string DailyTickKey =
            "ModifiedPolitics.SettlementPopulation.LastDailyTick.v1";
        private const string RecruitmentBurdenKey =
            "ModifiedPolitics.SettlementPopulation.RecruitmentBurden.v1";

        private Dictionary<Settlement, float> _hearth = new();
        private Dictionary<string, PopulationTransactionRecord> _transactions =
            new();
        private Dictionary<Settlement, bool> _recovery = new();
        private Dictionary<Settlement, int> _lastDailyTick = new();
        private Dictionary<Settlement, float> _recruitmentBurden = new();
        private readonly Dictionary<string, int> _lastRejectionLogDay = new();
        private readonly Dictionary<Settlement, PopulationDailyDiagnostics>
            _dailyDiagnostics = new();
        private bool _initializationSummaryLogged;

        public override void RegisterEvents()
        {
            SettlementPopulationServiceRegistry.Register(this);
            CampaignEvents.OnAfterSessionLaunchedEvent
                .AddNonSerializedListener(this, OnSessionReady);
            CampaignEvents.OnGameLoadedEvent
                .AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.DailyTickSettlementEvent
                .AddNonSerializedListener(this, OnDailyTick);
        }

        /// <summary>
        /// Writes active V3 state. The retired recruitment-pressure key is read
        /// only while loading old saves and is never written into new saves.
        /// </summary>
        public override void SyncData(IDataStore store)
        {
            store.SyncData(HearthKey, ref _hearth);
            store.SyncData(TransactionKey, ref _transactions);
            store.SyncData(RecoveryKey, ref _recovery);
            store.SyncData(DailyTickKey, ref _lastDailyTick);
            if (store.IsLoading)
                store.SyncData(RecruitmentBurdenKey, ref _recruitmentBurden);
            _hearth ??= new Dictionary<Settlement, float>();
            _transactions ??=
                new Dictionary<string, PopulationTransactionRecord>();
            _recovery ??= new Dictionary<Settlement, bool>();
            _lastDailyTick ??= new Dictionary<Settlement, int>();
            _recruitmentBurden ??= new Dictionary<Settlement, float>();
        }

        private void OnSessionReady(CampaignGameStarter starter)
        {
            EnsureFortifications();
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            SettlementPopulationServiceRegistry.Register(this);
            EnsureFortifications();
        }

        private void EnsureFortifications()
        {
            int initializedTowns = 0;
            int initializedCastles = 0;
            int compatibleWallLevels = 0;
            foreach (Settlement settlement in Settlement.All)
            {
                if (settlement?.Town == null ||
                    (!settlement.IsTown && !settlement.IsCastle) ||
                    _hearth.ContainsKey(settlement))
                {
                    continue;
                }

                int rawLevel = settlement.Town.GetWallLevel();
                int level = Math.Max(1, Math.Min(3, rawLevel));
                SettlementEconomyConfig config =
                    SettlementEconomyConfigManager.Instance.RequireActive();
                _hearth.Add(settlement, config.GetInitialHearth(settlement));
                if (settlement.IsCastle) initializedCastles++;
                else initializedTowns++;
                if (rawLevel < 1 || rawLevel > 3)
                {
                    compatibleWallLevels++;
                    ModLogger.FileWarn(string.Format(
                        "[Population] Compatible wall level used: " +
                        "settlement={0}, id={1}, actual={2}, used={3}.",
                        settlement.Name,
                        settlement.StringId,
                        rawLevel,
                        level));
                }
            }
            if (!_initializationSummaryLogged &&
                (initializedTowns > 0 || initializedCastles > 0))
            {
                _initializationSummaryLogged = true;
                ModLogger.FileInfo(string.Format(
                    "[Population] Initialization summary: towns={0}, " +
                    "castles={1}, compatibleWallLevels={2}.",
                    initializedTowns,
                    initializedCastles,
                    compatibleWallLevels));
            }
        }

        private void OnDailyTick(Settlement settlement)
        {
            if (settlement == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            LogAndResetDailyDiagnostics(settlement, day);
            if (settlement?.Town == null ||
                (!settlement.IsTown && !settlement.IsCastle))
            {
                return;
            }

            EnsureDailyPopulationUpdate(settlement, day);
        }

        /// <summary>
        /// Advances natural Hearth once per settlement/day. Recruitment pressure
        /// no longer contributes to growth or prosperity support.
        /// </summary>
        private bool EnsureDailyPopulationUpdate(Settlement settlement,
            int day)
        {
            if (_lastDailyTick.TryGetValue(settlement, out int settledDay) &&
                settledDay == day) return true;
            if (!_hearth.ContainsKey(settlement)) EnsureFortifications();
            if (!_hearth.TryGetValue(settlement, out float hearthBefore))
                return false;
            if (IsRecoveryRequired(settlement)) return false;
            DailyHearthForecast forecast;
            try
            {
                SettlementEconomyConfig config =
                    SettlementEconomyConfigManager.Instance.RequireActive();
                float prosperityPerHearth = settlement.IsCastle
                    ? config.CastleProsperityPerHearth
                    : config.TownProsperityPerHearth;
                forecast = PopulationGrowthModel.CalculateDailyForecast(
                    hearthBefore, GetNaturalCapacity(settlement),
                    settlement.Town.Prosperity, settlement.Town.FoodChange,
                    prosperityPerHearth, config.GetGrowthRate(settlement),
                    config);
                float hearthAfter = Math.Max(0f,
                    hearthBefore + forecast.AppliedTotal);
                _hearth[settlement] = hearthAfter;
                _lastDailyTick[settlement] = day;
                if (ShouldLog(settlement))
                    ModLogger.FileDebug(string.Format(
                        "[Population] Daily settlement update: settlement={0}, " +
                        "id={1}, hearthBefore={2:F2}, capacity={3:F2}, " +
                        "natural={4:F3}, food={5:F3}, prosperity={6:F3}, " +
                        "raw={7:F3}, boundary={8:F3}, applied={9:F3}, " +
                        "hearthAfter={10:F2}, supportProsperity={11:F2}, day={12}.",
                        settlement.Name, settlement.StringId, hearthBefore,
                        forecast.Capacity, forecast.Natural, forecast.Food,
                        forecast.Prosperity, forecast.RawTotal,
                        forecast.CapacityAdjustment, forecast.AppliedTotal,
                        hearthAfter, forecast.SupportProsperity, day));
                return true;
            }
            catch (Exception exception)
            {
                _hearth[settlement] = hearthBefore;
                _recovery[settlement] = true;
                ModLogger.FileError(string.Format(
                    "[Population] Daily update failed; settlement frozen: " +
                    "settlement={0}, id={1}, day={2}, hearth={3:F2}, " +
                    "reason={4}, error={5}.", settlement.Name,
                    settlement.StringId, day, hearthBefore,
                    "DailyPopulationUpdateFailed", exception.Message));
                return false;
            }
        }

        public float GetHearth(Settlement settlement)
        {
            if (settlement?.Village != null) return settlement.Village.Hearth;
            return settlement != null &&
                _hearth.TryGetValue(settlement, out float value)
                    ? value
                    : 0f;
        }

        public float GetNaturalCapacity(Settlement settlement)
        {
            SettlementEconomyConfig config =
                SettlementEconomyConfigManager.Instance.RequireActive();
            return settlement == null ? 0f : config.GetCapacity(settlement);
        }

        public int GetRecruitableHearth(Settlement settlement)
        {
            return IsRecoveryRequired(settlement)
                ? 0
                : Math.Max(0, (int)Math.Floor(GetHearth(settlement)));
        }

        /// <summary>
        /// Pure affordability preview using the same Tier cost and civilian
        /// reserve formula that the commit path revalidates.
        /// </summary>
        public int GetMaxAffordableProfessionalCount(Settlement settlement,
            CharacterObject troop, int requested)
        {
            if (settlement?.IsFortification != true ||
                IsRecoveryRequired(settlement)) return 0;
            HearthEconomyConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig().HearthEconomy;
            return ProfessionalPopulationMath.GetMaxAffordableCount(
                GetHearth(settlement), GetNaturalCapacity(settlement),
                config.ProfessionalReserveRatio,
                config.GetProfessionalHearthCost(troop), requested);
        }

        /// <summary>
        /// Evaluates an exact professional batch without mutation. Partial
        /// affordability is reported through ApprovedCount.
        /// </summary>
        public PopulationTransactionResult EvaluateProfessionalRecruitment(
            Settlement settlement, CharacterObject troop, int count)
        {
            float hearth = GetHearth(settlement);
            if (settlement?.IsFortification != true || troop == null || count <= 0)
                return ProfessionalResult(null,
                    PopulationTransactionStatus.Rejected, count,
                    "InvalidProfessionalRecruitmentRequest", hearth, hearth, 0f);
            if (IsRecoveryRequired(settlement))
                return ProfessionalResult(null,
                    PopulationTransactionStatus.RecoveryRequired, count,
                    "SettlementRecoveryRequired", hearth, hearth, 0f);
            int approved = GetMaxAffordableProfessionalCount(settlement,
                troop, count);
            return ProfessionalResult(null,
                approved == count ? PopulationTransactionStatus.Committed :
                    PopulationTransactionStatus.Rejected,
                approved, approved == count ? null :
                    "InsufficientProfessionalHearth", hearth, hearth, 0f);
        }

        /// <summary>
        /// Atomically consumes Tier-adjusted Hearth for a professional batch.
        /// Mutation order is: refresh daily Hearth, re-read state, validate the
        /// reserve, deduct Hearth, then persist the operation record. Any record
        /// failure restores Hearth before returning rejection.
        /// </summary>
        public PopulationTransactionResult TryRecruitProfessional(
            Settlement settlement, CharacterObject troop, int count,
            string operationId)
        {
            float hearthBefore = GetHearth(settlement);
            if (string.IsNullOrWhiteSpace(operationId) ||
                settlement?.IsFortification != true || troop == null || count <= 0)
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.Rejected, 0,
                    "InvalidProfessionalRecruitmentRequest", hearthBefore,
                    hearthBefore, 0f);
            if (_transactions.ContainsKey(operationId))
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.Rejected, 0,
                    "OperationAlreadyExists", hearthBefore, hearthBefore, 0f);
            if (IsRecoveryRequired(settlement))
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.RecoveryRequired, 0,
                    "SettlementRecoveryRequired", hearthBefore, hearthBefore, 0f);
            int day = (int)CampaignTime.Now.ToDays;
            if (!EnsureDailyPopulationUpdate(settlement, day))
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.RecoveryRequired, 0,
                    "DailyPopulationUpdateFailed", hearthBefore, hearthBefore, 0f);
            hearthBefore = GetHearth(settlement);
            HearthEconomyConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig().HearthEconomy;
            float cost = config.GetProfessionalHearthCost(troop);
            float consumed = count * cost;
            if (!ProfessionalPopulationMath.CanRecruitExact(hearthBefore,
                    GetNaturalCapacity(settlement), config.ProfessionalReserveRatio,
                    cost, count))
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.Rejected, 0,
                    "InsufficientProfessionalHearth", hearthBefore,
                    hearthBefore, 0f);
            float hearthAfter = hearthBefore - consumed;
            try
            {
                if (!ProfessionalPopulationMath.IsFiniteNonNegative(hearthAfter))
                    throw new InvalidOperationException(
                        "ProfessionalTransactionNonFinite");
                _hearth[settlement] = hearthAfter;
                _transactions.Add(operationId,
                    PopulationTransactionRecord.ProfessionalRecruitment(
                        settlement, count, hearthBefore, hearthAfter,
                        consumed, day));
                PopulationDailyDiagnostics diagnostics =
                    GetDiagnostics(settlement);
                diagnostics.CommittedCount++;
                diagnostics.Consumed += consumed;
                diagnostics.ProfessionalRecruited += count;
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.Committed, count, null,
                    hearthBefore, hearthAfter, consumed);
            }
            catch (Exception exception)
            {
                _hearth[settlement] = hearthBefore;
                _transactions.Remove(operationId);
                ModLogger.FileError(string.Format(
                    "[ProfessionalRecruitment] transaction failed and " +
                    "rolled back: settlementName={0}, settlementId={1}, " +
                    "operationId={2}, requested={3}, hearthBefore={4:F2}, " +
                    "hearthConsumed={5:F2}, reason={6}, error={7}.",
                    settlement.Name, settlement.StringId, operationId, count,
                    hearthBefore, consumed,
                    "ProfessionalTransactionFailed", exception.Message));
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.Rejected, 0,
                    "ProfessionalTransactionFailed", hearthBefore,
                    hearthBefore, 0f);
            }
        }

        public PopulationTransactionResult EvaluateConsumption(
            Settlement settlement,
            float amount)
        {
            if (settlement == null || amount <= 0f)
            {
                return Result(null, PopulationTransactionStatus.Rejected,
                    amount, "InvalidRequest", 0f, 0f);
            }
            float hearth = GetHearth(settlement);
            if (IsRecoveryRequired(settlement))
            {
                return Reject(settlement, amount, null, hearth,
                    "SettlementRecoveryRequired",
                    PopulationTransactionStatus.RecoveryRequired);
            }
            if (hearth + 0.0001f < amount)
            {
                return Reject(settlement, amount, null, hearth,
                    "InsufficientHearth");
            }
            return Result(null, PopulationTransactionStatus.Committed,
                amount, null, hearth, hearth);
        }

        public PopulationTransactionResult TryConsume(
            Settlement settlement,
            float amount,
            string operationId)
        {
            PopulationTransactionResult validation = ValidateNewTransaction(
                settlement,
                amount,
                operationId);
            if (validation != null) return validation;

            float before = GetHearth(settlement);
            if (before + 0.0001f < amount)
            {
                return Reject(
                    settlement,
                    amount,
                    operationId,
                    before,
                    "InsufficientHearth");
            }

            float after = before - amount;
            SetHearth(settlement, after);
            _transactions.Add(
                operationId,
                new PopulationTransactionRecord(
                    settlement,
                    amount,
                    before,
                    after));
            RecordCommit(settlement, amount, false);
            LogCommitted("Consume", settlement, operationId, amount,
                before, after);
            return Result(operationId, PopulationTransactionStatus.Committed,
                amount, null, before, after);
        }

        public PopulationTransactionResult TryRestore(
            Settlement settlement,
            float amount,
            string operationId)
        {
            PopulationTransactionResult validation = ValidateNewTransaction(
                settlement,
                amount,
                operationId);
            if (validation != null) return validation;

            float before = GetHearth(settlement);
            float after = before + amount;
            SetHearth(settlement, after);
            _transactions.Add(
                operationId,
                new PopulationTransactionRecord(
                    settlement,
                    -amount,
                    before,
                    after));
            RecordCommit(settlement, amount, true);
            LogCommitted("Restore", settlement, operationId, amount,
                before, after);
            return Result(operationId, PopulationTransactionStatus.Committed,
                amount, null, before, after);
        }

        /// <summary>
        /// Compensates exactly one committed operation. New professional records
        /// restore HearthConsumed; legacy records without ID 11 restore one
        /// Hearth per recruit. Failure marks both record and settlement as
        /// RecoveryRequired instead of risking a duplicate refund.
        /// </summary>
        public PopulationTransactionResult TryCompensate(
            Settlement settlement,
            string operationId)
        {
            if (!_transactions.TryGetValue(
                    operationId,
                    out PopulationTransactionRecord record) ||
                record.Settlement != settlement)
            {
                return Reject(settlement, 0f, operationId,
                    GetHearth(settlement), "OperationNotFound");
            }

            PopulationTransactionStatus status =
                (PopulationTransactionStatus)record.Status;
            if (status == PopulationTransactionStatus.Compensated)
            {
                return ToResult(
                    operationId,
                    record,
                    "AlreadyCompensated");
            }
            if (status != PopulationTransactionStatus.Committed)
            {
                return ToResult(operationId, record,
                    "OperationNotCommitted");
            }

            float before = GetHearth(settlement);
            try
            {
                if (record.TransactionKind == 1 &&
                    record.CommittedDay != (int)CampaignTime.Now.ToDays)
                    throw new InvalidOperationException(
                        "CrossDayProfessionalCompensationUnverified");
                float compensation = record.TransactionKind == 1
                    ? (record.HearthConsumed > 0f
                        ? record.HearthConsumed
                        : Math.Abs(record.Amount))
                    : record.Amount;
                float after = before + compensation;
                if (after < -0.0001f)
                {
                    throw new InvalidOperationException(
                        "CompensationWouldCreateNegativeHearth");
                }
                SetHearth(settlement, Math.Max(0f, after));
                record.MarkCompensated(Math.Max(0f, after));
                GetDiagnostics(settlement).CompensatedCount++;
                ModLogger.FileInfo(string.Format(
                    "[Population] Compensation committed: settlement={0}, " +
                    "id={1}, operation={2}, amount={3:F2}, " +
                    "hearthBefore={4:F2}, hearthAfter={5:F2}, kind={6}.",
                    settlement.Name,
                    settlement.StringId,
                    operationId,
                    compensation, before, Math.Max(0f, after),
                    record.TransactionKind));
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.Compensated,
                    (int)Math.Abs(record.Amount), null, before,
                    Math.Max(0f, after), compensation);
            }
            catch (Exception exception)
            {
                record.MarkRecoveryRequired();
                _recovery[settlement] = true;
                ModLogger.FileError(string.Format(
                    "[Population] Compensation failed; settlement frozen: " +
                    "settlement={0}, id={1}, operation={2}, amount={3:F2}, " +
                    "hearthBefore={4:F2}, reason={5}, error={6}.",
                    settlement.Name,
                    settlement.StringId,
                    operationId,
                    Math.Abs(record.Amount),
                    before,
                    "CompensationFailed",
                    exception.Message));
                return ProfessionalResult(operationId,
                    PopulationTransactionStatus.RecoveryRequired,
                    0, exception.Message, before, before, 0f);
            }
        }

        public PopulationTransactionResult GetTransactionResult(
            string operationId)
        {
            return !string.IsNullOrWhiteSpace(operationId) &&
                _transactions.TryGetValue(
                    operationId,
                    out PopulationTransactionRecord record)
                ? ToResult(operationId, record, null)
                : Result(operationId, PopulationTransactionStatus.Rejected,
                    0f, "OperationNotFound", 0f, 0f);
        }

        public bool IsRecoveryRequired(Settlement settlement)
        {
            return settlement != null &&
                _recovery.TryGetValue(settlement, out bool value) && value;
        }

        private PopulationTransactionResult ValidateNewTransaction(
            Settlement settlement,
            float amount,
            string operationId)
        {
            if (settlement == null || amount <= 0f ||
                string.IsNullOrWhiteSpace(operationId))
            {
                ModLogger.FileError(string.Format(
                    "[Population] Invalid transaction request: " +
                    "settlement={0}, operation={1}, amount={2:F2}.",
                    settlement?.Name?.ToString() ?? "missing",
                    operationId ?? "missing",
                    amount));
                return Result(operationId,
                    PopulationTransactionStatus.Rejected,
                    amount, "InvalidRequest", 0f, 0f);
            }
            if (_transactions.TryGetValue(
                    operationId,
                    out PopulationTransactionRecord existing))
            {
                return ToResult(operationId, existing,
                    "OperationAlreadyExists");
            }
            if (IsRecoveryRequired(settlement))
            {
                return Reject(settlement, amount, operationId,
                    GetHearth(settlement), "SettlementRecoveryRequired",
                    PopulationTransactionStatus.RecoveryRequired);
            }
            return null;
        }

        private PopulationTransactionResult Reject(
            Settlement settlement,
            float amount,
            string operationId,
            float hearth,
            string reason,
            PopulationTransactionStatus status =
                PopulationTransactionStatus.Rejected)
        {
            if (settlement != null) GetDiagnostics(settlement).RejectedCount++;
            if (ShouldLogRejection(settlement, reason))
            {
                ModLogger.FileWarn(string.Format(
                    "[Population] Transaction rejected: settlement={0}, " +
                    "id={1}, operation={2}, amount={3:F2}, " +
                    "hearth={4:F2}, status={5}, reason={6}.",
                    settlement?.Name?.ToString() ?? "missing",
                    settlement?.StringId ?? "missing",
                    operationId ?? "missing",
                    amount,
                    hearth,
                    status,
                    reason));
            }
            return Result(operationId, status, amount, reason, hearth, hearth);
        }

        private void LogCommitted(
            string kind,
            Settlement settlement,
            string operationId,
            float amount,
            float before,
            float after)
        {
            if (!ShouldLog(settlement)) return;
            ModLogger.FileDebug(string.Format(
                "[Population] Transaction committed: settlement={0}, id={1}, " +
                "operation={2}, kind={3}, amount={4:F2}, " +
                "hearthBefore={5:F2}, hearthAfter={6:F2}.",
                settlement.Name,
                settlement.StringId,
                operationId,
                kind,
                amount,
                before,
                after));
        }

        private bool ShouldLogRejection(Settlement settlement, string reason)
        {
            if (!ShouldLog(settlement)) return false;
            int day = (int)CampaignTime.Now.ToDays;
            string key = (settlement?.StringId ?? "missing") + ":" + reason;
            if (_lastRejectionLogDay.TryGetValue(key, out int lastDay) &&
                lastDay == day)
            {
                return false;
            }
            _lastRejectionLogDay[key] = day;
            return true;
        }

        private void RecordCommit(
            Settlement settlement,
            float amount,
            bool restored)
        {
            PopulationDailyDiagnostics diagnostics = GetDiagnostics(settlement);
            diagnostics.CommittedCount++;
            if (restored) diagnostics.Restored += amount;
            else diagnostics.Consumed += amount;
        }

        private PopulationDailyDiagnostics GetDiagnostics(
            Settlement settlement)
        {
            if (!_dailyDiagnostics.TryGetValue(
                    settlement,
                    out PopulationDailyDiagnostics diagnostics))
            {
                diagnostics = new PopulationDailyDiagnostics();
                _dailyDiagnostics[settlement] = diagnostics;
            }
            return diagnostics;
        }

        private void LogAndResetDailyDiagnostics(
            Settlement settlement,
            int day)
        {
            if (!_dailyDiagnostics.TryGetValue(
                    settlement,
                    out PopulationDailyDiagnostics diagnostics))
            {
                return;
            }
            if (ShouldLog(settlement) &&
                (diagnostics.CommittedCount > 0 ||
                 diagnostics.CompensatedCount > 0 ||
                 diagnostics.RejectedCount > 0 ||
                 diagnostics.ProfessionalRecruited > 0))
            {
                ModLogger.FileInfo(string.Format(
                    "[Population] Daily transaction summary: " +
                    "settlement={0}, id={1}, committed={2}, " +
                    "compensated={3}, rejected={4}, consumed={5:F2}, " +
                    "restored={6:F2}, professionalRecruited={7}, " +
                    "hearth={8:F2}, supportedProsperity={9:F2}, day={10}.",
                    settlement.Name,
                    settlement.StringId,
                    diagnostics.CommittedCount,
                    diagnostics.CompensatedCount,
                    diagnostics.RejectedCount,
                    diagnostics.Consumed,
                    diagnostics.Restored,
                    diagnostics.ProfessionalRecruited, GetHearth(settlement),
                    GetHearth(settlement) *
                        (settlement.IsCastle ? 3f : 5f),
                    day));
            }
            _dailyDiagnostics.Remove(settlement);
        }

        private static bool ShouldLog(Settlement settlement)
        {
            Clan owner = settlement?.OwnerClan;
            Clan playerClan = Clan.PlayerClan;
            return owner != null && playerClan != null &&
                (owner == playerClan ||
                 owner.Kingdom != null && owner.Kingdom == playerClan.Kingdom);
        }

        private void SetHearth(Settlement settlement, float value)
        {
            if (settlement.Village != null)
                settlement.Village.Hearth = value;
            else
                _hearth[settlement] = value;
        }

        private static PopulationTransactionResult ToResult(
            string operationId,
            PopulationTransactionRecord record,
            string reason)
        {
            return ProfessionalResult(operationId,
                (PopulationTransactionStatus)record.Status,
                record.TransactionKind == 1 ? (int)Math.Abs(record.Amount) : 0,
                reason,
                record.HearthBefore,
                record.HearthAfter,
                record.TransactionKind == 1
                    ? (record.HearthConsumed > 0f
                        ? record.HearthConsumed
                        : Math.Abs(record.Amount))
                    : 0f,
                Math.Abs(record.Amount));
        }

        private static PopulationTransactionResult Result(
            string operationId,
            PopulationTransactionStatus status,
            float amount,
            string reason,
            float before,
            float after)
        {
            return new PopulationTransactionResult
            {
                OperationId = operationId,
                Status = status,
                Amount = amount,
                ReasonCode = reason,
                HearthBefore = before,
                HearthAfter = after
            };
        }

        private static PopulationTransactionResult ProfessionalResult(
            string operationId, PopulationTransactionStatus status,
            int approved, string reason, float hearthBefore,
            float hearthAfter, float hearthConsumed,
            float? amount = null)
        {
            return new PopulationTransactionResult
            {
                OperationId = operationId,
                Status = status,
                Amount = amount ?? approved,
                ApprovedCount = approved,
                ReasonCode = reason,
                HearthBefore = hearthBefore,
                HearthAfter = hearthAfter,
                HearthConsumed = hearthConsumed
            };
        }
    }
}
