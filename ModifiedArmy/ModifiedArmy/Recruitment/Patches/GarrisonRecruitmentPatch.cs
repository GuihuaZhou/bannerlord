using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Disables native notable-slot recruitment. The mod's garrison behavior
    /// now consumes the settlement professional pool through the unified
    /// composition and clan-budget model.
    /// </summary>
    [HarmonyPatch(
        typeof(GarrisonRecruitmentCampaignBehavior),
        "TickAutoRecruitmentGarrisonChange")]
    public static class GarrisonRecruitmentPatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }
}
