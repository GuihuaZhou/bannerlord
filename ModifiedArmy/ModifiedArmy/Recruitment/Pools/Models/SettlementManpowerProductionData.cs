using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Saves fractional production progress and the latest completed daily
    /// recruitment results. Generated troops are transferred directly to
    /// their target.
    /// </summary>
    [SaveableRootClass(5)]
    public sealed class SettlementManpowerProductionData
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

        [SaveableProperty(7)]
        public int LastProfessionalRecruited { get; private set; }

        [SaveableProperty(8)]
        public int LastFiefReplenished { get; private set; }

        [SaveableProperty(9)]
        public int LastProfessionalRecordedDay { get; private set; } = -1;

        [SaveableProperty(10)]
        public int LastFiefRecordedDay { get; private set; } = -1;

        public bool HasProfessionalRecord =>
            LastProfessionalRecordedDay >= 0;

        public bool HasFiefRecord => LastFiefRecordedDay >= 0;

        public void RecordProfessionalRecruitment(int day, int count)
        {
            if (day < 0 || day < LastProfessionalRecordedDay)
            {
                return;
            }

            LastProfessionalRecordedDay = day;
            LastProfessionalRecruited = Math.Max(0, count);
        }

        public void RecordFiefReplenishment(int day, int count)
        {
            if (day < 0 || day < LastFiefRecordedDay)
            {
                return;
            }

            LastFiefRecordedDay = day;
            LastFiefReplenished = Math.Max(0, count);
        }

        public void SetProductionProgress(
            TroopProductionKind kind,
            float value)
        {
            value = Math.Max(0f, Math.Min(0.9999f, value));
            if (kind == TroopProductionKind.Professional)
                ProfessionalProductionProgress = value;
            else
                FiefProductionProgress = value;
        }

        public float AddProductionProgress(
            TroopProductionKind kind,
            float amount)
        {
            if (kind == TroopProductionKind.Professional)
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
            TroopProductionKind kind,
            int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (kind == TroopProductionKind.Professional)
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
