using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace ModifiedArmy.Models.Fief
{
    [Flags]
    public enum FiefTroopState
    {
        None = 0,
        Ready = 1,
        Serving = 2,
        Cooling = 4,
        All = Ready | Serving | Cooling
    }

    public readonly struct FiefTroopCount
    {
        public int Total { get; }
        public int Wounded { get; }
        public int Healthy => Math.Max(0, Total - Wounded);

        internal FiefTroopCount(int total, int wounded)
        {
            Total = Math.Max(0, total);
            Wounded = Math.Min(Total, Math.Max(0, wounded));
        }
    }

    /// <summary>Read-only point-in-time view of feudal troop identities.</summary>
    public sealed class FiefTroopSnapshot
    {
        private readonly IReadOnlyDictionary<CharacterObject, FiefTroopCount>
            _troops;

        public FiefTroopState IncludedStates { get; }
        public IReadOnlyDictionary<CharacterObject, FiefTroopCount> Troops =>
            _troops;
        public int TotalCount => _troops.Values.Sum(value => value.Total);
        public int WoundedCount => _troops.Values.Sum(value => value.Wounded);
        public int HealthyCount => Math.Max(0, TotalCount - WoundedCount);

        internal FiefTroopSnapshot(FiefTroopState includedStates,
            IDictionary<CharacterObject, FiefTroopCount> troops)
        {
            IncludedStates = includedStates;
            _troops = new ReadOnlyDictionary<CharacterObject,
                FiefTroopCount>(new Dictionary<CharacterObject,
                    FiefTroopCount>(troops));
        }

        public TroopRoster ToTroopRoster()
        {
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            foreach (var pair in _troops.OrderBy(item =>
                item.Key?.StringId, StringComparer.Ordinal))
            {
                if (pair.Key == null || pair.Value.Total <= 0) continue;
                roster.AddToCounts(pair.Key, pair.Value.Total, false,
                    pair.Value.Wounded);
            }
            return roster;
        }
    }

    internal static class FiefTroopQuery
    {
        internal static FiefTroopSnapshot Query(VillageFiefData village,
            FiefPartyTemplate template, FiefTroopState states)
        {
            var counts = new Dictionary<CharacterObject, FiefTroopCount>();
            if (village == null || template == null)
                return new FiefTroopSnapshot(states, counts);

            void Add(CharacterObject troop, int count, int wounded = 0)
            {
                if (troop == null || count <= 0 ||
                    !template.IsEnableTroop(troop)) return;
                counts.TryGetValue(troop, out FiefTroopCount current);
                counts[troop] = new FiefTroopCount(
                    checked(current.Total + count),
                    checked(current.Wounded + Math.Max(0, wounded)));
            }

            if ((states & FiefTroopState.Ready) != 0)
                foreach (TroopRosterElement entry in FiefTroopRosterFilter
                    .GetEntries(village.GetReadyRoster(), template))
                    Add(entry.Character, entry.Number, entry.WoundedNumber);
            if ((states & FiefTroopState.Serving) != 0)
                foreach (FiefServiceBatch batch in village.ServiceBatches ??
                    Enumerable.Empty<FiefServiceBatch>())
                foreach (var pair in batch?.Troops ??
                    new Dictionary<CharacterObject, int>())
                    Add(pair.Key, pair.Value);
            if ((states & FiefTroopState.Cooling) != 0)
                foreach (FiefReturnBatch batch in village.ReturnBatches ??
                    Enumerable.Empty<FiefReturnBatch>())
                foreach (var pair in batch?.Troops ??
                    new Dictionary<CharacterObject, int>())
                    Add(pair.Key, pair.Value);
            return new FiefTroopSnapshot(states, counts);
        }

        internal static FiefTroopSnapshot Merge(
            IEnumerable<FiefTroopSnapshot> snapshots,
            FiefTroopState states)
        {
            var counts = new Dictionary<CharacterObject, FiefTroopCount>();
            foreach (FiefTroopSnapshot snapshot in snapshots ??
                Enumerable.Empty<FiefTroopSnapshot>())
            foreach (var pair in snapshot.Troops)
            {
                counts.TryGetValue(pair.Key, out FiefTroopCount current);
                counts[pair.Key] = new FiefTroopCount(
                    checked(current.Total + pair.Value.Total),
                    checked(current.Wounded + pair.Value.Wounded));
            }
            return new FiefTroopSnapshot(states, counts);
        }
    }
}
