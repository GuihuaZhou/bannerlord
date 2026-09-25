using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Disables native daily garrison volunteer recruitment. Garrison strength
    /// should be supplied by troop transfers and explicit logistics instead of
    /// silently growing beyond the settlement's sustainable establishment.
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
