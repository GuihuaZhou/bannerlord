using System;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
            int recruited = 0;
            int reservedReturningMembers = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(settlement);
            professionalRecruited = 0;
            prisonerRecruited = 0;
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

                if (evaluation.Source == RecruitmentSource.ProfessionalProduction)
                {
                    if (!ProfessionalRecruitmentService.TryCommit(
                        settlement, garrison, evaluation.Troop, count, pools))
                        continue;
                    professionalRecruited += count;
                }
                else if (evaluation.Source == RecruitmentSource.Prisoner)
                {
                    if (!PrisonerGarrisonRecruitmentService.TryCommit(
                        settlement, garrison, evaluation, count,
                        out int committed)) continue;
                    count = committed;
                    prisonerRecruited += committed;
                }
                else continue;

                settlement.OwnerClan.AutoRecruitmentExpenses +=
                    evaluation.UnitRecruitmentCost * count;
                ClanRecruitmentBudgetManager.CommitRecruitment(
                    garrison, count, evaluation.UnitRecruitmentCost,
                    evaluation.UnitDailyWage);
                recruited += count;
                if (recruited >= dailyLimit) break;
            }
            return recruited;
        }
    }
}
