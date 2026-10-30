using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Single decision boundary for adding new troops from recruitment pools.
    /// Implementations calculate plans but never mutate parties or troop pools.
    /// </summary>
    public abstract class AiTroopRecruitmentModel
    {
        /// <summary>Builds one non-mutating recruitment approval plan.</summary>
        public abstract RecruitmentPlan BuildPlan(
            MobileParty party,
            IReadOnlyList<RecruitmentCandidate> candidates);

        public abstract RecruitmentPlan BuildPlan(
            MobileParty party,
            IReadOnlyList<RecruitmentCandidate> candidates,
            AiGarrisonOrderRecruitmentContext context);

        /// <summary>Evaluates one candidate against projected party state.</summary>
        public abstract RecruitmentEvaluationResult EvaluateRecruitment(
            MobileParty party,
            CharacterObject troop,
            int availableCount,
            RecruitmentSource source,
            RecruitmentSimulationState state);
    }

    /// <summary>
    /// Compatibility name retained until all external integrations have moved
    /// to <see cref="AiTroopRecruitmentModel"/>. It adds no second policy.
    /// </summary>
    [System.Obsolete(
        "Use AiTroopRecruitmentModel; this compatibility type will be removed during the atomic entry-point switch.")]
    public abstract class AIRecruitmentModel : AiTroopRecruitmentModel
    {
    }
}
