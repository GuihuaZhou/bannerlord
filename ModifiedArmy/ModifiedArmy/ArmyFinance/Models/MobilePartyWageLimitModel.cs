using ModifiedArmy.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.ArmyFinance.Models
{
    /// <summary>
    /// Scales native AI war-party wage limits to the mod's higher troop wages.
    /// The original wealth bands remain intact.
    /// </summary>
    public static class MobilePartyWageLimitModel
    {
        private const int PoorClanGold = 30000;
        private const int RichClanGold = 90000;
        private const float VanillaTier5Wage = 12f;

        public static int Calculate(Clan clan, MobileParty party)
        {
            if (clan?.Leader == null || party == null)
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

            if (party.LeaderHero == clan.Leader)
            {
                vanillaLimit *= 1.5f;
            }

            float wageScale =
                NewPartyWageModel.Tier5TroopWage
                / VanillaTier5Wage
                * NewPartyWageModel.GetWarWageMultiplier(party);
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
