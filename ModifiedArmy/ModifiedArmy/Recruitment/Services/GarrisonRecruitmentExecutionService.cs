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

        private static GarrisonRecruitmentExecutionResult ExecuteDetailed(
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
                int count = Math.Min(evaluation.RecruitableCount,
                    dailyLimit - recruited);
                count = Math.Min(count,
                    GarrisonRecruitmentCapacity.GetHardFreeSlots(
                        garrison, reservedReturningMembers));
                count = Math.Min(count,
                    GarrisonRecruitmentCapacity.GetSoftRecruitmentFreeSlots(
                        garrison, reservedReturningMembers));
                if (count <= 0 || evaluation.Troop == null) continue;

                ProfessionalRecruitmentCommit professionalCommit = null;
                PrisonerRecruitmentCommit prisonerCommit = null;
                try
                {
                    if (evaluation.Source == RecruitmentSource.ProfessionalProduction)
                    {
                        if (!ProfessionalRecruitmentService.TryCommit(
                            settlement, garrison, evaluation.Troop, count, pools,
                            out professionalCommit))
                            continue;
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
