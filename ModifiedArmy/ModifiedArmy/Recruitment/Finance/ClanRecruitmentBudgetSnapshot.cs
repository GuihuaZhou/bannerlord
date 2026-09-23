namespace ModifiedArmy.Recruitment.Finance
{
    /// <summary>
    /// Read-only view of one clan's shared recruitment budget for the current
    /// campaign day. Every supported party draws from the remaining values.
    /// </summary>
    public sealed class ClanRecruitmentBudgetSnapshot
    {
        internal ClanRecruitmentBudgetSnapshot(
            int campaignDay,
            float dailyNetIncome,
            float expectedReserve,
            float totalCommitmentBudget,
            float totalPurchaseBudget)
        {
            CampaignDay = campaignDay;
            DailyNetIncome = dailyNetIncome;
            ExpectedReserve = expectedReserve;
            TotalCommitmentBudget = totalCommitmentBudget;
            RemainingCommitmentBudget = totalCommitmentBudget;
            TotalPurchaseBudget = totalPurchaseBudget;
            RemainingPurchaseBudget = totalPurchaseBudget;
        }

        public int CampaignDay { get; }

        public float DailyNetIncome { get; }

        public float ExpectedReserve { get; }

        public float TotalCommitmentBudget { get; }

        public float RemainingCommitmentBudget { get; internal set; }

        public float TotalPurchaseBudget { get; }

        public float RemainingPurchaseBudget { get; internal set; }
    }
}
