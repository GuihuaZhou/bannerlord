using System.Collections.Generic;
using System.Linq;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>Runtime index over save-owned village service batches.</summary>
    internal sealed class FiefServiceLedger
    {
        private readonly Dictionary<MobileParty, List<ServingDetachmentRef>> _index;

        public FiefServiceLedger(Dictionary<MobileParty, List<ServingDetachmentRef>> index)
        {
            _index = index;
        }

        public void Rebuild(IReadOnlyDictionary<Settlement, FiefPartyData> data)
        {
            _index.Clear();
            Dictionary<string, VillageFiefData> batchOwners =
                new Dictionary<string, VillageFiefData>(
                    System.StringComparer.Ordinal);
            foreach (var pair in data)
            foreach (VillageFiefData village in pair.Value?.Villages?.Values ??
                Enumerable.Empty<VillageFiefData>())
            foreach (FiefServiceBatch batch in village?.ServiceBatches ??
                Enumerable.Empty<FiefServiceBatch>())
            {
                VillageFiefData existingOwner = null;
                bool duplicate = batch != null &&
                    !string.IsNullOrEmpty(batch.BatchId) &&
                    batchOwners.TryGetValue(batch.BatchId,
                        out existingOwner);
                if (batch == null || batch.SourceFief != pair.Key ||
                    batch.SourceVillage != village.VillageSettlement ||
                    string.IsNullOrEmpty(batch.BatchId) ||
                    duplicate)
                {
                    village?.RequireRecovery();
                    if (duplicate) existingOwner?.RequireRecovery();
                    ModLogger.Error(string.Format(
                        "Fief service source mismatch: holderFief={0}, holderVillage={1}, batch={2}, sourceFief={3}, sourceVillage={4}, duplicate={5}.",
                        pair.Key?.StringId ?? "unknown",
                        village?.VillageSettlement?.StringId ?? "unknown",
                        batch?.BatchId ?? "missing",
                        batch?.SourceFief?.StringId ?? "unknown",
                        batch?.SourceVillage?.StringId ?? "unknown",
                        duplicate));
                    continue;
                }
                batchOwners[batch.BatchId] = village;
                batch?.ResolveServingParty();
                if (batch?.ServingParty == null || batch.IsEmpty()) continue;
                batch.Repair(batch.ServingParty);
                Register(batch.SourceFief, batch.SourceVillage, batch);
            }
        }

        public void Register(Settlement source, Settlement sourceVillage,
            FiefServiceBatch batch)
        {
            if (source == null || sourceVillage == null ||
                batch?.ServingParty == null || batch.IsEmpty() ||
                batch.SourceFief != source ||
                batch.SourceVillage != sourceVillage) return;
            if (!_index.TryGetValue(batch.ServingParty, out var list))
                _index[batch.ServingParty] = list = new List<ServingDetachmentRef>();
            if (!list.Any(item => item.SourceFief == source &&
                item.SourceVillage == sourceVillage && item.Detachment == batch))
                list.Add(new ServingDetachmentRef(
                    batch.SourceFief, batch.SourceVillage, batch));
        }

        public void Unregister(Settlement source, Settlement sourceVillage,
            FiefServiceBatch batch)
        {
            MobileParty party = batch?.ServingParty;
            if (party == null || !_index.TryGetValue(party, out var list)) return;
            list.RemoveAll(item => item.SourceFief == source &&
                item.SourceVillage == sourceVillage && item.Detachment == batch);
            if (list.Count == 0) _index.Remove(party);
        }

        public List<ServingDetachmentRef> Get(MobileParty party) =>
            party != null && _index.TryGetValue(party, out var list)
                ? list.ToList() : new List<ServingDetachmentRef>();
    }
}
