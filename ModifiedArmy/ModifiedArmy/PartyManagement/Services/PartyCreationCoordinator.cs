using Helpers;
using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Missions;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.PartyManagement.Diagnostics;
using ModifiedArmy.PartyManagement.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.PartyManagement.Services
{
    /// <summary>Orchestrates creation; existing systems retain every ledger.</summary>
    public sealed class PartyCreationCoordinator
    {
        private readonly PartyCreationSettlementModel _sites =
            new PartyCreationSettlementModel();

        public MobileParty TryCreateManagedLordParty(Hero leader, bool isNewGame)
        {
            if (!ManagedPartyPolicy.IsModManagedAiLeader(leader)) return null;
            string attemptId = Guid.NewGuid().ToString("N");
            PartySettlementBindingBehavior bindings = Campaign.Current?
                .GetCampaignBehavior<PartySettlementBindingBehavior>();
            AiForcePlanningBehavior planning = Campaign.Current?
                .GetCampaignBehavior<AiForcePlanningBehavior>();
            var evaluations = new List<PartyCreationSiteResult>();
            PartyCreationSiteResult site = _sites.FindEligibleBase(
                leader, leader.Clan, 50, evaluations);
            // One representative candidate is enough to reconstruct a failed
            // decision. Logging every town and attached village on every probe
            // floods the file without adding a distinct transaction outcome.
            PartyCreationSiteResult candidate = evaluations
                .OrderByDescending(item => item.BindingEligible)
                .ThenByDescending(item => item.EstimatedCombinedRegulars)
                .ThenBy(item => item.Distance)
                .FirstOrDefault();
            if (candidate != null)
                PartyCreationDiagnostics.Log(leader, null,
                    candidate.Settlement?.StringId, "Evaluated",
                    candidate.RejectionReason,
                    candidate.EstimatedGarrisonRegulars,
                    candidate.EstimatedFiefRegulars,
                    candidate.EstimatedCombinedRegulars,
                    candidate.PreferredInitialSource.ToString(), attemptId,
                    candidate.MinimumRequired, candidate.MinimumFiefBatch,
                    candidate.BindingEligible);
            if (bindings == null || planning == null || site?.Settlement == null)
            {
                int bindingEligibleSites = evaluations.Count(item =>
                    item.BindingEligible);
                int supplyEligibleSites = evaluations.Count(item =>
                    item.EstimatedCombinedRegulars >= item.MinimumRequired);
                string rejectionCounts = string.Join(",", evaluations
                    .GroupBy(item => item.RejectionReason ?? "Unknown")
                    .Select(group => group.Key + ":" + group.Count()));
                PartyCreationDiagnostics.Log(leader, null, null, "Rejected",
                    bindings == null || planning == null
                        ? "CreationServicesUnavailable"
                        : evaluations.Count == 0
                        ? "NoEligibleClanSettlement"
                        : "NoEligibleCreationBase",
                    candidate?.EstimatedGarrisonRegulars ?? 0,
                    candidate?.EstimatedFiefRegulars ?? 0,
                    candidate?.EstimatedCombinedRegulars ?? 0,
                    candidate?.PreferredInitialSource.ToString() ?? "None",
                    attemptId: attemptId,
                    minimumRequired: candidate?.MinimumRequired ?? 0,
                    minimumFiefBatch: candidate?.MinimumFiefBatch ?? 0,
                    bindingEligible: candidate?.BindingEligible ?? false,
                    diagnosticDetails: string.Format(
                        "path=InitialFief evaluatedSiteCount={0} " +
                        "eligibleBindingSiteCount={1} eligibleSupplySiteCount={2} " +
                        "rejectionReasonCounts=\"{3}\" closestSite={4}",
                        evaluations.Count, bindingEligibleSites,
                        supplyEligibleSites, rejectionCounts,
                        candidate?.Settlement?.StringId ?? "none"));
                return null;
            }
            if (!bindings.TryReserveForCreation(
                    leader, leader.Clan, site.Settlement, out var token))
            {
                PartyCreationDiagnostics.Log(leader, null,
                    site.Settlement.StringId, "Rejected",
                    "CreationBaseReservationRejected",
                    site.EstimatedGarrisonRegulars,
                    site.EstimatedFiefRegulars,
                    site.EstimatedCombinedRegulars,
                    attemptId: attemptId,
                    minimumRequired: site.MinimumRequired,
                    minimumFiefBatch: site.MinimumFiefBatch,
                    bindingEligible: site.BindingEligible);
                return null;
            }

            MobileParty party = null;
            bool committed = false;
            try
            {
                if (leader.GovernorOf != null)
                    ChangeGovernorAction.RemoveGovernorOf(leader);
                using (PartyCreationScope.Begin(leader))
                    party = MobilePartyHelper.SpawnLordParty(
                        leader, site.Settlement);
                if (party == null)
                {
                    PartyCreationDiagnostics.Log(leader, null,
                        site.Settlement.StringId, "Failed",
                        "SpawnReturnedNull", site.EstimatedGarrisonRegulars,
                        site.EstimatedFiefRegulars,
                        site.EstimatedCombinedRegulars,
                        site.PreferredInitialSource.ToString(), attemptId);
                    return null;
                }
                if (party.Party.PartySizeLimit < 51)
                    return FailAfterCreation(bindings, planning, leader, party,
                        site, "PartyCapacityBelowFiftyRegulars", attemptId);
                if (!bindings.TryCommitCreation(token, party))
                    return FailAfterCreation(bindings, planning, leader, party, site,
                        "BindingCommitRejected", attemptId);
                committed = true;
                if (!planning.TryRegisterInitialRecruitTask(
                        party, site.Settlement, site.PreferredInitialSource,
                        attemptId))
                    return FailAfterCreation(bindings, planning, leader, party, site,
                        "InitialRecruitTaskRejected", attemptId);

                PartyForceTask initialTask = planning
                    .GetLatestTaskForCreation(party);
                int beforeArrivalRegulars = party.MemberRoster.TotalRegulars;
                EnterSettlementAction.ApplyForParty(party, site.Settlement);
                int afterArrivalRegulars = party.MemberRoster.TotalRegulars;
                int fiefRecruited = initialTask?.SourceKind ==
                        PartyForceSourceKind.Fief
                    ? Math.Max(0, initialTask.Progress) : 0;
                int professionalRecruited = initialTask?.SourceKind ==
                        PartyForceSourceKind.Professional
                    ? Math.Max(0, initialTask.Progress) : 0;
                PartyForceTask fallback = null;
                PartyExchangeExecutionResult fallbackResult = null;
                bool fallbackAttempted = false;
                if (party.MemberRoster.TotalRegulars <= 0 &&
                    initialTask?.SourceKind == PartyForceSourceKind.Fief &&
                    planning.TryRegisterInitialRecruitTask(
                        party, site.Settlement,
                        PartyForceSourceKind.Professional, attemptId, true))
                {
                    fallbackAttempted = true;
                    fallback = planning
                        .GetLatestTaskForCreation(party);
                    fallbackResult = planning
                        .ExecuteAtSettlement(party, site.Settlement);
                    professionalRecruited = Math.Max(
                        Math.Max(0, fallback?.Progress ?? 0),
                        Math.Max(0, fallbackResult?.TransferredCount ?? 0));
                }

                int finalRegulars = party.MemberRoster.TotalRegulars;
                int recruited = fiefRecruited + professionalRecruited;
                PartyForceTask finalTask = planning
                    .GetLatestTaskForCreation(party);
                if (finalTask?.Status == PartyForceTaskStatus.Suspended &&
                    finalTask.ReasonCode?.StartsWith(
                        "RecoveryRequired:", StringComparison.Ordinal) == true)
                {
                    PartyCreationDiagnostics.Log(leader, party,
                        site.Settlement.StringId, "RecoveryRequired",
                        finalTask.ReasonCode,
                        site.EstimatedGarrisonRegulars,
                        site.EstimatedFiefRegulars,
                        site.EstimatedCombinedRegulars,
                        site.PreferredInitialSource.ToString(), attemptId,
                        site.MinimumRequired, site.MinimumFiefBatch,
                        site.BindingEligible, finalTask.TaskId,
                        PartyVisitExecutionService.BuildOperationId(
                            party, finalTask), recruited, fiefRecruited,
                        professionalRecruited, finalRegulars,
                        "RecoveryRequired");
                    return party;
                }
                if (finalRegulars <= 0)
                    return FailAfterCreation(bindings, planning, leader, party,
                        site, "InitialRecruitmentCommittedZeroTroops",
                        attemptId, finalTask, fiefRecruited,
                        professionalRecruited, initialTask, fallback,
                        fallbackResult, beforeArrivalRegulars,
                        afterArrivalRegulars, fallbackAttempted);
                PartyCreationDiagnostics.Log(leader, party,
                    site.Settlement.StringId, "Completed", site.ReasonCode,
                    site.EstimatedGarrisonRegulars,
                    site.EstimatedFiefRegulars,
                    site.EstimatedCombinedRegulars,
                    site.PreferredInitialSource.ToString(), attemptId,
                    site.MinimumRequired, site.MinimumFiefBatch,
                    site.BindingEligible, finalTask?.TaskId,
                    finalTask == null ? null : PartyVisitExecutionService
                        .BuildOperationId(party, finalTask),
                    recruited, fiefRecruited, professionalRecruited,
                    finalRegulars, "Completed");
                return party;
            }
            catch (Exception exception)
            {
                if (party?.IsActive == true)
                {
                    planning.CancelTask(party, "CreationTransactionException");
                    bindings.ReleasePartyBindings(
                        party, "CreationTransactionException");
                    DestroyPartyAction.Apply(null, party);
                }
                PartyCreationDiagnostics.Log(leader, party,
                    site.Settlement.StringId, "Failed",
                    "CreationTransactionException:" + exception.GetType().Name,
                    site.EstimatedGarrisonRegulars,
                    site.EstimatedFiefRegulars,
                    site.EstimatedCombinedRegulars,
                    attemptId: attemptId);
                return null;
            }
            finally
            {
                if (!committed) bindings.CancelCreationReservation(token);
            }
        }

        private static MobileParty FailAfterCreation(
            PartySettlementBindingBehavior bindings,
            AiForcePlanningBehavior planning, Hero leader,
            MobileParty party, PartyCreationSiteResult site, string reason,
            string attemptId, PartyForceTask task = null,
            int fiefRecruited = 0, int professionalRecruited = 0,
            PartyForceTask initialTask = null,
            PartyForceTask fallbackTask = null,
            PartyExchangeExecutionResult fallbackResult = null,
            int beforeArrivalRegulars = 0,
            int afterArrivalRegulars = 0,
            bool fallbackAttempted = false)
        {
            int beforeDestroyRegulars =
                party?.MemberRoster?.TotalRegulars ?? 0;
            string details = string.Format(
                "path=InitialFief initialTaskId={0} initialStatus={1} " +
                "initialReason={2} initialProgress={3} beforeArrivalRegulars={4} " +
                "afterArrivalRegulars={5} fallbackAttempted={6} " +
                "fallbackTaskId={7} fallbackStatus={8} fallbackReason={9} " +
                "fallbackProgress={10} fallbackExecutorReason={11} " +
                "fallbackApproved={12} fallbackTransferred={13} " +
                "beforeDestroyRegulars={14} finalFailureStage={15}",
                initialTask?.TaskId ?? "none",
                initialTask?.Status.ToString() ?? "NotRegistered",
                initialTask?.ReasonCode ?? "Unavailable",
                initialTask?.Progress ?? 0, beforeArrivalRegulars,
                afterArrivalRegulars, fallbackAttempted,
                fallbackTask?.TaskId ?? "none",
                fallbackTask?.Status.ToString() ?? "NotRegistered",
                fallbackTask?.ReasonCode ?? "Unavailable",
                fallbackTask?.Progress ?? 0,
                fallbackResult?.ReasonCode ?? "Unavailable",
                fallbackResult?.ApprovedCount ?? 0,
                fallbackResult?.TransferredCount ?? 0,
                beforeDestroyRegulars, reason);
            PartyCreationDiagnostics.Log(leader, party,
                site.Settlement.StringId, "Failed", reason,
                site.EstimatedGarrisonRegulars,
                site.EstimatedFiefRegulars,
                site.EstimatedCombinedRegulars,
                attemptId: attemptId,
                taskId: task?.TaskId,
                operationId: task == null ? null :
                    PartyVisitExecutionService.BuildOperationId(party, task),
                actualRecruited: fiefRecruited + professionalRecruited,
                actualFief: fiefRecruited,
                actualProfessional: professionalRecruited,
                finalRegulars: beforeDestroyRegulars,
                finalState: "Failed",
                diagnosticDetails: details);
            planning.CancelTask(party, reason);
            bindings.ReleasePartyBindings(party, reason);
            if (party?.IsActive == true) DestroyPartyAction.Apply(null, party);
            return null;
        }

    }
}
