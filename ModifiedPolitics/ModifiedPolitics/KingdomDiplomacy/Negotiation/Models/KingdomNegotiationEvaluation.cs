using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Models
{
    /// <summary>
    /// Clan-by-clan evaluation of one compound proposal from one kingdom's
    /// perspective. The same result will later feed both AI responses and the
    /// native kingdom decision support calculation.
    /// </summary>
    public sealed class KingdomNegotiationEvaluation
    {
        public KingdomNegotiationDraft Draft { get; set; }

        public Kingdom EvaluatingKingdom { get; set; }

        public List<KingdomNegotiationClanEvaluation> ClanEvaluations
            { get; } = new List<KingdomNegotiationClanEvaluation>();

        public float AcceptWeight => ClanEvaluations
            .Where(x => x.WouldAccept)
            .Sum(x => x.VoteWeight);

        public float TotalWeight => ClanEvaluations.Sum(x => x.VoteWeight);

        public float AcceptShare => TotalWeight <= 0f
            ? 0f
            : AcceptWeight / TotalWeight;

        public bool WouldAccept => TotalWeight > 0f && AcceptShare >= 0.5f;
    }

    public sealed class KingdomNegotiationClanEvaluation
    {
        public Clan Clan { get; set; }

        public List<KingdomNegotiationScoreComponent> Components
            { get; } = new List<KingdomNegotiationScoreComponent>();

        public float RawScore => Components.Sum(x => x.Score);

        // A tiny positive rounding difference is not sufficient political
        // support for a binding international package.
        public bool WouldAccept => RawScore >= 20f;

        public float VoteWeight => System.Math.Max(1f, Clan?.Influence ?? 0f);
    }

    public sealed class KingdomNegotiationScoreComponent
    {
        public string Name { get; set; }

        public float Score { get; set; }
    }
}
