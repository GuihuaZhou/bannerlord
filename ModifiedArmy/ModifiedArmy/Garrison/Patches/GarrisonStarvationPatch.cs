using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// Makes vanilla garrison morale use the military granary's starvation
    /// state instead of civilian settlement FoodStocks.
    /// </summary>
    [HarmonyPatch(typeof(SettlementHelper), "IsGarrisonStarving")]
    public static class GarrisonStarvationPatch
    {
        private static bool Prefix(
            Settlement settlement,
            ref bool __result)
        {
            __result = settlement?.Town?.GarrisonParty?.Party.IsStarving
                == true;
            return false;
        }
    }
}
