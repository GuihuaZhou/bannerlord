// Read-only map result presentation; the saved notice snapshots an outcome even if its proposal later changes.
using ModifiedFamily.Campaign;
using ModifiedFamily.Localization;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;
using TaleWorlds.Localization;
using TaleWorlds.Library;

namespace ModifiedFamily.UI.Notifications
{
    public sealed class FamilyProposalResultMapNotificationVM : MapNotificationItemBaseVM
    {
        private readonly FamilyProposalResultMapNotification _notice;

        public FamilyProposalResultMapNotificationVM(FamilyProposalResultMapNotification notice) : base(notice)
        {
            _notice = notice;
            NotificationIdentifier = "marriage";
            TitleText = notice.TitleText.ToString();
            SoundId = notice.SoundEventPath;
            ForceInspection = false;
            DescriptionText = Description().ToString();
            _onInspect = Inspect;
        }

        private TextObject Description()
        {
            var text = new TextObject(_notice.ResultFlag == 1
                ? "{=MF_Result_Accepted}Your marriage proposal was accepted. Execution is pending."
                : _notice.ResultFlag == 2
                    ? "{=MF_Result_Rejected}Your marriage proposal was rejected: {REASON}"
                    : _notice.ResultFlag == 4
                        ? "{=MF_Result_Expired}Your marriage proposal timed out."
                        : "{=MF_Result_Invalidated}Your marriage proposal became invalid: {REASON}");
            text.SetTextVariable("REASON", FamilyLogText.ReasonText((FamilyReasonCode)_notice.ReasonCode));
            return text;
        }

        private void Inspect()
        {
            var service = TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service;
            FamilyProposal proposal = service?.GetProposal(_notice.ProposalId);
            var text = Description();
            if (proposal?.HeroA != null && proposal.HeroB != null)
                text = new TextObject("{=MF_Result_Details}{HERO_A} and {HERO_B}: {RESULT}")
                    .SetTextVariable("HERO_A", proposal.HeroA.Name)
                    .SetTextVariable("HERO_B", proposal.HeroB.Name)
                    .SetTextVariable("RESULT", Description());
            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }
    }
}
