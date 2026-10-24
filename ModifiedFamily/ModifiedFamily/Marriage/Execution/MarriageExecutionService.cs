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
            if (!valid.IsAllowed || valid.TemporaryBlockers.Count > 0) { FamilyReasonCode reason = valid.TemporaryBlockers.Count > 0 ? valid.TemporaryBlockers[0] : valid.ReasonCode; FamilyDiagnostics.DebugLog(FamilyLogText.MarriageWaiting(proposal, reason)); return valid; }
            FamilyDiagnostics.Info(FamilyLogText.MarriageStarting(proposal));
            proposal.Status = FamilyProposalStatus.Executing;
            try
            {
                // Vanilla MarriageAction owns spouse links, party cleanup, relations, and clan movement.
                using (MarriageExecutionScope.Enter(proposal)) MarriageAction.Apply(proposal.HeroA, proposal.HeroB, false);
                if (proposal.HeroA.Spouse != proposal.HeroB || proposal.HeroB.Spouse != proposal.HeroA || proposal.HeroA.Clan != proposal.DestinationClan || proposal.HeroB.Clan != proposal.DestinationClan)
                {
                    proposal.Status = FamilyProposalStatus.ExecutionFailure; proposal.ReasonCode = FamilyReasonCode.ExecutionVerificationFailed;
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
                        FamilyDiagnostics.Warn(FamilyLogText.PaymentUncertain(proposal));
                        return FamilyResult.Deny(proposal.ReasonCode);
                    }
                }
                proposal.ExecutionStep = FamilyExecutionStep.GoldSettled;
                proposal.Status = FamilyProposalStatus.Completed; proposal.ExecutionStep = FamilyExecutionStep.Completed;
                FamilyDiagnostics.Notice(FamilyLogText.MarriageCompleted(proposal));
                return FamilyResult.Allow(proposal.ProposalId);
            }
            catch (Exception ex)
            {
                MarriageExecutionScope.Clear(); proposal.Status = FamilyProposalStatus.ExecutionFailure; proposal.ReasonCode = FamilyReasonCode.ExecutionVerificationFailed;
                FamilyDiagnostics.Error(FamilyLogText.MarriageFailed(proposal), ex); return FamilyResult.Deny(proposal.ReasonCode);
            }
        }
    }
}
