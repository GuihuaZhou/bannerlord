// Executes an accepted agreement through vanilla actions and verifies every observable result.
using System;
using ModifiedFamily.Core;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Proposals;
using ModifiedFamily.Localization;
using ModifiedFamily.Marriage.Models;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedFamily.Marriage.Execution
{
    public sealed class MarriageExecutionService
    {
        private readonly MarriageProposalModel _proposalModel;
        public MarriageExecutionService(MarriageProposalModel proposalModel) { _proposalModel = proposalModel; }

        public FamilyResult TryExecuteMarriage(FamilyProposal proposal)
        {
            var valid = _proposalModel.CanExecuteMarriage(proposal);
            if (!valid.IsAllowed || valid.TemporaryBlockers.Count > 0)
            {
                FamilyReasonCode reason = valid.TemporaryBlockers.Count > 0 ? valid.TemporaryBlockers[0] : valid.ReasonCode;
                if (MarriageDiagnosticTrace.IsPlayerRelated(proposal))
                    MarriageDiagnosticTrace.Write("MARRIAGE_EXECUTION_DEFERRED",
                    MarriageDiagnosticTrace.F("proposalId", proposal?.ProposalId),
                    MarriageDiagnosticTrace.F("chainId", proposal?.ChainId),
                    MarriageDiagnosticTrace.F("reason", reason),
                    MarriageDiagnosticTrace.F("temporaryBlockers", MarriageDiagnosticTrace.Blockers(valid)),
                    MarriageDiagnosticTrace.F("status", proposal?.Status.ToString() ?? "none"),
                    MarriageDiagnosticTrace.F("executionStep", proposal?.ExecutionStep.ToString() ?? "none"));
                // Funds and transient hero activity may recover. Permanent failures are returned to the lifecycle owner for invalidation.
                if (valid.TemporaryBlockers.Count > 0 || reason == FamilyReasonCode.InsufficientFunds)
                    FamilyDiagnostics.DebugLog(FamilyLogText.MarriageWaiting(proposal, reason));
                return valid;
            }
            FamilyDiagnostics.Info(FamilyLogText.MarriageStarting(proposal));
            proposal.Status = FamilyProposalStatus.Executing;
            try
            {
                // Vanilla MarriageAction owns spouse links, party cleanup, relations, and clan movement.
                using (MarriageExecutionScope.Enter(proposal)) MarriageAction.Apply(proposal.HeroA, proposal.HeroB, false);
                if (proposal.HeroA.Spouse != proposal.HeroB || proposal.HeroB.Spouse != proposal.HeroA || proposal.HeroA.Clan != proposal.DestinationClan || proposal.HeroB.Clan != proposal.DestinationClan)
                {
                    proposal.Status = FamilyProposalStatus.ExecutionFailure; proposal.ReasonCode = FamilyReasonCode.ExecutionVerificationFailed;
                    MarriageDiagnosticTrace.ProposalEvent("MARRIAGE_EXECUTION_FAILED",
                        proposal, FamilyProposalStatus.Executing,
                        FamilyResult.Deny(proposal.ReasonCode), "MarriageVerificationFailed");
                    return FamilyResult.Deny(proposal.ReasonCode);
                }
                proposal.ExecutionStep = FamilyExecutionStep.MarriageApplied;
                if (proposal.GiftAmount > 0)
                {
                    // GiveGoldAction may silently clamp payment, so compare both balances afterwards.
                    var payer = proposal.GoldPayerClan.Leader; var receiver = proposal.GoldReceiverClan.Leader;
                    int payerBefore = payer.Gold, receiverBefore = receiver.Gold;
                    GiveGoldAction.ApplyBetweenCharacters(payer, receiver, proposal.GiftAmount, true);
                    if (payerBefore - payer.Gold != proposal.GiftAmount || receiver.Gold - receiverBefore != proposal.GiftAmount)
                    {
                        // Never retry an uncertain payment automatically; doing so could charge twice.
                        proposal.Status = FamilyProposalStatus.PaymentReconcileRequired; proposal.ReasonCode = FamilyReasonCode.PaymentUncertain;
                        MarriageDiagnosticTrace.ProposalEvent("MARRIAGE_EXECUTION_FAILED",
                            proposal, FamilyProposalStatus.Executing,
                            FamilyResult.Deny(proposal.ReasonCode), "PaymentVerificationFailed");
                        FamilyDiagnostics.Warn(FamilyLogText.PaymentUncertain(proposal));
                        return FamilyResult.Deny(proposal.ReasonCode);
                    }
                }
                proposal.ExecutionStep = FamilyExecutionStep.GoldSettled;
                proposal.Status = FamilyProposalStatus.Completed; proposal.ExecutionStep = FamilyExecutionStep.Completed;
                MarriageDiagnosticTrace.ProposalEvent("MARRIAGE_EXECUTED",
                    proposal, FamilyProposalStatus.Executing,
                    FamilyResult.Allow(proposal.ProposalId), "Completed");
                FamilyDiagnostics.Notice(FamilyLogText.MarriageCompleted(proposal));
                return FamilyResult.Allow(proposal.ProposalId);
            }
            catch (Exception ex)
            {
                MarriageExecutionScope.Clear(); proposal.Status = FamilyProposalStatus.ExecutionFailure; proposal.ReasonCode = FamilyReasonCode.ExecutionVerificationFailed;
                MarriageDiagnosticTrace.ProposalEvent("MARRIAGE_EXECUTION_FAILED",
                    proposal, FamilyProposalStatus.Executing,
                    FamilyResult.Deny(proposal.ReasonCode), "Exception");
                FamilyDiagnostics.Error(FamilyLogText.MarriageFailed(proposal), ex); return FamilyResult.Deny(proposal.ReasonCode);
            }
        }
    }
}
