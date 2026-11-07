using System;
using ModifiedPolitics.SettlementEconomy.Config;

namespace ModifiedPolitics.SettlementEconomy.Models
{
    public static class PopulationGrowthModel
    {
        public static float Calculate(float effectiveHearth, float capacity, SettlementEconomyConfig config)
        {
            if (config == null || capacity <= 0f) return 0f;
            float effective = Math.Max(0f, effectiveHearth);
            float growth = config.NaturalGrowthRate * (effective + config.NaturalGrowthSeed) * (1f - effective / capacity);
            if (growth <= 0f) return growth;
            float threshold = capacity * config.LowPopulationThreshold;
            float recovery = 1f + (config.LowPopulationMultiplier - 1f) * Math.Max(0f, 1f - effective / threshold);
            return growth * recovery;
        }
    }
}
