using HarmonyLib;
using ModifiedArmy.Tool;
using ModifiedPolitics.Models.WarDisposition.Decisions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;

namespace ModifiedPolitics.Patch.WarDisposition.Decisions
{
    /// <summary>
    /// 在本体停战决议支持度上叠加 Clan 战争倾向.
    /// 本体明确返回 0/200 的强制分支保持不变.
    /// </summary>
    [HarmonyPatch(
        typeof(MakePeaceKingdomDecision),
        nameof(MakePeaceKingdomDecision.DetermineSupport),
        new System.Type[] { typeof(Clan), typeof(DecisionOutcome) })]
    public static class MakePeaceDecisionPatch
    {
        private static void Postfix(
            MakePeaceKingdomDecision __instance,
            Clan clan,
            DecisionOutcome possibleOutcome,
            bool ____isProposedByOpponent,
            ref float __result)
        {
            if (!(possibleOutcome is MakePeaceKingdomDecision.MakePeaceDecisionOutcome outcome))
            {
                return;
            }

            if (IsVanillaForcedBranch(
                __instance,
                clan,
                ____isProposedByOpponent))
            {
                ModLogger.Info(
                    $"[战争倾向] 跳过停战支持度修正 | 家族={clan?.Name} | " +
                    $"结果={(outcome.ShouldPeaceBeDeclared ? "MakePeace" : "ContinueWar")} | " +
                    "原因=本体强制分支");
                return;
            }

            WarDispositionDecisionSupport.Apply(
                clan,
                !outcome.ShouldPeaceBeDeclared,
                "MakePeace",
                outcome.ShouldPeaceBeDeclared ? "MakePeace" : "ContinueWar",
                ref __result);
        }

        private static bool IsVanillaForcedBranch(
            MakePeaceKingdomDecision decision,
            Clan clan,
            bool isProposedByOpponent)
        {
            if (!Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(
                    decision.Kingdom,
                    decision.FactionToMakePeaceWith)
                && !isProposedByOpponent)
            {
                return true;
            }

            return clan == decision.Kingdom.RulingClan
                && Clan.PlayerClan.MapFaction == decision.Kingdom
                && isProposedByOpponent;
        }
    }
}
