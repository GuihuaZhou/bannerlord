using System;
using System.Reflection;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Models;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Native base garrison growth creates new basic troops outside the mod's
    /// professional pool. Keep the native growth calculation, but reserve the
    /// settlement population transaction before writing the roster.
    /// </summary>
    [HarmonyPatch(typeof(GarrisonRecruitmentCampaignBehavior),
        "TickGarrisonChangeForTown")]
    internal static class NativeBaseGarrisonPopulationPatch
    {
        private static readonly MethodInfo GetBaseChange = AccessTools.Method(
            typeof(GarrisonRecruitmentCampaignBehavior),
            "GetBaseGarrisonChangeExplainedNumber");
        private static readonly MethodInfo GetBasicTroop = AccessTools.Method(
            typeof(GarrisonRecruitmentCampaignBehavior),
            "GetBasicTroopForTown");

        private static bool Prefix(GarrisonRecruitmentCampaignBehavior __instance,
            Town town)
        {
            Settlement settlement = town?.Settlement;
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (settlement == null) return false;
            if (population == null) return true;
            string operationId = "native-garrison-growth:" +
                Guid.NewGuid().ToString("N");
            try
            {
                ExplainedNumber change = (ExplainedNumber)GetBaseChange.Invoke(
                    __instance, new object[] { town, false });
                int requested = (int)change.ResultNumber;
                if (requested <= 0) return false;
                CharacterObject troop = (CharacterObject)GetBasicTroop.Invoke(
                    null, new object[] { town });
                int cost = MilitaryConfigManager.Instance.RequireActiveConfig()
                    .GetDoctrine(settlement)
                    .GetProfessionalBurdenCost(troop);
                int approved = population.GetMaxAffordableProfessionalCount(
                    settlement, cost, requested);
                if (approved <= 0)
                {
                    if (RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
                        ModLogger.FileDebug(string.Format(
                            "Native garrison growth rejected: settlement={0}, " +
                            "settlementId={1}, troop={2}, tier={3}, " +
                            "requested={4}, approved=0, reason={5}.",
                            settlement.Name, settlement.StringId, troop.Name,
                            troop.Tier, requested,
                            "InsufficientRecruitmentCapacity"));
                    return false;
                }
                PopulationTransactionResult result =
                    population.TryRecruitProfessional(settlement, approved,
                        cost, operationId);
                if (!result.IsCommitted) return false;
                try
                {
                    if (town.GarrisonParty == null)
                        town.Owner.Settlement.AddGarrisonParty();
                    town.GarrisonParty.MemberRoster.AddToCounts(troop,
                        approved, false, 0, 0, true, -1);
                    if (RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
                        ModLogger.FileInfo(string.Format(
                            "Native garrison growth committed: settlement={0}, " +
                            "settlementId={1}, troop={2}, tier={3}, " +
                            "requested={4}, approved={5}, burdenCost={6}, " +
                            "operationId={7}.", settlement.Name,
                            settlement.StringId, troop.Name, troop.Tier,
                            requested, approved, cost, operationId));
                }
                catch
                {
                    population.TryCompensate(settlement, operationId);
                    throw;
                }
            }
            catch (Exception exception)
            {
                ModLogger.FileError(string.Format(
                    "Native garrison growth failed: settlement={0}, " +
                    "settlementId={1}, operationId={2}, error={3}.",
                    settlement.Name, settlement.StringId, operationId,
                    exception.InnerException?.Message ?? exception.Message));
            }
            return false;
        }
    }

    /// <summary>
    /// Disables native notable-slot recruitment. The mod's garrison behavior
    /// now consumes daily professional production through the unified
    /// composition and clan-budget model.
    /// </summary>
    [HarmonyPatch(
        typeof(GarrisonRecruitmentCampaignBehavior),
        "TickAutoRecruitmentGarrisonChange")]
    public static class GarrisonRecruitmentPatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the native notable-slot forecast with professional production and
    /// prisoner forecast used by the actual daily recruitment behavior.
    /// </summary>
    [HarmonyPatch(
        typeof(GarrisonRecruitmentCampaignBehavior),
        "GetGarrisonChangeExplainedNumber")]
    public static class GarrisonRecruitmentPredictionPatch
    {
        private static void Postfix(Town town, ref ExplainedNumber __result)
        {
            if (town?.Settlement == null)
            {
                return;
            }

            ExplainedNumber replacement = Campaign.Current.Models
                .SettlementGarrisonModel.CalculateBaseGarrisonChange(
                    town.Settlement,
                    true);
            int recruitment = GarrisonRecruitFromPrisonersBehavior
                .GetPredictedDailyRecruitmentCount(town);
            if (recruitment > 0)
            {
                replacement.Add(
                    recruitment,
                    new TextObject("{=Uzsnek6O}Auto Recruitment"));
            }

            __result = replacement;
        }
    }
}
