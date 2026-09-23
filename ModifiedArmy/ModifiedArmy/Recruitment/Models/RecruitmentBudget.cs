using ModifiedArmy.PartyFinance.Models;
using ModifiedArmy.Recruitment.Finance;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Immutable economic snapshot used throughout one recruitment plan.
    /// </summary>
    public sealed class RecruitmentBudget
    {
        public const int DefaultMaintenanceDays = 30;
        public const float WageLimitUsageRatio = 0.90f;

        private RecruitmentBudget()
        {
        }

        public float CurrentLongTermWage { get; private set; }

        public float UsableWageLimit { get; private set; }

        public float SpendableFunds { get; private set; }

        public float AvailablePurchaseFunds { get; private set; }

        public int MaintenanceDays { get; private set; }

        /// <summary>
        /// Captures all financial values once so every candidate in the same
        /// plan is evaluated against a consistent budget snapshot.
        /// </summary>
        public static RecruitmentBudget Create(MobileParty party)
        {
            RecruitmentBudget result = new RecruitmentBudget
            {
                MaintenanceDays = DefaultMaintenanceDays
            };

            if (party == null)
            {
                return result;
            }

            result.CurrentLongTermWage =
                AiRecruitmentFinancialModel.GetLongTermDailyWage(party);
            result.UsableWageLimit = Math.Max(
                0f,
                party.PaymentLimit * WageLimitUsageRatio);

            Clan supportingClan =
                ClanRecruitmentBudgetManager.GetSupportingClan(party);
            ClanRecruitmentBudgetSnapshot clanBudget =
                ClanRecruitmentBudgetManager.GetSnapshot(supportingClan);

            result.SpendableFunds =
                clanBudget.RemainingCommitmentBudget;
            // Lord recruitment transactions withdraw from the leader, not
            // directly from PartyTradeGold. Keep this immediate-payment limit
            // separate from the broader long-term maintenance pool. Garrison
            // purchases are deferred into Clan.AutoRecruitmentExpenses, so
            // they use their allocated central share instead of requiring the
            // clan leader to pay the full amount immediately.
            float transactionFunds = party.IsGarrison
                ? clanBudget.RemainingPurchaseBudget
                : Math.Max(0f, party.LeaderHero?.Gold ?? party.PartyTradeGold);
            result.AvailablePurchaseFunds = Math.Min(
                clanBudget.RemainingPurchaseBudget,
                transactionFunds);
            return result;
        }

        /// <summary>
        /// Returns funds attributable to one party without counting clan gold
        /// twice when the clan leader commands that party. Non-leader lord
        /// parties retain their genuinely separate frontline cash in addition
        /// to one share of central support.
        /// </summary>
        public static float GetOperatingFunds(MobileParty party)
        {
            if (party == null)
            {
                return 0f;
            }

            Clan clan = GetSupportingClan(party);
            float allocatedClanFunds = GetAllocatedClanFunds(party);

            if (party.IsGarrison ||
                clan == null ||
                party.LeaderHero == clan.Leader)
            {
                return allocatedClanFunds;
            }

            return Math.Max(
                0f,
                party.PartyTradeGold + allocatedClanFunds);
        }

        /// <summary>
        /// Divides the central clan treasury among every active war party and
        /// garrison that relies on the same daily clan-finance settlement.
        /// </summary>
        public static float GetAllocatedClanFunds(MobileParty party)
        {
            Clan clan = GetSupportingClan(party);

            if (clan == null)
            {
                return 0f;
            }

            int supportedPartyCount = clan.WarPartyComponents.Count;

            foreach (Town town in clan.Fiefs)
            {
                if (town?.GarrisonParty?.IsActive == true)
                {
                    supportedPartyCount++;
                }
            }

            return Math.Max(0f, clan.Gold) /
                Math.Max(1, supportedPartyCount);
        }

        private static Clan GetSupportingClan(MobileParty party)
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
    }
}
