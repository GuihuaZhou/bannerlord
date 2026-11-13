using System;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using System.Globalization;
using ModifiedArmy.Tool;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.Recruitment.Services
{
    /// <summary>
    /// Executes one shared daily plan across professional and prisoner sources.
    /// The behavior owns scheduling/logging; this service owns quota and finance
    /// commits so each approved result is charged exactly once.
    /// </summary>
    internal static class GarrisonRecruitmentExecutionService
    {
        public static int Execute(
            Settlement settlement,
            MobileParty garrison,
            RecruitmentPlan plan,
            int dailyLimit,
            SettlementManpowerProductionBehavior pools,
            out int professionalRecruited,
            out int prisonerRecruited)
        {
            GarrisonRecruitmentExecutionResult result = ExecuteDetailed(
                settlement, garrison, plan, dailyLimit, pools, null);
            professionalRecruited = result.ProfessionalRecruited;
            prisonerRecruited = result.PrisonerRecruited;
            return result.TotalCommitted;
        }

        internal static GarrisonRecruitmentExecutionResult ExecuteDetailed(
            Settlement settlement,
            MobileParty garrison,
            RecruitmentPlan plan,
            int dailyLimit,
            SettlementManpowerProductionBehavior pools,
            string operationId)
        {
            var result = new GarrisonRecruitmentExecutionResult
            {
                OperationId = operationId ??
                    ((settlement?.StringId ?? "unknown") + ":" +
                    DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture))
            };
            int recruited = 0;
            int reservedReturningMembers = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(settlement);
            if (plan?.Evaluations == null)
            {
                result.FailureReason = "NoAuthorizedPlan";
                return result;
            }
            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
            {
                result.AttemptedCandidates++;
                result.ModelApproved += Math.Max(0,
                    evaluation.RecruitableCount);
                int quotaRemaining = Math.Max(0, dailyLimit - recruited);
                int hardFree = GarrisonRecruitmentCapacity.GetHardFreeSlots(
                    garrison, reservedReturningMembers);
                int softFree = GarrisonRecruitmentCapacity
                    .GetSoftRecruitmentFreeSlots(
                        garrison, reservedReturningMembers);
                int count = Math.Min(evaluation.RecruitableCount,
                    quotaRemaining);
                count = Math.Min(count, hardFree);
                count = Math.Min(count, softFree);
                if (count <= 0 || evaluation.Troop == null)
                {
                    result.Reject(evaluation.Troop == null
                        ? "CandidateValidation" : hardFree <= 0
                        ? "HardCapacity" : softFree <= 0
                        ? "SoftCapacity" : quotaRemaining <= 0
                        ? "DailyQuota" : "ModelApproval",
                        evaluation.Troop == null ? "InvalidTroop" :
                        hardFree <= 0 ? "HardCapacityInsufficient" :
                        softFree <= 0 ? "SoftCapacityInsufficient" :
                        quotaRemaining <= 0 ? "DailyQuotaExhausted" :
                        "ModelApprovedZero");
                    continue;
                }

                if (evaluation.Source ==
                    RecruitmentSource.ProfessionalProduction)
                {
                    int requested = count;
                    count = Math.Min(count,
                        pools.GetMaxAffordableProfessionalCount(
                            settlement, evaluation.Troop, count,
                            out ProfessionalAffordabilityDiagnostic affordability));
                    if (count <= 0)
                    {
                        result.Reject("PopulationAffordability",
                            affordability.ReasonCode);
                        if (ModifiedArmy.Recruitment.Diagnostics.RecruitmentLogFilter
                            .ShouldLog(settlement?.OwnerClan))
                            ModLogger.FileWarn(string.Format(
                                "Professional recruitment capacity rejected: " +
                                "settlement={0}, id={1}, troop={2}, tier={3}, " +
                                "path=ProfessionalOffer, dayIndex={0}, " +
                                "settlement={1}, id={2}, troop={3}, tier={4}, " +
                                "sourceOfferCount={5}, modelApproved={6}, " +
                                "dailyQuotaRemaining={7}, hardFree={8}, " +
                                "softFree={9}, reservedEscorts={10}, " +
                                "requestedBeforeBurden={11}, hearthBefore={12:F2}, " +
                                "burdenBefore={13:F2}, effectiveCapacityBefore={14:F2}, " +
                                "burdenCostPerTroop={15}, affordableCount={16}, " +
                                "firstFailedStage=PopulationAffordability, reasonCode={17}.",
                                (int)CampaignTime.Now.ToDays,
                                settlement?.Name, settlement?.StringId,
                                evaluation.Troop.Name, evaluation.Troop.Tier,
                                evaluation.RequestedCount,
                                evaluation.RecruitableCount, quotaRemaining,
                                hardFree, softFree, reservedReturningMembers,
                                requested, affordability.HearthBefore,
                                affordability.BurdenBefore,
                                affordability.EffectiveCapacityBefore,
                                affordability.BurdenCostPerTroop,
                                affordability.AffordableCount,
                                affordability.ReasonCode));
                        continue;
                    }
                }

                ProfessionalRecruitmentCommit professionalCommit = null;
                PrisonerRecruitmentCommit prisonerCommit = null;
                try
                {
                    if (evaluation.Source == RecruitmentSource.ProfessionalProduction)
                    {
                        if (!ProfessionalRecruitmentService.TryCommit(
                            settlement, garrison, evaluation.Troop, count, pools,
                            out professionalCommit, out string commitReason))
                        {
                            result.Reject("SourceCommit", commitReason);
                            continue;
                        }
                    }
                    else if (evaluation.Source == RecruitmentSource.Prisoner)
                    {
                        if (!PrisonerGarrisonRecruitmentService.TryCommit(
                            settlement, garrison, evaluation, count,
                            out int committed, out prisonerCommit)) continue;
                        count = committed;
                    }
                    else continue;
                }
                catch (Exception exception)
                {
                    result.FailureReason = "SourceCommitRecoveryRequired";
                    result.RecoveryRequired = true;
                    TextObject sourceFailure = GameTexts.FindText(
                        "str_modifiedarmy_garrison_order_source_failure");
                    sourceFailure.SetTextVariable("OPERATION_ID", result.OperationId);
                    sourceFailure.SetTextVariable("SETTLEMENT_ID", settlement.StringId ?? "");
                    sourceFailure.SetTextVariable("TROOP_ID", evaluation.Troop.StringId ?? "");
                    sourceFailure.SetTextVariable("COUNT", count);
                    sourceFailure.SetTextVariable("ERROR", exception.ToString());
                    ModLogger.Error(sourceFailure.ToString());
                    break;
                }

                result.OfferConsumed = true;
                result.RosterChanged = true;
                ClanRecruitmentBudgetSnapshot budget =
                    ClanRecruitmentBudgetManager.GetSnapshot(
                        settlement.OwnerClan);
                float purchaseBefore = budget.RemainingPurchaseBudget;
                float commitmentBefore = budget.RemainingCommitmentBudget;
                int expenseBefore = settlement.OwnerClan.AutoRecruitmentExpenses;
                try
                {
                    settlement.OwnerClan.AutoRecruitmentExpenses +=
                        evaluation.UnitRecruitmentCost * count;
                    result.ExpenseCharged = true;
                    ClanRecruitmentBudgetManager.CommitRecruitment(
                        garrison, count, evaluation.UnitRecruitmentCost,
                        evaluation.UnitDailyWage);
                    result.BudgetCommitted = true;
                }
                catch (Exception exception)
                {
                    settlement.OwnerClan.AutoRecruitmentExpenses = expenseBefore;
                    ClanRecruitmentBudgetManager.RestoreRemainingBudget(
                        budget, purchaseBefore, commitmentBefore);
                    Exception rollbackFailure;
                    bool rolledBack = professionalCommit != null
                        ? ProfessionalRecruitmentService.TryRollback(
                            professionalCommit, pools, out rollbackFailure)
                        : PrisonerGarrisonRecruitmentService.TryRollback(
                            prisonerCommit, out rollbackFailure);
                    result.FailureReason = "FinanceCommitFailed";
                    result.RecoveryRequired = !rolledBack;
                    TextObject financeFailure = GameTexts.FindText(
                        "str_modifiedarmy_garrison_order_finance_failure");
                    financeFailure.SetTextVariable("OPERATION_ID", result.OperationId);
                    financeFailure.SetTextVariable("SETTLEMENT_ID", settlement.StringId ?? "");
                    financeFailure.SetTextVariable("TROOP_ID", evaluation.Troop.StringId ?? "");
                    financeFailure.SetTextVariable("COUNT", count);
                    financeFailure.SetTextVariable("ROLLBACK", GameTexts.FindText(
                        rolledBack
                            ? "str_modifiedarmy_common_yes"
                            : "str_modifiedarmy_common_no"));
                    financeFailure.SetTextVariable("ERROR", exception.ToString());
                    financeFailure.SetTextVariable("ROLLBACK_ERROR",
                        rollbackFailure?.ToString() ?? "None");
                    ModLogger.Error(financeFailure.ToString());
                    if (result.RecoveryRequired) break;
                    continue;
                }

                if (evaluation.Source == RecruitmentSource.ProfessionalProduction)
                    result.ProfessionalRecruited += count;
                else
                    result.PrisonerRecruited += count;
                recruited += count;
                if (recruited >= dailyLimit) break;
            }
            return result;
        }
    }
}
