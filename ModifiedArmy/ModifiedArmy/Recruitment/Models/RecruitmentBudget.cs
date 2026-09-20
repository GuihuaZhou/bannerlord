using ModifiedArmy.PartyFinance.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Immutable economic snapshot used throughout one recruitment plan.
    /// </summary>
    public sealed class RecruitmentBudget
    {
        public const int DefaultMaintenanceDays = 30;
        public const float EmergencyReserveRatio = 0.20f;
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

            Clan clan = party.ActualClan ?? party.LeaderHero?.Clan;
            int partyCount = Math.Max(
                1,
                clan?.WarPartyComponents.Count ?? 1);
            float allocatedClanGold = (clan?.Gold ?? 0) / (float)partyCount;
            float liquidFunds = Math.Max(
                0f,
                party.PartyTradeGold + allocatedClanGold);

            result.SpendableFunds =
                liquidFunds * (1f - EmergencyReserveRatio);
            // Lord recruitment transactions withdraw from the leader, not
            // directly from PartyTradeGold. Keep this immediate-payment limit
            // separate from the broader long-term maintenance pool.
            result.AvailablePurchaseFunds = Math.Max(
                0f,
                party.LeaderHero?.Gold ?? party.PartyTradeGold);
            return result;
        }
    }
}
