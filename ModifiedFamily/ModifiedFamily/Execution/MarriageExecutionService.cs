using System;
using ModifiedFamily.Core;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedFamily.Execution
{
    public sealed class MarriageExecutionService
    {
        private readonly FamilyProposalValidator _validator;
        public MarriageExecutionService(FamilyProposalValidator validator) { _validator = validator; }

        public FamilyResult TryExecuteMarriage(FamilyProposal proposal)
        {
            var valid = _validator.CanExecuteMarriage(proposal);
            if (!valid.IsAllowed || valid.TemporaryBlockers.Count > 0) return valid;
            proposal.Status = FamilyProposalStatus.Executing;
            try
            {
                using (MarriageExecutionScope.Enter(proposal)) MarriageAction.Apply(proposal.HeroA, proposal.HeroB, false);
                if (proposal.HeroA.Spouse != proposal.HeroB || proposal.HeroB.Spouse != proposal.HeroA || proposal.HeroA.Clan != proposal.DestinationClan || proposal.HeroB.Clan != proposal.DestinationClan)
                {
                    proposal.Status = FamilyProposalStatus.ExecutionFailure; proposal.ReasonCode = FamilyReasonCode.ExecutionVerificationFailed;
                    return FamilyResult.Deny(proposal.ReasonCode);
                }
                proposal.ExecutionStep = FamilyExecutionStep.MarriageApplied;
                if (proposal.GiftAmount > 0)
                {
                    var payer = proposal.GoldPayerClan.Leader; var receiver = proposal.GoldReceiverClan.Leader;
                    int payerBefore = payer.Gold, receiverBefore = receiver.Gold;
                    GiveGoldAction.ApplyBetweenCharacters(payer, receiver, proposal.GiftAmount, true);
                    if (payerBefore - payer.Gold != proposal.GiftAmount || receiver.Gold - receiverBefore != proposal.GiftAmount)
                    {
                        proposal.Status = FamilyProposalStatus.PaymentReconcileRequired; proposal.ReasonCode = FamilyReasonCode.PaymentUncertain;
                        FamilyDiagnostics.Warn("Payment reconciliation required for " + proposal.ProposalId);
                        return FamilyResult.Deny(proposal.ReasonCode);
                    }
                }
                proposal.ExecutionStep = FamilyExecutionStep.GoldSettled;
                proposal.Status = FamilyProposalStatus.Completed; proposal.ExecutionStep = FamilyExecutionStep.Completed;
                return FamilyResult.Allow(proposal.ProposalId);
            }
            catch (Exception ex)
            {
                MarriageExecutionScope.Clear(); proposal.Status = FamilyProposalStatus.ExecutionFailure; proposal.ReasonCode = FamilyReasonCode.ExecutionVerificationFailed;
                FamilyDiagnostics.Error("Marriage execution failed for " + proposal.ProposalId, ex); return FamilyResult.Deny(proposal.ReasonCode);
            }
        }
    }
}
