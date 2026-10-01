using HarmonyLib;
using ModifiedDiplomacy.KingdomDiplomacy.Decisions;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Decisions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;

namespace ModifiedDiplomacy.Subjects.Patches
{
    /// <summary>
    /// Reuses Bannerlord's two-kingdom alliance decision panel while replacing
    /// its alliance wording for subject and compound diplomacy decisions.
    /// </summary>
    [HarmonyPatch(typeof(StartAllianceDecisionItemVM), "InitValues")]
    public static class SubjectDecisionPresentationPatch
    {
        [HarmonyPostfix]
        private static void Postfix(StartAllianceDecisionItemVM __instance)
        {
            StartAllianceDecision decision = AccessTools
                .Field(
                    typeof(StartAllianceDecisionItemVM),
                    "_startAllianceDecision")
                ?.GetValue(__instance) as StartAllianceDecision;

            if (decision is SubjectProposalKingdomDecision proposal)
            {
                ApplyText(
                    __instance,
                    proposal.GetGeneralTitle().ToString(),
                    proposal.GetPanelDescription().ToString());
            }
            else if (decision is SubjectReleaseKingdomDecision release)
            {
                ApplyText(
                    __instance,
                    release.GetGeneralTitle().ToString(),
                    release.GetPanelDescription().ToString());
            }
            else if (decision is SubjectResponseKingdomDecision response)
            {
                ApplyText(
                    __instance,
                    response.GetGeneralTitle().ToString(),
                    response.GetPanelDescription().ToString());
            }
            else if (decision is KingdomNegotiationDecision negotiation)
            {
                ApplyText(
                    __instance,
                    negotiation.GetGeneralTitle().ToString(),
                    negotiation.GetPanelDescription().ToString());
            }
        }

        private static void ApplyText(
            StartAllianceDecisionItemVM item,
            string title,
            string description)
        {
            item.NameText = title;
            item.StartAllianceDescriptionText = description;
        }
    }
}
