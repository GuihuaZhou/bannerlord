// Publishes each incoming player proposal once and leaves the notice recoverable across saves.
using ModifiedFamily.Proposals;
using ModifiedFamily.Localization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using ModifiedFamily.Diagnostics;
using System;

namespace ModifiedFamily.UI.Notifications
{
    public sealed class FamilyProposalNotificationService
    {
        public void Publish(FamilyProposal proposal)
        {
            if (proposal == null || proposal.PlayerNotified || proposal.ReceiverClan != TaleWorlds.CampaignSystem.Clan.PlayerClan ||
                proposal.Status != FamilyProposalStatus.PendingResponse || TaleWorlds.CampaignSystem.Campaign.Current?.CampaignInformationManager == null) return;
            var title = new TextObject("{=MF_UI_IncomingTitle}Incoming Title");
            try
            {
                TaleWorlds.CampaignSystem.Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(new FamilyProposalMapNotification(proposal.ProposalId, title));
                proposal.PlayerNotified = true;
            }
            catch (Exception exception)
            {
                FamilyDiagnostics.Error("Marriage proposal notification could not be published; pending for retry.", exception);
            }
        }

        public void PublishInvalidated(FamilyProposal proposal)
        {
            if (proposal == null || (proposal.ProposerClan != TaleWorlds.CampaignSystem.Clan.PlayerClan && proposal.ReceiverClan != TaleWorlds.CampaignSystem.Clan.PlayerClan)) return;
            if (proposal.ProposalType == FamilyProposalType.Marriage) return; // Marriage uses the saved map outcome.
            var text = new TextObject("{=MF_UI_ProposalInvalidated}Proposal Invalidated {REASON}");
            text.SetTextVariable("REASON", FamilyLogText.ReasonText(proposal.ReasonCode));
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }

        /// <summary>Records each player-facing result once, retrying a pending notice after a save or unavailable map manager.</summary>
        public void PublishResolved(FamilyProposal proposal)
        {
            if (proposal == null || proposal.ProposalType != FamilyProposalType.Marriage) return;
            int flag = proposal.Status == FamilyProposalStatus.AcceptedPendingExecution ? 1 :
                proposal.Status == FamilyProposalStatus.Rejected ? 2 :
                proposal.Status == FamilyProposalStatus.TimedOut ? 4 :
                proposal.Status == FamilyProposalStatus.Invalidated ? 8 : 0;
            if (flag == 0 || !ShouldNotify(proposal, flag) ||
                (proposal.PlayerResultNotificationFlags & flag) != 0) return;
            proposal.PendingResultNotificationFlags |= flag;
            PublishPending(proposal);
        }

        public void PublishPending(FamilyProposal proposal)
        {
            if (proposal == null || proposal.ProposalType != FamilyProposalType.Marriage) return;
            var manager = TaleWorlds.CampaignSystem.Campaign.Current?.CampaignInformationManager;
            if (manager == null) return;
            foreach (int flag in new[] { 1, 2, 4, 8 })
            {
                if (!ShouldNotify(proposal, flag) || (proposal.PendingResultNotificationFlags & flag) == 0 ||
                    (proposal.PlayerResultNotificationFlags & flag) != 0) continue;
                try
                {
                    manager.NewMapNoticeAdded(new FamilyProposalResultMapNotification(
                        proposal.ProposalId, flag, (int)proposal.ReasonCode));
                    proposal.PlayerResultNotificationFlags |= flag;
                    proposal.PendingResultNotificationFlags &= ~flag;
                }
                catch (Exception exception)
                {
                    FamilyDiagnostics.Error("Marriage result notification could not be published; pending for retry.", exception);
                }
            }
        }

        private static bool ShouldNotify(FamilyProposal proposal, int flag) =>
            flag == 8
                ? proposal.ProposerClan == TaleWorlds.CampaignSystem.Clan.PlayerClan ||
                    proposal.ReceiverClan == TaleWorlds.CampaignSystem.Clan.PlayerClan
                : proposal.ProposerClan == TaleWorlds.CampaignSystem.Clan.PlayerClan &&
                    proposal.ReceiverClan != TaleWorlds.CampaignSystem.Clan.PlayerClan;
    }
}
