using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Models
{
    /// <summary>
    /// Persists the most recently rejected foreign proposal so the player can
    /// reopen negotiations and edit it as a counteroffer after saving/loading.
    /// </summary>
    [SaveableRootClass(17)]
    public sealed class KingdomNegotiationCounterOfferRecord
    {
        [SaveableProperty(1)]
        public Kingdom PlayerKingdom { get; private set; }

        [SaveableProperty(2)]
        public Kingdom TargetKingdom { get; private set; }

        [SaveableProperty(3)]
        public List<KingdomNegotiationTermRecord> Terms { get; private set; }

        public KingdomNegotiationCounterOfferRecord(
            Kingdom playerKingdom,
            Kingdom targetKingdom,
            IEnumerable<KingdomNegotiationDraftTerm> terms)
        {
            PlayerKingdom = playerKingdom;
            TargetKingdom = targetKingdom;
            Terms = (terms
                    ?? Enumerable.Empty<KingdomNegotiationDraftTerm>())
                .Select(KingdomNegotiationTermRecord.FromDraftTerm)
                .ToList();
        }

        public KingdomNegotiationDraft CreateDraft()
        {
            return new KingdomNegotiationDraft(
                PlayerKingdom,
                TargetKingdom,
                (Terms ?? new List<KingdomNegotiationTermRecord>())
                    .Select(term => term.ToDraftTerm()));
        }
    }
}
