// Live initial mobilization for newly created AI lord parties. The real party,
// normal source owners, and unified recruitment executor remain authoritative;
// creation-time previews are never replayed or treated as resource reservations.
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.PartyManagement.Services
{
    internal sealed class InitialPartyMobilizationCommitter
    {
        /// <summary>
        /// Rebuilds feasible sources from live state and keeps every legal
        /// partial result. A changed source or lower count is not a creation
        /// failure and remaining source categories are still attempted.
        /// </summary>
        internal InitialMobilizationCommitResult Commit(
            MobileParty party,
            InitialMobilizationPreview preview)
        {
            if (party == null || preview?.Approved != true ||
                party.ActualClan != preview.Clan ||
                party.CurrentSettlement != preview.BaseSettlement)
                return Reject("PartyInitializationMismatch");

            PartyForcePlanningContext context =
                new PartyForceCandidateService().BuildContext(
                    party, null, true);
            var sources = (context?.Candidates ??
                    Array.Empty<PartyForceCandidate>())
                .Where(item => item?.Settlement == preview.BaseSettlement)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.SourceKind)
                .ToList();
            var executor = new UnifiedRecruitmentExecutionService();
            int fief = 0;
            int professional = 0;
            int mercenary = 0;
            int sequence = 0;
            foreach (PartyForceCandidate source in sources)
            {
                if (party.Party.NumberOfAllMembers >=
                    party.Party.PartySizeLimit)
                    break;
                PartyExchangeExecutionResult result = executor.Execute(
                    party,
                    preview.BaseSettlement,
                    source.SourceKind,
                    "party-create:" + preview.AttemptId + ":live:" +
                        sequence++,
                    attemptId: preview.AttemptId);
                int moved = Math.Max(0, result?.TransferredCount ?? 0);
                switch (source.SourceKind)
                {
                    case PartyForceSourceKind.Fief:
                        fief += moved;
                        break;
                    case PartyForceSourceKind.Professional:
                        professional += moved;
                        break;
                    case PartyForceSourceKind.Mercenary:
                        mercenary += moved;
                        break;
                }
            }

            return new InitialMobilizationCommitResult
            {
                Status = InitialMobilizationCommitStatus.Committed,
                ReasonCode = sources.Count == 0
                    ? "CompletedWithoutLiveSource"
                    : "CompletedFromLiveSources",
                CommittedFiefCount = fief,
                CommittedGarrisonCount = professional,
                CommittedMercenaryCount = mercenary
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
