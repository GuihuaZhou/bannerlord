using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Single decision boundary for all AI recruitment sources.
    /// Implementations calculate plans but never mutate parties or troop pools.
    /// </summary>
    public abstract class AIRecruitmentModel
    {
        public abstract RecruitmentPlan BuildPlan(
            MobileParty party,
            IReadOnlyList<RecruitmentCandidate> candidates);

        public abstract RecruitmentEvaluationResult EvaluateRecruitment(
            MobileParty party,
            CharacterObject troop,
            int availableCount,
            RecruitmentSource source,
            RecruitmentSimulationState state);
    }
}
