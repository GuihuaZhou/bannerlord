// Stateless scoring model used independently by proposal senders and receivers.
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Marriage.Models
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
            // A clan either gains the incoming spouse or loses its own member; it never receives both effects.
            float score = destination == evaluatingClan ? ValueToClan(otherHero, evaluatingClan) : -ValueToClan(ownHero, evaluatingClan);
            score += evaluatingClan.GetRelationWithClan(otherHero.Clan) * 0.5f + giftDelta / 1000f;
            int adultMembers = evaluatingClan.Heroes.Count(h => h != null && !h.IsDead && h.Age >= 18f);
            score += destination == evaluatingClan ? System.Math.Max(0, 8 - adultMembers) * 4f : System.Math.Max(0, 6 - adultMembers) * -6f;
            if (ownHero.Clan?.Culture == otherHero.Clan?.Culture) score += 20f;
            if (ownHero.Clan.Kingdom == otherHero.Clan.Kingdom) score += 20f;
            return score;
        }
    }
}
