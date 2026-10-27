using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>Runtime index over the save-owned FiefPartyData.ServiceBatches.</summary>
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
            foreach (var pair in data)
            foreach (FiefServiceBatch batch in pair.Value?.ServiceBatches ?? Enumerable.Empty<FiefServiceBatch>())
            {
                batch?.ResolveServingParty();
                if (batch?.ServingParty == null || batch.IsEmpty()) continue;
                batch.Repair(batch.ServingParty);
                Register(pair.Key, batch);
            }
        }

        public void Register(Settlement source, FiefServiceBatch batch)
        {
            if (source == null || batch?.ServingParty == null || batch.IsEmpty()) return;
            if (!_index.TryGetValue(batch.ServingParty, out var list))
                _index[batch.ServingParty] = list = new List<ServingDetachmentRef>();
            if (!list.Any(item => item.SourceFief == source && item.Detachment == batch))
                list.Add(new ServingDetachmentRef(source, batch));
        }

        public void Unregister(Settlement source, FiefServiceBatch batch)
        {
            MobileParty party = batch?.ServingParty;
            if (party == null || !_index.TryGetValue(party, out var list)) return;
            list.RemoveAll(item => item.SourceFief == source && item.Detachment == batch);
            if (list.Count == 0) _index.Remove(party);
        }

        public List<ServingDetachmentRef> Get(MobileParty party) =>
            party != null && _index.TryGetValue(party, out var list)
                ? list.ToList() : new List<ServingDetachmentRef>();
    }
}
