// Bridges the persisted proposal notice into the native round map-notification UI.
using ModifiedFamily.Campaign;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;

namespace ModifiedFamily.UI.Notifications
{
    public sealed class FamilyProposalMapNotificationVM : MapNotificationItemBaseVM
    {
        private readonly string _proposalId;
        private FamilyProposalService _service;

        public FamilyProposalMapNotificationVM(FamilyProposalMapNotification data) : base(data)
        {
            _proposalId = data.ProposalId;
            EnsureService();
            NotificationIdentifier = "marriage";
            TitleText = data.TitleText.ToString();
            DescriptionText = new TaleWorlds.Localization.TextObject("{=MF_UI_IncomingDescription}一个家族提议正在等待您的决定。点击查看详细内容。").ToString();
            SoundId = data.SoundEventPath;
            ForceInspection = true;
            _onInspect = InspectProposal;
        }

        public override void OnFinalize()
        {
            if (_service != null) _service.ProposalResolved -= OnProposalResolved;
            base.OnFinalize();
        }

        public override void ManualRefreshRelevantStatus()
        {
            base.ManualRefreshRelevantStatus();
            EnsureService();
            FamilyProposal proposal = _service?.GetProposal(_proposalId);
            if (proposal == null || proposal.Status != FamilyProposalStatus.PendingResponse) ExecuteRemove();
        }

        private void InspectProposal()
        {
            EnsureService();
            FamilyProposal proposal = _service?.GetProposal(_proposalId);
            if (proposal == null || proposal.Status != FamilyProposalStatus.PendingResponse) { ExecuteRemove(); return; }
            FamilyPlayerInteractionService.ShowIncomingProposal(_proposalId);
        }

        private void OnProposalResolved(FamilyProposal proposal)
        {
            if (proposal?.ProposalId == _proposalId) ExecuteRemove();
        }

        private void EnsureService()
        {
            if (_service != null) return;
            _service = TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service;
            if (_service != null) _service.ProposalResolved += OnProposalResolved;
        }
    }
}
