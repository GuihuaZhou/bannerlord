using HarmonyLib;
using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Diagnostics
{
    /// <summary>
    /// Observes top-level MobileParty movement commands. Prefix captures only
    /// state; postfix compares and logs. It never changes arguments, results,
    /// exceptions, tasks, or party movement.
    /// </summary>
    [HarmonyPatch]
    internal static class PartyMovementTracePatch
    {
        [ThreadStatic]
        private static int _movementCallDepth;

        private static readonly string[] TargetMethodNames =
        {
            "SetMoveModeHold",
            "SetMoveGoToSettlement",
            "SetMoveGoToPoint",
            "SetMoveEngageParty",
            "SetMoveGoAroundParty",
            "SetMoveEscortParty",
            "SetMovePatrolAroundPoint",
            "SetMovePatrolAroundSettlement",
            "SetMoveRaidSettlement",
            "SetMoveBesiegeSettlement",
            "SetMoveDefendSettlement",
            "SetMoveToNearestLand",
            "SetMoveGoToInteractablePoint",
            "SetTargetSettlement"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in TargetMethodNames)
            {
                MethodInfo method = AccessTools.Method(typeof(MobileParty), name);
                if (method != null) yield return method;
            }
        }

        internal static void LogInstallationStatus()
        {
            foreach (string name in TargetMethodNames)
            {
                MethodInfo method = AccessTools.Method(typeof(MobileParty), name);
                Patches patches = method == null
                    ? null
                    : Harmony.GetPatchInfo(method);
                bool installed = patches?.Postfixes?.Any(patch =>
                    patch.owner == "com.mod.ModifiedArmy" &&
                    patch.PatchMethod?.DeclaringType ==
                        typeof(PartyMovementTracePatch)) == true;
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_force_diag_movement_patch");
                message.SetTextVariable("METHOD", name);
                message.SetTextVariable(
                    "SIGNATURE",
                    method == null
                        ? "目标方法不存在"
                        : string.Join(
                            ", ",
                            method.GetParameters().Select(parameter =>
                                parameter.ParameterType.Name)));
                message.SetTextVariable(
                    "STATUS", installed ? "已安装" : "未安装");
                if (installed)
                    ModLogger.Debug(message.ToString());
                else
                    ModLogger.Warn(message.ToString());
            }
        }

        private static void Prefix(
            MobileParty __instance,
            MethodBase __originalMethod,
            out MovementTraceState __state)
        {
            __state = null;
            int depth = _movementCallDepth++;
            if (depth != 0) return;
            try
            {
                if (!ManagedPartyPolicy.IsModManagedAiParty(__instance) ||
                    !RecruitmentLogFilter.ShouldLog(__instance))
                    return;
                PartyForceTask task = Campaign.Current?
                    .GetCampaignBehavior<AiForcePlanningBehavior>()?
                    .GetTaskForDiagnostics(__instance);
                if (task?.Intent != PartyForceIntent.Recruit ||
                    task.TargetSettlement == null ||
                    task.Status == PartyForceTaskStatus.Completed ||
                    task.Status == PartyForceTaskStatus.Cancelled)
                    return;
                __state = MovementTraceState.Capture(
                    __instance,
                    task,
                    __originalMethod?.DeclaringType?.FullName + "." +
                        (__originalMethod?.Name ?? "<unknown>"));
            }
            catch
            {
                // Diagnostic failures must never affect the movement call.
                __state = null;
            }
        }

        private static void Postfix(
            MobileParty __instance,
            MovementTraceState __state)
        {
            _movementCallDepth = Math.Max(0, _movementCallDepth - 1);
            if (__state == null) return;
            try
            {
                __state.CaptureAfter(__instance);
                if (__state.HasChanged)
                {
                    ForceRecruitmentDiagnostics.RecordMovementChange(
                        __instance,
                        __state.Task,
                        __state.Method,
                        __state.BeforeDefault,
                        __state.AfterDefault,
                        __state.BeforeShortTerm,
                        __state.AfterShortTerm,
                        __state.BeforeSettlement,
                        __state.AfterSettlement,
                        __state.BeforeTargetParty,
                        __state.AfterTargetParty);
                }
            }
            catch
            {
                // Logging is deliberately isolated from the original method.
            }
        }

        private sealed class MovementTraceState
        {
            internal PartyForceTask Task { get; private set; }
            internal string Method { get; private set; }
            internal AiBehavior BeforeDefault { get; private set; }
            internal AiBehavior AfterDefault { get; private set; }
            internal AiBehavior BeforeShortTerm { get; private set; }
            internal AiBehavior AfterShortTerm { get; private set; }
            internal string BeforeSettlement { get; private set; }
            internal string AfterSettlement { get; private set; }
            internal string BeforeTargetParty { get; private set; }
            internal string AfterTargetParty { get; private set; }

            internal bool HasChanged =>
                BeforeDefault != AfterDefault ||
                BeforeShortTerm != AfterShortTerm ||
                BeforeSettlement != AfterSettlement ||
                BeforeTargetParty != AfterTargetParty;

            internal static MovementTraceState Capture(
                MobileParty party,
                PartyForceTask task,
                string method) => new MovementTraceState
            {
                Task = task,
                Method = method,
                BeforeDefault = party.DefaultBehavior,
                BeforeShortTerm = party.ShortTermBehavior,
                BeforeSettlement = party.TargetSettlement?.StringId,
                BeforeTargetParty = party.TargetParty?.StringId
            };

            internal void CaptureAfter(MobileParty party)
            {
                AfterDefault = party.DefaultBehavior;
                AfterShortTerm = party.ShortTermBehavior;
                AfterSettlement = party.TargetSettlement?.StringId;
                AfterTargetParty = party.TargetParty?.StringId;
            }
        }
    }
}
