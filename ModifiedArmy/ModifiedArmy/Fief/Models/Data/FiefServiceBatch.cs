using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// Authoritative record for one group of fief troops serving in one party.
    /// This type intentionally has no compatibility relationship with the old
    /// FiefTroopDetachment save format.
    /// </summary>
    [SaveableRootClass(6)]
    public sealed class FiefServiceBatch
    {
        [SaveableProperty(1)]
        public Dictionary<CharacterObject, int> Troops { get; private set; }

        [SaveableProperty(2)]
        public string ServingPartyId { get; private set; }

        [SaveableProperty(3)]
        public int ServiceDays { get; private set; }

        [SaveableProperty(4)]
        public int FreeWageDays { get; private set; }

        [SaveableProperty(5)]
        public int LastServiceUpdateDay { get; private set; }

        [SaveableProperty(6)]
        public string BatchId { get; private set; }

        public MobileParty ServingParty { get; private set; }

        public FiefServiceBatch()
        {
            Troops = new Dictionary<CharacterObject, int>();
            BatchId = Guid.NewGuid().ToString("N");
        }

        public static FiefServiceBatch Create(
            MobileParty party,
            int freeWageDays,
            IReadOnlyDictionary<CharacterObject, int> troops)
        {
            FiefServiceBatch batch = new FiefServiceBatch
            {
                ServingParty = party,
                ServingPartyId = party?.StringId,
                FreeWageDays = Math.Max(0, freeWageDays),
                LastServiceUpdateDay = CurrentDay()
            };
            batch.AddTroops(troops);
            return batch;
        }

        public FiefServiceBatch SplitTo(
            MobileParty party,
            IReadOnlyDictionary<CharacterObject, int> troops)
        {
            FiefServiceBatch split = Create(party, FreeWageDays, troops);
            split.ServiceDays = ServiceDays;
            split.LastServiceUpdateDay = LastServiceUpdateDay;
            return split;
        }

        public bool ResolveServingParty()
        {
            if (ServingParty != null) return true;
            if (string.IsNullOrEmpty(ServingPartyId)) return false;
            ServingParty = MobileParty.All.FirstOrDefault(
                party => party?.StringId == ServingPartyId);
            return ServingParty != null;
        }

        public void Repair(MobileParty party)
        {
            ServingParty = party;
            ServingPartyId = party?.StringId;
            ServiceDays = Math.Max(0, ServiceDays);
            FreeWageDays = Math.Max(0, FreeWageDays);
            if (LastServiceUpdateDay <= 0) LastServiceUpdateDay = CurrentDay();
            if (string.IsNullOrEmpty(BatchId)) BatchId = Guid.NewGuid().ToString("N");
        }

        public void AdvanceToCurrentDay()
        {
            if (ServingParty == null) return;
            int current = CurrentDay();
            if (LastServiceUpdateDay <= 0)
            {
                LastServiceUpdateDay = current;
                return;
            }
            ServiceDays += Math.Max(0, current - LastServiceUpdateDay);
            LastServiceUpdateDay = current;
        }

        public void ReplaceTroops(IReadOnlyDictionary<CharacterObject, int> troops)
        {
            Troops.Clear();
            AddTroops(troops);
        }

        public void AddTroops(IReadOnlyDictionary<CharacterObject, int> troops)
        {
            if (troops == null) return;
            foreach (var pair in troops)
            {
                if (pair.Key == null || pair.Value <= 0) continue;
                Troops[pair.Key] = Troops.TryGetValue(pair.Key, out int old)
                    ? old + pair.Value
                    : pair.Value;
            }
        }

        public int GetTotalCount() => Troops?.Values.Sum(value => Math.Max(0, value)) ?? 0;

        public bool IsEmpty() => Troops == null || Troops.Count == 0 || GetTotalCount() <= 0;

        private static int CurrentDay() => Campaign.Current == null
            ? 0
            : (int)Math.Floor(CampaignTime.Now.ToDays);
    }
}
