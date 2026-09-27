using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Models
{
    /// <summary>
    /// Reusable result for one clan. A future KingdomDecision can consume the
    /// same AcceptSupport and RejectSupport values without changing the formula.
    /// </summary>
    public sealed class SubjectClanSupportEvaluation
    {
        public Clan Clan { get; set; }
        public float SovereigntyScore { get; set; }
        public float MilitaryScore { get; set; }
        public float WarProgressScore { get; set; }
        public int WarPotential { get; set; }
        public float WarPotentialScore { get; set; }
        public float WarDisposition { get; set; }
        public float WarDispositionScore { get; set; }
        public float TerritoryScore { get; set; }
        public int DailyTribute { get; set; }
        public float TributeScore { get; set; }
        public float RulingClanScore { get; set; }

        public float RawScore => SovereigntyScore + MilitaryScore
            + WarProgressScore + WarPotentialScore + WarDispositionScore
            + TerritoryScore + TributeScore + RulingClanScore;
        public bool WouldAccept => RawScore >= 0f;
        public float AcceptSupport => WouldAccept ? 200f : 0f;
        public float RejectSupport => WouldAccept ? 0f : 200f;
        public float VoteWeight => System.Math.Max(1f, Clan?.Influence ?? 0f);
    }
}
