using HarmonyLib;
using ModifiedArmy.ForceManagement.Services;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Blocks Bannerlord's independent recruiting decision only for AI lord
    /// parties owned by the unified force planner. Parties outside that exact
    /// scope—including caravans, villagers, bandits and the player—continue
    /// through the native handler unchanged.
    /// </summary>
    [HarmonyPatch(typeof(RecruitmentCampaignBehavior), "CheckRecruiting")]
    public static class AiRecruitmentPatch
    {
        /// <summary>
        /// Acts only as an ownership gate. Recruitment execution belongs to
        /// PartyExchangeBehavior and never falls back to the removed legacy
        /// fief/garrison/mercenary routines during the same visit.
        /// </summary>
        private static bool Prefix(
            MobileParty mobileParty,
            Settlement settlement)
        {
            return !ManagedPartyPolicy.IsModManagedAiParty(mobileParty);
        }
    }
}
