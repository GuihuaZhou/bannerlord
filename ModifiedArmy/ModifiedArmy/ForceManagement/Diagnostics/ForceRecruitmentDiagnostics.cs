using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
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
            message.SetTextVariable("TRACE_ID", GetTraceId(party, task));
            ModLogger.Info(message.ToString());
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
    }
}
