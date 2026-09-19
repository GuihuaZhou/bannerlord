using HarmonyLib;
using ModifiedArmy.ArmyFinance.Models;
using ModifiedArmy.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
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
            TextObject message = isExceeded
                ? new TextObject(
                    "{=ModifiedArmy_PartyWageLimitExceeded}" +
                    "[ArmyFinance] Wage limit exceeded | " +
                    "Party='{PARTY_NAME}' | Clan='{CLAN_NAME}' | " +
                    "CurrentWage={CURRENT_WAGE} | " +
                    "WageLimit={WAGE_LIMIT} | " +
                    "LeaderGold={LEADER_GOLD} | State={WAR_STATE}")
                : new TextObject(
                    "{=ModifiedArmy_PartyWageLimitEvaluated}" +
                    "[ArmyFinance] Party='{PARTY_NAME}' | " +
                    "Clan='{CLAN_NAME}' | CurrentWage={CURRENT_WAGE} | " +
                    "WageLimit={WAGE_LIMIT} | " +
                    "LeaderGold={LEADER_GOLD} | State={WAR_STATE}");
            TextObject warState =
                NewPartyWageModel.GetWarWageMultiplier(party) > 1f
                    ? new TextObject(
                        "{=ModifiedArmy_PartyWageStateWar}War")
                    : new TextObject(
                        "{=ModifiedArmy_PartyWageStatePeace}Peace");

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
