using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Diagnostics;
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

        internal static int LeaderGateReplacementCount { get; private set; }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Kingdom), "CreateArmy")]
        private static bool RequireMarshalForArmy(
            Kingdom __instance,
            Hero armyLeader,
            Settlement targetSettlement,
            Army.ArmyTypes selectedArmyType)
        {
            bool relevant = Clan.PlayerClan?.Kingdom == __instance
                            && (armyLeader == __instance?.Leader || IsAppointedMarshal(armyLeader, __instance));
            if (__instance == null || armyLeader == null || armyLeader == __instance.Leader)
            {
                if (relevant)
                    TraceCreateArmy(__instance, armyLeader, targetSettlement, selectedArmyType, true);
                return true;
            }

            bool hasMarshal = HasAnyMarshal(__instance);
            if (!hasMarshal)
            {
                if (relevant)
                    TraceCreateArmy(__instance, armyLeader, targetSettlement, selectedArmyType, true);
                return true;
            }

            bool allowed = IsArmyAuthority(armyLeader, __instance);
            if (!allowed && armyLeader == Hero.MainHero)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=MP_MarshalRequired}Only the ruler or the kingdom marshal may create an army.").ToString()));
            }

            if (relevant || Clan.PlayerClan?.Kingdom == __instance)
                TraceCreateArmy(__instance, armyLeader, targetSettlement, selectedArmyType, allowed);

            return allowed;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CanPlayerCreateArmy")]
        private static void ExplainMarshalRequirement(ref bool __result, ref TextObject disabledReason)
        {
            Kingdom kingdom = Clan.PlayerClan?.Kingdom;
            if (!__result || kingdom == null || Hero.MainHero == kingdom.Leader)
                return;

            if (HasAnyMarshal(kingdom) && !IsArmyAuthority(Hero.MainHero, kingdom))
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
            if (!HasAnyMarshal(kingdom))
                return true;

            Hero leader = party?.LeaderHero;
            return IsArmyAuthority(leader, kingdom);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "CalculatePartyInfluenceCost")]
        private static void ApplyCallCostDiscount(MobileParty armyLeaderParty, ref int __result)
        {
            if (__result > 0 && IsArmyAuthority(
                    armyLeaderParty?.LeaderHero,
                    armyLeaderParty?.MapFaction as Kingdom))
                __result = MathF.Ceiling(__result * 0.75f);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(DefaultArmyManagementCalculationModel), "GetCohesionBoostInfluenceCost")]
        private static void ApplyMaintenanceDiscount(Army army, ref int __result)
        {
            if (__result > 0 && IsArmyAuthority(
                    army?.LeaderParty?.LeaderHero,
                    army?.LeaderParty?.MapFaction as Kingdom))
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
        private static void EndArmyEligibilityCheck(bool __result, MBList<MobileParty> __1)
        {
            MobileParty party = _armyEligibilityParty;
            if (MarshalArmyDebugTrace.IsRelevant(party))
            {
                MarshalArmyDebugTrace.Write(
                    "phase=ELIGIBILITY_NATIVE party=" + MarshalArmyDebugTrace.DescribeParty(party)
                    + " result=" + __result
                    + " candidates=" + (__1?.Count ?? 0)
                    + " reason=" + (__result
                        ? (__1?.Count > 0 ? "ELIGIBILITY_PASS" : "ELIGIBILITY_PASS_NO_MEMBERS")
                        : "ELIGIBILITY_FAIL_NATIVE_UNKNOWN")
                    + " model=" + (Campaign.Current?.Models?.ArmyManagementCalculationModel?.GetType().FullName ?? "none")
                    + " leaderGateReplacements=" + LeaderGateReplacementCount);
            }
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

            int replacementCount = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                // Only Clan.Leader reads inside native eligibility are replaced. Influence,
                // food, strength, distance and callable-party checks remain native.
                if (instruction.Calls(clanLeaderGetter))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = effectiveLeaderGetter;
                    replacementCount++;
                }

                yield return instruction;
            }

            LeaderGateReplacementCount = replacementCount;
        }

        private static Hero GetEffectiveClanLeader(Clan clan)
        {
            MobileParty party = _armyEligibilityParty;
            Hero leader = party?.LeaderHero;
            Kingdom kingdom = party?.MapFaction as Kingdom;
            bool isMarshal = IsAppointedMarshal(leader, kingdom);

            if (clan != null && leader != null && leader.Clan == clan && isMarshal)
                return leader;

            return clan?.Leader;
        }

        private static bool HasAnyMarshal(Kingdom kingdom)
        {
            return kingdom != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(assignment => assignment?.Hero != null) == true;
        }

        private static bool IsAppointedMarshal(Hero hero, Kingdom kingdom)
        {
            return hero != null && kingdom != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(assignment => assignment?.Hero == hero) == true;
        }

        private static bool IsArmyAuthority(Hero hero, Kingdom kingdom)
        {
            return hero != null && kingdom != null
                   && (hero == kingdom.Leader || IsAppointedMarshal(hero, kingdom));
        }

        private static void TraceCreateArmy(
            Kingdom kingdom,
            Hero armyLeader,
            Settlement targetSettlement,
            Army.ArmyTypes selectedArmyType,
            bool allowed)
        {
            bool hasMarshal = HasAnyMarshal(kingdom);
            MarshalArmyDebugTrace.Write(
                "phase=CREATE_ARMY_ATTEMPT kingdom=" + MarshalArmyDebugTrace.DescribeKingdom(kingdom)
                + " leader=" + MarshalArmyDebugTrace.DescribeHero(armyLeader)
                + " party=" + MarshalArmyDebugTrace.DescribeParty(armyLeader?.PartyBelongedTo)
                + " armyType=" + selectedArmyType
                + " target=" + MarshalArmyDebugTrace.DescribeSettlement(targetSettlement)
                + " hasMarshal=" + hasMarshal
                + " isRuler=" + (armyLeader == kingdom?.Leader)
                + " isAppointedMarshal=" + IsAppointedMarshal(armyLeader, kingdom)
                + " allowedByMarshalPatch=" + allowed
                + " result=" + (allowed
                    ? "CREATE_ARMY_ALLOWED_BY_OFFICE_PATCH"
                    : "CREATE_ARMY_REJECTED_BY_OFFICE_PATCH"));
        }
    }
}
