// Binds companion adoption to the hero currently selected on the clan members page.
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedFamily.UI.Clan
{
    [ViewModelMixin("RefreshValues", true)]
    public sealed class ClanManagementVMMixin : BaseViewModelMixin<ClanLordItemVM>
    {
        private string _adoptionActionsText;
        private bool _showAdoption;
        private readonly ClanLordItemVM _vm;
        public ClanManagementVMMixin(ClanLordItemVM vm) : base(vm) { _vm = vm; Refresh(); }
        [DataSourceProperty] public string AdoptionActionsText { get => _adoptionActionsText; set { if (value != _adoptionActionsText) { _adoptionActionsText = value; OnPropertyChangedWithValue(value); } } }
        [DataSourceProperty] public bool ShowAdoption { get => _showAdoption; set { if (value != _showAdoption) { _showAdoption = value; OnPropertyChangedWithValue(value); } } }
        public override void OnRefresh() => Refresh();
        [DataSourceMethod] public void ExecuteOpenAdoptionActions() { Hero hero = _vm.GetHero(); if (hero != null && ShowAdoption) FamilyPlayerInteractionService.ShowAdoptionActionsFor(hero); }
        private void Refresh()
        {
            Hero hero = _vm.GetHero();
            ShowAdoption = hero != null && !_vm.IsFamilyMember && hero.CompanionOf == TaleWorlds.CampaignSystem.Clan.PlayerClan;
            AdoptionActionsText = new TextObject("{=MF_UI_AdoptionButton}Adoption Button").ToString();
        }
    }
}
