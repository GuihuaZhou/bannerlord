using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
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
        [System.ThreadStatic]
        private static MobileParty _armyEligibilityParty;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Kingdom), "CreateArmy")]
        private static bool RequireMarshalForArmy(Kingdom __instance, Hero armyLeader)
        {
            if (__instance == null || armyLeader == null || armyLeader == __instance.Leader)
                return true;

            Hero marshal = GetMarshal(__instance);
            if (marshal == null)
                return true;

            bool allowed = armyLeader == marshal;
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

            Hero marshal = GetMarshal(kingdom);
            if (marshal != null && Hero.MainHero != marshal)
            {
                __result = false;
                disabledReason = new TextObject(
                    "{=MP_MarshalRequired}Only the ruler or the kingdom marshal may create an army.");
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(AiMilitaryBehavior), nameof(AiMilitaryBehavior.FindBestTargetAndItsValueForFaction))]
        private static bool RestrictVanillaArmyGathering(PartyThinkParams p)
        {
            if (p == null || !p.WillGatherAnArmy)
                return true;

            MobileParty party = p.MobilePartyOf;
            Kingdom kingdom = party?.MapFaction as Kingdom;
            Hero marshal = GetMarshal(kingdom);
            if (marshal == null)
                return true;

            Hero leader = party?.LeaderHero;
            return leader != null && (leader == kingdom.Leader || leader == marshal);
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

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), nameof(DefaultArmyManagementCalculationModel.CanLordCreateArmy))]
        private static void BeginArmyEligibilityCheck(MobileParty __0)
        {
            _armyEligibilityParty = __0;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), nameof(DefaultArmyManagementCalculationModel.CanLordCreateArmy))]
        private static void EndArmyEligibilityCheck()
        {
            _armyEligibilityParty = null;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), nameof(DefaultArmyManagementCalculationModel.CanLordCreateArmy))]
        private static System.Exception ClearArmyEligibilityCheck(System.Exception __exception)
        {
            _armyEligibilityParty = null;
            return __exception;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), nameof(DefaultArmyManagementCalculationModel.CanLordCreateArmy))]
        private static IEnumerable<CodeInstruction> AllowMarshalToPassLeaderGate(
            IEnumerable<CodeInstruction> instructions)
        {
            var clanLeaderGetter = AccessTools.PropertyGetter(typeof(Clan), nameof(Clan.Leader));
            var effectiveLeaderGetter = AccessTools.Method(
                typeof(MarshalArmyPatches),
                nameof(GetEffectiveClanLeader));

            foreach (CodeInstruction instruction in instructions)
            {
                // Only Clan.Leader reads inside native eligibility are replaced. Influence,
                // food, strength, distance and callable-party checks remain native.
                if (instruction.Calls(clanLeaderGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = effectiveLeaderGetter;
                }

                yield return instruction;
            }
        }

        private static Hero GetEffectiveClanLeader(Clan clan)
        {
            MobileParty party = _armyEligibilityParty;
            Hero leader = party?.LeaderHero;
            Kingdom kingdom = party?.MapFaction as Kingdom;
            Hero marshal = GetMarshal(kingdom);

            if (clan != null && leader != null && leader.Clan == clan && leader == marshal)
                return leader;

            return clan?.Leader;
        }

        private static Hero GetMarshal(Kingdom kingdom)
        {
            return kingdom == null
                ? null
                : HeroOfficeBehavior.Current?
                    .GetAssignments(kingdom, OfficeType.Marshal)
                    .Select(assignment => assignment?.Hero)
                    .FirstOrDefault(hero => hero != null);
        }
    }
}
