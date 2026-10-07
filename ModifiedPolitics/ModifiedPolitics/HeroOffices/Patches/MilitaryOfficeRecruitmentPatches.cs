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
        [ThreadStatic]
        private static Settlement _activeSettlement;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SettlementRecruitmentPoolBehavior), "OnDailySettlementTick")]
        private static void BeginPoolTick(Settlement settlement)
        {
            _activeSettlement = settlement;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(SettlementRecruitmentPoolBehavior), "OnDailySettlementTick")]
        private static void EndPoolTick()
        {
            _activeSettlement = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SettlementRecruitmentPoolData), "AddProductionProgress")]
        private static void IncreaseRegularProduction(RecruitmentPoolKind kind, ref float amount)
        {
            if (_activeSettlement == null || amount <= 0f)
                return;

            float factor = OfficeEffectService.GetRegularRecruitmentFactor(_activeSettlement);
            if (factor > 0f)
                amount *= 1f + factor;
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
