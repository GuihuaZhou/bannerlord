using System;
using ModifiedPolitics.SettlementEconomy.Config;

namespace ModifiedPolitics.SettlementEconomy.Models
{
    public sealed class DailyHearthForecast
    {
        public float Natural { get; }
        public float Food { get; }
        public float Prosperity { get; }
        public float RecruitmentBurden { get; }
        public float RawTotal { get; }
        public float AppliedTotal { get; }
        public float CapacityAdjustment { get; }
        public float Capacity { get; }
        public float SupportProsperity { get; }
        public float DynamicProsperity { get; }
        public float FinalProsperity { get; }
        public DailyHearthForecast(float natural, float food, float prosperity, float recruitmentBurden, float rawTotal, float appliedTotal, float capacity, float supportProsperity, float dynamicProsperity, float finalProsperity)
        {
            Natural = natural; Food = food; Prosperity = prosperity; RecruitmentBurden = recruitmentBurden;
            RawTotal = rawTotal; AppliedTotal = appliedTotal; CapacityAdjustment = appliedTotal - rawTotal;
            Capacity = capacity; SupportProsperity = supportProsperity; DynamicProsperity = dynamicProsperity; FinalProsperity = finalProsperity;
        }
    }

    public static class PopulationGrowthModel
    {
        public static float CalculateNatural(float hearth, float capacity, float growthRate, SettlementEconomyConfig config)
        {
            if (config == null || !IsFinite(hearth) || !IsFinite(capacity) || !IsFinite(growthRate) || capacity <= 0f) return 0f;
            float effective = Math.Max(0f, hearth);
            float growth = growthRate * (effective + config.NaturalGrowthSeed) * (1f - effective / capacity);
            if (growth <= 0f) return growth;
            float threshold = capacity * config.LowPopulationThreshold;
            float recovery = 1f + (config.LowPopulationMultiplier - 1f) * Math.Max(0f, 1f - effective / threshold);
            return growth * recovery;
        }

        public static DailyHearthForecast CalculateDailyForecast(float hearth, float capacity, float burden, float prosperity, float foodChange, float prosperityPerHearth, float growthRate, SettlementEconomyConfig config)
        {
            if (config == null || capacity <= 0f || prosperityPerHearth <= 0f || !IsFinite(hearth) || !IsFinite(capacity) || !IsFinite(burden) || !IsFinite(prosperity) || !IsFinite(foodChange) || !IsFinite(prosperityPerHearth) || !IsFinite(growthRate))
                throw new ArgumentOutOfRangeException(nameof(hearth), "Daily hearth forecast inputs must be finite and valid.");
            hearth = Math.Max(0f, hearth); burden = Math.Max(0f, burden);
            float natural = CalculateNatural(hearth, capacity, growthRate, config);
            float food = Clamp(foodChange * config.FoodPerChange, config.FoodMinimum, config.FoodMaximum);
            float dynamicProsperity = prosperityPerHearth * hearth;
            float finalProsperity = prosperityPerHearth * capacity;
            float prosperityContribution = 0f;
            if (hearth < capacity && prosperity > dynamicProsperity && prosperity < finalProsperity)
                prosperityContribution = Math.Min(config.ProsperityMaximum, config.ProsperityMaximum * (prosperity - dynamicProsperity) / config.ProsperityTriggerSpan);
            float burdenRatio = Clamp(burden / Math.Max(hearth, 1f), 0f, 1f);
            float burdenContribution = -config.BurdenPerCapacity * capacity * burdenRatio;
            float raw = natural + food + prosperityContribution + burdenContribution;
            float applied = hearth > capacity ? Math.Min(raw, natural) : Clamp(hearth + raw, 0f, capacity) - hearth;
            if (!IsFinite(applied)) throw new InvalidOperationException("Daily hearth forecast produced a non-finite result.");
            float support = prosperityPerHearth * Math.Max(0f, hearth - burden);
            return new DailyHearthForecast(natural, food, prosperityContribution, burdenContribution, raw, applied, capacity, support, dynamicProsperity, finalProsperity);
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float minimum, float maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}
