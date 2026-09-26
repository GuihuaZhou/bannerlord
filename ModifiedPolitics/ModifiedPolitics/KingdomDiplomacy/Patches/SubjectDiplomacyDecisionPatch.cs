using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Patches
{
    /// <summary>
    /// Prevents puppets from creating native war and peace decisions. Vassals
    /// retain these decisions except for a normal declaration on the overlord.
    /// Final action-level safeguards will be added with the rebellion system.
    /// </summary>
    [HarmonyPatch]
    public static class SubjectDiplomacyDecisionPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DeclareWarDecision), nameof(DeclareWarDecision.IsAllowed))]
        private static void DeclareWarIsAllowedPostfix(
            DeclareWarDecision __instance,
            ref bool __result)
        {
            if (__result)
            {
                __result = SubjectDiplomacyPermissionService
                    .CanStartNormalDiplomacy(
                        __instance.Kingdom,
                        __instance.FactionToDeclareWarOn);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MakePeaceKingdomDecision), nameof(MakePeaceKingdomDecision.IsAllowed))]
        private static void MakePeaceIsAllowedPostfix(
            MakePeaceKingdomDecision __instance,
            ref bool __result)
        {
            if (__result)
            {
                __result = SubjectDiplomacyPermissionService
                    .CanStartNormalDiplomacy(
                        __instance.Kingdom,
                        __instance.FactionToMakePeaceWith);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartAllianceDecision), nameof(StartAllianceDecision.IsAllowed))]
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
        [HarmonyPatch(typeof(StartAllianceDecision), nameof(StartAllianceDecision.CanMakeDecision))]
        private static void StartAllianceCanMakeDecisionPostfix(
            StartAllianceDecision __instance,
            ref bool __result,
            ref TextObject reason)
        {
            if (!SubjectAllianceRestrictionService.CanFormAlliance(
                    __instance.Kingdom,
                    __instance.KingdomToStartAllianceWith))
            {
                __result = false;
                SubjectType blockingType = SubjectAllianceRestrictionService
                    .GetBlockingSubjectType(
                        __instance.Kingdom,
                        __instance.KingdomToStartAllianceWith);
                reason = blockingType == SubjectType.Puppet
                    ? new TextObject(
                        "{=ModifiedPolitics_PuppetAllianceForbidden}Puppet kingdoms cannot form alliances.")
                    : new TextObject(
                        "{=ModifiedPolitics_VassalAllianceForbidden}Vassal kingdoms cannot form alliances.");
            }
        }
    }
}
