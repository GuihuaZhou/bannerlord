using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Garrison.Behaviors;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Owns the single daily AI force plan and every route created by it.
    /// </summary>
    public sealed class AiForcePlanningBehavior : CampaignBehaviorBase
    {
        private List<PartyForceTask> _tasks = new List<PartyForceTask>();
        private Dictionary<string, int> _savedTaskRecords = new();
        private readonly PartyForceCandidateService _candidates =
            new PartyForceCandidateService();
        private readonly UnifiedRecruitmentExecutionService _recruitment =
            new UnifiedRecruitmentExecutionService();

        /// <summary>Registers the unified daily planning pass.</summary>
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this,
                OnGameLoadFinished);
        }

        /// <summary>
        /// Saves only task identifiers and scalar state. Volatile candidates,
        /// roster snapshots and approvals are always rebuilt after loading.
        /// </summary>
        public override void SyncData(IDataStore dataStore)
        {
            if (!dataStore.IsLoading)
                CaptureTaskSaveFields();
            dataStore.SyncData(
                "modifiedarmy_force_task_records_v1",
                ref _savedTaskRecords);
            EnsureSaveCollections();
            if (dataStore.IsLoading)
                RestoreTaskSaveFields();
        }

        /// <summary>
        /// Converts live tasks into primitive dictionaries already supported
        /// by the base save system; no custom Saveable class is registered.
        /// </summary>
        private void CaptureTaskSaveFields()
        {
            EnsureSaveCollections();
            _savedTaskRecords.Clear();
            foreach (PartyForceTask task in _tasks ??
                Enumerable.Empty<PartyForceTask>())
            {
                string partyId = task?.Party?.StringId ?? task?.PartyId;
                string targetId = task?.TargetSettlement?.StringId;
                if (string.IsNullOrEmpty(partyId) ||
                    string.IsNullOrEmpty(targetId) ||
                    task.Status == PartyForceTaskStatus.Completed ||
                    task.Status == PartyForceTaskStatus.Cancelled)
                    continue;
                string record = string.Join("\u001f", new[]
                {
                    partyId,
                    targetId,
                    task.SourceBatchId ?? string.Empty,
                    ((int)task.Intent).ToString(CultureInfo.InvariantCulture),
                    ((int)task.SourceKind).ToString(CultureInfo.InvariantCulture),
                    task.Version.ToString(CultureInfo.InvariantCulture),
                    task.CreatedDay.ToString(CultureInfo.InvariantCulture),
                    ((int)task.Status).ToString(CultureInfo.InvariantCulture),
                    task.Progress.ToString(CultureInfo.InvariantCulture)
                });
                _savedTaskRecords[record] = 1;
            }
        }

        /// <summary>
        /// Resolves saved identifiers back to live objects without replaying
        /// movement or roster changes during loading.
        /// </summary>
        private void RestoreTaskSaveFields()
        {
            _tasks = new List<PartyForceTask>();
            foreach (string record in _savedTaskRecords.Keys)
            {
                if (!TryParseRecord(record, out string partyId,
                        out string targetId, out string batchId,
                        out PartyForceIntent intent,
                        out PartyForceSourceKind sourceKind,
                        out int version, out int createdDay,
                        out PartyForceTaskStatus status, out int progress))
                    continue;
                MobileParty party = MobileParty.All.FirstOrDefault(item =>
                    item?.StringId == partyId);
                Settlement target = Settlement.All.FirstOrDefault(item =>
                    item?.StringId == targetId);
                if (party == null || target == null) continue;
                _tasks.Add(new PartyForceTask
                {
                    Party = party,
                    PartyId = partyId,
                    ClanId = party.ActualClan?.StringId,
                    OwnerKey = "ModifiedArmy.AiForcePlanning",
                    TargetSettlement = target,
                    SourceBatchId = batchId,
                    Intent = intent,
                    SourceKind = sourceKind,
                    Version = version,
                    CreatedDay = createdDay,
                    LastValidatedDay = createdDay,
                    Status = status,
                    Progress = progress,
                    OwnsMovementOrder = false
                });
            }
        }

        /// <summary>Revalidates restored tasks once the campaign is fully loaded.</summary>
        private void OnGameLoadFinished()
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
                bool ownsRoute = party?.DefaultBehavior ==
                        AiBehavior.GoToSettlement &&
                    party.TargetSettlement == task.TargetSettlement;
                bool valid = ManagedPartyPolicy.IsModManagedAiParty(party) &&
                    task.OwnerKey == "ModifiedArmy.AiForcePlanning" &&
                    task.ClanId == party.ActualClan?.StringId &&
                    task.TargetSettlement != null &&
                    !task.TargetSettlement.IsUnderSiege &&
                    ownsRoute;
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
                    PartyForcePlanningContext context = _candidates.BuildContext(
                        party,
                        task,
                        true);
                    valid = context.Candidates.Any(candidate =>
                        candidate.Settlement == task.TargetSettlement &&
                        candidate.SourceKind == task.SourceKind &&
                        candidate.IsSafe);
                }
                task.LastValidatedDay = (int)CampaignTime.Now.ToDays;
                task.OwnsMovementOrder = valid;
                if (valid)
                {
                    task.Status = PartyForceTaskStatus.Traveling;
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
        private static bool TryParseRecord(
            string record, out string partyId, out string targetId,
            out string batchId, out PartyForceIntent intent,
            out PartyForceSourceKind sourceKind, out int version,
            out int createdDay, out PartyForceTaskStatus status,
            out int progress)
        {
            string[] parts = (record ?? string.Empty).Split('\u001f');
            partyId = parts.Length > 0 ? parts[0] : null;
            targetId = parts.Length > 1 ? parts[1] : null;
            batchId = parts.Length > 2 ? parts[2] : null;
            intent = PartyForceIntent.Suspended;
            sourceKind = PartyForceSourceKind.None;
            status = PartyForceTaskStatus.Suspended;
            version = createdDay = progress = 0;
            if (parts.Length != 9 || string.IsNullOrEmpty(partyId) ||
                string.IsNullOrEmpty(targetId) ||
                !int.TryParse(parts[3], out int intentValue) ||
                !int.TryParse(parts[4], out int sourceValue) ||
                !int.TryParse(parts[5], out version) ||
                !int.TryParse(parts[6], out createdDay) ||
                !int.TryParse(parts[7], out int statusValue) ||
                !int.TryParse(parts[8], out progress) ||
                !Enum.IsDefined(typeof(PartyForceIntent), intentValue) ||
                !Enum.IsDefined(typeof(PartyForceSourceKind), sourceValue) ||
                !Enum.IsDefined(typeof(PartyForceTaskStatus), statusValue))
                return false;
            intent = (PartyForceIntent)intentValue;
            sourceKind = (PartyForceSourceKind)sourceValue;
            status = (PartyForceTaskStatus)statusValue;
            return true;
        }

        /// <summary>Runs the single daily planning pass and owns approved routes.</summary>
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
            int today = (int)CampaignTime.Now.ToDays;
            _tasks.RemoveAll(task => task == null || task.Party == null ||
                !task.Party.IsActive ||
                task.Status == PartyForceTaskStatus.Completed ||
                task.Status == PartyForceTaskStatus.Cancelled ||
                today - task.LastValidatedDay > 7);
        }

        /// <summary>
        /// Produces a task decision and applies only the movement order owned
        /// by that task. Roster mutation remains arrival-only.
        /// </summary>
        public PartyForcePlan EvaluateParty(
            MobileParty party,
            bool randomGateOpen)
        {
            PartyForceTask current = FindTask(party);
            if (current?.OwnsMovementOrder == true &&
                party.TargetSettlement != current.TargetSettlement)
            {
                // Another game system has already replaced our route. Drop
                // ownership without touching that newer order.
                current.OwnsMovementOrder = false;
                current.Status = PartyForceTaskStatus.Cancelled;
                current.ReasonCode = "MovementOrderChangedExternally";
            }
            PartyForcePlanningContext context = _candidates.BuildContext(
                party,
                current?.Status == PartyForceTaskStatus.Cancelled
                    ? null
                    : current,
                randomGateOpen);
            PartyForcePlan plan = ForceManagementFacade.Current.ForcePlanning
                .BuildDailyPlan(context);
            ApplyLivePlan(party, current, plan, context.Today);
            LogPlan(party, plan);
            return plan;
        }

        /// <summary>Stores the planner result and takes ownership of its route.</summary>
        private void ApplyLivePlan(
            MobileParty party,
            PartyForceTask current,
            PartyForcePlan plan,
            int today)
        {
            PartyForceTask next = plan?.Task;
            if (next == current)
            {
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
            // A planner result with no replacement is still a cancellation;
            // the old route was released above if this behavior owned it.
            if (next == null) return;
            if (next.Status == PartyForceTaskStatus.Completed ||
                next.TargetSettlement == null)
                return;
            next.Party = party;
            next.PartyId = party?.StringId;
            next.ClanId = party?.ActualClan?.StringId;
            next.LastValidatedDay = today;
            next.OwnsMovementOrder = false;
            _tasks.Add(next);
            if (ManagedPartyPolicy.IsModManagedAiParty(party) &&
                party.Army == null && party.MapEvent == null &&
                !TransportThreatModel.IsNativeAvoidanceActive(party))
            {
                party.SetMoveGoToSettlement(
                    next.TargetSettlement,
                    MobileParty.NavigationType.Default,
                    isTargetingThePort: false);
                next.OwnsMovementOrder = true;
                next.Status = PartyForceTaskStatus.Traveling;
            }
        }

        /// <summary>
        /// Clears only the exact GoToSettlement order previously issued by
        /// this behavior. Army, combat, avoidance, or externally replaced
        /// orders are never taken over.
        /// </summary>
        private static void ReleaseOwnedMovementOrder(
            MobileParty party,
            PartyForceTask task)
        {
            if (task?.OwnsMovementOrder != true || party == null)
                return;
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
            ModLogger.Info(message.ToString());
        }

        /// <summary>
        /// Executes only the task whose stored destination matches this visit.
        /// All approvals are rebuilt from current campaign state on arrival.
        /// </summary>
        public PartyExchangeExecutionResult ExecuteAtSettlement(
            MobileParty party,
            Settlement settlement)
        {
            PartyForceTask task = FindTask(party);
            if (task == null || task.TargetSettlement != settlement)
            {
                PartyExchangeExecutionResult donation = TryRoutineDonation(
                    party, settlement);
                if (donation.Executed) return donation;
                TryPeaceDemobilization(party, settlement);
                return new PartyExchangeExecutionResult
                    { ReasonCode = donation.ReasonCode ?? "NoMatchingForceTask" };
            }
            task.Status = PartyForceTaskStatus.Executing;
            PartyExchangeExecutionResult result;
            string operationId = string.Format(
                "force:{0}:{1}:{2}:{3}",
                party.StringId,
                task.CreatedDay,
                task.Version,
                task.Intent);
            if (task.Intent == PartyForceIntent.Recruit)
            {
                result = _recruitment.Execute(
                    party, settlement, task.SourceKind, operationId);
            }
            else if (task.Intent == PartyForceIntent.WarRest)
            {
                result = ExecuteWarRest(party, task, operationId);
            }
            else
            {
                result = new PartyExchangeExecutionResult
                    { ReasonCode = "TaskHasNoArrivalOperation" };
            }
            task.Progress += Math.Max(0, result.TransferredCount);
            task.ReasonCode = result.ReasonCode;
            switch (result.Status)
            {
                case TransferCommitStatus.Committed:
                case TransferCommitStatus.PartiallyCommitted:
                    task.Status = PartyForceTaskStatus.Completed;
                    break;
                case TransferCommitStatus.RecoveryRequired:
                    task.Status = PartyForceTaskStatus.Suspended;
                    task.ReasonCode = "RecoveryRequired:" +
                        (result.ReasonCode ?? "Unknown");
                    break;
                default:
                    task.Status = PartyForceTaskStatus.Cancelled;
                    break;
            }
            task.OwnsMovementOrder = false;
            return result;
        }

        /// <summary>
        /// Gives a safe field surplus to an owned garrison through the same
        /// one-way model. This is not a bidirectional automatic swap.
        /// </summary>
        private static PartyExchangeExecutionResult TryRoutineDonation(
            MobileParty party,
            Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            int surplus = ForceManagementFacade.Current
                .GetSafeFieldSurplus(party);
            if (garrison == null || settlement.OwnerClan != party?.ActualClan ||
                surplus <= 0)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "NoSafeDonationSurplus" };
            PartyExchangeService runner = ForceManagementFacade.Current
                .PartyExchangeRunner;
            PartyTroopSource source = new PartyTroopSource
            {
                Kind = PartyTroopSourceKind.Mobile,
                Party = party,
                Settlement = settlement,
                Roster = party.MemberRoster
            };
            PartyTroopSource destination = new PartyTroopSource
            {
                Kind = PartyTroopSourceKind.Garrison,
                Party = garrison,
                Settlement = settlement,
                Roster = garrison.MemberRoster
            };
            PartyExchangeContext context = new PartyExchangeContext
            {
                OperationId = string.Format(
                    "visit:{0}:{1}:{2}",
                    party.StringId,
                    settlement.StringId,
                    (int)CampaignTime.Now.ToDays),
                Initiator = party,
                Operation = PartyExchangeOperation.FieldToGarrison,
                Intent = PartyForceIntent.Maintain,
                Purpose = PartyExchangePurpose.NaturalVisit,
                Source = source,
                Destination = destination,
                TargetSettlement = settlement,
                RequestedCount = surplus,
                Today = (int)CampaignTime.Now.ToDays
            };
            runner.PrepareContext(context);
            PartyExchangePlan plan = ForceManagementFacade.Current
                .PartyExchange.BuildPlan(context);
            return runner.Execute(plan);
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
            if (capture?.TryConsumeTroopDonationPermit(
                    party, settlement) != true)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "CapturePermitUnavailable" };

            MobileParty garrison = settlement?.Town?.GarrisonParty;
            int requested = ForceManagementFacade.Current
                .GetSafeFieldSurplus(party);
            if (garrison == null || requested <= 0)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "NoSafeDonationSurplus" };

            PartyExchangeService runner = ForceManagementFacade.Current
                .PartyExchangeRunner;
            PartyExchangeContext context = new PartyExchangeContext
            {
                OperationId = string.Format(
                    "capture:{0}:{1}:{2}",
                    party.StringId,
                    settlement.StringId,
                    (int)CampaignTime.Now.ToDays),
                Initiator = party,
                Operation = PartyExchangeOperation.FieldToGarrison,
                Intent = PartyForceIntent.Maintain,
                Purpose = PartyExchangePurpose.NewlyCapturedSettlement,
                Source = new PartyTroopSource
                {
                    Kind = PartyTroopSourceKind.Mobile,
                    Party = party,
                    Settlement = settlement,
                    Roster = party.MemberRoster
                },
                Destination = new PartyTroopSource
                {
                    Kind = PartyTroopSourceKind.Garrison,
                    Party = garrison,
                    Settlement = settlement,
                    Roster = garrison.MemberRoster
                },
                TargetSettlement = settlement,
                RequestedCount = requested,
                Today = (int)CampaignTime.Now.ToDays
            };
            runner.PrepareContext(context);
            PartyExchangePlan plan = ForceManagementFacade.Current
                .PartyExchange.BuildPlan(context);
            return runner.Execute(plan);
        }

        /// <summary>Rebuilds and executes the approved real fief batch return.</summary>
        private PartyExchangeExecutionResult ExecuteWarRest(
            MobileParty party,
            PartyForceTask task,
            string operationId)
        {
            PartyForceTargetResult target = ForceManagementFacade.Current
                .GetPartyTarget(party);
            WarRestDecision decision = ForceManagementFacade.Current.WarRest
                .Evaluate(ForceManagementFacade.Current.WarRestPlanning
                    .BuildContext(party, target, false));
            FiefBatchTransferApproval approval = decision.CandidateFiefBatches
                .FirstOrDefault(item => item.BatchId == task.SourceBatchId &&
                    item.SourceFief == task.TargetSettlement);
            PartyExchangeContext context = ForceManagementFacade.Current
                .WarRestPlanning.BuildReturnExchangeContext(
                    party, decision, approval, operationId);
            if (context == null)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "WarRestNoLongerApproved" };
            PartyExchangeService runner = ForceManagementFacade.Current
                .PartyExchangeRunner;
            runner.PrepareContext(context);
            PartyExchangePlan plan = ForceManagementFacade.Current
                .PartyExchange.BuildPlan(context);
            return runner.Execute(plan);
        }

        /// <summary>Runs peaceful demobilization only when no force task owns the visit.</summary>
        private static void TryPeaceDemobilization(
            MobileParty party,
            Settlement settlement)
        {
            if (!ForceManagementFacade.IsAtFormalWar(party?.ActualClan))
                Campaign.Current?.GetCampaignBehavior<DemobilizationBehavior>()?
                    .OnNaturalVisit(party, settlement);
        }

        /// <summary>Finds the newest unfinished task owned by this behavior.</summary>
        private PartyForceTask FindTask(MobileParty party) => _tasks
            .Where(task => task != null &&
                (task.Party == party || task.PartyId == party?.StringId) &&
                task.Status != PartyForceTaskStatus.Completed &&
                task.Status != PartyForceTaskStatus.Cancelled)
            .OrderByDescending(task => task.Version)
            .FirstOrDefault();

        /// <summary>Writes an explainable shadow decision using display names.</summary>
        private static void LogPlan(
            MobileParty party,
            PartyForcePlan plan)
        {
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
