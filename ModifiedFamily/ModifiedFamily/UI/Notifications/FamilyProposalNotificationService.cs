// Publishes each incoming player proposal once and leaves the notice recoverable across saves.
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedFamily.UI.Notifications
{
    public sealed class FamilyProposalNotificationService
    {
        public void Publish(FamilyProposal proposal)
        {
            if (proposal == null || proposal.PlayerNotified || proposal.ReceiverClan != TaleWorlds.CampaignSystem.Clan.PlayerClan ||
                proposal.Status != FamilyProposalStatus.PendingResponse || TaleWorlds.CampaignSystem.Campaign.Current?.CampaignInformationManager == null) return;
            var title = new TextObject("{=MF_UI_IncomingTitle}收到家族提议");
            TaleWorlds.CampaignSystem.Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new FamilyProposalMapNotification(proposal.ProposalId, title));
            proposal.PlayerNotified = true;
        }
    }
}
