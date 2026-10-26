using HarmonyLib;
using ModifiedArmy.Recruitment.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Patches
{
    [HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior), "RecruitPrisonersAi")]
    public static class RecruitPrisonersAiPatch
    {
        public static bool Prefix(
            RecruitPrisonersCampaignBehavior __instance,
            MobileParty mobileParty,
            CharacterObject troop,
            int num,
            int conformityCost)
        {
            PrisonerRecruitmentService.RecruitAiPrisoners(
                mobileParty, troop, num, conformityCost);
            return false;
        }
    }

    [HarmonyPatch(typeof(RecruitPrisonersCampaignBehavior),
        "OnMainPartyPrisonerRecruited")]
    public static class OnMainPartyPrisonerRecruitedPatch
    {
        public static bool OnMainPartyPrisonerRecruitedPrefix(
            FlattenedTroopRoster flattenedTroopRosters)
        {
            PrisonerRecruitmentService.RecruitMainPartyPrisoners(
                flattenedTroopRosters);
            return false;
        }
    }
}
