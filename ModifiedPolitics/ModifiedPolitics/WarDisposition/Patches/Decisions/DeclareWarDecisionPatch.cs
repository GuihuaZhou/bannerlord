using HarmonyLib;
using ModifiedPolitics.Models.WarDisposition.Decisions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace ModifiedPolitics.Patch.WarDisposition.Decisions
{
    /// <summary>
    /// 在本体宣战决议支持度上叠加 Clan 战争倾向.
    /// </summary>
    [HarmonyPatch(
        typeof(DeclareWarDecision),
        nameof(DeclareWarDecision.DetermineSupport),
        new System.Type[] { typeof(Clan), typeof(DecisionOutcome) })]
    public static class DeclareWarDecisionPatch
    {
        private static void Postfix(
            Clan clan,
            DecisionOutcome possibleOutcome,
            ref float __result)
        {
            if (!(possibleOutcome is DeclareWarDecision.DeclareWarDecisionOutcome outcome))
            {
                return;
            }

            WarDispositionDecisionSupport.Apply(
                clan,
                outcome.ShouldWarBeDeclared,
                "DeclareWar",
                outcome.ShouldWarBeDeclared ? "SupportWar" : "OpposeWar",
                ref __result);
        }
    }
}
