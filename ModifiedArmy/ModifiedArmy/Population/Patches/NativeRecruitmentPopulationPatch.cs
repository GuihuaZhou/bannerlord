using System;
using System.Reflection;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Population.Patches
{
    /// <summary>
    /// Enforces population availability before native notable recruitment.
    /// The original method remains authoritative for gold, volunteer slots,
    /// roster mutation and event dispatch; population commits only after it
    /// returns successfully.
    /// </summary>
    [HarmonyPatch]
    internal static class NativeRecruitmentPopulationPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.DeclaredMethod(
                typeof(RecruitmentCampaignBehavior),
                "ApplyInternal");
        }

        private static bool Prefix(
            Settlement settlement,
            Hero individual,
            CharacterObject troop,
            int number,
            ref int __state)
        {
            __state = 0;
            if (individual == null || settlement == null || troop == null ||
                number <= 0 || troop.Occupation != Occupation.Soldier)
            {
                return true;
            }

            try
            {
                int unitCost = MilitaryConfigManager.Instance
                    .RequireActiveConfig().HearthEconomy
                    .GetProfessionalRecruitmentCost(troop);
                __state = checked(number * unitCost);
            }
            catch (Exception exception)
            {
                ModLogger.FileError(string.Format(
                    "Native recruitment population precheck failed: " +
                    "settlement={0}, troop={1}, count={2}, reason={3}.",
                    settlement.Name,
                    troop.Name,
                    number,
                    exception.Message));
                return false;
            }

            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (population == null)
            {
                return false;
            }

            return population.EvaluateConsumption(settlement, __state)
                ?.IsCommitted == true;
        }

        private static void Postfix(
            Settlement settlement,
            Hero individual,
            int __state)
        {
            if (individual == null || settlement == null || __state <= 0)
                return;
            string operationId = "native-recruitment:" +
                Guid.NewGuid().ToString("N");
            PopulationTransactionResult result =
                SettlementPopulationServiceRegistry.Current?.TryConsume(
                    settlement,
                    __state,
                    operationId);
            if (result?.IsCommitted != true)
            {
                ModLogger.FileError(string.Format(
                    "Native recruitment population commit mismatch: " +
                    "settlement={0}, id={1}, operation={2}, amount={3}, " +
                    "reason={4}.",
                    settlement.Name,
                    settlement.StringId,
                    operationId,
                    __state,
                    result?.ReasonCode ?? "PopulationServiceUnavailable"));
            }
        }
    }
}
