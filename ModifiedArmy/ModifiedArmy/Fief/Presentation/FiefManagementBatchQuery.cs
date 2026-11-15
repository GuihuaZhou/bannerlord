// Builds detached read models from authoritative fief service/return batches.
// Queries never advance timers, reconcile parties, merge saved batches, or
// write troop counts back into campaign state.
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.Fief.Presentation
{
    internal sealed class FiefManagementBatchSnapshot
    {
        internal string GroupKey { get; set; }
        internal string HeaderText { get; set; }
        internal int TotalCount { get; set; }
        internal IReadOnlyList<KeyValuePair<CharacterObject, int>> Troops
            { get; set; }
    }

    internal static class FiefManagementBatchQuery
    {
        /// <summary>
        /// Returns one snapshot per real service batch. Party grouping affects
        /// sort order only; equal troops or service days never merge batches.
        /// </summary>
        internal static IReadOnlyList<FiefManagementBatchSnapshot> Serving(
            FiefPartyData fiefData, Settlement villageFilter)
        {
            if (fiefData == null)
                return Array.Empty<FiefManagementBatchSnapshot>();

            var snapshots = new List<Tuple<string, int, string,
                FiefManagementBatchSnapshot>>();
            foreach (VillageFiefData village in fiefData.OrderedVillages(
                villageFilter))
            foreach (FiefServiceBatch batch in village.ServiceBatches ??
                new List<FiefServiceBatch>())
            {
                IReadOnlyList<KeyValuePair<CharacterObject, int>> troops =
                    PositiveTroops(batch?.Troops);
                if (batch == null || troops.Count == 0) continue;

                MobileParty party = batch.ServingParty ?? MobileParty.All
                    .FirstOrDefault(x => x?.StringId == batch.ServingPartyId);
                string partyName = party?.Name?.ToString();
                if (string.IsNullOrWhiteSpace(partyName))
                {
                    TextObject unknown = new TextObject(
                        "{=ModifiedArmy_FiefBatch_UnknownParty}" +
                        "Unknown serving party ({PARTY_ID})");
                    unknown.SetTextVariable("PARTY_ID",
                        batch.ServingPartyId ?? "unknown");
                    partyName = unknown.ToString();
                }

                int serviceDays = Math.Max(0, batch.ServiceDays);
                int freeRemaining = Math.Max(0,
                    batch.FreeWageDays - serviceDays);
                TextObject header = new TextObject(
                    "{=ModifiedArmy_FiefBatch_ServiceRemaining}" +
                    "{PARTY} · service time remaining: {DAYS} days");
                header.SetTextVariable("PARTY", partyName);
                header.SetTextVariable("DAYS", freeRemaining);

                string batchId = string.IsNullOrEmpty(batch.BatchId)
                    ? "missing" : batch.BatchId;
                string villageId = village.VillageSettlement?.StringId ??
                    "unknown";
                var snapshot = new FiefManagementBatchSnapshot
                {
                    GroupKey = "service:" + villageId + ":" + batchId,
                    HeaderText = header.ToString(),
                    TotalCount = troops.Sum(x => x.Value),
                    Troops = troops
                };
                snapshots.Add(Tuple.Create(batch.ServingPartyId ?? string.Empty,
                    serviceDays, batchId, snapshot));
            }
            return snapshots.OrderBy(x => x.Item1, StringComparer.Ordinal)
                .ThenBy(x => x.Item2).ThenBy(x => x.Item3,
                    StringComparer.Ordinal).Select(x => x.Item4).ToList();
        }

        /// <summary>
        /// Aggregates return records by their saved week counter for display
        /// only. Source records and their per-village ownership remain intact.
        /// </summary>
        internal static IReadOnlyList<FiefManagementBatchSnapshot> Cooling(
            FiefPartyData fiefData, Settlement villageFilter)
        {
            if (fiefData == null)
                return Array.Empty<FiefManagementBatchSnapshot>();
            var groups = new SortedDictionary<int,
                Dictionary<CharacterObject, int>>();
            foreach (VillageFiefData village in fiefData.OrderedVillages(
                villageFilter))
            foreach (FiefReturnBatch batch in village.ReturnBatches ??
                new List<FiefReturnBatch>())
            {
                if (batch == null || batch.IsEmpty()) continue;
                int weeks = Math.Max(0, batch.WaitWeeks);
                if (!groups.TryGetValue(weeks,
                    out Dictionary<CharacterObject, int> aggregate))
                    groups[weeks] = aggregate =
                        new Dictionary<CharacterObject, int>();
                foreach (var pair in batch.Troops)
                    if (pair.Key != null && pair.Value > 0)
                        aggregate[pair.Key] = aggregate.TryGetValue(pair.Key,
                            out int old) ? old + pair.Value : pair.Value;
            }

            return groups.Select(group =>
            {
                IReadOnlyList<KeyValuePair<CharacterObject, int>> troops =
                    PositiveTroops(group.Value);
                TextObject header = group.Key > 0
                    ? new TextObject(
                        "{=ModifiedArmy_FiefBatch_Cooling}" +
                        "{WEEKS} weeks remaining")
                    : new TextObject(
                        "{=ModifiedArmy_FiefBatch_CoolingPending}" +
                        "Pending (0 weeks)");
                if (group.Key > 0)
                    header.SetTextVariable("WEEKS", group.Key);
                return new FiefManagementBatchSnapshot
                {
                    GroupKey = "cooldown:" + group.Key,
                    HeaderText = header.ToString(),
                    TotalCount = troops.Sum(x => x.Value),
                    Troops = troops
                };
            }).ToList();
        }

        private static IReadOnlyList<KeyValuePair<CharacterObject, int>>
            PositiveTroops(IDictionary<CharacterObject, int> troops)
        {
            return (troops ?? new Dictionary<CharacterObject, int>())
                .Where(x => x.Key != null && x.Value > 0)
                .OrderBy(x => x.Key.StringId, StringComparer.Ordinal)
                .ToList();
        }
    }
}
