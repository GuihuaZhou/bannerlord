using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

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
    /// Villages no longer supply recruits. Town recruitment remains visible
    /// until the later player-facing manpower-pool recruitment UI replaces it.
    /// </summary>
    [HarmonyPatch(
        typeof(PlayerTownVisitCampaignBehavior),
        "game_menu_recruit_volunteers_on_condition")]
    public static class VillageRecruitmentMenuPatch
    {
        private static bool Prefix(
            MenuCallbackArgs args,
            ref bool __result)
        {
            if (Settlement.CurrentSettlement?.IsVillage != true)
            {
                return true;
            }

            args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
            __result = false;
            return false;
        }
    }
}
