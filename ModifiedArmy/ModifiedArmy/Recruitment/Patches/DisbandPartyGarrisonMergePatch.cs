using HarmonyLib;
using ModifiedArmy.ForceManagement.Services;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Patches
{
    [HarmonyPatch(typeof(DisbandPartyCampaignBehavior),
        "MergeDisbandPartyToFortification")]
    public static class DisbandPartyGarrisonMergePatch
    {
        private static bool Prefix(MobileParty disbandParty,
            Settlement relatedSettlement) =>
            DisbandSettlementCoordinator.HandleFortificationMerge(
                disbandParty, relatedSettlement);
    }

    [HarmonyPatch(typeof(DisbandPartyCampaignBehavior), "OnPartyDisbanded")]
    public static class DisbandPartyFinalizationPatch
    {
        private static void Prefix(MobileParty disbandParty,
            Settlement relatedSettlement)
        {
            DisbandSettlementCoordinator.HandleFinalization(
                disbandParty, relatedSettlement);
        }
    }
}
