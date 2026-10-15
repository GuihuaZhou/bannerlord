using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Saves fractional production progress and the weekly fief-production
    /// counter. Generated troops are transferred directly to their target.
    /// </summary>
    [SaveableRootClass(5)]
    public sealed class SettlementRecruitmentPoolData
    {
        [SaveableProperty(3)]
        public float ProfessionalProductionProgress
        {
            get;
            private set;
        }

        [SaveableProperty(4)]
        public float FiefProductionProgress
        {
            get;
            private set;
        }

        [SaveableProperty(5)]
        public int FiefProducedThisWeek { get; private set; }

        [SaveableProperty(6)]
        public int WeekKey { get; private set; } = -1;

        public void ResetWeekIfNeeded(int weekKey)
        {
            if (WeekKey == weekKey) return;
            WeekKey = weekKey;
            FiefProducedThisWeek = 0;
        }

        public void RecordFiefProduction(int count)
        {
            FiefProducedThisWeek += Math.Max(0, count);
        }

        public void SetProductionProgress(
            RecruitmentPoolKind kind,
            float value)
        {
            value = Math.Max(0f, Math.Min(0.9999f, value));
            if (kind == RecruitmentPoolKind.Professional)
                ProfessionalProductionProgress = value;
            else
                FiefProductionProgress = value;
        }

        public float AddProductionProgress(
            RecruitmentPoolKind kind,
            float amount)
        {
            if (kind == RecruitmentPoolKind.Professional)
            {
                ProfessionalProductionProgress = Math.Max(
                    0f,
                    ProfessionalProductionProgress + amount);
                return ProfessionalProductionProgress;
            }

            FiefProductionProgress = Math.Max(
                0f,
                FiefProductionProgress + amount);
            return FiefProductionProgress;
        }

        public void ConsumeProductionProgress(
            RecruitmentPoolKind kind,
            int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (kind == RecruitmentPoolKind.Professional)
            {
                ProfessionalProductionProgress = Math.Max(
                    0f,
                    ProfessionalProductionProgress - amount);
            }
            else
            {
                FiefProductionProgress = Math.Max(
                    0f,
                    FiefProductionProgress - amount);
            }
        }

    }
}
