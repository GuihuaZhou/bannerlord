using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Decisions;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Decisions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Decisions.ItemTypes;

namespace ModifiedPolitics.KingdomDiplomacy.Patches
{
    /// <summary>
    /// Reuses Bannerlord's two-kingdom alliance decision panel while replacing
    /// its alliance-specific heading with subject proposal text.
    /// </summary>
    [HarmonyPatch(typeof(StartAllianceDecisionItemVM), "InitValues")]
    public static class SubjectProposalDecisionItemPatch
    {
        [HarmonyPostfix]
        private static void Postfix(StartAllianceDecisionItemVM __instance)
        {
            StartAllianceDecision decision = AccessTools
                .Field(typeof(StartAllianceDecisionItemVM),
                    "_startAllianceDecision")
                ?.GetValue(__instance)
                as StartAllianceDecision;
            if (decision is SubjectProposalKingdomDecision proposal)
            {
                __instance.NameText = proposal.GetGeneralTitle().ToString();
                __instance.StartAllianceDescriptionText = proposal
                    .GetPanelDescription()
                    .ToString();
                return;
            }

            if (decision is SubjectReleaseKingdomDecision release)
            {
                __instance.NameText = release.GetGeneralTitle().ToString();
                __instance.StartAllianceDescriptionText = release
                    .GetPanelDescription()
                    .ToString();
                return;
            }

            if (decision is SubjectResponseKingdomDecision response)
            {
                __instance.NameText = response.GetGeneralTitle().ToString();
                __instance.StartAllianceDescriptionText = response
                    .GetPanelDescription()
                    .ToString();
                return;
            }

            if (decision is KingdomNegotiationDecision negotiation)
            {
                __instance.NameText = negotiation.GetGeneralTitle().ToString();
                __instance.StartAllianceDescriptionText = negotiation
                    .GetPanelDescription()
                    .ToString();
            }
        }
    }
}
