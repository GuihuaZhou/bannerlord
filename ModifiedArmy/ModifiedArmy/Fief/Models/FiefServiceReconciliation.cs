using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Models.Fief
{
    internal readonly struct ServingDetachmentRef
    {
        public Settlement SourceFief { get; }
        public Settlement SourceVillage { get; }
        public FiefServiceBatch Detachment { get; }

        public ServingDetachmentRef(Settlement sourceFief,
            Settlement sourceVillage, FiefServiceBatch detachment)
        {
            SourceFief = sourceFief;
            SourceVillage = sourceVillage;
            Detachment = detachment;
        }
    }

    public sealed class FiefReconciliationResult
    {
        public int MatchedCount { get; internal set; }
        public int LostCount { get; internal set; }
        public bool BlockedByRecovery { get; internal set; }
        public string ReasonCode { get; internal set; }
        public bool PlacementSnapshotValid { get; internal set; }
        public IReadOnlyDictionary<CharacterObject, FiefTroopCount>
            UnassignedTroops { get; internal set; } =
                new ReadOnlyDictionary<CharacterObject, FiefTroopCount>(
                    new Dictionary<CharacterObject, FiefTroopCount>());
    }

    internal static class FiefServiceReconciler
    {
        public static FiefReconciliationResult Reconcile(
            FiefPartyManager manager,
            MobileParty party,
            bool includeUnassigned = false)
        {
            FiefReconciliationResult result = new();
            if (manager == null || party?.MemberRoster == null) return result;
            List<ServingDetachmentRef> refs = manager
                .GetServingDetachments(party).ToList();
            Dictionary<CharacterObject, FiefTroopCount> actualSnapshot = party
                .MemberRoster.GetTroopRoster()
                .Where(e => e.Character != null && !e.Character.IsHero &&
                    e.Number > 0)
                .GroupBy(e => e.Character)
                .ToDictionary(g => g.Key, g => new FiefTroopCount(
                    g.Sum(e => e.Number), g.Sum(e => e.WoundedNumber)));
            if (refs.Count == 0)
            {
                if (includeUnassigned)
                    SetPlacementSnapshot(result, actualSnapshot);
                result.ReasonCode = "NoServingDetachments";
                return result;
            }
            string reason = manager.IsServicePartyBlocked(party)
                ? "LedgerBlockedParty"
                : null;
            if (reason != null ||
                !ValidatePartyRefs(manager, party, refs, out reason))
            {
                result.BlockedByRecovery = true;
                result.ReasonCode = reason ?? "BlockedServiceParty";
                return result;
            }

            Dictionary<CharacterObject, int> actual = actualSnapshot
                .ToDictionary(pair => pair.Key, pair => pair.Value.Total);
            Dictionary<CharacterObject, int> registered = refs
                .SelectMany(r => r.Detachment.Troops)
                .Where(p => p.Key != null && p.Value > 0)
                .GroupBy(p => p.Key)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Value));

            ReconciliationMatchPlan matchPlan = CalculatePreferredMatches(
                registered, actualSnapshot);
            Dictionary<(CharacterObject From, CharacterObject To), int>
                matches = matchPlan.Matches;

            Dictionary<FiefServiceBatch, Dictionary<CharacterObject, int>> plans =
                refs.ToDictionary(r => r.Detachment, _ => new Dictionary<CharacterObject, int>());
            foreach (var group in matches.GroupBy(p => p.Key.From))
            {
                List<ServingDetachmentRef> eligible = refs
                    .Where(r => r.Detachment.Troops.TryGetValue(group.Key, out int n) && n > 0)
                    .OrderBy(r => r.Detachment.BatchId, StringComparer.Ordinal).ToList();
                int registeredForTroop = eligible.Sum(r => r.Detachment.Troops[group.Key]);
                int totalMatched = group.Sum(p => p.Value);
                Dictionary<FiefServiceBatch, int> quotas = new();
                List<(FiefServiceBatch Batch, double Remainder)> remainders = new();
                int allocated = 0;
                foreach (ServingDetachmentRef item in eligible)
                {
                    double exact = registeredForTroop <= 0 ? 0d :
                        (double)totalMatched * item.Detachment.Troops[group.Key] / registeredForTroop;
                    int quota = Math.Min(item.Detachment.Troops[group.Key], (int)Math.Floor(exact));
                    quotas[item.Detachment] = quota;
                    allocated += quota;
                    remainders.Add((item.Detachment, exact - quota));
                }
                foreach (var item in remainders
                    .OrderByDescending(p => p.Remainder)
                    .ThenBy(p => p.Batch.BatchId, StringComparer.Ordinal))
                {
                    if (allocated >= totalMatched) break;
                    if (quotas[item.Batch] >= item.Batch.Troops[group.Key]) continue;
                    quotas[item.Batch]++;
                    allocated++;
                }

                foreach (var match in group.OrderBy(p => p.Key.To.StringId))
                {
                    int remainingMatch = match.Value;
                    foreach (ServingDetachmentRef item in eligible)
                    {
                        if (remainingMatch <= 0) break;
                        int quota = quotas[item.Detachment];
                        if (quota <= 0) continue;
                        int assigned = Math.Min(quota, remainingMatch);
                        Add(plans[item.Detachment], match.Key.To, assigned);
                        quotas[item.Detachment] -= assigned;
                        remainingMatch -= assigned;
                    }
                }
            }

            Dictionary<FiefServiceBatch, Dictionary<CharacterObject, int>>
                snapshots = refs.ToDictionary(
                    item => item.Detachment,
                    item => item.Detachment.Troops.ToDictionary(
                        pair => pair.Key, pair => pair.Value));
            Dictionary<Settlement, int> lossesByVillage = refs
                .GroupBy(item => item.SourceVillage)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(item => Math.Max(0,
                        item.Detachment.GetTotalCount() -
                        plans[item.Detachment].Values.Sum())));
            Dictionary<Settlement, int> mobilizationSnapshots =
                lossesByVillage.Keys.ToDictionary(
                    village => village,
                    village => manager.GetVillageFiefData(village)?
                        .MobilizedHearth ?? -1);
            foreach (var loss in lossesByVillage)
            {
                VillageFiefData village = manager.GetVillageFiefData(loss.Key);
                if (village == null || village.RecoveryRequired ||
                    village.MobilizedHearth < loss.Value)
                {
                    village?.RequireRecovery("ReconciliationPrecheckFailed");
                    result.BlockedByRecovery = true;
                    result.ReasonCode = "ReconciliationPrecheckFailed";
                    return result;
                }
            }

            Dictionary<Settlement, int> released = new();
            try
            {
                foreach (ServingDetachmentRef item in refs)
                    item.Detachment.ReplaceTroops(plans[item.Detachment]);
                foreach (var loss in lossesByVillage.Where(p => p.Value > 0))
                {
                    if (manager.ReleaseVillageFiefService(
                        loss.Key, loss.Value, false) != loss.Value)
                        throw new InvalidOperationException(
                            "MobilizationReleaseFailed");
                    released[loss.Key] = loss.Value;
                }
            }
            catch
            {
                bool restored = true;
                foreach (ServingDetachmentRef item in refs)
                {
                    try { item.Detachment.ReplaceTroops(
                        snapshots[item.Detachment]); }
                    catch { restored = false; }
                }
                foreach (var release in released)
                {
                    try
                    {
                        manager.GetVillageFiefData(release.Key)?
                            .RollbackMobilization(release.Value, false);
                    }
                    catch { restored = false; }
                }
                restored = restored && refs.All(item =>
                    DictionaryEquals(item.Detachment.Troops,
                        snapshots[item.Detachment])) &&
                    mobilizationSnapshots.All(item =>
                        manager.GetVillageFiefData(item.Key)?
                            .MobilizedHearth == item.Value);
                if (!restored)
                    foreach (ServingDetachmentRef item in refs)
                        manager.GetVillageFiefData(item.SourceVillage)?
                            .RequireRecovery(
                                "ReconciliationRollbackUnverified");
                result.BlockedByRecovery = !restored;
                result.ReasonCode = restored
                    ? "ReconciliationRolledBackVerified"
                    : "ReconciliationRollbackUnverified";
                return result;
            }

            foreach (ServingDetachmentRef item in refs)
            {
                int after = plans[item.Detachment].Values.Sum();
                result.MatchedCount += after;
                result.LostCount += Math.Max(0,
                    snapshots[item.Detachment].Values.Sum() - after);
                if (item.Detachment.IsEmpty())
                {
                    manager.GetVillageFiefData(item.SourceVillage)?
                        .ServiceBatches.Remove(item.Detachment);
                    manager.UnregisterServingDetachment(item.SourceFief,
                        item.SourceVillage, item.Detachment);
                }
                manager.GetFiefData(item.SourceFief)?.RefreshManpowerCounters();
            }
            result.ReasonCode = "Completed";
            if (includeUnassigned)
            {
                Dictionary<CharacterObject, FiefTroopCount> unassigned =
                    new();
                foreach (var pair in actualSnapshot)
                {
                    int matched = matches.Where(item => item.Key.To == pair.Key)
                        .Sum(item => item.Value);
                    int matchedWounded = matchPlan.MatchedWounded.TryGetValue(
                        pair.Key, out int wounded) ? wounded : 0;
                    int remaining = Math.Max(0, pair.Value.Total - matched);
                    int remainingWounded = Math.Max(0,
                        pair.Value.Wounded - matchedWounded);
                    if (remaining > 0)
                        unassigned[pair.Key] = new FiefTroopCount(
                            remaining, Math.Min(remaining, remainingWounded));
                }
                SetPlacementSnapshot(result, unassigned);
            }
            return result;
        }

        private static void SetPlacementSnapshot(FiefReconciliationResult result,
            IReadOnlyDictionary<CharacterObject, FiefTroopCount> troops)
        {
            result.UnassignedTroops = new ReadOnlyDictionary<CharacterObject,
                FiefTroopCount>(troops.ToDictionary(
                    pair => pair.Key, pair => pair.Value));
            result.PlacementSnapshotValid = true;
        }

        private static bool ValidatePartyRefs(FiefPartyManager manager,
            MobileParty party, IReadOnlyList<ServingDetachmentRef> refs,
            out string reason)
        {
            reason = null;
            HashSet<string> ids = new(StringComparer.Ordinal);
            foreach (ServingDetachmentRef item in refs)
            {
                VillageFiefData village = manager.GetVillageFiefData(
                    item.SourceVillage);
                FiefServiceBatch batch = item.Detachment;
                if (village == null || village.RecoveryRequired ||
                    batch == null || batch.IsEmpty() ||
                    string.IsNullOrEmpty(batch.BatchId) ||
                    !ids.Add(batch.BatchId) ||
                    batch.SourceFief != item.SourceFief ||
                    batch.SourceVillage != item.SourceVillage ||
                    batch.ServingParty != party ||
                    !village.ServiceBatches.Contains(batch))
                {
                    village?.RequireRecovery("ServicePartyValidationFailed");
                    reason = "ServicePartyValidationFailed";
                    return false;
                }
            }
            return true;
        }

        private static bool DictionaryEquals(
            IReadOnlyDictionary<CharacterObject, int> left,
            IReadOnlyDictionary<CharacterObject, int> right) =>
            left != null && right != null && left.Count == right.Count &&
            left.All(pair => right.TryGetValue(pair.Key, out int value) &&
                value == pair.Value);

        private static void Add(Dictionary<CharacterObject, int> map, CharacterObject troop, int count)
        {
            map[troop] = map.TryGetValue(troop, out int old) ? old + count : count;
        }

        private sealed class ReconciliationMatchPlan
        {
            public Dictionary<(CharacterObject From, CharacterObject To), int>
                Matches { get; } = new();
            public Dictionary<CharacterObject, int> MatchedWounded { get; } =
                new();
        }

        private sealed class FlowEdge
        {
            public int To;
            public int Reverse;
            public int Capacity;
            public int Cost;
            public int InitialCapacity;
        }

        /// <summary>
        /// Maximum-cardinality, minimum-cost matching. Wounded assignments
        /// carry the dominant cost, so health is maximized without reducing
        /// the number of protected serving identities. Exact matches then win
        /// over upgrade-tree substitutions; stable node order breaks ties.
        /// </summary>
        private static ReconciliationMatchPlan CalculatePreferredMatches(
            IReadOnlyDictionary<CharacterObject, int> registered,
            IReadOnlyDictionary<CharacterObject, FiefTroopCount> actual)
        {
            List<CharacterObject> left = registered
                .Where(p => p.Value > 0).Select(p => p.Key)
                .OrderBy(t => t.StringId).ToList();
            List<CharacterObject> right = actual
                .Where(p => p.Value.Total > 0).Select(p => p.Key)
                .OrderBy(t => t.StringId).ToList();
            int source = 0;
            int leftStart = 1;
            int healthyStart = leftStart + left.Count;
            int woundedStart = healthyStart + right.Count;
            int sink = woundedStart + right.Count;
            int size = sink + 1;
            int healthyPriorityCost = Math.Max(2,
                registered.Values.Where(value => value > 0).Sum() + 1);
            List<FlowEdge>[] graph = Enumerable.Range(0, size)
                .Select(_ => new List<FlowEdge>()).ToArray();
            void AddEdge(int from, int to, int capacity, int cost)
            {
                FlowEdge forward = new FlowEdge { To = to,
                    Reverse = graph[to].Count, Capacity = capacity,
                    InitialCapacity = capacity, Cost = cost };
                FlowEdge reverse = new FlowEdge { To = from,
                    Reverse = graph[from].Count, Capacity = 0,
                    InitialCapacity = 0, Cost = -cost };
                graph[from].Add(forward);
                graph[to].Add(reverse);
            }
            for (int i = 0; i < left.Count; i++)
            {
                AddEdge(source, leftStart + i, registered[left[i]], 0);
                for (int j = 0; j < right.Count; j++)
                    if (CanMatchUpgradeTree(left[i], right[j]))
                    {
                        int exactCost = left[i] == right[j] ? 0 : 1;
                        AddEdge(leftStart + i, healthyStart + j,
                            int.MaxValue / 4, exactCost);
                        AddEdge(leftStart + i, woundedStart + j,
                            int.MaxValue / 4,
                            healthyPriorityCost + exactCost);
                    }
            }
            for (int j = 0; j < right.Count; j++)
            {
                AddEdge(healthyStart + j, sink, actual[right[j]].Healthy, 0);
                AddEdge(woundedStart + j, sink, actual[right[j]].Wounded, 0);
            }

            while (true)
            {
                long[] distance = Enumerable.Repeat(long.MaxValue, size)
                    .ToArray();
                int[] parentNode = Enumerable.Repeat(-1, size).ToArray();
                int[] parentEdge = Enumerable.Repeat(-1, size).ToArray();
                distance[source] = 0;
                for (int pass = 0; pass < size - 1; pass++)
                {
                    bool changed = false;
                    for (int node = 0; node < size; node++)
                    {
                        if (distance[node] == long.MaxValue) continue;
                        for (int edgeIndex = 0;
                            edgeIndex < graph[node].Count; edgeIndex++)
                        {
                            FlowEdge edge = graph[node][edgeIndex];
                            long candidate = distance[node] + edge.Cost;
                            if (edge.Capacity <= 0 ||
                                candidate >= distance[edge.To]) continue;
                            distance[edge.To] = candidate;
                            parentNode[edge.To] = node;
                            parentEdge[edge.To] = edgeIndex;
                            changed = true;
                        }
                    }
                    if (!changed) break;
                }
                if (parentNode[sink] < 0) break;
                int augment = int.MaxValue;
                for (int node = sink; node != source;
                    node = parentNode[node])
                    augment = Math.Min(augment, graph[parentNode[node]][
                        parentEdge[node]].Capacity);
                for (int node = sink; node != source;
                    node = parentNode[node])
                {
                    FlowEdge edge = graph[parentNode[node]][parentEdge[node]];
                    edge.Capacity -= augment;
                    graph[node][edge.Reverse].Capacity += augment;
                }
            }

            ReconciliationMatchPlan result = new();
            for (int i = 0; i < left.Count; i++)
                for (int j = 0; j < right.Count; j++)
                {
                    int healthyFlow = GetFlow(graph[leftStart + i],
                        healthyStart + j);
                    int woundedFlow = GetFlow(graph[leftStart + i],
                        woundedStart + j);
                    int total = healthyFlow + woundedFlow;
                    if (total > 0)
                        result.Matches[(left[i], right[j])] = total;
                    if (woundedFlow > 0)
                        Add(result.MatchedWounded, right[j], woundedFlow);
                }
            return result;
        }

        private static int GetFlow(IEnumerable<FlowEdge> edges, int to) =>
            edges.Where(edge => edge.To == to && edge.InitialCapacity > 0)
                .Sum(edge => edge.InitialCapacity - edge.Capacity);

        /// <summary>
        /// 判断两个具体兵种是否位于同一条可达升级链。返乡对账只关心
        /// 实际升级关系，不应借用带五类语义的全局分类器。
        /// </summary>
        private static bool CanMatchUpgradeTree(CharacterObject left, CharacterObject right)
        {
            return left != null && right != null &&
                (left == right || CanUpgradeTo(left, right) || CanUpgradeTo(right, left));
        }

        private static bool CanUpgradeTo(CharacterObject from, CharacterObject to)
        {
            bool startedFromBasicTroop = from?.IsBasicTroop == true;
            HashSet<CharacterObject> visited = new();
            Stack<CharacterObject> pending = new();
            pending.Push(from);
            while (pending.Count > 0)
            {
                CharacterObject current = pending.Pop();
                if (current == null || !visited.Add(current)) continue;
                foreach (CharacterObject target in current.UpgradeTargets ??
                    Array.Empty<CharacterObject>())
                {
                    // A basic troop is the root of one military recruitment
                    // tree. Civilian predecessors (for example a villager
                    // upgrading into that basic troop) must not be treated as
                    // members of the military tree during reconciliation.
                    if (!startedFromBasicTroop &&
                        target?.IsBasicTroop == true)
                        continue;
                    if (target == to) return true;
                    if (target != null) pending.Push(target);
                }
            }
            return false;
        }
    }
}
