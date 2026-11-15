// Initial mobilization transaction coordinator. Existing roster, Hearth,
// service-batch, budget, and journal owners perform every authoritative write.
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Models.Fief;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.PartyManagement.Services
{
    internal sealed class InitialPartyMobilizationCommitter
    {
        internal InitialMobilizationCommitResult Commit(
            MobileParty party,
            InitialMobilizationPreview preview)
        {
            if (party == null || preview?.Approved != true ||
                party.ActualClan != preview.Clan ||
                party.CurrentSettlement != preview.BaseSettlement)
                return Reject("PartyInitializationMismatch");
            if (party.Party.PartySizeLimit != preview.AssumedPartyLimit ||
                party.PaymentLimit != preview.PaymentLimit ||
                party.Party.NumberOfAllMembers != 1 ||
                party.MemberRoster.TotalRegulars != 0)
                return Reject("PartyInitializationMismatch");
            string liveFingerprint = InitialPartyMobilizationPlanner
                .BuildSourceFingerprint(
                    preview.BaseSettlement, preview.FiefByVillage);
            if (liveFingerprint != preview.SourceFingerprint)
                return Reject("SourceSnapshotChanged");

            string prefix = "party-create:" + preview.AttemptId;
            PartyExchangeService exchange = ForceManagementFacade.Current
                .PartyExchangeRunner;
            MobileParty garrison = preview.BaseSettlement?.Town?.GarrisonParty;
            PartyExchangeExecutionResult garrisonResult = null;
            PartyRosterSnapshot garrisonBefore = null;
            PartyRosterSnapshot partyBefore = null;
            if (preview.ApprovedGarrisonCount > 0)
            {
                var source = new PartyTroopSource
                {
                    Kind = PartyTroopSourceKind.Garrison,
                    Party = garrison,
                    Settlement = preview.BaseSettlement
                };
                var destination = new PartyTroopSource
                {
                    Kind = PartyTroopSourceKind.Mobile,
                    Party = party,
                    Settlement = preview.BaseSettlement
                };
                garrisonBefore = exchange.CaptureSnapshot(source);
                partyBefore = exchange.CaptureSnapshot(destination);
                var plan = new PartyExchangePlan
                {
                    Initiator = party,
                    AttemptId = preview.AttemptId,
                    OperationId = prefix + ":garrison",
                    ShouldExecute = true,
                    ReasonCode = "Approved",
                    Operation = PartyExchangeOperation.GarrisonToField,
                    Purpose = PartyExchangePurpose.NaturalVisit,
                    Executor = PartyExchangeExecutorKind.MobileRosterTransfer,
                    RequestedCount = preview.ApprovedGarrisonCount,
                    Source = source,
                    Destination = destination,
                    TargetSettlement = preview.BaseSettlement,
                    ExpectedSource = garrisonBefore,
                    ExpectedDestination = partyBefore,
                    ExpectedSourceFinalCount = Math.Max(0,
                        garrisonBefore.RegularCount -
                        preview.ApprovedGarrisonCount),
                    ExpectedDestinationFinalCount =
                        partyBefore.RegularCount +
                        preview.ApprovedGarrisonCount
                };
                foreach (var item in preview.GarrisonManifest.Troops)
                    plan.SourceToDestination.Troops[item.Key] =
                        new TroopExchangeAmount
                        {
                            Count = item.Value.Count,
                            WoundedCount = item.Value.WoundedCount
                        };
                garrisonResult = exchange.Execute(plan);
                if (garrisonResult.Status != TransferCommitStatus.Committed)
                    return new InitialMobilizationCommitResult
                    {
                        Status = garrisonResult.Status ==
                            TransferCommitStatus.RecoveryRequired
                            ? InitialMobilizationCommitStatus.RecoveryRequired
                            : InitialMobilizationCommitStatus.Rejected,
                        ReasonCode = garrisonResult.ReasonCode
                    };
            }

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            int fiefCount = 0;
            bool fiefRecovery = false;
            string fiefReason = "FiefManagerUnavailable";
            bool fiefCommitted = manager?.CommitPlannedCreationRecruitment(
                preview.BaseSettlement, party, preview.FiefByVillage,
                prefix + ":fief", out fiefCount,
                out fiefRecovery, out fiefReason) == true;
            if (!fiefCommitted)
            {
                if (preview.ApprovedGarrisonCount <= 0)
                    return new InitialMobilizationCommitResult
                    {
                        Status = fiefRecovery
                            ? InitialMobilizationCommitStatus.RecoveryRequired
                            : InitialMobilizationCommitStatus.RolledBack,
                        ReasonCode = fiefReason
                    };
                PartyExchangeExecutionResult compensation = exchange
                    .CompensateCreationGarrisonWithdrawal(
                        garrison, party, preview.GarrisonManifest,
                        prefix + ":garrison-compensation",
                        garrisonBefore.Fingerprint, partyBefore.Fingerprint);
                return new InitialMobilizationCommitResult
                {
                    Status = fiefRecovery || compensation.Status ==
                        TransferCommitStatus.RecoveryRequired
                        ? InitialMobilizationCommitStatus.RecoveryRequired
                        : InitialMobilizationCommitStatus.RolledBack,
                    ReasonCode = fiefReason + ":" + compensation.ReasonCode,
                    CommittedGarrisonCount = compensation.Status ==
                        TransferCommitStatus.RolledBack ? 0 :
                        preview.ApprovedGarrisonCount
                };
            }

            int finalRegulars = party.MemberRoster.TotalRegulars;
            if (fiefCount != preview.ApprovedFiefCount ||
                finalRegulars != preview.ApprovedTotal ||
                finalRegulars < preview.RequiredRegulars)
                return new InitialMobilizationCommitResult
                {
                    Status = InitialMobilizationCommitStatus.RecoveryRequired,
                    ReasonCode = "FinalConsistencyMismatch",
                    CommittedFiefCount = fiefCount,
                    CommittedGarrisonCount =
                        garrisonResult?.TransferredCount ?? 0
                };
            return new InitialMobilizationCommitResult
            {
                Status = InitialMobilizationCommitStatus.Committed,
                ReasonCode = "Committed",
                CommittedFiefCount = fiefCount,
                CommittedGarrisonCount =
                    garrisonResult?.TransferredCount ?? 0
            };
        }

        private static InitialMobilizationCommitResult Reject(string reason) =>
            new InitialMobilizationCommitResult
            {
                Status = InitialMobilizationCommitStatus.Rejected,
                ReasonCode = reason
            };
    }
}
