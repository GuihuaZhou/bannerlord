// Pure eligibility helpers shared by proposal validation and AI candidate discovery.
using System.Collections.Generic;
using ModifiedFamily.Persistence;
using ModifiedFamily.Proposals;
using ModifiedFamily.Core;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Marriage.Models
{
    /// <summary>
    /// Defines marriage-specific clan, hero, bloodline, and temporary-action rules.
    /// The model is stateless so AI, proposal validation, and tests share one standard.
    /// </summary>
    public static class MarriageEligibilityModel
    {
        public static FamilyResult CanUseClan(Clan clan)
        {
            if (clan == null || clan.IsEliminated || clan.IsMinorFaction || clan.IsClanTypeMercenary || clan.IsUnderMercenaryService || clan.IsBanditFaction || clan.IsRebelClan || clan.Kingdom == null)
                return FamilyResult.Deny(FamilyReasonCode.InvalidClan);
            return FamilyResult.Allow();
        }

        public static FamilyResult CanNegotiateHero(Hero hero)
        {
            if (hero == null) return FamilyResult.Deny(FamilyReasonCode.InvalidHero);
            if (hero.IsDead) return FamilyResult.Deny(FamilyReasonCode.Dead);
            if (hero.Spouse != null) return FamilyResult.Deny(FamilyReasonCode.AlreadyMarried);
            if (!hero.IsLord || hero.IsMinorFactionHero || hero.IsNotable || hero.IsTemplate) return FamilyResult.Deny(FamilyReasonCode.InvalidHero);
            int minimumAge = hero.IsFemale ? 18 : 18;
            if (hero.Age < minimumAge) return FamilyResult.Deny(FamilyReasonCode.UnderAge);
            // Captivity and campaign activity delay execution but do not invalidate negotiations.
            var blockers = new List<FamilyReasonCode>();
            if (!hero.IsActive) blockers.Add(FamilyReasonCode.TemporaryInactive);
            if (hero.IsPrisoner) blockers.Add(FamilyReasonCode.Prisoner);
            if (hero.IsFugitive) blockers.Add(FamilyReasonCode.Fugitive);
            if (hero.PartyBelongedTo?.MapEvent != null) blockers.Add(FamilyReasonCode.InBattle);
            if (hero.PartyBelongedTo?.Army != null) blockers.Add(FamilyReasonCode.InArmy);
            return FamilyResult.Allow(null, blockers.ToArray());
        }

        public static bool AreBloodRelated(Hero first, Hero second, int depth = 3)
        {
            // This mirrors the vanilla three-generation ancestor check without calling private methods.
            return IsAncestor(first, second, depth) || IsAncestor(second, first, depth) || ShareAncestor(first, second, depth);
        }

        private static bool IsAncestor(Hero ancestor, Hero hero, int depth)
        {
            if (ancestor == null || hero == null || depth < 0) return false;
            if (ancestor == hero) return true;
            return depth > 0 && (IsAncestor(ancestor, hero.Father, depth - 1) || IsAncestor(ancestor, hero.Mother, depth - 1));
        }

        private static bool ShareAncestor(Hero first, Hero second, int depth)
        {
            var ancestors = new HashSet<Hero>();
            CollectAncestors(first, depth, ancestors);
            return ContainsAncestor(second, depth, ancestors);
        }

        private static void CollectAncestors(Hero hero, int depth, HashSet<Hero> result)
        {
            if (hero == null || depth < 0 || !result.Add(hero)) return;
            CollectAncestors(hero.Father, depth - 1, result); CollectAncestors(hero.Mother, depth - 1, result);
        }

        private static bool ContainsAncestor(Hero hero, int depth, HashSet<Hero> ancestors)
        {
            if (hero == null || depth < 0) return false;
            return ancestors.Contains(hero) || ContainsAncestor(hero.Father, depth - 1, ancestors) || ContainsAncestor(hero.Mother, depth - 1, ancestors);
        }

        public static bool AreAdoptiveRelated(Hero first, Hero second, IEnumerable<AdoptiveKinshipRecord> records)
        {
            if (records == null) return false;
            foreach (var r in records)
                if (r != null && ((r.Adopter == first && r.Adoptee == second) || (r.Adopter == second && r.Adoptee == first))) return true;
            return false;
        }
    }
}
