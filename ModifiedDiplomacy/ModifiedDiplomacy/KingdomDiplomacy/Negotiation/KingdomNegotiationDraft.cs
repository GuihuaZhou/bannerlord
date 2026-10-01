using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedDiplomacy.KingdomDiplomacy.Negotiation
{
    /// <summary>
    /// A UI-independent snapshot of one compound diplomatic proposal. Keeping
    /// this separate from the view model lets evaluation, voting and execution
    /// consume exactly the same immutable set of terms.
    /// </summary>
    public sealed class KingdomNegotiationDraft
    {
        public KingdomNegotiationDraft(
            Kingdom playerKingdom,
            Kingdom targetKingdom,
            IEnumerable<KingdomNegotiationDraftTerm> terms)
        {
            PlayerKingdom = playerKingdom;
            TargetKingdom = targetKingdom;
            Terms = (terms ?? Enumerable.Empty<KingdomNegotiationDraftTerm>())
                .ToList()
                .AsReadOnly();
        }

        public Kingdom PlayerKingdom { get; }

        public Kingdom TargetKingdom { get; }

        public IReadOnlyList<KingdomNegotiationDraftTerm> Terms { get; }
    }

    /// <summary>
    /// One term in a compound proposal. ProviderKingdom is explicit because a
    /// settlement, prisoner or payment has a direction even when its wording
    /// is otherwise identical on both sides of the screen.
    /// </summary>
    public sealed class KingdomNegotiationDraftTerm
    {
        public KingdomNegotiationDraftTerm(
            KingdomNegotiationTermType type,
            Kingdom providerKingdom,
            object subject,
            int amount)
        {
            Type = type;
            ProviderKingdom = providerKingdom;
            Subject = subject;
            Amount = amount;
        }

        public KingdomNegotiationTermType Type { get; }

        public Kingdom ProviderKingdom { get; }

        public object Subject { get; }

        public int Amount { get; }
    }
}
