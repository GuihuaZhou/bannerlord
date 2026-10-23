using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Historical professional production by concrete troop. This is a
        /// production ledger, not a garrison-composition target. It lets small
        /// daily batches converge toward XML weights across many days.
        /// </summary>
        [SaveableProperty(11)]
        public Dictionary<CharacterObject, int> ProfessionalProducedByTroop
        {
            get;
            private set;
        } = new Dictionary<CharacterObject, int>();

        /// <summary>
        /// Identifies the exact eligible weighted set represented by the
        /// ledger. Culture, template or barracks changes safely start a new
        /// accounting period instead of mixing incompatible histories.
        /// </summary>
        [SaveableProperty(12)]
        public string ProfessionalProductionSignature { get; private set; }

        public bool HasProfessionalRecord =>
            LastProfessionalRecordedDay >= 0;

        public bool HasFiefRecord => LastFiefRecordedDay >= 0;

        /// <summary>Checks whether the saved ledger matches this pool.</summary>
        public bool IsProfessionalLedgerCurrent(string signature)
        {
            return !string.IsNullOrEmpty(signature) &&
                string.Equals(
                    ProfessionalProductionSignature,
                    signature,
                    StringComparison.Ordinal) &&
                ProfessionalProducedByTroop != null;
        }

        /// <summary>
        /// Returns a detached ledger for prediction or allocation. Old saves
        /// naturally begin with an empty ledger because the new field is null.
        /// </summary>
        public Dictionary<CharacterObject, int> CopyProfessionalLedger(
            string signature)
        {
            if (!IsProfessionalLedgerCurrent(signature))
            {
                return new Dictionary<CharacterObject, int>();
            }

            return new Dictionary<CharacterObject, int>(
                ProfessionalProducedByTroop);
        }

        /// <summary>
        /// Commits a completed allocation. Counts are periodically scaled down
        /// together to prevent an extremely long campaign from overflowing;
        /// their relative weighted history is retained.
        /// </summary>
        public void CommitProfessionalLedger(
            string signature,
            IDictionary<CharacterObject, int> ledger)
        {
            ProfessionalProductionSignature = signature;
            ProfessionalProducedByTroop = ledger == null
                ? new Dictionary<CharacterObject, int>()
                : new Dictionary<CharacterObject, int>(ledger);

            int maximum = ProfessionalProducedByTroop.Count == 0
                ? 0
                : ProfessionalProducedByTroop.Values.Max();
            if (maximum < 1000000)
            {
                return;
            }

            foreach (CharacterObject troop in
                new List<CharacterObject>(ProfessionalProducedByTroop.Keys))
            {
                ProfessionalProducedByTroop[troop] =
                    ProfessionalProducedByTroop[troop] / 2;
            }
        }

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
