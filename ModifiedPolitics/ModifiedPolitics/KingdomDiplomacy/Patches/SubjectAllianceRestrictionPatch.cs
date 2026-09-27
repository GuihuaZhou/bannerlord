using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedPolitics.KingdomDiplomacy.Patches
{
    /// <summary>
    /// Guards the native mutation entry point so direct calls from Bannerlord
    /// or another mod must obey puppet and vassal alliance rules.
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
