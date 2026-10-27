using ModifiedArmy.Models;
using ModifiedArmy.PartyManagement.Finance.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.PartyManagement.Finance.Services
{
    internal static class ClanFinancialEvaluationService
    {
        public static void EvaluateWarPartyWageLimits(Clan clan)
        {
            if (clan == null) return;
            foreach (WarPartyComponent component in clan.WarPartyComponents)
            {
                MobileParty party = component?.MobileParty;
                if (party == null) continue;
                int limit = MobilePartyWageLimitCalculator.Calculate(clan, party);
                party.SetWagePaymentLimit(limit);
                Log(clan, party, limit);
            }
        }

        private static void Log(Clan clan, MobileParty party, int wageLimit)
        {
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            if (clan != Clan.PlayerClan &&
                (playerKingdom == null || clan.Kingdom != playerKingdom)) return;

            int currentWage = party.TotalWage;
            bool exceeded = currentWage > wageLimit;
            TextObject message = GameTexts.FindText(exceeded
                ? "str_modifiedarmy_party_wage_limit_exceeded"
                : "str_modifiedarmy_party_wage_limit_evaluated");
            TextObject warState = NewPartyWageModel.GetWarWageMultiplier(party) > 1f
                ? GameTexts.FindText("str_modifiedarmy_party_wage_state_war")
                : GameTexts.FindText("str_modifiedarmy_party_wage_state_peace");
            message.SetTextVariable("PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("CURRENT_WAGE", currentWage);
            message.SetTextVariable("WAGE_LIMIT", wageLimit);
            message.SetTextVariable("LEADER_GOLD", clan.Leader?.Gold ?? 0);
            message.SetTextVariable("WAR_STATE", warState);
            if (exceeded) ModLogger.Notice(message.ToString());
            else ModLogger.Info(message.ToString());
        }
    }
}
