namespace ModifiedArmy.Recruitment.Services
{
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
        public int TotalCommitted => ProfessionalRecruited + PrisonerRecruited;
    }
}
