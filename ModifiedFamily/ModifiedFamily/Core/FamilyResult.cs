// Structured business result that separates permanent denial from temporary blockers.
using System.Collections.Generic;
using ModifiedFamily.Proposals;

namespace ModifiedFamily.Core
{
    public sealed class FamilyResult
    {
        public bool IsAllowed { get; }
        public FamilyReasonCode ReasonCode { get; }
        public IReadOnlyList<FamilyReasonCode> TemporaryBlockers { get; }
        public string ProposalId { get; }

        private FamilyResult(bool allowed, FamilyReasonCode reason, List<FamilyReasonCode> blockers, string proposalId)
        { IsAllowed = allowed; ReasonCode = reason; TemporaryBlockers = blockers ?? new List<FamilyReasonCode>(); ProposalId = proposalId; }

        public static FamilyResult Allow(string proposalId = null, params FamilyReasonCode[] blockers) => new FamilyResult(true, FamilyReasonCode.None, new List<FamilyReasonCode>(blockers ?? new FamilyReasonCode[0]), proposalId);
        public static FamilyResult Deny(FamilyReasonCode reason) => new FamilyResult(false, reason, null, null);
    }
}
