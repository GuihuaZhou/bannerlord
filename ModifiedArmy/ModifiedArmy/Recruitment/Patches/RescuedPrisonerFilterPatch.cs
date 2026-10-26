using HarmonyLib;
using ModifiedArmy.Recruitment.Services;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Patches
{
    [HarmonyPatch(typeof(MapEvent), "LootDefeatedPartyPrisoners")]
    internal static class RescuedPrisonerFilterPatch
    {
        private static void Prefix(
            MBReadOnlyList<MapEventParty> winnerParties,
            out RescuedPrisonerSnapshot __state)
        {
            __state = RescuedPrisonerFilterService.Capture(winnerParties);
        }

        private static void Postfix(RescuedPrisonerSnapshot __state)
        {
            RescuedPrisonerFilterService.RemoveNewBandits(__state);
        }
    }
}
