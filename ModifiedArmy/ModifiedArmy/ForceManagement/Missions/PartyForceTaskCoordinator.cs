using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Diagnostics;
using ModifiedArmy.ForceManagement.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyManagement.Models;

namespace ModifiedArmy.ForceManagement.Missions
{
    /// <summary>Owns runtime task selection and pruning.</summary>
    public sealed class PartyForceTaskCoordinator
    {
        private readonly Func<List<PartyForceTask>> _tasks;
        private readonly PartyForceCandidateService _candidates;

        public PartyForceTaskCoordinator(
            Func<List<PartyForceTask>> tasks,
            PartyForceCandidateService candidates)
        {
            _tasks = tasks;
            _candidates = candidates;
        }

        public PartyForcePlan EvaluateParty(
            MobileParty party,
            bool randomGateOpen)
        {
            PartyForceTask current = FindTask(party);
            TransferOperationJournalBehavior journal = Campaign.Current?
                .GetCampaignBehavior<TransferOperationJournalBehavior>();
            bool frozen = journal?.IsDomainFrozen(
                party?.StringId ?? current?.PartyId) == true;
            if (current?.Status == PartyForceTaskStatus.Suspended || frozen)
            {
                if (current != null)
                {
                    ForceRecruitmentDiagnostics.LogTaskRouteDecision(
                        party,
                        current,
                        false,
                        frozen
                            ? "调兵恢复域被冻结"
                            : "任务当前处于暂停状态");
                    ReleaseOwnedMovementOrder(party, current);
                    current.Status = PartyForceTaskStatus.Suspended;
                    current.ReasonCode = frozen
                        ? "TransferDomainFrozenForRecovery"
                        : current.ReasonCode ?? "TaskSuspended";
                    current.LastValidatedDay = (int)CampaignTime.Now.ToDays;
                }
                return new PartyForcePlan
                {
                    Task = current,
                    ReasonCode = current?.ReasonCode ??
                        "TransferDomainFrozenForRecovery"
                };
            }
            PartyForcePlanningContext context = _candidates.BuildContext(
                party, current,
                randomGateOpen);
            PartyForcePlan plan = ForceManagementFacade.Current.ForcePlanning
                .BuildDailyPlan(context);
            ApplyLivePlan(party, current, plan, context.Today);
            AiForcePlanningBehavior.LogPlan(party, plan, context);
            return plan;
        }

        public void PruneTerminalTasks()
        {
            int today = (int)CampaignTime.Now.ToDays;
            _tasks().RemoveAll(task => task == null || task.Party == null ||
                !task.Party.IsActive ||
                task.Status == PartyForceTaskStatus.Completed ||
                task.Status == PartyForceTaskStatus.Cancelled ||
                (task.Status != PartyForceTaskStatus.Suspended &&
                    today - task.LastValidatedDay > 7));
        }

        public PartyForceTask FindTask(MobileParty party) => _tasks()
            .Where(task => task != null &&
                (task.Party == party || task.PartyId == party?.StringId) &&
                task.Status != PartyForceTaskStatus.Completed &&
                task.Status != PartyForceTaskStatus.Cancelled)
            .OrderByDescending(task => task.Version)
            .FirstOrDefault();

        public bool TryRegisterInitialRecruitTask(MobileParty party,
            Settlement boundSettlement, PartyForceSourceKind preferredSource)
        {
            if (!ManagedPartyPolicy.IsModManagedAiParty(party) ||
                boundSettlement == null ||
                preferredSource != PartyForceSourceKind.Fief &&
                preferredSource != PartyForceSourceKind.Professional ||
                Campaign.Current?.GetCampaignBehavior<
                    PartySettlementBindingBehavior>()?.IsBoundSettlement(
                        party, boundSettlement) != true)
                return false;
            PartyForceTask current = FindTask(party);
            if (current != null)
                return current.Intent == PartyForceIntent.Recruit &&
                    current.TargetSettlement == boundSettlement &&
                    current.SourceKind == preferredSource;

            PartyForceSourceKind source = SelectAvailableSource(
                boundSettlement, preferredSource);
            if (source != PartyForceSourceKind.Fief &&
                source != PartyForceSourceKind.Professional)
                return false;
            int today = (int)CampaignTime.Now.ToDays;
            int version = _tasks().Where(task => task?.PartyId == party.StringId)
                .Select(task => task.Version).DefaultIfEmpty(0).Max() + 1;
            _tasks().Add(new PartyForceTask
            {
                Party = party,
                PartyId = party.StringId,
                ClanId = party.ActualClan.StringId,
                OwnerKey = "ModifiedArmy.AiForcePlanning",
                Intent = PartyForceIntent.Recruit,
                SourceKind = source,
                TargetSettlement = boundSettlement,
                Status = PartyForceTaskStatus.Planned,
                ReasonCode = "InitialPartyRecruitment",
                CreatedDay = today,
                LastValidatedDay = today,
                Version = version,
                OwnsMovementOrder = false
            });
            if (party.Ai != null) party.Ai.RethinkAtNextHourlyTick = true;
            return true;
        }

        private static PartyForceSourceKind SelectAvailableSource(
            Settlement settlement, PartyForceSourceKind preferred)
        {
            FiefPartyManager fiefs = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            int minimumFiefBatch = ModifiedArmy.common.MilitaryConfigManager
                .Instance.RequireActiveConfig().AiRecruitment.ForceManagement
                .MinimumFiefRecruitBatch;
            bool hasFief = PartyCreationSettlementModel.GetEligibleFiefRegulars(
                    fiefs, settlement) >=
                Math.Max(1, minimumFiefBatch);
            bool hasGarrison = GarrisonTransferPolicyModel
                .GetTransferableGarrisonTroopCount(settlement) > 0;
            if (preferred == PartyForceSourceKind.Fief && hasFief ||
                preferred == PartyForceSourceKind.Professional && hasGarrison)
                return preferred;
            if (hasFief) return PartyForceSourceKind.Fief;
            if (hasGarrison) return PartyForceSourceKind.Professional;
            return PartyForceSourceKind.None;
        }

        public void CancelTask(MobileParty party, string reason)
        {
            PartyForceTask task = FindTask(party);
            if (task == null) return;
            task.Status = PartyForceTaskStatus.Cancelled;
            task.OwnsMovementOrder = false;
            task.ReasonCode = reason ?? "CancelledByCaller";
        }

        private void ApplyLivePlan(
            MobileParty party,
            PartyForceTask current,
            PartyForcePlan plan,
            int today)
        {
            PartyForceTask next = plan?.Task;
            if (next == current)
            {
                ForceRecruitmentDiagnostics.LogTaskContinuation(party, current);
                current.LastValidatedDay = today;
                if (current.Status == PartyForceTaskStatus.Completed ||
                    current.Status == PartyForceTaskStatus.Cancelled ||
                    current.TargetSettlement == null)
                    ReleaseOwnedMovementOrder(party, current);
                return;
            }
            if (current != null)
            {
                ReleaseOwnedMovementOrder(party, current);
                current.Status = PartyForceTaskStatus.Cancelled;
                current.ReasonCode = "ReplacedByDailyPlan";
            }
            if (next == null ||
                next.Status == PartyForceTaskStatus.Completed ||
                next.TargetSettlement == null)
                return;
            next.Party = party;
            next.PartyId = party?.StringId;
            next.ClanId = party?.ActualClan?.StringId;
            next.LastValidatedDay = today;
            next.OwnsMovementOrder = false;
            next.Status = PartyForceTaskStatus.Planned;
            _tasks().Add(next);
            if (party?.Ai != null)
                party.Ai.RethinkAtNextHourlyTick = true;
        }

        private static void ReleaseOwnedMovementOrder(
            MobileParty party,
            PartyForceTask task)
        {
            if (task == null) return;
            task.OwnsMovementOrder = false;
            if (party?.Ai != null)
                party.Ai.RethinkAtNextHourlyTick = true;
        }
    }
}
