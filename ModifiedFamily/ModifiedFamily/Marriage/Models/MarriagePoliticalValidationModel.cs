// Pure political validation shared by event handlers and lifecycle entry points.
using ModifiedFamily.Core;
using ModifiedFamily.Proposals;

namespace ModifiedFamily.Marriage.Models
{
    public sealed class MarriagePoliticalValidationModel
    {
        public FamilyResult Validate(FamilyProposal proposal)
        {
            if (proposal == null || proposal.ProposalType != FamilyProposalType.Marriage ||
                proposal.HeroA == null || proposal.HeroB == null)
                return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);

            FamilyResult firstClan = MarriageEligibilityModel.CanUseClan(proposal.OriginClanA);
            if (!firstClan.IsAllowed) return firstClan;
            FamilyResult secondClan = MarriageEligibilityModel.CanUseClan(proposal.OriginClanB);
            if (!secondClan.IsAllowed) return secondClan;

            if (proposal.OriginClanA.IsAtWarWith(proposal.OriginClanB))
                return FamilyResult.Deny(FamilyReasonCode.AtWar);
            if (!MarriageProposalModel.CultureAndKingdomAllowed(proposal.HeroA, proposal.HeroB))
                return FamilyResult.Deny(FamilyReasonCode.CultureForbidden);
            if (proposal.HeroA.Clan != proposal.OriginClanA || proposal.HeroB.Clan != proposal.OriginClanB)
                return FamilyResult.Deny(FamilyReasonCode.ClanChanged);

            return FamilyResult.Allow();
        }

        public static bool IsPoliticalFailure(FamilyReasonCode reason)
        {
            return reason == FamilyReasonCode.AtWar || reason == FamilyReasonCode.InvalidClan ||
                   reason == FamilyReasonCode.CultureForbidden || reason == FamilyReasonCode.ClanChanged ||
                   reason == FamilyReasonCode.PoliticalInterestChanged;
        }
    }
}
