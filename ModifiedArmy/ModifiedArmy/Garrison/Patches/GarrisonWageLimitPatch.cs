using HarmonyLib;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

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

                if (currentGarrisonWage > previousLimit
                    && currentGarrisonWage <= newLimit)
                {
                    ModLogger.Notice(
                        $"[GarrisonWageLimit] Prevented wage-limit dismissal | " +
                        $"Settlement='{town.Name}' | Wage={currentGarrisonWage} | " +
                        $"PreviousLimit={previousLimit} | NewLimit={newLimit}");
                }
                else if (currentGarrisonWage > newLimit)
                {
                    ModLogger.Notice(
                        $"[GarrisonWageLimit] Wage limit is still exceeded | " +
                        $"Settlement='{town.Name}' | Wage={currentGarrisonWage} | " +
                        $"NewLimit={newLimit} | Excess={currentGarrisonWage - newLimit}");
                }

                ModLogger.Debug(
                    $"[GarrisonWageLimit] Settlement='{town.Name}' | " +
                    $"Type={(town.Settlement.IsCastle ? "Castle" : "Town")} | " +
                    $"Border={isBorder} | DesiredSize={desiredSize} | " +
                    $"ExpectedAverageWage={expectedAverageWage:F2} | " +
                    $"GoldFactor={goldFactor:F2} | " +
                    $"RawLimit={rawWageLimit:F2} | " +
                    $"PreviousLimit={previousLimit} | NewLimit={newLimit}");
            }

            // The replacement has fully handled every fief of this clan.
            return false;
        }
    }
}
