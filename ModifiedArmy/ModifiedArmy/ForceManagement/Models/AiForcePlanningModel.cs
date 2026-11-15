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
            if (context.CurrentTask?.Intent ==
                    PartyForceIntent.PeaceDemobilize &&
                context.IsAtFormalWar)
                return BuildMaintainPlan(
                    context, "PeaceDemobilizationCancelledByWar");
            if (context.IsProtectedByHigherPriorityOrder)
                return PreserveOrSuspend(context, "HigherPriorityOrder");
            if (context.IsAtFormalWar && context.WarRest?.IsApproved == true)
                return BuildWarRestPlan(context);
            if (!context.IsAtFormalWar &&
                context.PeaceDemobilization?.CanDemobilize == true &&
                context.PeaceDestinations?.Any(item =>
                    item?.IsSafe == true && item.AvailableCount > 0) == true)
                return BuildPeaceDemobilizationPlan(context);

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
            if (IsSameLiveTask(current, PartyForceIntent.Recruit, selected,
                    null, context.Today))
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
            var allowed = (context.WarRest.CandidateFiefBatches ??
                    Array.Empty<FiefBatchTransferApproval>())
                .Where(item => item != null && item.ApprovedCount > 0 &&
                    item.SourceFief != null &&
                    !string.IsNullOrEmpty(item.BatchId))
                .Select(item => new
                {
                    Approval = item,
                    Destination = (context.WarRest.DestinationOptions ??
                        Array.Empty<PartyForceCandidate>()).FirstOrDefault(
                        candidate => candidate?.IsSafe == true &&
                            candidate.Settlement == item.SourceFief &&
                            candidate.SourceBatchId == item.BatchId)
                })
                .Where(item => item.Destination != null)
                .ToList();
            if (allowed.Count == 0)
                return PreserveOrSuspend(context, "NoWarRestDestination");

            var selected = allowed.FirstOrDefault(item =>
                IsSameLiveTask(context.CurrentTask, PartyForceIntent.WarRest,
                    item.Destination, item.Approval.BatchId, context.Today)) ??
                allowed.OrderByDescending(item => item.Destination.Score)
                    .ThenByDescending(item => item.Approval.ApprovedCount)
                    .ThenBy(item => item.Destination.Distance)
                    .ThenBy(item => item.Approval.BatchId,
                        StringComparer.Ordinal).First();
            PartyForceCandidate candidate = selected.Destination;
            if (IsSameLiveTask(context.CurrentTask, PartyForceIntent.WarRest,
                    candidate, selected.Approval.BatchId, context.Today))
            {
                return new PartyForcePlan
                {
                    Task = context.CurrentTask,
                    RankedCandidates = new[] { candidate },
                    ReasonCode = "ContinueExistingTask"
                };
            }
            PartyForceTask task = CreateTask(
                context,
                PartyForceIntent.WarRest,
                candidate.SourceKind,
                candidate.Settlement,
                context.WarRest.ReasonCode);
            task.SourceBatchId = selected.Approval.BatchId;
            return new PartyForcePlan
            {
                Task = task,
                RankedCandidates = new[] { candidate },
                ShouldReplaceCurrentTask = context.CurrentTask != null,
                ReasonCode = "WarRestTaskPlanned"
            };
        }

        private static PartyForcePlan BuildPeaceDemobilizationPlan(
            PartyForcePlanningContext context)
        {
            List<PartyForceCandidate> ranked = context.PeaceDestinations
                .Where(item => item?.IsSafe == true &&
                    item.AvailableCount > 0 && item.Settlement != null)
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.AvailableCount)
                .ThenBy(item => item.Distance)
                .ThenBy(item => item.Settlement.StringId,
                    StringComparer.Ordinal)
                .ThenBy(item => item.SourceBatchId, StringComparer.Ordinal)
                .ToList();
            PartyForceCandidate selected = ranked.FirstOrDefault(item =>
                IsSameLiveTask(context.CurrentTask,
                    PartyForceIntent.PeaceDemobilize, item,
                    item.SourceBatchId, context.Today)) ?? ranked[0];
            if (IsSameLiveTask(context.CurrentTask,
                    PartyForceIntent.PeaceDemobilize, selected,
                    selected.SourceBatchId, context.Today))
            {
                return new PartyForcePlan
                {
                    Task = context.CurrentTask,
                    RankedCandidates = ranked,
                    ReasonCode = "ContinueExistingTask"
                };
            }
            PartyForceTask task = CreateTask(context,
                PartyForceIntent.PeaceDemobilize, selected.SourceKind,
                selected.Settlement, "PeaceDemobilizationTaskPlanned");
            task.SourceBatchId = selected.SourceBatchId;
            return new PartyForcePlan
            {
                Task = task,
                RankedCandidates = ranked,
                ShouldReplaceCurrentTask = context.CurrentTask != null,
                ReasonCode = "PeaceDemobilizationTaskPlanned"
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
            // Identity and the monotonic version are assigned by the task
            // coordinator. The pure planner must not manufacture identities.
            Version = 0,
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
            PartyForceIntent intent,
            PartyForceCandidate selected,
            string batchId,
            int today) => task != null && task.Intent == intent &&
            task.TargetSettlement == selected.Settlement &&
            task.SourceKind == selected.SourceKind &&
            string.Equals(task.SourceBatchId ?? string.Empty,
                batchId ?? string.Empty, StringComparison.Ordinal) &&
            task.Status != PartyForceTaskStatus.Cancelled &&
            task.Status != PartyForceTaskStatus.Completed &&
            task.CreatedDay <= today;
    }
}
