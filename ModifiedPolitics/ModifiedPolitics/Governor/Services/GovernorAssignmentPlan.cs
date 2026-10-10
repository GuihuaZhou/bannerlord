using System.Collections.Generic;
using ModifiedPolitics.Governor.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Services
{
    public sealed class GovernorAssignmentPlan
    {
        public GovernorAssignmentPlan(
            Kingdom kingdom,
            IDictionary<Town, GovernorSettlementContext> contexts,
            IDictionary<Town, Hero> targets,
            ISet<Town> lockedTowns)
        {
            Kingdom = kingdom;
            Contexts = new Dictionary<Town, GovernorSettlementContext>(contexts);
            Targets = new Dictionary<Town, Hero>(targets);
            LockedTowns = new HashSet<Town>(lockedTowns);
        }

        public Kingdom Kingdom { get; }
        public IReadOnlyDictionary<Town, GovernorSettlementContext> Contexts { get; }
        public IReadOnlyDictionary<Town, Hero> Targets { get; }
        public ISet<Town> LockedTowns { get; }
    }
}
