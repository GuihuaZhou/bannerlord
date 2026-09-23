using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Recruitment.Finance
{
    /// <summary>
    /// Owns the daily recruitment pool shared by every AI party and garrison
    /// of a clan. The cache is intentionally transient and is recalculated
    /// after loading a save or when the campaign day changes.
    /// </summary>
    public static class ClanRecruitmentBudgetManager
    {
        public const int ForecastDays = 30;
        public const int PositiveIncomeForecastDays = 7;
        public const int ReserveDays = 14;
        public const float PositiveIncomeRecognitionRatio = 0.10f;
        public const float NegativeIncomeRiskMultiplier = 1.50f;
        public const float WealthReserveRatio = 0.40f;
        public const float DailyCommitmentRatio = 0.10f;
        public const float MinimumReserve = 30000f;

        private static readonly Dictionary<Clan, ClanRecruitmentBudgetSnapshot>
            Snapshots = new Dictionary<Clan, ClanRecruitmentBudgetSnapshot>();

        private static int _cachedCampaignDay = int.MinValue;

        /// <summary>
        /// Returns the clan that ultimately finances the supplied party.
        /// </summary>
        public static Clan GetSupportingClan(MobileParty party)
        {
            if (party?.IsGarrison == true)
            {
                return party.CurrentSettlement?.OwnerClan
                    ?? party.ActualClan;
            }

            return party?.ActualClan
                ?? party?.LeaderHero?.Clan
                ?? party?.CurrentSettlement?.OwnerClan;
        }

        /// <summary>
        /// Returns today's shared budget. Positive income is heavily
        /// discounted while losses are amplified, preventing an already
        /// declining clan from recruiting against optimistic future revenue.
        /// </summary>
        public static ClanRecruitmentBudgetSnapshot GetSnapshot(Clan clan)
        {
            int campaignDay = GetCampaignDay();
            ResetCacheWhenDayChanges(campaignDay);

            if (clan == null || Campaign.Current == null)
            {
                return new ClanRecruitmentBudgetSnapshot(
                    campaignDay,
                    0f,
                    MinimumReserve,
                    0f,
                    0f);
            }

            if (Snapshots.TryGetValue(clan, out ClanRecruitmentBudgetSnapshot snapshot))
            {
                return snapshot;
            }

            float dailyNetIncome = Campaign.Current.Models.ClanFinanceModel
                .CalculateClanGoldChange(clan, false, false, true)
                .ResultNumber;
            float dailyExpenses = Math.Abs(
                Campaign.Current.Models.ClanFinanceModel
                    .CalculateClanExpenses(clan, false, false, true)
                    .ResultNumber);
            // Reserve at least two weeks of all current expenses. Wealthier
            // clans also retain a percentage of their existing treasury, so
            // temporary prosperity does not immediately become recruitment.
            float expectedReserve = Math.Max(
                MinimumReserve,
                Math.Max(
                    dailyExpenses * ReserveDays,
                    clan.Gold * WealthReserveRatio));

            // Positive income is uncertain and therefore contributes only a
            // small seven-day allowance. Losses are more dangerous: project
            // them for the full horizon and amplify them by fifty percent.
            float forecastIncomeAdjustment = dailyNetIncome > 0f
                ? dailyNetIncome
                    * PositiveIncomeRecognitionRatio
                    * PositiveIncomeForecastDays
                : dailyNetIncome
                    * NegativeIncomeRiskMultiplier
                    * ForecastDays;
            float safeCapacity = Math.Max(
                0f,
                clan.Gold
                    + forecastIncomeAdjustment
                    - expectedReserve);
            float totalCommitmentBudget =
                safeCapacity * DailyCommitmentRatio;
            float totalPurchaseBudget = Math.Min(
                totalCommitmentBudget,
                Math.Max(0f, clan.Gold - expectedReserve));

            snapshot = new ClanRecruitmentBudgetSnapshot(
                campaignDay,
                dailyNetIncome,
                expectedReserve,
                totalCommitmentBudget,
                totalPurchaseBudget);
            Snapshots[clan] = snapshot;
            return snapshot;
        }

        /// <summary>
        /// Deducts an executed recruitment transaction from the shared pool.
        /// Planning alone never spends the pool, so stale or cancelled offers
        /// cannot consume another party's daily allowance.
        /// </summary>
        public static void CommitRecruitment(
            MobileParty party,
            int count,
            float unitRecruitmentCost,
            float unitDailyWage)
        {
            Clan clan = GetSupportingClan(party);

            if (clan == null || count <= 0)
            {
                return;
            }

            ClanRecruitmentBudgetSnapshot snapshot = GetSnapshot(clan);
            float purchaseCost = Math.Max(0f, unitRecruitmentCost) * count;
            float commitment = purchaseCost
                + Math.Max(0f, unitDailyWage) * ForecastDays * count;

            snapshot.RemainingPurchaseBudget = Math.Max(
                0f,
                snapshot.RemainingPurchaseBudget - purchaseCost);
            snapshot.RemainingCommitmentBudget = Math.Max(
                0f,
                snapshot.RemainingCommitmentBudget - commitment);
        }

        private static int GetCampaignDay()
        {
            return Campaign.Current == null
                ? 0
                : (int)CampaignTime.Now.ToDays;
        }

        private static void ResetCacheWhenDayChanges(int campaignDay)
        {
            if (_cachedCampaignDay == campaignDay)
            {
                return;
            }

            Snapshots.Clear();
            _cachedCampaignDay = campaignDay;
        }
    }
}
