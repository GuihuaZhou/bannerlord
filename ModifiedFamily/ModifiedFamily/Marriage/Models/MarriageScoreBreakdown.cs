using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Marriage.Models
{
    public sealed class MarriageScoreBreakdown
    {
        public Clan EvaluatingClan { get; internal set; }
        public Clan OtherClan { get; internal set; }
        public Hero OwnHero { get; internal set; }
        public Hero OtherHero { get; internal set; }
        public MarriageDirection Direction { get; internal set; }
        public int AdultHeroCount { get; internal set; }
        public int RelationRaw { get; internal set; }
        public float BaseHeroValue { get; internal set; }
        public float InflowValue { get; internal set; }
        public float OutflowCost { get; internal set; }
        public float SuccessionPenalty { get; internal set; }
        public float AllianceBenefit { get; internal set; }
        public float RelationValue { get; internal set; }
        public float CultureValue { get; internal set; }
        public float KingdomValue { get; internal set; }
        public float GiftValue { get; internal set; }
        public float Total => InflowValue - OutflowCost - SuccessionPenalty + AllianceBenefit + RelationValue + CultureValue + KingdomValue + GiftValue;
    }
}
