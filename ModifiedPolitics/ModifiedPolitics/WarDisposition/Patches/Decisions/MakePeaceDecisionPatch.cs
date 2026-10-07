using HarmonyLib;
using ModifiedPolitics.Models.WarDisposition.Decisions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Patch.WarDisposition.Decisions
{
    /// <summary>
    /// 让所有和平提案都使用正常的 Clan 评分，并在该评分上叠加战争倾向。
    /// 本体对主动求和和敌国向玩家求和设置的 0/200 强制结果会绕过议会意见，
    /// 因此这里只替换这两个特殊分支，不改动其余本体评分公式。
    /// </summary>
    [HarmonyPatch(
        typeof(MakePeaceKingdomDecision),
        nameof(MakePeaceKingdomDecision.DetermineSupport),
        new System.Type[] { typeof(Clan), typeof(DecisionOutcome) })]
    public static class MakePeaceDecisionPatch
    {
        private static bool Prefix(
            MakePeaceKingdomDecision __instance,
            Clan clan,
            DecisionOutcome possibleOutcome,
            bool ____isProposedByOpponent,
            ref float __result)
        {
            if (!(possibleOutcome is MakePeaceKingdomDecision.MakePeaceDecisionOutcome outcome)
                || !UsesVanillaForcedResult(
                    __instance,
                    clan,
                    ____isProposedByOpponent))
            {
                return true;
            }

            // Reproduce Bannerlord's ordinary council calculation while
            // deliberately bypassing only its two hard-coded 0/200 branches.
            __result = CalculateOrdinarySupport(
                __instance,
                clan,
                outcome);
            return false;
        }

        private static void Postfix(
            MakePeaceKingdomDecision __instance,
            Clan clan,
            DecisionOutcome possibleOutcome,
            ref float __result)
        {
            if (!(possibleOutcome is MakePeaceKingdomDecision.MakePeaceDecisionOutcome outcome))
            {
                return;
            }

            WarDispositionDecisionSupport.Apply(
                clan,
                !outcome.ShouldPeaceBeDeclared,
                "MakePeace",
                outcome.ShouldPeaceBeDeclared ? "MakePeace" : "ContinueWar",
                ref __result);
        }

        private static bool UsesVanillaForcedResult(
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

        private static float CalculateOrdinarySupport(
            MakePeaceKingdomDecision decision,
            Clan clan,
            MakePeaceKingdomDecision.MakePeaceDecisionOutcome outcome)
        {
            TextObject explanation;
            float clanScore = Campaign.Current.Models.DiplomacyModel
                .GetScoreOfDeclaringPeaceForClan(
                    decision.Kingdom,
                    decision.FactionToMakePeaceWith,
                    clan,
                    out explanation,
                    false);
            float kingdomScore = Campaign.Current.Models.DiplomacyModel
                .GetScoreOfDeclaringPeace(
                    decision.Kingdom,
                    decision.FactionToMakePeaceWith);
            float threshold = Campaign.Current.Models.DiplomacyModel
                .GetDecisionMakingThreshold(decision.Kingdom);

            kingdomScore *= kingdomScore > 0f ? 0.95f : 1.05f;

            if (outcome.ShouldPeaceBeDeclared
                && (decision.DailyTributeToBePaid < 0
                    || kingdomScore > threshold)
                && clanScore > kingdomScore)
            {
                return 200f;
            }

            if (!outcome.ShouldPeaceBeDeclared
                && ((decision.DailyTributeToBePaid >= 0
                        && kingdomScore <= threshold)
                    || clanScore <= kingdomScore))
            {
                return 200f;
            }

            return 0f;
        }
    }
}
