using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.UI.Marriage;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using CampaignClan = TaleWorlds.CampaignSystem.Clan;

namespace ModifiedFamily.UI.Clan
{
    [ViewModelMixin("RefreshValues", true)]
    public sealed class ClanMembersMarriageVMMixin : BaseViewModelMixin<ClanMembersVM>
    {
        private bool _canOpenMarriage;
        private string _marriageText;

        public ClanMembersMarriageVMMixin(ClanMembersVM vm) : base(vm)
        {
            Refresh();
        }

        [DataSourceProperty]
        public bool CanOpenMarriage
        {
            get => _canOpenMarriage;
            private set
            {
                if (value == _canOpenMarriage) return;
                _canOpenMarriage = value;
                OnPropertyChangedWithValue(value);
            }
        }

        [DataSourceProperty]
        public string MarriageText
        {
            get => _marriageText;
            private set
            {
                if (value == _marriageText) return;
                _marriageText = value;
                OnPropertyChangedWithValue(value);
            }
        }

        public override void OnRefresh() => Refresh();

        [DataSourceMethod]
        public void ExecuteOpenMarriage()
        {
            if (CanOpenMarriage) MarriageScreenService.Open();
        }

        private void Refresh()
        {
            CampaignClan playerClan = CampaignClan.PlayerClan;
            CanOpenMarriage = playerClan != null && playerClan.Heroes.Any(hero =>
                hero != null && hero.Clan == playerClan &&
                MarriageEligibilityModel.CanNegotiateHero(hero).IsAllowed);
            MarriageText = new TextObject("{=MF_UI_MarriageButton}Marriage Button").ToString();
        }
    }
}
