using ModifiedFamily.Campaign;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.UI.Marriage
{
    public static class MarriageProposalScreenService
    {
        public static bool Open(string proposalId)
        {
            FamilyProposalService service = TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service;
            FamilyProposal proposal = service?.GetProposal(proposalId);
            if (proposal == null || proposal.ProposalType != FamilyProposalType.Marriage || proposal.Status != FamilyProposalStatus.PendingResponse || proposal.ReceiverClan != TaleWorlds.CampaignSystem.Clan.PlayerClan) return false;
            return FamilyActionScreenHost.Open("ModifiedFamilyMarriageProposal", "ModifiedFamilyMarriageProposalLayer", new MarriageProposalScreenVM(proposal, service));
        }
    }
}
