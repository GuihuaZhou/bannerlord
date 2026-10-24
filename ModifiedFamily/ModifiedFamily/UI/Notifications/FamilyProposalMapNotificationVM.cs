// Bridges the persisted proposal notice into the native round map-notification UI.
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapNotificationTypes;

namespace ModifiedFamily.UI.Notifications
{
    public sealed class FamilyProposalMapNotificationVM : MapNotificationItemBaseVM
    {
        public FamilyProposalMapNotificationVM(FamilyProposalMapNotification data) : base(data)
        {
            NotificationIdentifier = "kingdomdecision";
            TitleText = data.TitleText.ToString();
            DescriptionText = new TaleWorlds.Localization.TextObject("{=MF_UI_IncomingDescription}一个家族提议正在等待您的决定。点击查看详细内容。").ToString();
            SoundId = data.SoundEventPath;
            ForceInspection = true;
            _onInspect = () => FamilyPlayerInteractionService.ShowIncomingProposal(data.ProposalId);
        }
    }
}
