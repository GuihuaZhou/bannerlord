using System;
using System.Collections.Generic;
using ModifiedArmy.Population.Contracts;
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
    }

    public sealed class PopulationTransactionRecord
    {
        [SaveableProperty(1)] public Settlement Settlement { get; private set; }
        [SaveableProperty(2)] public float Amount { get; private set; }
        [SaveableProperty(3)] public int Status { get; private set; }
        [SaveableProperty(4)] public float HearthBefore { get; private set; }
        [SaveableProperty(5)] public float HearthAfter { get; private set; }

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

        public void MarkCompensated(float hearthAfter)
        {
            HearthAfter = hearthAfter;
            Status = (int)PopulationTransactionStatus.Compensated;
        }

        public void MarkRecoveryRequired()
        {
            Status = (int)PopulationTransactionStatus.RecoveryRequired;
        }
    }

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

        private Dictionary<Settlement, float> _hearth = new();
        private Dictionary<string, PopulationTransactionRecord> _transactions =
            new();
        private Dictionary<Settlement, bool> _recovery = new();
        private Dictionary<Settlement, int> _lastDailyTick = new();
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

        public override void SyncData(IDataStore store)
        {
            store.SyncData(HearthKey, ref _hearth);
            store.SyncData(TransactionKey, ref _transactions);
            store.SyncData(RecoveryKey, ref _recovery);
            store.SyncData(DailyTickKey, ref _lastDailyTick);
            _hearth ??= new Dictionary<Settlement, float>();
            _transactions ??=
                new Dictionary<string, PopulationTransactionRecord>();
            _recovery ??= new Dictionary<Settlement, bool>();
            _lastDailyTick ??= new Dictionary<Settlement, int>();
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
                PopulationWallConfig wall = GetWall(settlement, level);
                _hearth.Add(settlement, wall.Initial);
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

            if (_lastDailyTick.TryGetValue(settlement, out int settledDay) &&
                settledDay == day)
            {
                return;
            }

            if (!_hearth.ContainsKey(settlement)) EnsureFortifications();
            if (!_hearth.TryGetValue(settlement, out float before)) return;
            float growth = PopulationGrowthModel.Calculate(
                before,
                GetNaturalCapacity(settlement),
                SettlementEconomyConfigManager.Instance.RequireActive());
            float after = Math.Max(0f, before + growth);
            _lastDailyTick[settlement] = day;
            _hearth[settlement] = after;
            if (ShouldLog(settlement))
            {
                ModLogger.FileDebug(string.Format(
                    "[Population] Daily growth: settlement={0}, id={1}, " +
                    "hearthBefore={2:F2}, naturalChange={3:F3}, " +
                    "hearthAfter={4:F2}, capacity={5:F2}, day={6}.",
                    settlement.Name,
                    settlement.StringId,
                    before,
                    growth,
                    after,
                    GetNaturalCapacity(settlement),
                    day));
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
            if (settlement?.Village != null) return config.VillageCapacity;
            if (settlement?.Town == null) return 0f;
            int level = Math.Max(
                1,
                Math.Min(3, settlement.Town.GetWallLevel()));
            return GetWall(settlement, level).Capacity;
        }

        public int GetRecruitableHearth(Settlement settlement)
        {
            return IsRecoveryRequired(settlement)
                ? 0
                : Math.Max(0, (int)Math.Floor(GetHearth(settlement)));
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
                float after = before + record.Amount;
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
                    "hearthBefore={4:F2}, hearthAfter={5:F2}.",
                    settlement.Name,
                    settlement.StringId,
                    operationId,
                    Math.Abs(record.Amount),
                    before,
                    Math.Max(0f, after)));
                return Result(operationId,
                    PopulationTransactionStatus.Compensated,
                    Math.Abs(record.Amount), null, before, Math.Max(0f, after));
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
                return Result(operationId,
                    PopulationTransactionStatus.RecoveryRequired,
                    Math.Abs(record.Amount), "CompensationFailed",
                    before, before);
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
                 diagnostics.RejectedCount > 0))
            {
                ModLogger.FileInfo(string.Format(
                    "[Population] Daily transaction summary: " +
                    "settlement={0}, id={1}, committed={2}, " +
                    "compensated={3}, rejected={4}, consumed={5:F2}, " +
                    "restored={6:F2}, hearth={7:F2}, day={8}.",
                    settlement.Name,
                    settlement.StringId,
                    diagnostics.CommittedCount,
                    diagnostics.CompensatedCount,
                    diagnostics.RejectedCount,
                    diagnostics.Consumed,
                    diagnostics.Restored,
                    GetHearth(settlement),
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

        private static PopulationWallConfig GetWall(
            Settlement settlement,
            int level)
        {
            SettlementEconomyConfig config =
                SettlementEconomyConfigManager.Instance.RequireActive();
            return (settlement.IsCastle
                ? config.CastleWalls
                : config.TownWalls)[level];
        }

        private static PopulationTransactionResult ToResult(
            string operationId,
            PopulationTransactionRecord record,
            string reason)
        {
            return Result(operationId,
                (PopulationTransactionStatus)record.Status,
                Math.Abs(record.Amount),
                reason,
                record.HearthBefore,
                record.HearthAfter);
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
    }
}
