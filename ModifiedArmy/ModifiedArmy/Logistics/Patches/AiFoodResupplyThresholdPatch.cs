using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;

namespace ModifiedArmy.Logistics.Patches
{
    /// <summary>
    /// Changes only the food-resupply scoring threshold in AiVisitSettlement.
    /// Siege, raid and army-creation thresholds continue to use the native
    /// MobilePartyAIModel values.
    /// </summary>
    [HarmonyPatch(typeof(AiVisitSettlementBehavior), "AiHourlyTick")]
    internal static class AiFoodResupplyThresholdPatch
    {
        internal const float FoodResupplyTriggerDays = 20f;

        internal static int ReplacementCount { get; private set; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ReplaceFoodThreshold(
            IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo nativeGetter = AccessTools.PropertyGetter(
                typeof(MobilePartyAIModel),
                nameof(MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege));
            MethodInfo replacement = AccessTools.Method(
                typeof(AiFoodResupplyThresholdPatch),
                nameof(GetFoodResupplyTriggerDays));
            int replacements = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (nativeGetter != null && instruction.Calls(nativeGetter))
                {
                    // Preserve the original stack contract: the native model
                    // instance is already on the stack and is consumed here.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replacements++;
                }

                yield return instruction;
            }

            ReplacementCount = replacements;
            if (replacements != 1)
            {
                ModLogger.FileError(string.Format(
                    "[AI Food Supply] phase=PATCH_VALIDATION result=FAILED " +
                    "reasonCode=UnexpectedReplacementCount expected=1 " +
                    "actual={0} target=AiVisitSettlementBehavior.AiHourlyTick.",
                    replacements));
            }
        }

        private static float GetFoodResupplyTriggerDays(
            MobilePartyAIModel nativeModel)
        {
            return FoodResupplyTriggerDays;
        }
    }
}
