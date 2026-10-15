using System;
using HarmonyLib;
using ModifiedArmy.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.HeroOffices.Patches
{
    /// <summary>
    /// Integrates the military office with ModifiedArmy without changing its public data model.
    /// </summary>
    [HarmonyPatch]
    internal static class MilitaryOfficeRecruitmentPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SettlementManpowerProductionBehavior), "GetDailyProduction")]
        private static void IncreaseRegularProduction(
            Settlement settlement,
            TroopProductionKind kind,
            ref float __result)
        {
            if (settlement == null || __result <= 0f ||
                kind != TroopProductionKind.Professional)
                return;

            float factor = OfficeEffectService.GetRegularRecruitmentFactor(settlement);
            if (factor > 0f)
                __result *= 1f + factor;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GarrisonRecruitFromPrisonersBehavior), "GetDailyRecruitmentLimit")]
        private static void IncreaseGarrisonRecruitment(Town town, ref int __result)
        {
            if (__result <= 0 || town?.GarrisonParty == null)
                return;

            float factor = OfficeEffectService.GetGarrisonRecruitmentFactor(town.Settlement);
            if (factor <= 0f)
                return;

            int freeSlots = Math.Max(
                0,
                town.GarrisonParty.Party.PartySizeLimit
                - town.GarrisonParty.Party.NumberOfAllMembers);
            __result = Math.Min(freeSlots, (int)Math.Ceiling(__result * (1f + factor)));
        }
    }
}
