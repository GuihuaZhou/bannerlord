using HarmonyLib;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// Replaces the vanilla AI garrison wage-limit update. The original
    /// calculation scales the limit with prosperity and food potential, which
    /// can make a strategically important poor settlement dismiss its troops.
    /// </summary>
    [HarmonyPatch(
        typeof(ClanVariablesCampaignBehavior),
        "UpdateClanSettlementsPaymentLimit")]
    public static class GarrisonWageLimitPatch
    {
        private static bool Prefix(Clan clan)
        {
            if (clan == Clan.PlayerClan)
            {
                // Preserve the wage limit selected by the player in the UI.
                return true;
            }

            if (clan?.MapFaction == null
                || (!clan.IsRebelClan
                    && !clan.MapFaction.IsKingdomFaction))
            {
                // Match vanilla behavior for clans outside the supported scope.
                return false;
            }

            foreach (Town town in clan.Fiefs)
            {
                int previousLimit =
                    town.Settlement.GarrisonWagePaymentLimit;

                int newLimit =
                    GarrisonWageLimitModel.CalculateWagePaymentLimit(
                        town,
                        out int desiredSize,
                        out float goldFactor,
                        out float expectedAverageWage,
                        out float rawWageLimit,
                        out bool isBorder);

                town.Settlement.SetGarrisonWagePaymentLimit(newLimit);

                int currentGarrisonWage =
                    town.GarrisonParty?.TotalWage ?? 0;

                bool shouldLog = clan == Clan.PlayerClan ||
                    (Clan.PlayerClan?.Kingdom != null &&
                        clan.Kingdom == Clan.PlayerClan.Kingdom);

                if (shouldLog &&
                    currentGarrisonWage > previousLimit
                    && currentGarrisonWage <= newLimit)
                {
                    TextObject message = GameTexts.FindText(
                        "str_modifiedarmy_garrison_wage_dismissal_prevented");
                    message.SetTextVariable("SETTLEMENT_NAME", town.Name);
                    message.SetTextVariable("CURRENT_WAGE", currentGarrisonWage);
                    message.SetTextVariable("PREVIOUS_LIMIT", previousLimit);
                    message.SetTextVariable("NEW_LIMIT", newLimit);
                    ModLogger.Notice(message.ToString());
                }
                else if (shouldLog && currentGarrisonWage > newLimit)
                {
                    TextObject message = GameTexts.FindText(
                        "str_modifiedarmy_garrison_wage_still_exceeded");
                    message.SetTextVariable("SETTLEMENT_NAME", town.Name);
                    message.SetTextVariable("CURRENT_WAGE", currentGarrisonWage);
                    message.SetTextVariable("NEW_LIMIT", newLimit);
                    message.SetTextVariable(
                        "EXCESS",
                        currentGarrisonWage - newLimit);
                    ModLogger.Notice(message.ToString());
                }
            }

            // The replacement has fully handled every fief of this clan.
            return false;
        }
    }
}
