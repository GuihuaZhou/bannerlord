// Presents the top-level family actions on the standalone Gauntlet screen.
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedFamily.UI
{
    public sealed class FamilyManagementVM : ViewModel
    {
        private string _title;
        private string _description;
        private string _marriageText;
        private string _adoptionText;
        private string _inboxText;
        private string _closeText;

        public FamilyManagementVM()
        {
            RefreshValues();
        }

        [DataSourceProperty] public string Title { get => _title; private set => SetField(ref _title, value, nameof(Title)); }
        [DataSourceProperty] public string Description { get => _description; private set => SetField(ref _description, value, nameof(Description)); }
        [DataSourceProperty] public string MarriageText { get => _marriageText; private set => SetField(ref _marriageText, value, nameof(MarriageText)); }
        [DataSourceProperty] public string AdoptionText { get => _adoptionText; private set => SetField(ref _adoptionText, value, nameof(AdoptionText)); }
        [DataSourceProperty] public string InboxText { get => _inboxText; private set => SetField(ref _inboxText, value, nameof(InboxText)); }
        [DataSourceProperty] public string CloseText { get => _closeText; private set => SetField(ref _closeText, value, nameof(CloseText)); }

        public override void RefreshValues()
        {
            base.RefreshValues();
            Title = new TextObject("{=MF_UI_FamilyManagementTitle}Family Management Title").ToString();
            Description = new TextObject("{=MF_UI_FamilyManagementDescription}Family Management Description").ToString();
            MarriageText = new TextObject("{=MF_UI_ArrangeMarriageShort}Arrange Marriage Short").ToString();
            AdoptionText = new TextObject("{=MF_UI_AdoptionActionsShort}Adoption Actions Short").ToString();
            InboxText = new TextObject("{=MF_UI_OpenInboxShort}Open Inbox Short").ToString();
            CloseText = new TextObject("{=MF_UI_Close}Close").ToString();
        }

        public void ExecuteMarriage()
        {
            FamilyManagementScreenService.Close();
            FamilyPlayerInteractionService.ShowMarriageActions();
        }

        public void ExecuteAdoption()
        {
            FamilyManagementScreenService.Close();
            FamilyPlayerInteractionService.ShowAdoptionActions();
        }

        public void ExecuteInbox()
        {
            FamilyManagementScreenService.Close();
            FamilyPlayerInteractionService.ShowProposalInbox();
        }

        public void ExecuteClose() => FamilyManagementScreenService.Close();
    }
}
