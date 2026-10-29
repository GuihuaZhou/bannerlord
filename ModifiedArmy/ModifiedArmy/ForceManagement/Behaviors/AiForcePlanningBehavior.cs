using Helpers;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Diagnostics;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Missions;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.Garrison.Behaviors;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>
    /// Owns the daily force plan and contributes it to native AI scoring.
    /// </summary>
    public sealed class AiForcePlanningBehavior : CampaignBehaviorBase
    {
        private List<PartyForceTask> _tasks = new List<PartyForceTask>();
        private Dictionary<string, int> _savedTaskRecords = new();
        private readonly PartyForceCandidateService _candidates =
            new PartyForceCandidateService();
        private readonly PartyVisitExecutionService _visitExecution =
            new PartyVisitExecutionService();
        private readonly PartyForceTaskPersistence _persistence =
            new PartyForceTaskPersistence();
        private PartyForceTaskCoordinator _coordinator;

        private PartyForceTaskCoordinator Coordinator => _coordinator ??=
            new PartyForceTaskCoordinator(() => _tasks, _candidates);

        /// <summary>Registers the unified daily planning pass.</summary>
        public override void RegisterEvents()
        {
            PartyMovementTracePatch.LogInstallationStatus();
            CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(
                this,
                OnAiHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this,
                OnGameLoadFinished);
        }

        /// <summary>
        /// Offers active force tasks to the native strategic AI. The daily
        /// planner owns the destination; the native AI owns movement.
        /// </summary>
        private void OnAiHourlyTick(MobileParty party, PartyThinkParams p)
        {
            PartyForceTask task = FindTask(party);
            if (task == null || p == null ||
                (task.Intent != PartyForceIntent.Recruit &&
                    task.Intent != PartyForceIntent.WarRest) ||
                task.Status == PartyForceTaskStatus.Completed ||
                task.Status == PartyForceTaskStatus.Cancelled ||
                task.Status == PartyForceTaskStatus.Suspended ||
                task.TargetSettlement == null ||
                party?.CurrentSettlement == task.TargetSettlement)
                return;

            if (!IsRecruitmentDestinationAuthorized(party, task))
            {
                task.Status = PartyForceTaskStatus.Cancelled;
                task.OwnsMovementOrder = false;
                task.ReasonCode = "RecruitmentSettlementBindingInvalid";
                if (party?.Ai != null)
                    party.Ai.RethinkAtNextHourlyTick = true;
                return;
            }

            Settlement target = task.TargetSettlement;
            GetBestNavigationData(
                party,
                target,
                out MobileParty.NavigationType navigationType,
                out bool isFromPort,
                out bool isTargetingPort);
            if (navigationType == MobileParty.NavigationType.None) return;

            AIBehaviorData candidate = new AIBehaviorData(
                target,
                AiBehavior.GoToSettlement,
                navigationType,
                false,
                isFromPort,
                isTargetingPort);
            float score = CalculateTaskBehaviorScore(p);
            if (p.TryGetBehaviorScore(candidate, out float oldScore))
                p.SetBehaviorScore(
                    candidate,
                    float.IsNaN(oldScore) || float.IsInfinity(oldScore)
                        ? score
                        : Math.Max(oldScore, score));
            else
                p.AddBehaviorScore((candidate, score));
        }

        private static void GetBestNavigationData(
            MobileParty party,
            Settlement target,
            out MobileParty.NavigationType navigationType,
            out bool isFromPort,
            out bool isTargetingPort)
        {
            navigationType = MobileParty.NavigationType.None;
            isFromPort = false;
            isTargetingPort = false;
            float bestDistance = float.MaxValue;

            if (!target.HasPort || target.SiegeEvent == null ||
                target.SiegeEvent.IsBlockadeActive ||
                !party.HasNavalNavigationCapability)
            {
                AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                    party, target, false, out navigationType,
                    out bestDistance, out isFromPort);
            }

            if (!party.HasNavalNavigationCapability || !target.HasPort) return;

            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party, target, true,
                out MobileParty.NavigationType portNavigationType,
                out float portDistance, out bool portIsFromPort);
            if (portDistance >= bestDistance) return;

            navigationType = portNavigationType;
            isFromPort = portIsFromPort;
            isTargetingPort = true;
        }

        private static float CalculateTaskBehaviorScore(PartyThinkParams p)
        {
            float score = 1000f;
            foreach (var behaviorScore in p.AIBehaviorScores)
            {
                float existing = behaviorScore.Item2;
                if (float.IsNaN(existing) || float.IsInfinity(existing) ||
                    existing < score)
                    continue;
                score = existing < float.MaxValue / 1.000001f
                    ? existing + Math.Max(1f, existing * 0.000001f)
                    : float.MaxValue;
            }
            return score;
        }

        private static bool IsRecruitmentDestinationAuthorized(
            MobileParty party,
            PartyForceTask task)
        {
            if (task?.Intent != PartyForceIntent.Recruit ||
                task.SourceKind == PartyForceSourceKind.Mercenary)
                return true;
            return Campaign.Current?
                .GetCampaignBehavior<PartySettlementBindingBehavior>()?
                .IsBoundSettlement(party, task.TargetSettlement) == true;
        }

        /// <summary>
        /// Saves only task identifiers and scalar state. Volatile candidates,
        /// roster snapshots and approvals are always rebuilt after loading.
        /// </summary>
        public override void SyncData(IDataStore dataStore)
        {
            if (!dataStore.IsLoading)
                _persistence.Capture(_tasks, _savedTaskRecords);
            dataStore.SyncData(
                "modifiedarmy_force_task_records_v1",
                ref _savedTaskRecords);
            EnsureSaveCollections();
            if (dataStore.IsLoading)
                _tasks = _persistence.Restore(_savedTaskRecords);
        }

        /// <summary>Revalidates restored tasks once the campaign is fully loaded.</summary>
        private void OnGameLoadFinished()
        {
            // The coordinator registered after all specialist behaviors runs
            // this validation once the fief service index is rebuilt.
            if (Campaign.Current?.GetCampaignBehavior<
                    CampaignRecoveryCoordinator>() == null)
                RevalidateAfterCampaignRecovery();
        }

        /// <summary>
        /// Revalidates runtime task references after persistent fief indexes
        /// are available. The operation is idempotent and never replays a
        /// roster or financial mutation.
        /// </summary>
        public void RevalidateAfterCampaignRecovery()
        {
            foreach (PartyForceTask task in _tasks.ToList())
            {
                try
                {
                    RevalidateLoadedTask(task);
                }
                catch (Exception ex)
                {
                    if (task != null)
                    {
                        task.Status = PartyForceTaskStatus.Cancelled;
                        task.OwnsMovementOrder = false;
                        task.ReasonCode = "SavedTaskRevalidationException";
                    }
                    if (RecruitmentLogFilter.ShouldLog(task?.Party))
                    {
                        TextObject message = GameTexts.FindText(
                            "str_modifiedarmy_force_saved_task_failed");
                        message.SetTextVariable(
                            "PARTY_ID", task?.PartyId ?? "<unknown>");
                        message.SetTextVariable("EXCEPTION", ex.ToString());
                        ModLogger.Error(message.ToString());
                    }
                }
            }
            PruneTerminalTasks();
        }

        /// <summary>
        /// Rebuilds volatile approvals after loading and never replays a saved
        /// roster mutation. Invalid ownership, routes, or source batches cancel
        /// the task conservatively.
        /// </summary>
        private void RevalidateLoadedTask(PartyForceTask task)
        {
                MobileParty party = task.Party;
                TransferOperationJournalBehavior journal = Campaign.Current?
                    .GetCampaignBehavior<TransferOperationJournalBehavior>();
                bool frozen = journal?.IsDomainFrozen(
                    party?.StringId ?? task.PartyId) == true;
                if (task.Status == PartyForceTaskStatus.Suspended || frozen)
                {
                    task.Status = PartyForceTaskStatus.Suspended;
                    task.ReasonCode = frozen
                        ? "TransferDomainFrozenForRecovery"
                        : task.ReasonCode ?? "SavedTaskSuspended";
                    task.LastValidatedDay = (int)CampaignTime.Now.ToDays;
                    task.OwnsMovementOrder = false;
                    return;
                }
                bool valid = ManagedPartyPolicy.IsModManagedAiParty(party) &&
                    task.OwnerKey == "ModifiedArmy.AiForcePlanning" &&
                    task.ClanId == party.ActualClan?.StringId &&
                    task.TargetSettlement != null &&
                    !task.TargetSettlement.IsUnderSiege;
                if (valid && task.Intent == PartyForceIntent.WarRest)
                {
                    WarRestContext rest = ForceManagementFacade.Current
                        .WarRestPlanning.BuildContext(
                            party,
                            ForceManagementFacade.Current.GetPartyTarget(party),
                            false);
                    valid = ForceManagementFacade.IsAtFormalWar(
                            party.ActualClan) &&
                        rest.FiefBatches.Any(batch =>
                            batch.BatchId == task.SourceBatchId &&
                            batch.SourceFief == task.TargetSettlement &&
                            batch.IsSafe);
                }
                if (valid && task.Intent == PartyForceIntent.Recruit)
                {
                    valid = IsRecruitmentDestinationAuthorized(party, task);
                    PartyForcePlanningContext context = _candidates.BuildContext(
                        party,
                        task,
                        true);
                    valid = valid && context.Candidates.Any(candidate =>
                        candidate.Settlement == task.TargetSettlement &&
                        candidate.SourceKind == task.SourceKind &&
                        candidate.IsSafe);
                }
                task.LastValidatedDay = (int)CampaignTime.Now.ToDays;
                task.OwnsMovementOrder = false;
                if (valid)
                {
                    task.Status = PartyForceTaskStatus.Planned;
                    party.Ai.RethinkAtNextHourlyTick = true;
                    if (!RecruitmentLogFilter.ShouldLog(party)) return;
                    TextObject resumed = GameTexts.FindText(
                        "str_modifiedarmy_force_task_resumed");
                    resumed.SetTextVariable(
                        "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
                    resumed.SetTextVariable(
                        "SETTLEMENT_NAME", task.TargetSettlement.Name);
                    resumed.SetTextVariable(
                        "INTENT", GetIntentText(task.Intent));
                    ModLogger.Info(resumed.ToString());
                }
                else
                {
                    task.Status = PartyForceTaskStatus.Cancelled;
                    task.ReasonCode = "SavedTaskFailedRevalidation";
                    if (party?.Ai != null)
                        party.Ai.RethinkAtNextHourlyTick = true;
                    if (!RecruitmentLogFilter.ShouldLog(party)) return;
                    TextObject cancelled = GameTexts.FindText(
                        "str_modifiedarmy_force_task_cancelled_on_load");
                    cancelled.SetTextVariable(
                        "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
                    cancelled.SetTextVariable(
                        "SETTLEMENT_NAME",
                        task.TargetSettlement?.Name ??
                            new TextObject("Unknown settlement"));
                    cancelled.SetTextVariable(
                        "INTENT", GetIntentText(task.Intent));
                    ModLogger.Info(cancelled.ToString());
                }
        }

        /// <summary>Repairs absent keys when loading an older save.</summary>
        private void EnsureSaveCollections()
        {
            _savedTaskRecords ??= new Dictionary<string, int>();
            _tasks ??= new List<PartyForceTask>();
        }

        /// <summary>Parses one versioned primitive task record defensively.</summary>
        /// <summary>Runs the single daily planning pass.</summary>
        private void OnDailyTick()
        {
            MilitaryConfig config = MilitaryConfigManager.Instance
                .GetActiveConfig();
            if (Campaign.Current == null || config == null) return;
            PruneTerminalTasks();
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;
                foreach (Clan clan in kingdom.Clans)
                {
                    if (clan.IsEliminated) continue;
                    foreach (var component in clan.WarPartyComponents)
                    {
                        MobileParty party = component?.MobileParty;
                        if (!ManagedPartyPolicy.IsModManagedAiParty(party))
                            continue;
                        bool gate = MBRandom.RandomFloat <=
                            config.AiRecruitRandomChance;
                        try
                        {
                            EvaluateParty(party, gate);
                        }
                        catch (Exception ex)
                        {
                            PartyForceTask task = FindTask(party);
                            if (task != null)
                            {
                                task.Status = PartyForceTaskStatus.Cancelled;
                                task.OwnsMovementOrder = false;
                                task.ReasonCode = "DailyPlanningException";
                                if (party?.Ai != null)
                                    party.Ai.RethinkAtNextHourlyTick = true;
                            }
                            if (RecruitmentLogFilter.ShouldLog(party))
                            {
                                TextObject message = GameTexts.FindText(
                                    "str_modifiedarmy_force_daily_plan_failed");
                                message.SetTextVariable(
                                    "PARTY_ID", party.StringId ?? "<unknown>");
                                message.SetTextVariable(
                                    "EXCEPTION", ex.ToString());
                                ModLogger.Error(message.ToString());
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Removes completed, cancelled, missing, and stale task records. This
        /// keeps both memory and the primitive save payload bounded.
        /// </summary>
        private void PruneTerminalTasks()
        {
            Coordinator.PruneTerminalTasks();
        }

        /// <summary>
        /// Produces a task decision. Native strategic AI owns movement, while
        /// roster mutation remains arrival-only.
        /// </summary>
        public PartyForcePlan EvaluateParty(
            MobileParty party,
            bool randomGateOpen)
        {
            return Coordinator.EvaluateParty(party, randomGateOpen);
        }

        /// <summary>
        /// Executes only the task whose stored destination matches this visit.
        /// All approvals are rebuilt from current campaign state on arrival.
        /// </summary>
        public PartyExchangeExecutionResult ExecuteAtSettlement(
            MobileParty party,
            Settlement settlement)
        {
            return _visitExecution.Execute(
                party, settlement, FindTask(party));
        }

        /// <summary>
        /// Consumes and executes the one-shot post-conquest donation through
        /// the same model/service boundary as every other managed transfer.
        /// The target may belong to another clan, but must remain in the
        /// capturing party's kingdom and use the real settlement garrison.
        /// </summary>
        public PartyExchangeExecutionResult ExecuteCaptureDonation(
            MobileParty party,
            Settlement settlement,
            GarrisonCaptureSupplyBehavior capture)
        {
            return _visitExecution.ExecuteCaptureDonation(
                party, settlement, capture);
        }

        /// <summary>Finds the newest unfinished task owned by this behavior.</summary>
        private PartyForceTask FindTask(MobileParty party) =>
            Coordinator.FindTask(party);

        internal PartyForceTask GetTaskForDiagnostics(MobileParty party) =>
            FindTask(party);

        /// <summary>Writes an explainable shadow decision using display names.</summary>
        internal static void LogPlan(
            MobileParty party,
            PartyForcePlan plan,
            PartyForcePlanningContext context)
        {
            ForceRecruitmentDiagnostics.LogDailyPlan(party, context, plan);
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            PartyForceTask task = plan?.Task;
            PartyForceIntent? intent = task?.Intent;
            bool suspended = plan == null || task == null ||
                plan.ReasonCode == "HigherPriorityOrder" ||
                plan.ReasonCode == "RecruitmentRandomGateClosed" ||
                plan.ReasonCode == "NoWarRestDestination" ||
                plan.ReasonCode == "InvalidParty";
            string templateId = suspended
                ? "str_modifiedarmy_force_plan_suspended"
                : intent switch
                {
                    PartyForceIntent.Recruit =>
                        "str_modifiedarmy_force_plan_recruit",
                    PartyForceIntent.WarRest =>
                        "str_modifiedarmy_force_plan_war_rest",
                    PartyForceIntent.Maintain =>
                        "str_modifiedarmy_force_plan_maintain",
                    _ => "str_modifiedarmy_force_plan_suspended"
                };
            TextObject message = GameTexts.FindText(templateId);
            message.SetTextVariable(
                "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable(
                "REASON",
                GetPlanReasonText(plan?.ReasonCode));

            // Only actionable travel plans have a destination, source, and ranked
            // candidates. Maintain and suspended templates intentionally omit
            // those variables instead of rendering artificial "none" values.
            if (!suspended && (intent == PartyForceIntent.Recruit ||
                intent == PartyForceIntent.WarRest))
            {
                message.SetTextVariable(
                    "SETTLEMENT_NAME",
                    task?.TargetSettlement?.Name ?? GetNoneText());
                message.SetTextVariable(
                    "SOURCE", GetSourceText(task?.SourceKind));
                message.SetTextVariable(
                    "CANDIDATE_COUNT", plan?.RankedCandidates?.Count ?? 0);
            }
            ModLogger.Debug(message.ToString());
        }

        /// <summary>
        /// Converts planner reason codes into localized player-facing explanations.
        /// Unknown codes use a neutral fallback; the raw code remains available in
        /// the debugger and does not need to leak into routine campaign logs.
        /// </summary>
        private static TextObject GetPlanReasonText(string reason)
        {
            string id = reason switch
            {
                "ForceTargetSatisfied" =>
                    "str_modifiedarmy_force_reason_target_satisfied",
                "RecruitmentNeedBelowThreshold" =>
                    "str_modifiedarmy_force_reason_need_below_threshold",
                "RecruitmentRandomGateClosed" =>
                    "str_modifiedarmy_force_reason_random_gate",
                "NoFeasibleRecruitmentSource" =>
                    "str_modifiedarmy_force_reason_no_source",
                "ContinueExistingTask" =>
                    "str_modifiedarmy_force_reason_continue_task",
                "RecruitmentTaskPlanned" =>
                    "str_modifiedarmy_force_reason_recruit_planned",
                "WarRestTaskPlanned" =>
                    "str_modifiedarmy_force_reason_rest_planned",
                "NoWarRestDestination" =>
                    "str_modifiedarmy_force_reason_no_rest_destination",
                "WarRestCancelledByPeace" =>
                    "str_modifiedarmy_force_reason_rest_cancelled_peace",
                "HigherPriorityOrder" =>
                    "str_modifiedarmy_force_reason_higher_priority",
                "InvalidParty" =>
                    "str_modifiedarmy_force_reason_invalid_party",
                _ => "str_modifiedarmy_force_reason_other"
            };
            return GameTexts.FindText(id);
        }

        /// <summary>Returns localized text for a force-task intent.</summary>
        private static TextObject GetIntentText(PartyForceIntent? intent)
        {
            switch (intent)
            {
                case PartyForceIntent.Recruit:
                    return GameTexts.FindText("str_modifiedarmy_force_intent_recruit");
                case PartyForceIntent.WarRest:
                    return GameTexts.FindText("str_modifiedarmy_force_intent_war_rest");
                case PartyForceIntent.Maintain:
                    return GameTexts.FindText("str_modifiedarmy_force_intent_maintain");
                case PartyForceIntent.Suspended:
                    return GameTexts.FindText("str_modifiedarmy_force_intent_suspended");
                default:
                    return GetNoneText();
            }
        }

        /// <summary>Returns localized text for the selected troop source.</summary>
        private static TextObject GetSourceText(PartyForceSourceKind? source)
        {
            switch (source)
            {
                case PartyForceSourceKind.Fief:
                    return GameTexts.FindText("str_modifiedarmy_force_source_fief");
                case PartyForceSourceKind.Professional:
                    return GameTexts.FindText("str_modifiedarmy_force_source_professional");
                case PartyForceSourceKind.Mercenary:
                    return GameTexts.FindText("str_modifiedarmy_force_source_mercenary");
                default:
                    return GetNoneText();
            }
        }

        /// <summary>Returns the shared localized empty-value label.</summary>
        private static TextObject GetNoneText() => GameTexts.FindText(
            "str_modifiedarmy_common_none");
    }
}
