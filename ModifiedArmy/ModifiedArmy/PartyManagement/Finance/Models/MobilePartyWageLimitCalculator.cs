using ModifiedArmy.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.PartyManagement.Finance.Models
{
    /// <summary>
    /// Calculates the wage-payment limit assigned to one AI mobile party.
    /// This is not a replacement for PartyWageModel: it does not calculate
    /// troop wages. It only decides the spending ceiling consumed by the
    /// native clan financial evaluation behavior.
    /// </summary>
    public static class MobilePartyWageLimitCalculator
    {
        private const int PoorClanGold = 30000;
        private const int RichClanGold = 90000;
        private const float VanillaTier5Wage = 12f;

        /// <summary>
        /// Reproduces the native wealth bands and then scales their result for
        /// the mod's higher T5 wage and the active formal-war multiplier.
        /// </summary>
        public static int Calculate(Clan clan, MobileParty party)
        {
            if (clan?.Leader == null || party == null)
            {
                return Campaign.Current.Models.PartyWageModel
                    .MaxWagePaymentLimit;
            }

            return Calculate(
                clan,
                party.LeaderHero,
                NewPartyWageModel.GetWarWageMultiplier(party) > 1f);
        }

        /// <summary>
        /// Calculates the same limit without requiring a live party. Creation
        /// previews use the leader and formal-war snapshot supplied here.
        /// </summary>
        public static int Calculate(
            Clan clan,
            Hero leader,
            bool isAtFormalWar)
        {
            if (clan?.Leader == null || leader == null)
            {
                return Campaign.Current.Models.PartyWageModel
                    .MaxWagePaymentLimit;
            }

            int leaderGold = Math.Max(0, clan.Leader.Gold);

            if (leaderGold > RichClanGold)
            {
                return Campaign.Current.Models.PartyWageModel
                    .MaxWagePaymentLimit;
            }

            // Preserve the native curved progression for poor clans and the
            // linear progression for established clans.
            float vanillaLimit;

            if (leaderGold > PoorClanGold)
            {
                vanillaLimit = 600f
                    + (leaderGold - PoorClanGold)
                    / (float)(RichClanGold - PoorClanGold)
                    * 600f;
            }
            else
            {
                float wealthRatio = leaderGold / (float)PoorClanGold;
                vanillaLimit = 200f
                    + wealthRatio * wealthRatio * 400f;
            }

            if (leader == clan.Leader)
            {
                vanillaLimit *= 1.5f;
            }

            // The native thresholds assume a T5 wage of 12. Scale the limit
            // instead of changing wage calculation a second time.
            float wageScale =
                NewPartyWageModel.Tier5TroopWage
                / VanillaTier5Wage
                * (isAtFormalWar ? NewPartyWageModel.WarWageMultiplier : 1f);
            int scaledLimit = (int)Math.Round(
                vanillaLimit * wageScale,
                MidpointRounding.AwayFromZero);

            return Math.Min(
                scaledLimit,
                Campaign.Current.Models.PartyWageModel
                    .MaxWagePaymentLimit);
        }
    }
}
