using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.Governor.Models;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Services
{
    /// <summary>
    /// Builds a complete assignment map without changing campaign state.
    /// </summary>
    public sealed class GovernorAssignmentPlanner
    {
        private readonly GovernorCandidateSelector _selector = new GovernorCandidateSelector();

        public GovernorAssignmentPlan Build(Kingdom kingdom)
        {
            List<Town> towns = Town.AllTowns.Concat(Town.AllCastles)
                .Where(town => town?.OwnerClan?.Kingdom == kingdom)
                .Distinct()
                .ToList();
            var contexts = towns.ToDictionary(town => town, town => GovernorAssignmentModel.BuildContext(kingdom, town));
            var targets = new Dictionary<Town, Hero>();
            var lockedTowns = new HashSet<Town>();
            var lockedHeroes = new HashSet<Hero>();

            foreach (Town town in towns.Where(HasIncomingGovernor))
            {
                Hero incoming = GetIncomingGovernor(town);
                targets[town] = incoming;
                lockedTowns.Add(town);
                if (incoming != null)
                    lockedHeroes.Add(incoming);
            }

            // A local officer must retain a compatible governor post. Locking the current post
            // prevents a global reshuffle from silently invalidating the office assignment.
            foreach (Town town in towns.Where(town => !lockedTowns.Contains(town)))
            {
                Hero governor = town.Governor;
                var office = governor == null ? null : HeroOfficeBehavior.Current?.GetAssignment(governor);
                if (office != null && OfficeRules.IsLocal(office.OfficeType))
                {
                    targets[town] = governor;
                    lockedTowns.Add(town);
                    lockedHeroes.Add(governor);
                }
            }

            List<Hero> availableHeroes = kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => !lockedHeroes.Contains(hero)
                               && GovernorCandidateSelector.IsGovernorCandidate(hero, kingdom))
                .Distinct()
                .ToList();
            List<GovernorSettlementContext> remaining = contexts.Values
                .Where(context => !lockedTowns.Contains(context.Town))
                .ToList();

            while (remaining.Count > 0)
            {
                foreach (GovernorSettlementContext context in remaining)
                {
                    context.EligibleCandidateCount = availableHeroes.Count(hero =>
                        _selector.IsQualified(hero, kingdom, context));
                }

                GovernorSettlementContext next = remaining
                    .OrderByDescending(context => context.IsTown)
                    .ThenByDescending(context => context.ProsperityBand)
                    .ThenByDescending(context => context.IsBorder)
                    .ThenBy(context => context.EligibleCandidateCount == 0 ? int.MaxValue : context.EligibleCandidateCount)
                    .ThenByDescending(context => context.Prosperity)
                    .ThenBy(context => context.Town.Settlement.StringId)
                    .First();

                Hero selected = _selector.GetBestRulingClanCandidate(kingdom, next, availableHeroes)
                                ?? _selector.GetBestOtherClanCandidate(kingdom, next, availableHeroes);
                targets[next.Town] = selected;
                if (selected != null)
                    availableHeroes.Remove(selected);
                remaining.Remove(next);
            }

            return new GovernorAssignmentPlan(kingdom, contexts, targets, lockedTowns);
        }

        private static bool HasIncomingGovernor(Town town)
        {
            return GovernorTravelService.GetIncomingGovernor(town) != null;
        }

        private static Hero GetIncomingGovernor(Town town)
        {
            return GovernorTravelService.GetIncomingGovernor(town);
        }
    }
}
