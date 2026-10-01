using HarmonyLib;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.Subjects.Patches
{
    /// <summary>
    /// Applies subject permissions to native war, peace and alliance
    /// decisions. These are query guards; final mutation points remain guarded
    /// separately so other mods cannot bypass the rule.
    /// </summary>
    [HarmonyPatch]
    public static class SubjectDiplomacyDecisionPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(DeclareWarDecision),
            nameof(DeclareWarDecision.IsAllowed))]
        private static void DeclareWarIsAllowedPostfix(
            DeclareWarDecision __instance,
            ref bool __result)
        {
            if (__result)
            {
                __result = SubjectDiplomacyPermissionService.CanDeclareWar(
                    __instance.Kingdom,
                    __instance.FactionToDeclareWarOn);
            }

            if (__result)
            {
                __result = AiWarProposalCooldownService
                    .CanProposeWar(__instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(MakePeaceKingdomDecision),
            nameof(MakePeaceKingdomDecision.IsAllowed))]
        private static void MakePeaceIsAllowedPostfix(
            MakePeaceKingdomDecision __instance,
            ref bool __result)
        {
            if (__result)
            {
                __result = SubjectDiplomacyPermissionService.CanMakePeace(
                    __instance.Kingdom,
                    __instance.FactionToMakePeaceWith);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(StartAllianceDecision),
            nameof(StartAllianceDecision.IsAllowed))]
        private static void StartAllianceIsAllowedPostfix(
            StartAllianceDecision __instance,
            ref bool __result)
        {
            if (__result)
            {
                __result = SubjectAllianceRestrictionService.CanFormAlliance(
                    __instance.Kingdom,
                    __instance.KingdomToStartAllianceWith);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(StartAllianceDecision),
            nameof(StartAllianceDecision.CanMakeDecision))]
        private static void StartAllianceCanMakeDecisionPostfix(
            StartAllianceDecision __instance,
            ref bool __result,
            ref TextObject reason)
        {
            if (SubjectAllianceRestrictionService.CanFormAlliance(
                __instance.Kingdom,
                __instance.KingdomToStartAllianceWith))
            {
                return;
            }

            __result = false;
            reason = new TextObject(
                "{=ModifiedDiplomacy_SubjectAllianceForbidden}" +
                "Subject kingdoms cannot form alliances.");
        }
    }
}
