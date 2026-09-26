using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem.Election;

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
    }
}
