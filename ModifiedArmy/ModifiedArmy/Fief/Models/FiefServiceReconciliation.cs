using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
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
        public FiefServiceBatch Detachment { get; }

        public ServingDetachmentRef(Settlement sourceFief, FiefServiceBatch detachment)
        {
            SourceFief = sourceFief;
            Detachment = detachment;
        }
    }

    public sealed class FiefReconciliationResult
    {
        public int MatchedCount { get; internal set; }
        public int LostCount { get; internal set; }
    }

    internal static class FiefServiceReconciler
    {
        public static FiefReconciliationResult Reconcile(
            FiefPartyManager manager,
            MobileParty party)
        {
            FiefReconciliationResult result = new();
            if (manager == null || party == null) return result;
            List<ServingDetachmentRef> refs = manager.GetServingDetachments(party)
                .Where(r => r.Detachment != null && !r.Detachment.IsEmpty())
                .ToList();
            if (refs.Count == 0) return result;

            Dictionary<CharacterObject, int> actual = party.MemberRoster
                .GetTroopRoster()
                .Where(e => e.Character != null && !e.Character.IsHero && e.Number > 0)
                .GroupBy(e => e.Character)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Number));
            Dictionary<CharacterObject, int> registered = refs
                .SelectMany(r => r.Detachment.Troops)
                .Where(p => p.Key != null && p.Value > 0)
                .GroupBy(p => p.Key)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Value));

            Dictionary<(CharacterObject From, CharacterObject To), int> matches = new();
            foreach (CharacterObject troop in registered.Keys.ToList())
            {
                int exact = Math.Min(registered[troop], actual.TryGetValue(troop, out int n) ? n : 0);
                if (exact <= 0) continue;
                matches[(troop, troop)] = exact;
                registered[troop] -= exact;
                actual[troop] -= exact;
            }

            foreach (var match in CalculateMaximumTreeMatches(registered, actual))
                matches[match.Key] = match.Value;

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

            foreach (ServingDetachmentRef item in refs)
            {
                int before = item.Detachment.GetTotalCount();
                Dictionary<CharacterObject, int> plan = plans[item.Detachment];
                int after = plan.Values.Sum();
                item.Detachment.ReplaceTroops(plan);
                result.MatchedCount += after;
                int lost = Math.Max(0, before - after);
                result.LostCount += lost;
                if (lost > 0) manager.ReleaseFiefService(item.SourceFief, lost, false);
                if (item.Detachment.IsEmpty())
                {
                    manager.GetFiefData(item.SourceFief)?.ServiceBatches.Remove(item.Detachment);
                    manager.UnregisterServingDetachment(item.SourceFief, item.Detachment);
                }
                manager.GetFiefData(item.SourceFief)?.RefreshManpowerCounters();
            }
            return result;
        }

        private static void Add(Dictionary<CharacterObject, int> map, CharacterObject troop, int count)
        {
            map[troop] = map.TryGetValue(troop, out int old) ? old + count : count;
        }

        private static Dictionary<(CharacterObject From, CharacterObject To), int>
            CalculateMaximumTreeMatches(
                IReadOnlyDictionary<CharacterObject, int> registered,
                IReadOnlyDictionary<CharacterObject, int> actual)
        {
            List<CharacterObject> left = registered
                .Where(p => p.Value > 0).Select(p => p.Key)
                .OrderBy(t => t.StringId).ToList();
            List<CharacterObject> right = actual
                .Where(p => p.Value > 0).Select(p => p.Key)
                .OrderBy(t => t.StringId).ToList();
            int source = 0;
            int leftStart = 1;
            int rightStart = leftStart + left.Count;
            int sink = rightStart + right.Count;
            int size = sink + 1;
            int[,] capacity = new int[size, size];
            int[,] flow = new int[size, size];
            for (int i = 0; i < left.Count; i++)
            {
                capacity[source, leftStart + i] = registered[left[i]];
                for (int j = 0; j < right.Count; j++)
                    if (SoldierTypeClassifier.CanMatchUpgradeTree(left[i], right[j]))
                        capacity[leftStart + i, rightStart + j] = int.MaxValue / 4;
            }
            for (int j = 0; j < right.Count; j++)
                capacity[rightStart + j, sink] = actual[right[j]];

            while (true)
            {
                int[] parent = Enumerable.Repeat(-1, size).ToArray();
                Queue<int> queue = new();
                parent[source] = source;
                queue.Enqueue(source);
                while (queue.Count > 0 && parent[sink] < 0)
                {
                    int node = queue.Dequeue();
                    for (int next = 0; next < size; next++)
                    {
                        if (parent[next] >= 0 || capacity[node, next] - flow[node, next] <= 0)
                            continue;
                        parent[next] = node;
                        queue.Enqueue(next);
                    }
                }
                if (parent[sink] < 0) break;
                int augment = int.MaxValue;
                for (int node = sink; node != source; node = parent[node])
                    augment = Math.Min(augment,
                        capacity[parent[node], node] - flow[parent[node], node]);
                for (int node = sink; node != source; node = parent[node])
                {
                    flow[parent[node], node] += augment;
                    flow[node, parent[node]] -= augment;
                }
            }

            Dictionary<(CharacterObject From, CharacterObject To), int> result = new();
            for (int i = 0; i < left.Count; i++)
                for (int j = 0; j < right.Count; j++)
                    if (flow[leftStart + i, rightStart + j] > 0)
                        result[(left[i], right[j])] = flow[leftStart + i, rightStart + j];
            return result;
        }
    }
}
