using HarmonyLib;
using ModifiedPolitics.Governor.Config;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedPolitics.Governor.Patches
{
    /// <summary>
    /// Suppresses vanilla clan-level assignment only for centralized kingdoms.
    /// </summary>
    [HarmonyPatch(typeof(ClanVariablesCampaignBehavior), "UpdateGovernorsOfClan")]
    public static class ClanGovernorAssignmentPatch
    {
        private static bool Prefix(Clan clan)
        {
            Kingdom kingdom = clan?.Kingdom;
            // A true prefix result runs vanilla; false hands responsibility to the central behavior.
            return kingdom == null
                   || kingdom.IsEliminated
                   || !GovernorPolicyManager.Instance.IsCentralizedAssignment(kingdom.Culture);
        }
    }
}
