using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Services
{
    public sealed class GovernorAssignmentResult
    {
        public GovernorAssignmentResult(
            Kingdom kingdom,
            IDictionary<Town, Hero> before,
            IDictionary<Town, Hero> after,
            ISet<Town> failedTowns)
        {
            Kingdom = kingdom;
            Before = new Dictionary<Town, Hero>(before);
            After = new Dictionary<Town, Hero>(after);
            FailedTowns = new HashSet<Town>(failedTowns);
        }

        public Kingdom Kingdom { get; }
        public IReadOnlyDictionary<Town, Hero> Before { get; }
        public IReadOnlyDictionary<Town, Hero> After { get; }
        public ISet<Town> FailedTowns { get; }
    }
}
