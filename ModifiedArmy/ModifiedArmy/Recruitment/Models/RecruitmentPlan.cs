using System.Collections.Generic;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Complete, non-mutating recruitment decision for one party visit.
    /// </summary>
    public sealed class RecruitmentPlan
    {
        public RecruitmentPlan(
            RecruitmentPartyType partyType,
            string cultureId,
            RecruitmentSimulationState finalState,
            IList<RecruitmentEvaluationResult> evaluations)
        {
            PartyType = partyType;
            CultureId = cultureId;
            FinalState = finalState;
            Evaluations = new List<RecruitmentEvaluationResult>(evaluations);
        }

        public RecruitmentPartyType PartyType { get; }

        public string CultureId { get; }

        public RecruitmentSimulationState FinalState { get; }

        public IReadOnlyList<RecruitmentEvaluationResult> Evaluations
        {
            get;
        }
    }
}
