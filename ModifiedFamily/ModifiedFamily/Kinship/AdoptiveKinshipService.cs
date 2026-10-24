// Maintains legal adoptive relationships without overwriting biological parent fields.
using System.Collections.Generic;
using ModifiedFamily.Persistence;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Kinship
{
    public sealed class AdoptiveKinshipService
    {
        private readonly IList<AdoptiveKinshipRecord> _records;
        public AdoptiveKinshipService(IList<AdoptiveKinshipRecord> records) { _records = records; }
        public void Record(Hero adopter, Hero adoptee, Clan source)
        {
            foreach (var r in _records) if (r.Adopter == adopter && r.Adoptee == adoptee) return;
            _records.Add(new AdoptiveKinshipRecord { Adopter = adopter, Adoptee = adoptee, SourceClan = source, Date = CampaignTime.Now });
        }
    }
}
