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
                PartyCreationDiagnostics.Log(leader, null, null, "Rejected",
                    bindings == null || planning == null
                        ? "CreationServicesUnavailable"
                        : evaluations.Count == 0
                        ? "NoEligibleClanSettlement"
                        : "NoEligibleCreationBase", 0, 0, 0,
                    attemptId: attemptId);
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
                EnterSettlementAction.ApplyForParty(party, site.Settlement);
                int fiefRecruited = initialTask?.SourceKind ==
                        PartyForceSourceKind.Fief
                    ? Math.Max(0, initialTask.Progress) : 0;
                int professionalRecruited = initialTask?.SourceKind ==
                        PartyForceSourceKind.Professional
                    ? Math.Max(0, initialTask.Progress) : 0;
                if (party.MemberRoster.TotalRegulars <= 0 &&
                    initialTask?.SourceKind == PartyForceSourceKind.Fief &&
                    planning.TryRegisterInitialRecruitTask(
                        party, site.Settlement,
                        PartyForceSourceKind.Professional, attemptId, true))
                {
                    PartyForceTask fallback = planning
                        .GetLatestTaskForCreation(party);
                    PartyExchangeExecutionResult fallbackResult = planning
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
                        professionalRecruited);
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
            int fiefRecruited = 0, int professionalRecruited = 0)
        {
            planning.CancelTask(party, reason);
            bindings.ReleasePartyBindings(party, reason);
            if (party?.IsActive == true) DestroyPartyAction.Apply(null, party);
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
                finalRegulars: party?.MemberRoster?.TotalRegulars ?? 0,
                finalState: "Failed");
            return null;
        }

    }
}
