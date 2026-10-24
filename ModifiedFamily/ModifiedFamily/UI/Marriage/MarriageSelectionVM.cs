// Presents valid clans and marriage candidates using the kingdom clan screen visual model.
using System;
using System.Linq;
using ModifiedFamily.Campaign;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using CampaignClan = TaleWorlds.CampaignSystem.Clan;

namespace ModifiedFamily.UI.Marriage
{
    internal sealed class MarriageSelectionVM : ViewModel
    {
        private readonly Hero _ownHero;
        private MarriageClanItemVM _selectedClan;
        private MarriageHeroItemVM _selectedHero;
        private string _title;
        private string _membersText;
        private string _proposeText;
        private string _closeText;
        private string _noCandidatesText;

        internal MarriageSelectionVM(Hero ownHero)
        {
            _ownHero = ownHero;
            Clans = new MBBindingList<MarriageClanItemVM>();
            Heroes = new MBBindingList<MarriageHeroItemVM>();
            Title = new TextObject("{=MF_UI_MarriageScreenTitle}联姻").ToString();
            MembersText = new TextObject("{=MF_UI_EligibleMarriageHeroes}可联姻成员").ToString();
            ProposeText = new TextObject("{=MF_UI_SelectMarriageHero}选择对象").ToString();
            CloseText = new TextObject("{=MF_UI_Close}关闭").ToString();
            NoCandidatesText = new TextObject("{=MF_UI_NoMarriageCandidates}当前没有符合全部联姻条件且未被其他提议占用的家族或成员。").ToString();
            LoadClans();
        }

        [DataSourceProperty] public MBBindingList<MarriageClanItemVM> Clans { get; }
        [DataSourceProperty] public MBBindingList<MarriageHeroItemVM> Heroes { get; }
        [DataSourceProperty] public MarriageClanItemVM SelectedClan { get => _selectedClan; private set { _selectedClan = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(HasSelectedClan)); } }
        [DataSourceProperty] public MarriageHeroItemVM SelectedHero { get => _selectedHero; private set { _selectedHero = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(CanContinue)); } }
        [DataSourceProperty] public bool HasSelectedClan => SelectedClan != null;
        [DataSourceProperty] public bool HasClans => Clans.Count > 0;
        [DataSourceProperty] public bool CanContinue => SelectedHero != null;
        [DataSourceProperty] public string Title { get => _title; private set { _title = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string MembersText { get => _membersText; private set { _membersText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string ProposeText { get => _proposeText; private set { _proposeText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string CloseText { get => _closeText; private set { _closeText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string NoCandidatesText { get => _noCandidatesText; private set { _noCandidatesText = value; OnPropertyChangedWithValue(value); } }

        public void ExecuteContinue()
        {
            Hero other = SelectedHero?.Hero;
            if (other == null) return;
            FamilyActionScreenHost.Close();
            FamilyPlayerInteractionService.ShowMarriageTermsFor(_ownHero, other);
        }

        public void ExecuteClose() => FamilyActionScreenHost.Close();

        private void LoadClans()
        {
            foreach (CampaignClan clan in CampaignClan.All.Where(c => c != CampaignClan.PlayerClan && MarriageEligibilityModel.CanUseClan(c).IsAllowed).OrderBy(c => c.Name.ToString()))
            {
                var candidates = clan.Heroes.Where(IsValidCandidate).ToList();
                if (candidates.Count > 0) Clans.Add(new MarriageClanItemVM(clan, candidates.Count, SelectClan));
            }
            if (Clans.Count > 0) SelectClan(Clans[0]);
        }

        private bool IsValidCandidate(Hero other)
        {
            if (other == null || other.IsFemale == _ownHero.IsFemale || !MarriageEligibilityModel.CanNegotiateHero(other).IsAllowed) return false;
            FamilyProposalService service = TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service;
            return service != null && (service.CanCreateMarriageProposal(Draft(other, MarriageDirection.FemaleJoinsMaleClan)).IsAllowed || service.CanCreateMarriageProposal(Draft(other, MarriageDirection.MaleJoinsFemaleClan)).IsAllowed);
        }

        private MarriageProposalDraft Draft(Hero other, MarriageDirection direction) => new MarriageProposalDraft { ProposerClan = CampaignClan.PlayerClan, ReceiverClan = other.Clan, HeroA = _ownHero, HeroB = other, Direction = direction };

        private void SelectClan(MarriageClanItemVM item)
        {
            if (SelectedClan != null) SelectedClan.IsSelected = false;
            SelectedClan = item;
            if (item != null) item.IsSelected = true;
            SelectedHero = null;
            Heroes.Clear();
            if (item == null) return;
            foreach (Hero hero in item.Clan.Heroes.Where(IsValidCandidate).OrderBy(h => h.Name.ToString())) Heroes.Add(new MarriageHeroItemVM(hero, SelectHero));
        }

        private void SelectHero(MarriageHeroItemVM item)
        {
            if (SelectedHero != null) SelectedHero.IsSelected = false;
            SelectedHero = item;
            if (item != null) item.IsSelected = true;
        }
    }

    internal sealed class MarriageClanItemVM : ViewModel
    {
        private readonly Action<MarriageClanItemVM> _select;
        private bool _isSelected;
        internal MarriageClanItemVM(CampaignClan clan, int count, Action<MarriageClanItemVM> select) { Clan = clan; _select = select; Name = clan.Name.ToString(); CandidateCountText = count.ToString(); Banner = new BannerImageIdentifierVM(clan.Banner, true); }
        internal CampaignClan Clan { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string CandidateCountText { get; }
        [DataSourceProperty] public BannerImageIdentifierVM Banner { get; }
        [DataSourceProperty] public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChangedWithValue(value); } }
        public void ExecuteSelect() => _select(this);
    }

    internal sealed class MarriageHeroItemVM : ViewModel
    {
        private readonly Action<MarriageHeroItemVM> _select;
        private bool _isSelected;
        internal MarriageHeroItemVM(Hero hero, Action<MarriageHeroItemVM> select) { Hero = hero; _select = select; Name = hero.Name.ToString(); Visual = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(hero.CharacterObject)); }
        internal Hero Hero { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM Visual { get; }
        [DataSourceProperty] public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChangedWithValue(value); } }
        public void ExecuteSelect() => _select(this);
    }
}
