using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// Returned fief troops waiting until they can rejoin the ready roster.
    /// </summary>
    [SaveableRootClass(7)]
    public sealed class FiefReturnBatch
    {
        [SaveableProperty(1)]
        public Dictionary<CharacterObject, int> Troops { get; private set; }

        [SaveableProperty(2)]
        public int WaitWeeks { get; set; }

        public FiefReturnBatch()
        {
            Troops = new Dictionary<CharacterObject, int>();
        }

        public FiefReturnBatch(int waitWeeks) : this()
        {
            WaitWeeks = Math.Max(0, waitWeeks);
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

        /// <summary>
        /// Replaces the runtime roster while retaining this batch's saved
        /// cooldown. Used to restore the exact pre-transaction state.
        /// </summary>
        public void ReplaceTroops(
            IReadOnlyDictionary<CharacterObject, int> troops)
        {
            Troops.Clear();
            AddTroops(troops);
        }

        public void Tick()
        {
            if (WaitWeeks > 0) WaitWeeks--;
        }

        public bool IsReady() => WaitWeeks <= 0 && !IsEmpty();

        public int GetTotalCount() => Troops?.Values.Sum(value => Math.Max(0, value)) ?? 0;

        public bool IsEmpty() => Troops == null ||
            GetTotalCount() <= 0;
    }
}
