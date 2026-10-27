using ModifiedArmy.ForceManagement.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>Persists terminal operation ids and recovery freezes.</summary>
    public sealed class TransferOperationJournalBehavior : CampaignBehaviorBase
    {
        private const int MaximumCompletedRecords = 2048;
        private Dictionary<string, string> _records =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_transfer_operation_journal_v1",
                ref _records);
            _records ??= new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public bool Contains(string operationId) =>
            !string.IsNullOrWhiteSpace(operationId) &&
            _records.ContainsKey(operationId);

        public bool IsDomainFrozen(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return false;
            return _records.Values.Any(value =>
                Parse(value, out TransferCommitStatus status, out _, out _,
                    out string frozenDomain, out _) &&
                status == TransferCommitStatus.RecoveryRequired &&
                string.Equals(frozenDomain, domain, StringComparison.Ordinal));
        }

        public void Record(PartyExchangeExecutionResult result, int today)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.OperationId) ||
                (result.Status != TransferCommitStatus.Committed &&
                 result.Status != TransferCommitStatus.PartiallyCommitted &&
                 result.Status != TransferCommitStatus.RecoveryRequired))
                return;
            _records[result.OperationId] = string.Join("\u001f", new[]
            {
                ((int)result.Status).ToString(CultureInfo.InvariantCulture),
                Math.Max(0, result.TransferredCount)
                    .ToString(CultureInfo.InvariantCulture),
                result.SourceFingerprint ?? string.Empty,
                result.DestinationFingerprint ?? string.Empty,
                result.FreezeDomain ?? string.Empty,
                today.ToString(CultureInfo.InvariantCulture)
            });
            TrimCompletedRecords();
        }

        private void TrimCompletedRecords()
        {
            int removable = _records.Count - MaximumCompletedRecords;
            if (removable <= 0) return;
            foreach (string id in _records
                .Where(pair => Parse(pair.Value,
                    out TransferCommitStatus status, out _, out _, out _,
                    out _) && status != TransferCommitStatus.RecoveryRequired)
                .OrderBy(pair => ReadDay(pair.Value)).Take(removable)
                .Select(pair => pair.Key).ToList())
                _records.Remove(id);
        }

        private static int ReadDay(string value) =>
            Parse(value, out _, out _, out _, out _, out int day)
                ? day : int.MinValue;

        private static bool Parse(
            string value,
            out TransferCommitStatus status,
            out string sourceFingerprint,
            out string destinationFingerprint,
            out string domain,
            out int day)
        {
            status = TransferCommitStatus.Rejected;
            sourceFingerprint = destinationFingerprint = domain = string.Empty;
            day = 0;
            string[] parts = (value ?? string.Empty).Split('\u001f');
            if (parts.Length < 6 ||
                !int.TryParse(parts[0], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int rawStatus) ||
                !Enum.IsDefined(typeof(TransferCommitStatus), rawStatus) ||
                !int.TryParse(parts[5], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out day))
                return false;
            status = (TransferCommitStatus)rawStatus;
            sourceFingerprint = parts[2];
            destinationFingerprint = parts[3];
            domain = parts[4];
            return true;
        }
    }
}
