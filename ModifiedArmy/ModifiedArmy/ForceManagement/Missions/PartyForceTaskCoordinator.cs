using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Diagnostics;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Missions
{
    /// <summary>Owns runtime task selection, pruning and movement orders.</summary>
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
                    ReleaseOwnedMovementOrder(
                        party, current, "任务已暂停或恢复域被冻结");
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
            if (current?.OwnsMovementOrder == true &&
                party.TargetSettlement != current.TargetSettlement)
            {
                ForceRecruitmentDiagnostics.LogMovementChanged(party, current);
                current.OwnsMovementOrder = false;
                current.Status = PartyForceTaskStatus.Cancelled;
                current.ReasonCode = "MovementOrderChangedExternally";
            }
            PartyForcePlanningContext context = _candidates.BuildContext(
                party,
                current?.Status == PartyForceTaskStatus.Cancelled
                    ? null : current,
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
                    ReleaseOwnedMovementOrder(
                        party, current, "任务已终止或目标定居点不可用");
                return;
            }
            if (current != null)
            {
                ReleaseOwnedMovementOrder(
                    party, current, "每日规划已替换原任务");
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
            _tasks().Add(next);
            bool managed = ManagedPartyPolicy.IsModManagedAiParty(party);
            bool inArmy = party?.Army != null;
            bool inBattle = party?.MapEvent != null;
            bool avoiding = TransportThreatModel.IsNativeAvoidanceActive(party);
            if (managed && !inArmy && !inBattle && !avoiding)
            {
                party.SetMoveGoToSettlement(next.TargetSettlement,
                    MobileParty.NavigationType.Default,
                    isTargetingThePort: false);
                next.OwnsMovementOrder = true;
                next.Status = PartyForceTaskStatus.Traveling;
                ForceRecruitmentDiagnostics.LogTaskRouteDecision(
                    party, next, true, null);
            }
            else
            {
                string blocker = !managed ? "不是受 Mod 管理的 AI 领主部队" :
                    inArmy ? "当前处于军团中" :
                    inBattle ? "当前处于战斗中" :
                    avoiding ? "当前处于原版避敌状态" :
                    "现有条件未允许下达移动指令";
                ForceRecruitmentDiagnostics.LogTaskRouteDecision(
                    party, next, false, blocker);
            }
        }

        private static void ReleaseOwnedMovementOrder(
            MobileParty party,
            PartyForceTask task,
            string reason)
        {
            if (task?.OwnsMovementOrder != true || party == null) return;
            bool stillOurOrder = party.Army == null && party.MapEvent == null &&
                !TransportThreatModel.IsNativeAvoidanceActive(party) &&
                party.DefaultBehavior == AiBehavior.GoToSettlement &&
                party.TargetSettlement == task.TargetSettlement;
            task.OwnsMovementOrder = false;
            if (!stillOurOrder) return;
            party.SetMoveModeHold();
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_route_released");
            message.SetTextVariable(
                "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable(
                "SETTLEMENT_NAME", task.TargetSettlement.Name);
            message.SetTextVariable("REASON", reason ?? "未说明");
            message.SetTextVariable(
                "TRACE_ID",
                ForceRecruitmentDiagnostics.GetTraceId(party, task));
            ModLogger.Info(message.ToString());
        }
    }
}
