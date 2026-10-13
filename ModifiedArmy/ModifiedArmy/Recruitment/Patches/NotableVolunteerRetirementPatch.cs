using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Keeps Hero.VolunteerTypes for save compatibility while stopping the
    /// native notable-slot economy. Settlement manpower pools now own troop
    /// production, so notable slots must neither refill nor upgrade.
    /// </summary>
    [HarmonyPatch(
        typeof(RecruitmentCampaignBehavior),
        "UpdateVolunteersOfNotablesInSettlement")]
    public static class NotableVolunteerMaintenancePatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }

    /// <summary>
    /// Hides the native notable-recruitment entry in every settlement. The
    /// shared manpower system replaces notable volunteer slots completely.
    /// </summary>
    [HarmonyPatch(
        typeof(PlayerTownVisitCampaignBehavior),
        "game_menu_recruit_volunteers_on_condition")]
    public static class NotableRecruitmentMenuPatch
    {
        private static bool Prefix(
            MenuCallbackArgs args,
            ref bool __result)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
            __result = false;
            return false;
        }
    }
}
