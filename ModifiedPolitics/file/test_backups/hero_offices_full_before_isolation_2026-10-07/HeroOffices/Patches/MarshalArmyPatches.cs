using HarmonyLib;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace ModifiedPolitics.HeroOffices.Patches
{
    /// <summary>
    /// Enforces marshal army authority and applies the marshal's influence discounts.
    /// </summary>
    [HarmonyPatch]
    internal static class MarshalArmyPatches
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Kingdom), "CreateArmy")]
        private static bool RequireMarshalForArmy(Kingdom __instance, Hero armyLeader)
        {
            if (__instance == null || armyLeader == null || armyLeader == __instance.Leader)
                return true;

            bool allowed = OfficeEffectService.HasOffice(armyLeader, OfficeType.Marshal);
            if (!allowed && armyLeader == Hero.MainHero)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=MP_MarshalRequired}Only the ruler or the kingdom marshal may create an army.").ToString()));
            }

            return allowed;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CanPlayerCreateArmy")]
        private static void ExplainMarshalRequirement(ref bool __result, ref TextObject disabledReason)
        {
            Kingdom kingdom = Clan.PlayerClan?.Kingdom;
            if (!__result || kingdom == null || Hero.MainHero == kingdom.Leader)
                return;

            if (!OfficeEffectService.HasOffice(Hero.MainHero, OfficeType.Marshal))
            {
                __result = false;
                disabledReason = new TextObject(
                    "{=MP_MarshalRequired}Only the ruler or the kingdom marshal may create an army.");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CalculatePartyInfluenceCost")]
        private static void ApplyCallCostDiscount(MobileParty armyLeaderParty, ref int __result)
        {
            if (__result > 0 && OfficeEffectService.HasOffice(armyLeaderParty?.LeaderHero, OfficeType.Marshal))
                __result = MathF.Ceiling(__result * 0.75f);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "GetCohesionBoostInfluenceCost")]
        private static void ApplyMaintenanceDiscount(Army army, ref int __result)
        {
            if (__result > 0 && OfficeEffectService.HasOffice(army?.LeaderParty?.LeaderHero, OfficeType.Marshal))
                __result = MathF.Ceiling(__result * 0.80f);
        }
    }
}
