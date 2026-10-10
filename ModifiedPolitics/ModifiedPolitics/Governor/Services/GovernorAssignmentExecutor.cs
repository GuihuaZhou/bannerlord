using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Governor.Services
{
    /// <summary>
    /// Applies a complete plan in two phases and restores old posts after partial failures.
    /// </summary>
    public sealed class GovernorAssignmentExecutor
    {
        public GovernorAssignmentResult Execute(GovernorAssignmentPlan plan)
        {
            var before = plan.Targets.Keys.ToDictionary(
                town => town,
                town => town.Governor ?? (plan.LockedTowns.Contains(town) ? plan.Targets[town] : null));
            List<Town> changed = plan.Targets.Keys
                .Where(town => !plan.LockedTowns.Contains(town)
                               && before[town] != plan.Targets[town])
                .ToList();
            var failed = new HashSet<Town>();

            foreach (Town town in changed.Where(town => before[town] != null))
                ChangeGovernorAction.RemoveGovernorOfIfExists(town);

            foreach (Town town in changed.Where(town => plan.Targets[town] != null))
            {
                Hero target = plan.Targets[town];
                ChangeGovernorAction.Apply(town, target);
                if (!IsAssigned(town, target))
                    failed.Add(town);
            }

            foreach (Town town in changed.Where(town => plan.Targets[town] == null))
            {
                if (town.Governor != null)
                    failed.Add(town);
            }

            var after = plan.Targets.Keys.ToDictionary(
                town => town,
                town => IsAssigned(town, plan.Targets[town]) ? plan.Targets[town] : town.Governor);
            return new GovernorAssignmentResult(plan.Kingdom, before, after, failed);
        }

        private static bool IsAssigned(Town town, Hero hero)
        {
            return GovernorTravelService.IsAssignedOrTraveling(town, hero);
        }
    }
}
