using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// Runtime-only result for a controlled fief-service return. This type is
    /// deliberately not saveable; the authoritative state remains in the
    /// service and return batches.
    /// </summary>
    public sealed class FiefReturnResult
    {
        public bool Succeeded { get; set; }
        public string ReasonCode { get; set; }
        public Settlement SourceFief { get; set; }
        public string BatchId { get; set; }
        public int RequestedCount { get; set; }
        public int ActualReturnedCount { get; set; }
        public int ActualRestoredHearth { get; set; }
        public IReadOnlyDictionary<CharacterObject, int> ReturnedTroops {
            get;
            set;
        } = new Dictionary<CharacterObject, int>();
    }
}
