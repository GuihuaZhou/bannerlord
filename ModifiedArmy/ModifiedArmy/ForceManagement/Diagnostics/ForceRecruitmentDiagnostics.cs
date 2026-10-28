using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Diagnostics
{
    /// <summary>
    /// Read-only diagnostics for the AI reinforcement lifecycle. These helpers
    /// never evaluate plans, consume random values, issue orders, or mutate a
    /// task; they only format values already produced by the live path.
    /// </summary>
    internal static class ForceRecruitmentDiagnostics
    {
        private static readonly object MovementLock = new object();
        private static readonly Dictionary<string, MovementTraceEvent>
            LatestMovementChanges = new Dictionary<string, MovementTraceEvent>();
        private static Campaign _movementCampaign;
        private static long _nextMovementEventId;

        internal static string GetTraceId(
            MobileParty party,
            PartyForceTask task)
        {
            if (task == null)
            {
                return string.Format(
                    "{0}/{1}/evaluation",
                    party?.StringId ?? "<unknown>",
                    (int)CampaignTime.Now.ToDays);
            }

            return string.Format(
                "{0}/{1}/{2}/{3}",
                task.PartyId ?? party?.StringId ?? "<unknown>",
                task.CreatedDay,
                task.Version,
                task.Intent);
        }

        internal static void LogDailyPlan(
            MobileParty party,
            PartyForcePlanningContext context,
            PartyForcePlan plan)
        {
            if (!RecruitmentLogFilter.ShouldLog(party) || context == null)
                return;
            PartyForceTargetResult target = context.ForceTarget;
            PartyForceTask task = plan?.Task ?? context.CurrentTask;
            int current = target?.CurrentCount ??
                party?.MemberRoster?.TotalRegulars ?? 0;
            int gap = Math.Max(0, target?.GapCount ?? 0);
            int goal = Math.Max(0, target?.TargetCount ?? 0);
            int ratio = goal <= 0 ? 0 : (int)Math.Round(gap * 100d / goal);
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_diag_daily");
            SetParty(message, party);
            message.SetTextVariable("DAY", context.Today);
            message.SetTextVariable("CURRENT", current);
            message.SetTextVariable("TARGET", goal);
            message.SetTextVariable("GAP", gap);
            message.SetTextVariable("GAP_RATIO", ratio);
            message.SetTextVariable(
                "GATE",
                context.IsDailyRandomGateOpen ? "已通过" : "未通过");
            message.SetTextVariable("SCORE", context.RecruitmentNeedScore);
            message.SetTextVariable(
                "THRESHOLD", context.RecruitmentNeedThreshold);
            message.SetTextVariable("REASON", plan?.ReasonCode ?? "<none>");
            message.SetTextVariable("TASK_STATUS", task?.Status.ToString() ?? "无");
            message.SetTextVariable(
                "SETTLEMENT_NAME", task?.TargetSettlement?.Name ?? None());
            message.SetTextVariable("SOURCE", task?.SourceKind.ToString() ?? "无");
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            ModLogger.Debug(message.ToString());
        }

        internal static void LogMovementChanged(
            MobileParty party,
            PartyForceTask task)
        {
            if (!RecruitmentLogFilter.ShouldLog(party) || task == null) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_diag_target_changed");
            SetParty(message, party);
            message.SetTextVariable(
                "PLANNED_TARGET", task.TargetSettlement?.Name ?? None());
            message.SetTextVariable(
                "CURRENT_TARGET", party?.TargetSettlement?.Name ?? None());
            message.SetTextVariable("DEFAULT_BEHAVIOR", party?.DefaultBehavior.ToString() ?? "无");
            message.SetTextVariable("SHORT_BEHAVIOR", party?.ShortTermBehavior.ToString() ?? "无");
            message.SetTextVariable("IN_ARMY", party?.Army != null ? "是" : "否");
            message.SetTextVariable("IN_BATTLE", party?.MapEvent != null ? "是" : "否");
            MovementTraceEvent trace = GetLatestMovementChange(party);
            message.SetTextVariable("EVENT_ID", trace?.EventId ?? "未记录");
            message.SetTextVariable("CHANGE_METHOD", trace?.Method ?? "尚未确认");
            message.SetTextVariable(
                "CHANGE_TIME",
                FormatHour(trace?.CampaignHour));
            message.SetTextVariable(
                "CURRENT_SETTLEMENT", party?.CurrentSettlement?.Name ?? None());
            message.SetTextVariable(
                "TARGET_PARTY", party?.TargetParty?.Name ?? None());
            message.SetTextVariable(
                "SHORT_TARGET_PARTY", party?.ShortTermTargetParty?.Name ?? None());
            message.SetTextVariable(
                "ALERTED", party?.Ai?.IsAlerted == true ? "是" : "否");
            message.SetTextVariable(
                "TASK_AGE",
                Math.Max(0, (int)CampaignTime.Now.ToDays - task.CreatedDay));
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            ModLogger.Info(message.ToString());
        }

        internal static void RecordMovementChange(
            MobileParty party,
            PartyForceTask task,
            string method,
            AiBehavior beforeDefault,
            AiBehavior afterDefault,
            AiBehavior beforeShortTerm,
            AiBehavior afterShortTerm,
            string beforeSettlement,
            string afterSettlement,
            string beforeTargetParty,
            string afterTargetParty)
        {
            if (!RecruitmentLogFilter.ShouldLog(party) || task == null) return;
            try
            {
                MovementTraceEvent trace;
                lock (MovementLock)
                {
                    EnsureMovementCampaign();
                    trace = new MovementTraceEvent
                    {
                        EventId = "route-" + (++_nextMovementEventId),
                        PartyId = party.StringId,
                        TraceId = GetTraceId(party, task),
                        Method = method ?? "尚未确认",
                        CampaignHour = CampaignTime.Now.ToHours,
                        CallPath = BuildCallPath()
                    };
                    LatestMovementChanges[party.StringId ?? string.Empty] = trace;
                }

                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_force_diag_route_change");
                SetParty(message, party);
                message.SetTextVariable("EVENT_ID", trace.EventId);
                message.SetTextVariable(
                    "CAMPAIGN_HOUR", FormatHour(trace.CampaignHour));
                message.SetTextVariable("METHOD", trace.Method);
                message.SetTextVariable(
                    "ASSEMBLY", MethodAssembly(trace.Method));
                message.SetTextVariable(
                    "BEFORE_DEFAULT", GetBehaviorText(beforeDefault));
                message.SetTextVariable(
                    "AFTER_DEFAULT", GetBehaviorText(afterDefault));
                message.SetTextVariable(
                    "BEFORE_SHORT", GetBehaviorText(beforeShortTerm));
                message.SetTextVariable(
                    "AFTER_SHORT", GetBehaviorText(afterShortTerm));
                message.SetTextVariable(
                    "BEFORE_TARGET", beforeSettlement ?? "无");
                message.SetTextVariable(
                    "AFTER_TARGET", afterSettlement ?? "无");
                message.SetTextVariable(
                    "BEFORE_PARTY", beforeTargetParty ?? "无");
                message.SetTextVariable(
                    "AFTER_PARTY", afterTargetParty ?? "无");
                message.SetTextVariable(
                    "PLANNED_TARGET", task.TargetSettlement?.Name ?? None());
                message.SetTextVariable("TASK_STATUS", GetTaskStatusText(task.Status));
                message.SetTextVariable(
                    "OWNS_ORDER", task.OwnsMovementOrder ? "是" : "否");
                message.SetTextVariable("IN_ARMY", party.Army != null ? "是" : "否");
                message.SetTextVariable("IN_BATTLE", party.MapEvent != null ? "是" : "否");
                message.SetTextVariable(
                    "ALERTED", party.Ai?.IsAlerted == true ? "是" : "否");
                message.SetTextVariable("CALL_PATH", trace.CallPath);
                message.SetTextVariable("TRACE_ID", trace.TraceId);
                bool lostPlannedRoute = task.OwnsMovementOrder &&
                    (afterDefault != AiBehavior.GoToSettlement ||
                        afterSettlement != task.TargetSettlement?.StringId);
                if (lostPlannedRoute)
                    ModLogger.Warn(message.ToString());
                else
                    ModLogger.Debug(message.ToString());
            }
            catch
            {
                // Diagnostics must never escape into a movement method.
            }
        }

        internal static MovementTraceEvent GetLatestMovementChange(
            MobileParty party)
        {
            lock (MovementLock)
            {
                EnsureMovementCampaign();
                return party != null && LatestMovementChanges.TryGetValue(
                    party.StringId ?? string.Empty,
                    out MovementTraceEvent trace)
                        ? trace
                        : null;
            }
        }

        internal static void LogTaskContinuation(
            MobileParty party,
            PartyForceTask task)
        {
            if (!RecruitmentLogFilter.ShouldLog(party) || task == null) return;
            bool targetMatches = party?.TargetSettlement == task.TargetSettlement;
            string template = task.Status == PartyForceTaskStatus.Planned &&
                !task.OwnsMovementOrder
                    ? "str_modifiedarmy_force_diag_task_without_order"
                    : "str_modifiedarmy_force_diag_task_continued";
            TextObject message = GameTexts.FindText(template);
            SetParty(message, party);
            message.SetTextVariable(
                "SETTLEMENT_NAME", task.TargetSettlement?.Name ?? None());
            message.SetTextVariable("TASK_STATUS", task.Status.ToString());
            message.SetTextVariable(
                "OWNS_ORDER", task.OwnsMovementOrder ? "是" : "否");
            message.SetTextVariable(
                "TARGET_MATCHES", targetMatches ? "是" : "否");
            message.SetTextVariable("DEFAULT_BEHAVIOR", party?.DefaultBehavior.ToString() ?? "无");
            message.SetTextVariable(
                "STATE_SUMMARY",
                GetContinuationStateText(party, task, targetMatches));
            MovementTraceEvent trace = GetLatestMovementChange(party);
            message.SetTextVariable("EVENT_ID", trace?.EventId ?? "未记录");
            message.SetTextVariable("CHANGE_METHOD", trace?.Method ?? "尚未确认");
            message.SetTextVariable(
                "CHANGE_TIME",
                FormatHour(trace?.CampaignHour));
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            if (template == "str_modifiedarmy_force_diag_task_without_order")
                ModLogger.Warn(message.ToString());
            else
                ModLogger.Debug(message.ToString());
        }

        internal static void LogTaskRouteDecision(
            MobileParty party,
            PartyForceTask task,
            bool commandIssued,
            string blocker)
        {
            if (!RecruitmentLogFilter.ShouldLog(party) || task == null) return;
            TextObject message = GameTexts.FindText(commandIssued
                ? "str_modifiedarmy_force_diag_route_issued"
                : "str_modifiedarmy_force_diag_route_blocked");
            SetParty(message, party);
            message.SetTextVariable(
                "SETTLEMENT_NAME", task.TargetSettlement?.Name ?? None());
            message.SetTextVariable("BLOCKER", blocker ?? "无");
            message.SetTextVariable("DEFAULT_BEHAVIOR", party?.DefaultBehavior.ToString() ?? "无");
            message.SetTextVariable(
                "CURRENT_TARGET", party?.TargetSettlement?.Name ?? None());
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            ModLogger.Info(message.ToString());
        }

        internal static void LogVisit(
            MobileParty party,
            Settlement settlement,
            string eventName,
            string outcome,
            int lastDay,
            PartyForceTask task)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_diag_visit");
            SetParty(message, party);
            message.SetTextVariable(
                "SETTLEMENT_NAME", settlement?.Name ?? None());
            message.SetTextVariable("EVENT", eventName ?? "未知");
            message.SetTextVariable("OUTCOME", outcome ?? "未知");
            message.SetTextVariable("LAST_DAY", lastDay);
            message.SetTextVariable("HAS_TASK", task != null ? "是" : "否");
            message.SetTextVariable(
                "TASK_TARGET", task?.TargetSettlement?.Name ?? None());
            message.SetTextVariable(
                "TARGET_MATCHES",
                task?.TargetSettlement == settlement ? "是" : "否");
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            ModLogger.Debug(message.ToString());
        }

        internal static void LogVisitTaskMatch(
            MobileParty party,
            Settlement settlement,
            PartyForceTask task)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            string id = task == null
                ? "str_modifiedarmy_force_diag_visit_no_task"
                : task.TargetSettlement != settlement
                    ? "str_modifiedarmy_force_diag_visit_wrong_target"
                    : "str_modifiedarmy_force_diag_visit_task_target";
            TextObject message = GameTexts.FindText(id);
            SetParty(message, party);
            message.SetTextVariable(
                "ACTUAL_SETTLEMENT", settlement?.Name ?? None());
            message.SetTextVariable(
                "TASK_TARGET", task?.TargetSettlement?.Name ?? None());
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            ModLogger.Debug(message.ToString());
        }

        internal static void LogExecution(
            MobileParty party,
            Settlement settlement,
            PartyForceTask task,
            PartyExchangeExecutionResult result,
            int before,
            int after)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_diag_execution");
            SetParty(message, party);
            message.SetTextVariable(
                "SETTLEMENT_NAME", settlement?.Name ?? None());
            message.SetTextVariable("BEFORE", before);
            message.SetTextVariable("AFTER", after);
            message.SetTextVariable("REQUESTED", result?.RequestedCount ?? 0);
            message.SetTextVariable("APPROVED", result?.ApprovedCount ?? 0);
            message.SetTextVariable("TRANSFERRED", result?.TransferredCount ?? 0);
            message.SetTextVariable("STATUS", result?.Status.ToString() ?? "无");
            message.SetTextVariable("REASON", result?.ReasonCode ?? "无");
            message.SetTextVariable("OPERATION_ID", result?.OperationId ?? "无");
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            ModLogger.Info(message.ToString());
        }

        internal static void LogLiveApproval(
            PartyExchangeContext context,
            PartyExchangePlan plan,
            PartyExchangeExecutionResult result)
        {
            MobileParty party = context?.Initiator;
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_diag_live_approval");
            SetParty(message, party);
            message.SetTextVariable(
                "SETTLEMENT_NAME", context?.TargetSettlement?.Name ?? None());
            message.SetTextVariable("REQUESTED", context?.RequestedCount ?? 0);
            message.SetTextVariable(
                "PLAN_APPROVED", plan?.SourceToDestination?.TotalCount ?? 0);
            message.SetTextVariable(
                "PLAN_REASON", plan?.ReasonCode ?? "无");
            message.SetTextVariable(
                "SOURCE_BEFORE", context?.SourceSnapshot?.RegularCount ?? 0);
            message.SetTextVariable(
                "DESTINATION_BEFORE",
                context?.DestinationSnapshot?.RegularCount ?? 0);
            message.SetTextVariable("STATUS", result?.Status.ToString() ?? "无");
            message.SetTextVariable("TRANSFERRED", result?.TransferredCount ?? 0);
            message.SetTextVariable("REASON", result?.ReasonCode ?? "无");
            message.SetTextVariable(
                "OPERATION_ID", context?.OperationId ?? "无");
            ModLogger.Debug(message.ToString());
        }

        internal static void LogGarrisonCandidate(
            MobileParty party,
            Settlement settlement,
            int resident,
            int reserved,
            int effective,
            int minimum,
            int safe,
            int protectedCount,
            int offered,
            int approved,
            int gap)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_diag_garrison_candidate");
            SetParty(message, party);
            message.SetTextVariable(
                "SETTLEMENT_NAME", settlement?.Name ?? None());
            message.SetTextVariable("RESIDENT", resident);
            message.SetTextVariable("RESERVED", reserved);
            message.SetTextVariable("EFFECTIVE", effective);
            message.SetTextVariable("MINIMUM", minimum);
            message.SetTextVariable("SAFE", safe);
            message.SetTextVariable("PROTECTED", protectedCount);
            message.SetTextVariable("OFFERED", offered);
            message.SetTextVariable("APPROVED", approved);
            message.SetTextVariable("GAP", gap);
            ModLogger.Debug(message.ToString());
        }

        private static void SetParty(TextObject message, MobileParty party)
        {
            message.SetTextVariable(
                "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("PARTY_ID", party?.StringId ?? "<unknown>");
        }

        private static TextObject None() =>
            GameTexts.FindText("str_modifiedarmy_common_none");

        private static void EnsureMovementCampaign()
        {
            if (ReferenceEquals(_movementCampaign, Campaign.Current)) return;
            _movementCampaign = Campaign.Current;
            LatestMovementChanges.Clear();
            _nextMovementEventId = 0;
        }

        private static string BuildCallPath()
        {
            try
            {
                IEnumerable<string> frames = new StackTrace(2, false)
                    .GetFrames()
                    ?.Select(frame => frame.GetMethod())
                    .Where(method => method != null &&
                        method.DeclaringType != typeof(PartyMovementTracePatch) &&
                        method.DeclaringType != typeof(ForceRecruitmentDiagnostics))
                    .Take(12)
                    .Select(method => string.Format(
                        "{0}:{1}.{2}",
                        method.Module?.Assembly?.GetName()?.Name ?? "<unknown>",
                        method.DeclaringType?.FullName ?? "<unknown>",
                        method.Name)) ?? Array.Empty<string>();
                string result = string.Join(" <- ", frames);
                return string.IsNullOrWhiteSpace(result)
                    ? "调用来源尚未确认"
                    : result;
            }
            catch
            {
                return "调用来源尚未确认";
            }
        }

        private static string MethodAssembly(string method)
        {
            return typeof(MobileParty).Assembly.GetName().Name;
        }

        private static string FormatHour(double? hour) => hour.HasValue
            ? hour.Value.ToString("F1", CultureInfo.InvariantCulture)
            : "未记录";

        private static TextObject GetBehaviorText(AiBehavior behavior)
        {
            string id = behavior switch
            {
                AiBehavior.Hold => "str_modifiedarmy_force_diag_behavior_hold",
                AiBehavior.GoToSettlement =>
                    "str_modifiedarmy_force_diag_behavior_go_settlement",
                AiBehavior.GoToPoint =>
                    "str_modifiedarmy_force_diag_behavior_go_point",
                AiBehavior.EngageParty =>
                    "str_modifiedarmy_force_diag_behavior_engage",
                AiBehavior.EscortParty =>
                    "str_modifiedarmy_force_diag_behavior_escort",
                AiBehavior.RaidSettlement =>
                    "str_modifiedarmy_force_diag_behavior_raid",
                AiBehavior.BesiegeSettlement =>
                    "str_modifiedarmy_force_diag_behavior_besiege",
                AiBehavior.DefendSettlement =>
                    "str_modifiedarmy_force_diag_behavior_defend",
                AiBehavior.None => "str_modifiedarmy_force_diag_behavior_none",
                _ => "str_modifiedarmy_force_diag_behavior_other"
            };
            TextObject result = GameTexts.FindText(id);
            result.SetTextVariable("TECHNICAL_VALUE", behavior.ToString());
            return result;
        }

        private static TextObject GetTaskStatusText(PartyForceTaskStatus status)
        {
            string id = status switch
            {
                PartyForceTaskStatus.Planned =>
                    "str_modifiedarmy_force_diag_status_planned",
                PartyForceTaskStatus.Traveling =>
                    "str_modifiedarmy_force_diag_status_traveling",
                PartyForceTaskStatus.Cancelled =>
                    "str_modifiedarmy_force_diag_status_cancelled",
                PartyForceTaskStatus.Suspended =>
                    "str_modifiedarmy_force_diag_status_suspended",
                PartyForceTaskStatus.Completed =>
                    "str_modifiedarmy_force_diag_status_completed",
                PartyForceTaskStatus.Executing =>
                    "str_modifiedarmy_force_diag_status_executing",
                PartyForceTaskStatus.Visiting =>
                    "str_modifiedarmy_force_diag_status_visiting",
                _ => "str_modifiedarmy_force_diag_status_unknown"
            };
            return GameTexts.FindText(id);
        }

        private static TextObject GetContinuationStateText(
            MobileParty party,
            PartyForceTask task,
            bool targetMatches)
        {
            string id = task.Status == PartyForceTaskStatus.Completed ||
                task.Status == PartyForceTaskStatus.Cancelled ||
                task.TargetSettlement == null
                    ? "str_modifiedarmy_force_diag_continue_terminal"
                    : TransportThreatModel.IsNativeAvoidanceActive(party)
                        ? "str_modifiedarmy_force_diag_continue_avoidance"
                        : !task.OwnsMovementOrder
                            ? "str_modifiedarmy_force_diag_continue_no_order"
                            : !targetMatches
                                ? "str_modifiedarmy_force_diag_continue_mismatch"
                                : "str_modifiedarmy_force_diag_continue_valid";
            return GameTexts.FindText(id);
        }

        internal sealed class MovementTraceEvent
        {
            internal string EventId { get; set; }
            internal string PartyId { get; set; }
            internal string TraceId { get; set; }
            internal string Method { get; set; }
            internal double CampaignHour { get; set; }
            internal string CallPath { get; set; }
        }
    }
}
