using HarmonyLib;
using ModifiedPolitics.Models.WarDisposition.Decisions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace ModifiedPolitics.Patch.WarDisposition.Decisions
{
    /// <summary>
    /// 在盟友召战决议支持度上叠加 Clan 战争倾向.
    /// </summary>
    [HarmonyPatch(
        typeof(AcceptCallToWarAgreementDecision),
        nameof(AcceptCallToWarAgreementDecision.DetermineSupport),
        new System.Type[] { typeof(Clan), typeof(DecisionOutcome) })]
    public static class AcceptCallToWarDecisionPatch
    {
        private static void Postfix(
            Clan clan,
            DecisionOutcome possibleOutcome,
            ref float __result)
        {
            if (!(possibleOutcome is AcceptCallToWarAgreementDecision.AcceptCallToWarAgreementDecisionOutcome outcome))
            {
                return;
            }

            WarDispositionDecisionSupport.Apply(
                clan,
                outcome.ShouldAcceptCallToWar,
                "AcceptCallToWar",
                outcome.ShouldAcceptCallToWar ? "Accept" : "Reject",
                ref __result);
        }
    }
}
