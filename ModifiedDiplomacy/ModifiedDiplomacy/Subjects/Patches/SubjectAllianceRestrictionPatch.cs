using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedDiplomacy.Subjects.Patches
{
    /// <summary>
    /// Guards the native alliance mutation point. Keeping this patch in the
    /// diplomacy module ensures every caller follows the same subject rules.
    /// </summary>
    [HarmonyPatch(
        typeof(AllianceCampaignBehavior),
        nameof(AllianceCampaignBehavior.StartAlliance))]
    public static class SubjectAllianceRestrictionPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(
            Kingdom proposerKingdom,
            Kingdom receiverKingdom)
        {
            return SubjectAllianceRestrictionService.CanFormAlliance(
                proposerKingdom,
                receiverKingdom);
        }
    }
}
