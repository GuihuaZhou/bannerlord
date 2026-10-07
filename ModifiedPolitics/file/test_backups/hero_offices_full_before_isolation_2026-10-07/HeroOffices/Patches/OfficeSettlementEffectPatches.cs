using HarmonyLib;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.Patches
{
    /// <summary>
    /// Adds office effects as separate explained entries so direct percentage bonuses remain additive.
    /// </summary>
    [HarmonyPatch]
    internal static class OfficeSettlementEffectPatches
    {
        private static readonly TextObject TaxOfficeText =
            new TextObject("{=MP_TaxOfficerEffect}Tax officer");
        private static readonly TextObject AgricultureOfficeText =
            new TextObject("{=MP_AgricultureOfficerEffect}Agriculture officer");
        private static readonly TextObject SecurityOfficeText =
            new TextObject("{=MP_SecurityOfficerEffect}Security officer");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultSettlementTaxModel), "CalculateTownTax")]
        private static void AddTaxOfficeEffect(Town town, ref ExplainedNumber __result)
        {
            float factor = OfficeEffectService.GetTaxFactor(town);
            if (factor > 0f)
                __result.AddFactor(factor, TaxOfficeText);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultSettlementFoodModel), "CalculateTownFoodStocksChange")]
        private static void AddAgricultureOfficeEffect(Town town, ref ExplainedNumber __result)
        {
            float factor = OfficeEffectService.GetFoodProductionFactor(town);
            if (factor <= 0f)
                return;

            // Only increase positive net production; an office must not amplify starvation losses.
            if (__result.ResultNumber > 0f)
                __result.AddFactor(factor, AgricultureOfficeText);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultSettlementLoyaltyModel), "CalculateLoyaltyChange")]
        private static void AddSecurityOfficeEffect(Town town, ref ExplainedNumber __result)
        {
            float value = OfficeEffectService.GetDailyLoyalty(town);
            if (value > 0f)
                __result.Add(value, SecurityOfficeText);
        }
    }
}
