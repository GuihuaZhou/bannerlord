using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Models
{
    public sealed class ClanHeroValueModel
    {
        public float ValueToClan(Hero hero, Clan clan)
        {
            if (hero == null || clan == null) return float.MinValue;
            float ageValue = 60f - System.Math.Abs(hero.Age - 28f);
            float leaderPenalty = clan.Leader == hero ? 10000f : 0f;
            return ageValue + clan.Tier * 10f - leaderPenalty;
        }
        public float MarriageScore(Hero ownHero, Hero otherHero, Clan evaluatingClan, Clan destination, int giftDelta)
        {
            float score = ValueToClan(otherHero, evaluatingClan) - (destination == evaluatingClan ? 0f : ValueToClan(ownHero, evaluatingClan));
            score += evaluatingClan.GetRelationWithClan(otherHero.Clan) * 0.5f + giftDelta / 1000f;
            if (ownHero.Culture == otherHero.Culture) score += 20f;
            if (ownHero.Clan.Kingdom == otherHero.Clan.Kingdom) score += 20f;
            return score;
        }
    }
}
