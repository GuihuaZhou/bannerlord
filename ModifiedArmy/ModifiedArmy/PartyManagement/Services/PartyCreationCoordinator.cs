// AI lord-party creation coordinator. The preview proves only site eligibility;
// the spawned party and normal live recruitment paths own all final decisions.
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
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Services
{
    /// <summary>Selects by read-only approval, then synchronously mobilizes once.</summary>
    public sealed class PartyCreationCoordinator
    {
        private const int MinimumPartyMembers = 50;
        private readonly PartyCreationSettlementModel _sites = new PartyCreationSettlementModel();

        public MobileParty TryCreateManagedLordParty(Hero leader, bool isNewGame)
        {
            if (!ManagedPartyPolicy.IsModManagedAiLeader(leader)) return null;
            string attemptId = Guid.NewGuid().ToString("N");
            PartySettlementBindingBehavior bindings = Campaign.Current?.GetCampaignBehavior<PartySettlementBindingBehavior>();
            AiForcePlanningBehavior planning = Campaign.Current?.GetCampaignBehavior<AiForcePlanningBehavior>();
            var evaluations = new List<PartyCreationSiteResult>();
            int requiredNewTroops = Math.Max(0, MinimumPartyMembers -
                PlannedLordPartySubjectFactory.InitialMemberCount);
            _sites.FindEligibleBase(
                leader, leader.Clan, requiredNewTroops, evaluations);
            PartyCreationSiteResult representative = evaluations
                .OrderByDescending(item => item.BindingEligible)
                .ThenByDescending(item => item.EstimatedCombinedRegulars)
                .ThenBy(item => item.Distance).FirstOrDefault();
            if (representative != null)
                LogSite(leader, representative, attemptId, "Evaluated", representative.RejectionReason);
            if (bindings == null || planning == null)
            {
                LogNoCandidate(leader, representative, evaluations, attemptId, "CreationServicesUnavailable");
                return null;
            }

            PendingPartyCreation selectedToken = null;
            PartyCreationSiteResult selectedSite = null;
            InitialMobilizationPreview selectedPreview = null;
            foreach (PartyCreationSiteResult candidate in evaluations
                .Where(item => item?.Settlement != null && item.BindingEligible)
                .OrderByDescending(item => item.EstimatedCombinedRegulars)
                .ThenBy(item => item.Distance)
                .ThenBy(item => item.Settlement.StringId, StringComparer.Ordinal))
            {
                if (!bindings.TryReserveForCreation(leader, leader.Clan,
                        candidate.Settlement, out PendingPartyCreation token))
                    continue;
                InitialMobilizationPreview preview = RunSingleDryRun(
                    leader, candidate.Settlement, token, attemptId);
                if (preview?.Approved != true)
                {
                    bindings.CancelCreationReservation(token);
                    continue;
                }
                if (selectedPreview == null || IsBetter(preview, candidate,
                        selectedPreview, selectedSite))
                {
                    if (selectedToken != null) bindings.CancelCreationReservation(selectedToken);
                    selectedToken = token;
                    selectedSite = candidate;
                    selectedPreview = preview;
                }
                else bindings.CancelCreationReservation(token);
            }
            if (selectedPreview == null)
            {
                LogNoCandidate(leader, representative, evaluations, attemptId, "NoApprovedCreationBase");
                return null;
            }

            MobileParty party = null;
            bool bindingCommitted = false;
            bool liveRecruitmentStarted = false;
            try
            {
                if (leader.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(leader);
                using (PartyCreationScope.Begin(leader))
                    party = MobilePartyHelper.SpawnLordParty(leader, selectedSite.Settlement);
                if (party == null)
                    return Fail(bindings, planning, leader, party, selectedToken,
                        selectedSite, selectedPreview, attemptId, "SpawnReturnedNull", false);

                if (!bindings.TryCommitCreation(selectedToken, party))
                    return Fail(bindings, planning, leader, party, selectedToken,
                        selectedSite, selectedPreview, attemptId, "BindingCommitRejected", false);
                bindingCommitted = true;

                using (PartyCreationInitializationScope.Begin(party))
                    EnterSettlementAction.ApplyForParty(party, selectedSite.Settlement);
                liveRecruitmentStarted = true;
                InitialMobilizationCommitResult result =
                    new InitialPartyMobilizationCommitter().Commit(party, selectedPreview);
                LogCommit(leader, party, selectedSite, selectedPreview, result, attemptId);
                // A live shortfall is ordinary replenishment work. Once the
                // party is validly spawned and bound, keep it regardless of
                // how much the current sources can provide.
                return party;
            }
            catch (Exception exception)
            {
                string reason = "CreationTransactionException:" + exception.GetType().Name;
                bool retain = liveRecruitmentStarted;
                LogException(leader, party, selectedSite, selectedPreview, attemptId, reason, retain);
                if (retain) return party;
                if (party?.IsActive == true)
                {
                    planning.CancelTask(party, reason);
                    if (bindingCommitted) bindings.ReleasePartyBindings(party, reason);
                    DestroyPartyAction.Apply(null, party);
                }
                return null;
            }
            finally
            {
                if (!bindingCommitted) bindings.CancelCreationReservation(selectedToken);
            }
        }

        /// <summary>
        /// Runs one production preview and proves only that the preview did not
        /// mutate authoritative resources. Determinism belongs to dedicated
        /// tests and is deliberately not inferred from a single invocation.
        /// </summary>
        private static InitialMobilizationPreview RunSingleDryRun(Hero leader,
            Settlement settlement, PendingPartyCreation token, string attemptId)
        {
            PartyCreationDryRunDiagnostics.ResourceSnapshot before = null;
            PartyCreationDryRunDiagnostics.ResourceSnapshot after = null;
            InitialMobilizationPreview first = null;
            bool noMutation = false;
            string failure = null;
            string stage = "CaptureBefore";
            try
            {
                before = PartyCreationDryRunDiagnostics.Capture(leader, settlement);
                var planner = new InitialPartyMobilizationPlanner();
                stage = "DryRun";
                first = planner.DryRun(leader, leader.Clan, settlement, token,
                    attemptId, MinimumPartyMembers);
                stage = "CaptureAfter";
                after = PartyCreationDryRunDiagnostics.Capture(leader, settlement);
                noMutation = before.SameAs(after);
            }
            catch (Exception exception)
            {
                failure = stage + ":" + exception.ToString().Replace('\r', ' ').Replace('\n', ' ');
                after = PartyCreationDryRunDiagnostics.Capture(leader, settlement);
                noMutation = before?.SameAs(after) == true;
            }
            PartyCreationDryRunDiagnostics.Log(leader, settlement, attemptId,
                first, null, noMutation, before, after, failure);
            return noMutation && string.IsNullOrEmpty(failure) ? first : null;
        }

        private static bool IsBetter(InitialMobilizationPreview candidate,
            PartyCreationSiteResult candidateSite, InitialMobilizationPreview current,
            PartyCreationSiteResult currentSite) =>
            candidate.ApprovedTotal > current.ApprovedTotal ||
            candidate.ApprovedTotal == current.ApprovedTotal &&
            (candidateSite.Distance < currentSite.Distance ||
             candidateSite.Distance.Equals(currentSite.Distance) &&
             string.Compare(candidateSite.Settlement.StringId,
                 currentSite.Settlement.StringId, StringComparison.Ordinal) < 0);

        private static MobileParty Fail(PartySettlementBindingBehavior bindings,
            AiForcePlanningBehavior planning, Hero leader, MobileParty party,
            PendingPartyCreation token, PartyCreationSiteResult site,
            InitialMobilizationPreview preview, string attemptId, string reason,
            bool bindingCommitted, string diagnosticDetails = null)
        {
            PartyCreationDiagnostics.Log(leader, party, site.Settlement.StringId,
                "Failed", reason, preview.ApprovedGarrisonCount,
                preview.ApprovedFiefCount, preview.ApprovedTotal,
                attemptId: attemptId,
                requiredNewTroops: preview.RequiredNewTroops,
                minimumPartyMembers: preview.MinimumPartyMembers,
                initialMemberCount: preview.InitialMemberCount,
                bindingEligible: true,
                actualRecruited: party?.MemberRoster.TotalRegulars ?? 0,
                finalRegulars: party?.MemberRoster.TotalRegulars ?? 0,
                finalTotalMembers: party?.Party.NumberOfAllMembers ?? 0,
                finalState: "RolledBack",
                diagnosticDetails: diagnosticDetails);
            if (party?.IsActive == true)
            {
                planning.CancelTask(party, reason);
                if (bindingCommitted) bindings.ReleasePartyBindings(party, reason);
                DestroyPartyAction.Apply(null, party);
            }
            else bindings.CancelCreationReservation(token);
            return null;
        }

        private static void LogCommit(Hero leader, MobileParty party,
            PartyCreationSiteResult site, InitialMobilizationPreview preview,
            InitialMobilizationCommitResult result, string attemptId)
        {
            string phase = result.Status == InitialMobilizationCommitStatus.Committed
                ? "Completed" : "Failed";
            PartyCreationDiagnostics.Log(leader, party, site.Settlement.StringId,
                phase, result.ReasonCode, preview.ApprovedGarrisonCount,
                preview.ApprovedFiefCount, preview.ApprovedTotal,
                attemptId: attemptId,
                requiredNewTroops: preview.RequiredNewTroops,
                minimumPartyMembers: preview.MinimumPartyMembers,
                initialMemberCount: preview.InitialMemberCount,
                bindingEligible: true,
                actualRecruited: result.CommittedFiefCount +
                    result.CommittedGarrisonCount +
                    result.CommittedMercenaryCount,
                actualFief: result.CommittedFiefCount,
                actualProfessional: result.CommittedGarrisonCount,
                finalRegulars: party?.MemberRoster.TotalRegulars ?? 0,
                finalTotalMembers: party?.Party.NumberOfAllMembers ?? 0,
                finalState: result.Status.ToString(),
                diagnosticDetails: string.Format(
                    "[PartyCreationCommit] approvedFief={0} approvedGarrison={1} " +
                    "committedFief={2} committedGarrison={3} " +
                    "committedMercenary={4} status={5}",
                    preview.ApprovedFiefCount, preview.ApprovedGarrisonCount,
                    result.CommittedFiefCount, result.CommittedGarrisonCount,
                    result.CommittedMercenaryCount, result.Status));
        }

        private static void LogException(Hero leader, MobileParty party,
            PartyCreationSiteResult site, InitialMobilizationPreview preview,
            string attemptId, string reason, bool retained) =>
            PartyCreationDiagnostics.Log(leader, party, site.Settlement.StringId,
                retained ? "Completed" : "Failed", reason,
                preview.ApprovedGarrisonCount, preview.ApprovedFiefCount,
                preview.ApprovedTotal, attemptId: attemptId,
                requiredNewTroops: preview.RequiredNewTroops,
                minimumPartyMembers: preview.MinimumPartyMembers,
                initialMemberCount: preview.InitialMemberCount,
                bindingEligible: true,
                finalRegulars: party?.MemberRoster.TotalRegulars ?? 0,
                finalTotalMembers: party?.Party.NumberOfAllMembers ?? 0,
                finalState: retained
                    ? "RetainedAfterRecruitmentException"
                    : "Failed",
                diagnosticDetails: "[PartyCreationCommit] exception=true");

        private static void LogSite(Hero leader, PartyCreationSiteResult site,
            string attemptId, string phase, string reason) =>
            PartyCreationDiagnostics.Log(leader, null, site.Settlement?.StringId,
                phase, reason, site.EstimatedGarrisonRegulars,
                site.EstimatedFiefRegulars, site.EstimatedCombinedRegulars,
                site.PreferredInitialSource.ToString(), attemptId,
                requiredNewTroops: site.MinimumRequired,
                minimumFiefBatch: site.MinimumFiefBatch,
                bindingEligible: site.BindingEligible);

        private static void LogNoCandidate(Hero leader,
            PartyCreationSiteResult representative,
            IReadOnlyCollection<PartyCreationSiteResult> evaluations,
            string attemptId, string reason) =>
            PartyCreationDiagnostics.Log(leader, null, null, "Rejected", reason,
                representative?.EstimatedGarrisonRegulars ?? 0,
                representative?.EstimatedFiefRegulars ?? 0,
                representative?.EstimatedCombinedRegulars ?? 0,
                representative?.PreferredInitialSource.ToString() ?? "None",
                attemptId: attemptId,
                requiredNewTroops: representative?.MinimumRequired ??
                    Math.Max(0, MinimumPartyMembers -
                        PlannedLordPartySubjectFactory.InitialMemberCount),
                minimumFiefBatch: representative?.MinimumFiefBatch ?? 0,
                bindingEligible: representative?.BindingEligible ?? false,
                diagnosticDetails: string.Format(
                    "evaluatedSiteCount={0} rejectionReasonCounts=\"{1}\"",
                    evaluations.Count, string.Join(",", evaluations
                        .GroupBy(item => item.RejectionReason ?? "Unknown")
                        .Select(group => group.Key + ":" + group.Count()))));
    }
}
