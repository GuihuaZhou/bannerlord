using ModifiedArmy.ForceManagement.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModifiedArmy.ForceManagement.Models
{
    /// <summary>
    /// Pure daily planner that selects an intent and a stable destination.
    /// It never changes movement orders, rosters, money, or fief manpower.
    /// </summary>
    public sealed class AiForcePlanningModel : IAiForcePlanningModel
    {
        /// <summary>Builds one daily strategic decision from captured inputs.</summary>
        public PartyForcePlan BuildDailyPlan(PartyForcePlanningContext context)
        {
            if (context?.Party == null || !context.Party.IsActive)
                return PartyForcePlan.NoPlan("InvalidParty");
            if (context.CurrentTask?.Intent == PartyForceIntent.WarRest &&
                !context.IsAtFormalWar)
                return BuildMaintainPlan(context, "WarRestCancelledByPeace");
            if (context.IsProtectedByHigherPriorityOrder)
                return PreserveOrSuspend(context, "HigherPriorityOrder");
            if (context.IsAtFormalWar && context.WarRest?.IsApproved == true)
                return BuildWarRestPlan(context);

            PartyForceTargetResult target = context.ForceTarget;
            if (target == null || target.GapCount <= 0 ||
                !target.CanStartRecruitmentVisit)
                return BuildMaintainPlan(context, "ForceTargetSatisfied");
            if (context.RecruitmentNeedScore <
                context.RecruitmentNeedThreshold)
                return BuildMaintainPlan(context, "RecruitmentNeedBelowThreshold");
            if (!context.IsRandomGateOpen)
                return PreserveOrSuspend(context, "RecruitmentRandomGateClosed");

            List<PartyForceCandidate> ranked = (context.Candidates ??
                    Array.Empty<PartyForceCandidate>())
                .Where(candidate => candidate != null && candidate.IsSafe &&
                    candidate.AvailableCount > 0 && candidate.GapCovered > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Settlement?.StringId,
                    StringComparer.Ordinal)
                .ThenBy(candidate => candidate.SourceKind)
                .ToList();
            if (ranked.Count == 0)
                return BuildMaintainPlan(context, "NoFeasibleRecruitmentSource");

            PartyForceCandidate selected = ranked[0];
            PartyForceTask current = context.CurrentTask;
            if (IsSameLiveTask(current, selected, context.Today))
            {
                return new PartyForcePlan
                {
                    Task = current,
                    RankedCandidates = ranked,
                    ReasonCode = "ContinueExistingTask"
                };
            }

            return new PartyForcePlan
            {
                Task = CreateTask(
                    context,
                    PartyForceIntent.Recruit,
                    selected.SourceKind,
                    selected.Settlement,
                    "BestFeasibleRecruitmentCandidate"),
                RankedCandidates = ranked,
                ShouldReplaceCurrentTask = current != null,
                ReasonCode = "RecruitmentTaskPlanned"
            };
        }

        /// <summary>Creates the phase-four compatible war-rest task shell.</summary>
        private static PartyForcePlan BuildWarRestPlan(
            PartyForcePlanningContext context)
        {
            PartyForceCandidate candidate = (context.WarRest.DestinationOptions ??
                    Array.Empty<PartyForceCandidate>())
                .Where(item => item?.IsSafe == true && item.Settlement != null)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Settlement.StringId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (candidate == null)
                return PreserveOrSuspend(context, "NoWarRestDestination");
            PartyForceTask task = CreateTask(
                context,
                PartyForceIntent.WarRest,
                candidate.SourceKind,
                candidate.Settlement,
                context.WarRest.ReasonCode);
            task.SourceBatchId = context.WarRest.CandidateFiefBatches
                .FirstOrDefault(item => item.SourceFief == candidate.Settlement)?
                .BatchId;
            return new PartyForcePlan
            {
                Task = task,
                RankedCandidates = new[] { candidate },
                ShouldReplaceCurrentTask = context.CurrentTask != null,
                ReasonCode = "WarRestTaskPlanned"
            };
        }

        /// <summary>Returns control to ordinary AI when no mod task is needed.</summary>
        private static PartyForcePlan BuildMaintainPlan(
            PartyForcePlanningContext context,
            string reason) => new PartyForcePlan
        {
            Task = CreateTask(
                context,
                PartyForceIntent.Maintain,
                PartyForceSourceKind.None,
                null,
                reason),
            ShouldReplaceCurrentTask = context.CurrentTask != null,
            ReasonCode = reason
        };

        /// <summary>Preserves a valid owned task without taking over other AI orders.</summary>
        private static PartyForcePlan PreserveOrSuspend(
            PartyForcePlanningContext context,
            string reason)
        {
            if (context.CurrentTask != null &&
                context.CurrentTask.Status != PartyForceTaskStatus.Cancelled &&
                context.CurrentTask.Status != PartyForceTaskStatus.Completed)
            {
                return new PartyForcePlan
                {
                    Task = context.CurrentTask,
                    ReasonCode = reason
                };
            }
            return PartyForcePlan.NoPlan(reason);
        }

        /// <summary>Creates a persisted task with stable owner identity.</summary>
        private static PartyForceTask CreateTask(
            PartyForcePlanningContext context,
            PartyForceIntent intent,
            PartyForceSourceKind source,
            TaleWorlds.CampaignSystem.Settlements.Settlement settlement,
            string reason) => new PartyForceTask
        {
            Party = context.Party,
            PartyId = context.Party.StringId,
            ClanId = context.Party.ActualClan?.StringId,
            OwnerKey = "ModifiedArmy.AiForcePlanning",
            Version = (context.CurrentTask?.Version ?? 0) + 1,
            CreatedDay = context.Today,
            LastValidatedDay = context.Today,
            Intent = intent,
            SourceKind = source,
            TargetSettlement = settlement,
            Status = settlement == null
                ? PartyForceTaskStatus.Completed
                : PartyForceTaskStatus.Planned,
            ReasonCode = reason
        };

        /// <summary>Prevents same-day destination churn for a still-valid task.</summary>
        private static bool IsSameLiveTask(
            PartyForceTask task,
            PartyForceCandidate selected,
            int today) => task != null && task.Intent == PartyForceIntent.Recruit &&
            task.TargetSettlement == selected.Settlement &&
            task.SourceKind == selected.SourceKind &&
            task.Status != PartyForceTaskStatus.Cancelled &&
            task.Status != PartyForceTaskStatus.Completed &&
            task.CreatedDay <= today;
    }
}
