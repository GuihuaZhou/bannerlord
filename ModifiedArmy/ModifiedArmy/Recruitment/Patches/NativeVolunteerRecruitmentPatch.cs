using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Stops the native daily maintenance that creates and upgrades volunteer
    /// slots for settlement notables.
    /// </summary>
    [HarmonyPatch(
        typeof(RecruitmentCampaignBehavior),
        "UpdateVolunteersOfNotablesInSettlement")]
    public static class NativeVolunteerMaintenancePatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }

    /// <summary>
    /// Hides the native Recruit Troops option from the town menu. Custom
    /// manpower access remains available through its own menu.
    /// </summary>
    [HarmonyPatch(
        typeof(PlayerTownVisitCampaignBehavior),
        "game_menu_town_recruit_troops_on_condition")]
    public static class NativeVolunteerMenuPatch
    {
        private static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// Hides the matching native Recruit Troops option from the village menu.
    /// </summary>
    [HarmonyPatch(
        typeof(PlayerTownVisitCampaignBehavior),
        "game_menu_recruit_volunteers_on_condition")]
    public static class NativeVillageVolunteerMenuPatch
    {
        private static bool Prefix(ref bool __result)
        {
            __result = false;
            return false;
        }
    }
}
