using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.Governor.Models;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.Governor.Services
{
    /// <summary>
    /// Applies hard governor eligibility before deterministic political ordering.
    /// </summary>
    public sealed class GovernorCandidateSelector
    {
        public Hero GetBestRulingClanCandidate(
            Kingdom kingdom,
            GovernorSettlementContext context,
            IReadOnlyCollection<Hero> availableHeroes)
        {
            return availableHeroes
                .Where(hero => hero?.Clan == kingdom?.RulingClan && IsQualified(hero, kingdom, context))
                .OrderByDescending(hero => GovernorAssignmentModel.GetEffectiveGovernorAbility(hero, context))
                .ThenByDescending(hero => hero.GovernorOf == context.Town)
                .ThenBy(hero => hero.StringId)
                .FirstOrDefault();
        }

        public Hero GetBestOtherClanCandidate(
            Kingdom kingdom,
            GovernorSettlementContext context,
            IReadOnlyCollection<Hero> availableHeroes)
        {
            return availableHeroes
                .Where(hero => hero?.Clan != kingdom?.RulingClan && IsQualified(hero, kingdom, context))
                .OrderBy(hero => hero.Clan.Tier)
                .ThenBy(hero => HeroOfficeBehavior.Current?.GetOfficeCount(hero.Clan) ?? 0)
                .ThenBy(hero => GovernorAssignmentModel.GetInfluenceBand(hero.Clan.Influence))
                .ThenBy(hero => hero.Clan.Influence)
                .ThenBy(hero => GovernorAssignmentModel.GetFiefWeight(hero.Clan))
                .ThenByDescending(hero => GovernorAssignmentModel.GetEffectiveGovernorAbility(hero, context))
                .ThenByDescending(hero => hero.GovernorOf == context.Town)
                .ThenBy(hero => hero.StringId)
                .FirstOrDefault();
        }

        public bool IsQualified(Hero hero, Kingdom kingdom, GovernorSettlementContext context)
        {
            return IsGovernorCandidate(hero, kingdom)
                   && IsOfficeCompatible(hero, context)
                   && GovernorAssignmentModel.MeetsAbilityRequirement(hero, context);
        }

        public static bool IsGovernorCandidate(Hero hero, Kingdom kingdom)
        {
            return hero != null
                   && hero.IsAlive
                   && hero.IsActive
                   && hero.Clan?.Kingdom == kingdom
                   && !hero.Clan.IsEliminated
                   && !hero.Clan.IsClanTypeMercenary
                   && hero.PartyBelongedTo == null
                   && !hero.IsPrisoner
                   && !hero.IsTraveling
                   && Campaign.Current.Models.ClanPoliticsModel.CanHeroBeGovernor(hero);
        }

        public static bool IsOfficeCompatible(Hero hero, GovernorSettlementContext context)
        {
            var assignment = HeroOfficeBehavior.Current?.GetAssignment(hero);
            if (assignment == null || !OfficeRules.IsLocal(assignment.OfficeType))
                return true;

            return assignment.OfficeType == OfficeType.MilitaryOfficer
                ? context.Town.IsCastle
                : context.Town.IsTown;
        }
    }
}
