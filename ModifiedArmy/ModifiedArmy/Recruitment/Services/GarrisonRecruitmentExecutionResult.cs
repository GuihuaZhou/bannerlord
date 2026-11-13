namespace ModifiedArmy.Recruitment.Services
{
    using System.Collections.Generic;

    public sealed class GarrisonRecruitmentExecutionResult
    {
        public string OperationId { get; internal set; }
        public int ProfessionalRecruited { get; internal set; }
        public int PrisonerRecruited { get; internal set; }
        public string FailureReason { get; internal set; }
        public bool RecoveryRequired { get; internal set; }
        public bool OfferConsumed { get; internal set; }
        public bool RosterChanged { get; internal set; }
        public bool ExpenseCharged { get; internal set; }
        public bool BudgetCommitted { get; internal set; }
        public string FirstFailedStage { get; internal set; }
        public int AttemptedCandidates { get; internal set; }
        public int ModelApproved { get; internal set; }
        public Dictionary<string, int> FailureReasonCounts { get; } =
            new Dictionary<string, int>();
        public int TotalCommitted => ProfessionalRecruited + PrisonerRecruited;

        internal void Reject(string stage, string reason)
        {
            if (string.IsNullOrEmpty(FirstFailedStage)) FirstFailedStage = stage;
            if (string.IsNullOrEmpty(FailureReason)) FailureReason = reason;
            string key = reason ?? "Unknown";
            FailureReasonCounts[key] = FailureReasonCounts.TryGetValue(
                key, out int count) ? count + 1 : 1;
        }
    }
}
