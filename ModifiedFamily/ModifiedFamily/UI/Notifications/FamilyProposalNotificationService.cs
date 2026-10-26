// Publishes each incoming player proposal once and leaves the notice recoverable across saves.
using ModifiedFamily.Proposals;
using ModifiedFamily.Localization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace ModifiedFamily.UI.Notifications
{
    public sealed class FamilyProposalNotificationService
    {
        public void Publish(FamilyProposal proposal)
        {
            if (proposal == null || proposal.PlayerNotified || proposal.ReceiverClan != TaleWorlds.CampaignSystem.Clan.PlayerClan ||
                proposal.Status != FamilyProposalStatus.PendingResponse || TaleWorlds.CampaignSystem.Campaign.Current?.CampaignInformationManager == null) return;
            var title = new TextObject("{=MF_UI_IncomingTitle}Incoming Title");
            TaleWorlds.CampaignSystem.Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new FamilyProposalMapNotification(proposal.ProposalId, title));
            proposal.PlayerNotified = true;
        }

        public void PublishInvalidated(FamilyProposal proposal)
        {
            if (proposal == null || (proposal.ProposerClan != TaleWorlds.CampaignSystem.Clan.PlayerClan && proposal.ReceiverClan != TaleWorlds.CampaignSystem.Clan.PlayerClan)) return;
            var text = new TextObject("{=MF_UI_ProposalInvalidated}Proposal Invalidated {REASON}");
            text.SetTextVariable("REASON", FamilyLogText.ReasonText(proposal.ReasonCode));
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }
    }
}
