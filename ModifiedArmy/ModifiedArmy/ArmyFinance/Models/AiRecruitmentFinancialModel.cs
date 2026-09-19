using ModifiedArmy.common;
using ModifiedArmy.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.ArmyFinance.Models
{
    /// <summary>
    /// Prevents AI recruitment from creating a party whose full post-exemption
    /// wage cannot be sustained for the configured reserve period.
    /// </summary>
    public static class AiRecruitmentFinancialModel
    {
        public static float GetAvailableAdditionalDailyWage(
            MobileParty party)
        {
            if (party?.LeaderHero?.Clan == null)
            {
                return 0f;
            }

            float currentLongTermWage = GetLongTermDailyWage(party);
            float reserveDays = Math.Max(
                1f,
                ModConfigManager.Instance
                    .GetActiveConfig()
                    .AiNeedEconomicWeeks);
            Clan clan = party.LeaderHero.Clan;
            int partyCount = Math.Max(1, clan.WarPartyComponents.Count);
            float allocatedClanGold = clan.Gold / (float)partyCount;
            float liquidFunds = Math.Max(
                0f,
                party.PartyTradeGold + allocatedClanGold);
            float cashSupportedWage = liquidFunds / reserveDays;
            float sustainableWage = Math.Min(
                party.PaymentLimit,
                cashSupportedWage);

            return Math.Max(
                0f,
                sustainableWage - currentLongTermWage);
        }

        public static int GetAffordableTroopCount(
            MobileParty party,
            CharacterObject troop,
            int requestedCount)
        {
            if (requestedCount <= 0 || troop == null)
            {
                return 0;
            }

            float unitWage = EstimateUnitDailyWage(party, troop);

            if (unitWage <= 0f)
            {
                return requestedCount;
            }

            return Math.Min(
                requestedCount,
                (int)Math.Floor(
                    GetAvailableAdditionalDailyWage(party)
                    / unitWage));
        }

        public static float EstimateUnitDailyWage(
            MobileParty party,
            CharacterObject troop)
        {
            return Campaign.Current.Models.PartyWageModel
                .GetCharacterWage(troop)
                * NewPartyWageModel.GetWarWageMultiplier(party);
        }

        private static float GetLongTermDailyWage(MobileParty party)
        {
            if (Campaign.Current.Models.PartyWageModel
                is NewPartyWageModel wageModel)
            {
                return wageModel.GetTotalWageWithoutFiefExemption(
                    party,
                    party.MemberRoster,
                    false).ResultNumber;
            }

            return party.TotalWage;
        }
    }
}
