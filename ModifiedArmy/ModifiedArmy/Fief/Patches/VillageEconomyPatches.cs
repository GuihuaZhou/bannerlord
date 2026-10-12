using HarmonyLib;
using ModifiedArmy.Models.Fief;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Fief.Patches
{
    /// <summary>
    /// Applies the shared rural-capacity formulas without replacing unrelated
    /// vanilla perks, policies, issues, or building effects.
    /// </summary>
    [HarmonyPatch(typeof(DefaultSettlementProsperityModel), nameof(DefaultSettlementProsperityModel.CalculateHearthChange))]
    internal static class VillageHearthGrowthPatch
    {
        private static void Postfix(Village village, ref ExplainedNumber __result)
        {
            if (village == null || village.VillageState != Village.VillageStates.Normal)
                return;

            int mobilized = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetMobilizedHearth(village) ?? 0;
            float effectiveHearth = Math.Max(0f, village.Hearth + mobilized);
            float replacementBase = 4f /
                (1f + MathF.Pow(effectiveHearth / 225f, 3f));
            float vanillaBase = village.Hearth < 300f
                ? 4f
                : village.Hearth < 600f ? 1.2f : 0.2f;
            __result.Add(replacementBase - vanillaBase);
        }
    }

    [HarmonyPatch(typeof(DefaultVillageProductionCalculatorModel), nameof(DefaultVillageProductionCalculatorModel.CalculateDailyProductionAmount))]
    internal static class VillageProductionPatch
    {
        private static void Postfix(Village village, ref ExplainedNumber __result)
        {
            if (village == null || village.VillageState != Village.VillageStates.Normal)
                return;

            float hearth = Math.Max(0f, village.Hearth);
            float desiredMultiplier = 2f * hearth / (hearth + 300f);
            float vanillaMultiplier = (village.GetHearthLevel() + 1) * 0.5f;
            if (vanillaMultiplier > 0f)
                __result.AddFactor(desiredMultiplier / vanillaMultiplier - 1f);
        }
    }

    [HarmonyPatch(typeof(DefaultVillageProductionCalculatorModel), nameof(DefaultVillageProductionCalculatorModel.CalculateDailyFoodProductionAmount))]
    internal static class VillageFoodProductionPatch
    {
        private static void Postfix(Village village, ref float __result)
        {
            if (village == null || village.VillageState != Village.VillageStates.Normal)
                return;

            float hearth = Math.Max(0f, village.Hearth);
            float desiredMultiplier = 2f * hearth / (hearth + 300f);
            float vanillaMultiplier = village.GetHearthLevel() + 1f;
            if (vanillaMultiplier > 0f)
                __result *= desiredMultiplier / vanillaMultiplier;
        }
    }
}
