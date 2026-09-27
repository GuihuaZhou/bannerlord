using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Patches
{
    /// <summary>
    /// Applies subject trade restrictions both during native eligibility
    /// evaluation and at the final mutation entry point.
    /// </summary>
    [HarmonyPatch]
    public static class SubjectTradeRestrictionPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(DefaultTradeAgreementModel),
            nameof(DefaultTradeAgreementModel.CanMakeTradeAgreement))]
        private static void CanMakeTradeAgreementPostfix(
            Kingdom querierKingdom,
            Kingdom queriedKingdom,
            ref bool __result,
            ref TextObject reason)
        {
            if (!__result)
            {
                return;
            }

            TextObject restrictionReason;
            if (!SubjectTradeRestrictionService.CanFormTradeAgreement(
                querierKingdom,
                queriedKingdom,
                out restrictionReason))
            {
                __result = false;
                reason = restrictionReason;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(TradeAgreementsCampaignBehavior),
            nameof(TradeAgreementsCampaignBehavior.MakeTradeAgreement))]
        private static bool MakeTradeAgreementPrefix(
            Kingdom kingdom1,
            Kingdom kingdom2)
        {
            return SubjectTradeRestrictionService.CanFormTradeAgreement(
                kingdom1,
                kingdom2,
                out _);
        }
    }
}
