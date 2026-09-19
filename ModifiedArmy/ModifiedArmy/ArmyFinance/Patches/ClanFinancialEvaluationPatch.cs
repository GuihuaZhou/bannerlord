using HarmonyLib;
using ModifiedArmy.ArmyFinance.Models;
using ModifiedArmy.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ArmyFinance.Patches
{
    /// <summary>
    /// Replaces native AI war-party wage-limit evaluation with limits scaled
    /// to the mod's troop wages. Garrison limits remain independent.
    /// </summary>
    [HarmonyPatch(
        typeof(ClanVariablesCampaignBehavior),
        "MakeClanFinancialEvaluation")]
    public static class ClanFinancialEvaluationPatch
    {
        private static bool Prefix(Clan clan)
        {
            if (clan == null)
            {
                return false;
            }

            foreach (WarPartyComponent warParty in
                clan.WarPartyComponents)
            {
                if (warParty?.MobileParty == null)
                {
                    continue;
                }

                MobileParty party = warParty.MobileParty;
                int wageLimit = MobilePartyWageLimitModel.Calculate(
                    clan,
                    party);
                party.SetWagePaymentLimit(wageLimit);
                LogWageLimit(clan, party, wageLimit);
            }

            return false;
        }

        /// <summary>
        /// Reports every recalculated mobile-party wage limit. Exceeded limits
        /// use Notice so imminent troop dismissals remain visible at the
        /// default logging level; ordinary evaluations use Info.
        /// </summary>
        private static void LogWageLimit(
            Clan clan,
            MobileParty party,
            int wageLimit)
        {
            int currentWage = party.TotalWage;
            bool isExceeded = currentWage > wageLimit;
            TextObject message = GameTexts.FindText(
                isExceeded
                    ? "str_modifiedarmy_party_wage_limit_exceeded"
                    : "str_modifiedarmy_party_wage_limit_evaluated");
            TextObject warState = GameTexts.FindText(
                NewPartyWageModel.GetWarWageMultiplier(party) > 1f
                    ? "str_modifiedarmy_party_wage_state_war"
                    : "str_modifiedarmy_party_wage_state_peace");

            message.SetTextVariable("PARTY_NAME", party.Name);
            message.SetTextVariable("CLAN_NAME", clan.Name);
            message.SetTextVariable("CURRENT_WAGE", currentWage);
            message.SetTextVariable("WAGE_LIMIT", wageLimit);
            message.SetTextVariable(
                "LEADER_GOLD",
                clan.Leader?.Gold ?? 0);
            message.SetTextVariable("WAR_STATE", warState);

            if (isExceeded)
            {
                ModLogger.Notice(message.ToString());
            }
            else
            {
                ModLogger.Info(message.ToString());
            }
        }
    }
}
