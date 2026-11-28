using System;
using System.Collections.Generic;
using System.Linq;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>Calculates a deterministic capture contribution split without mutating rosters.</summary>
    public static class CaptureContributionAllocationModel
    {
        public static Dictionary<string, int> Allocate(int requestedCount,
            IReadOnlyDictionary<string, int> donorCapacities)
        {
            var result = donorCapacities?.Keys.OrderBy(x => x, StringComparer.Ordinal)
                .ToDictionary(x => x, _ => 0, StringComparer.Ordinal)
                ?? new Dictionary<string, int>(StringComparer.Ordinal);
            var eligible = result.Keys.Where(x => donorCapacities[x] > 0).ToList();
            int remaining = Math.Min(Math.Max(0, requestedCount),
                eligible.Sum(x => Math.Max(0, donorCapacities[x])));
            foreach (string key in eligible.Take(remaining))
            {
                result[key] = 1;
                remaining--;
            }
            while (remaining > 0)
            {
                bool progress = false;
                foreach (string key in eligible)
                {
                    if (result[key] >= donorCapacities[key]) continue;
                    result[key]++;
                    remaining--;
                    progress = true;
                    if (remaining == 0) break;
                }
                if (!progress) break;
            }
            return result;
        }
    }
}
