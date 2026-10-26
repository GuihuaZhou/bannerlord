using System;
using ModifiedFamily.Core;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedFamily.UI.Marriage
{
    internal sealed class MarriageProposalScreenVM : ViewModel
    {
        private readonly FamilyProposal _proposal;
        private readonly FamilyProposalService _service;
        private readonly Hero _playerHero;
        private readonly Hero _otherHero;
        private MarriageDirection _direction;
        private int _giftMode;
        private int _giftAmount;
        private bool _isViewing = true;
        private bool _isNegotiating;
        private string _directionText;
        private string _giftText;
        private string _giftAmountText;
        private string _statusText;
        private bool _canSubmitCounter;

        internal MarriageProposalScreenVM(FamilyProposal proposal, FamilyProposalService service)
        {
            _proposal = proposal; _service = service;
            _playerHero = proposal.HeroA?.Clan == TaleWorlds.CampaignSystem.Clan.PlayerClan ? proposal.HeroA : proposal.HeroB;
            _otherHero = _playerHero == proposal.HeroA ? proposal.HeroB : proposal.HeroA;
            PlayerHeroName = _playerHero?.Name?.ToString() ?? string.Empty;
            OtherHeroName = _otherHero?.Name?.ToString() ?? string.Empty;
            PlayerHeroVisual = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(_playerHero.CharacterObject));
            OtherHeroVisual = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(_otherHero.CharacterObject));
            _direction = GetDirection(proposal);
            _giftMode = proposal.GiftAmount <= 0 ? 0 : proposal.GoldPayerClan == TaleWorlds.CampaignSystem.Clan.PlayerClan ? 1 : 2;
            _giftAmount = Math.Max(0, proposal.GiftAmount);
            DirectionSelector = new SelectorVM<SelectorItemVM>(0, OnDirectionSelected);
            DirectionSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_NormalMarriageDirection}Normal Marriage Direction")));
            DirectionSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_MatrilocalMarriageDirection}Matrilocal Marriage Direction")));
            int giftSelectorIndex = _giftMode == 2 ? 1 : _giftMode == 1 ? 2 : 0;
            GiftSelector = new SelectorVM<SelectorItemVM>(0, OnGiftSelected);
            GiftSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_NoMarriageGift}No Marriage Gift")));
            GiftSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_OtherPaysMarriageGift}Other Pays Marriage Gift")));
            GiftSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_PlayerPaysMarriageGift}Player Pays Marriage Gift")));
            DirectionSelector.SelectedIndex = _direction == MarriageDirection.MaleJoinsFemaleClan ? 1 : 0;
            GiftSelector.SelectedIndex = giftSelectorIndex;
            RefreshTerms();
        }

        [DataSourceProperty] public string Title => new TextObject("{=MF_UI_IncomingMarriageTitle}Incoming Marriage Title").ToString();
        [DataSourceProperty] public string ConditionsTitle => new TextObject("{=MF_UI_MarriageConditions}Marriage Conditions").ToString();
        [DataSourceProperty] public string PlayerSideText => new TextObject("{=MF_UI_OurHero}Our Hero").ToString();
        [DataSourceProperty] public string OtherSideText => new TextObject("{=MF_UI_OtherHero}Other Hero").ToString();
        [DataSourceProperty] public string PlayerHeroName { get; }
        [DataSourceProperty] public string OtherHeroName { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM PlayerHeroVisual { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM OtherHeroVisual { get; }
        [DataSourceProperty] public SelectorVM<SelectorItemVM> DirectionSelector { get; }
        [DataSourceProperty] public SelectorVM<SelectorItemVM> GiftSelector { get; }
        [DataSourceProperty] public string DirectionLabel => new TextObject("{=MF_UI_DirectionLabel}Direction Label").ToString();
        [DataSourceProperty] public string GiftLabel => new TextObject("{=MF_UI_GiftLabel}Gift Label").ToString();
        [DataSourceProperty] public string GiftAmountLabel => new TextObject("{=MF_UI_GiftAmount}Gift Amount").ToString();
        [DataSourceProperty] public string CounterAmountLabel => new TextObject("{=MF_UI_CounterAmount}Counter Amount").ToString();
        [DataSourceProperty] public string AcceptText => new TextObject("{=MF_UI_Accept}Accept").ToString();
        [DataSourceProperty] public string RejectText => new TextObject("{=MF_UI_Reject}Reject").ToString();
        [DataSourceProperty] public string NegotiateText => new TextObject("{=MF_UI_Negotiate}Negotiate").ToString();
        [DataSourceProperty] public string SubmitCounterText => new TextObject("{=MF_UI_SubmitCounter}Submit Counter").ToString();
        [DataSourceProperty] public string BackText => new TextObject("{=MF_UI_Back}Back").ToString();
        [DataSourceProperty] public string DirectionText { get => _directionText; private set { _directionText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string GiftText { get => _giftText; private set { _giftText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string GiftAmountText { get => _giftAmountText; private set { _giftAmountText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public int CounterGiftAmount { get => _giftAmount; set { _giftAmount = Math.Max(0, Math.Min(2000000, value)); RefreshTerms(); } }
        [DataSourceProperty] public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(HasStatus)); } }
        [DataSourceProperty] public bool HasStatus => !string.IsNullOrWhiteSpace(StatusText);
        [DataSourceProperty] public bool IsViewing { get => _isViewing; private set { _isViewing = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsNegotiating { get => _isNegotiating; private set { _isNegotiating = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(CanAdjustGiftAmount)); OnPropertyChanged(nameof(ConditionsHeight)); } }
        [DataSourceProperty] public int ConditionsHeight => IsNegotiating ? 190 : 125;
        [DataSourceProperty] public bool CanNegotiate => _proposal.CounterCount == 0;
        [DataSourceProperty] public bool HasGift => _giftMode != 0;
        [DataSourceProperty] public bool CanAdjustGiftAmount => IsNegotiating && HasGift;
        [DataSourceProperty] public bool CanSubmitCounter { get => _canSubmitCounter; private set { _canSubmitCounter = value; OnPropertyChangedWithValue(value); } }

        public void ExecuteAccept() => ExecuteResolution("accept", () => _service.TryAccept(_proposal.ProposalId, TaleWorlds.CampaignSystem.Clan.PlayerClan));
        public void ExecuteReject() => ExecuteResolution("reject", () => _service.TryReject(_proposal.ProposalId, TaleWorlds.CampaignSystem.Clan.PlayerClan, FamilyReasonCode.PlayerDeclined));
        public void ExecuteNegotiate()
        {
            FamilyDiagnostics.DebugLog($"Marriage proposal dialog received negotiate click: proposal={_proposal.ProposalId}, canNegotiate={CanNegotiate}.");
            if (!CanNegotiate) return;
            ResetTermsToProposal();
            IsViewing = false; IsNegotiating = true;
            StatusText = string.Empty;
            RefreshTerms();
        }
        public void ExecuteBack() { ResetTermsToProposal(); IsNegotiating = false; IsViewing = true; StatusText = string.Empty; RefreshTerms(); }
        public void ExecuteClose()
        {
            FamilyDiagnostics.DebugLog($"Marriage proposal dialog received close click: proposal={_proposal.ProposalId}.");
            FamilyActionScreenHost.Close();
        }
        public void ExecuteOpenPlayerHero() => OpenHero(_playerHero);
        public void ExecuteOpenOtherHero() => OpenHero(_otherHero);
        public void ExecutePreviousDirection() { _direction = Opposite(_direction); RefreshTerms(); }
        public void ExecuteNextDirection() { _direction = Opposite(_direction); RefreshTerms(); }
        public void ExecutePreviousGift() { _giftMode = (_giftMode + 2) % 3; RefreshTerms(); }
        public void ExecuteNextGift() { _giftMode = (_giftMode + 1) % 3; RefreshTerms(); }
        public void ExecuteDecreaseGift() { _giftAmount = Math.Max(0, _giftAmount - 5000); RefreshTerms(); }
        public void ExecuteIncreaseGift() { _giftAmount = Math.Min(2000000, _giftAmount + 5000); RefreshTerms(); }
        public void ExecuteSubmitCounter()
        {
            if (!CanSubmitCounter) return;
            TaleWorlds.CampaignSystem.Clan payer = _giftMode == 1 ? TaleWorlds.CampaignSystem.Clan.PlayerClan : _giftMode == 2 ? _otherHero.Clan : null;
            FamilyResult result = _service.TryCounter(_proposal.ProposalId, TaleWorlds.CampaignSystem.Clan.PlayerClan, _direction, payer, _giftMode == 0 ? 0 : _giftAmount);
            Finish(result);
        }

        private void RefreshTerms()
        {
            DirectionText = _direction == MarriageDirection.FemaleJoinsMaleClan
                ? new TextObject("{=MF_UI_DirectionNormal}Direction Normal").ToString()
                : new TextObject("{=MF_UI_DirectionMatrilocal}Direction Matrilocal").ToString();
            GiftText = _giftMode == 0 ? new TextObject("{=MF_UI_NoGiftShort}No Gift Short").ToString()
                : _giftMode == 1 ? new TextObject("{=MF_UI_WePayShort}We Pay Short").ToString()
                : new TextObject("{=MF_UI_OtherPaysShort}Other Pays Short").ToString();
            GiftAmountText = _giftAmount.ToString("N0");
            OnPropertyChanged(nameof(CounterGiftAmount));
            OnPropertyChanged(nameof(HasGift));
            OnPropertyChanged(nameof(CanAdjustGiftAmount));
            bool changed = _direction != GetDirection(_proposal) || _giftMode != (_proposal.GiftAmount <= 0 ? 0 : _proposal.GoldPayerClan == TaleWorlds.CampaignSystem.Clan.PlayerClan ? 1 : 2) || (_giftMode != 0 && _giftAmount != _proposal.GiftAmount);
            TaleWorlds.CampaignSystem.Clan payer = _giftMode == 1 ? TaleWorlds.CampaignSystem.Clan.PlayerClan : _giftMode == 2 ? _otherHero.Clan : null;
            CanSubmitCounter = CanNegotiate && changed && (_giftMode == 0 || (_giftAmount > 0 && payer?.Leader != null && payer.Leader.Gold >= _giftAmount));
        }

        private void OnDirectionSelected(SelectorVM<SelectorItemVM> selector)
        {
            if (selector == null) return;
            _direction = selector.SelectedIndex == 1 ? MarriageDirection.MaleJoinsFemaleClan : MarriageDirection.FemaleJoinsMaleClan;
            RefreshTerms();
        }

        private void OnGiftSelected(SelectorVM<SelectorItemVM> selector)
        {
            if (selector == null) return;
            _giftMode = selector.SelectedIndex == 1 ? 2 : selector.SelectedIndex == 2 ? 1 : 0;
            if (_giftMode != 0 && _giftAmount <= 0) _giftAmount = Math.Max(5000, _proposal.GiftAmount);
            RefreshTerms();
        }

        private void ResetTermsToProposal()
        {
            _direction = GetDirection(_proposal);
            _giftMode = _proposal.GiftAmount <= 0 ? 0 : _proposal.GoldPayerClan == TaleWorlds.CampaignSystem.Clan.PlayerClan ? 1 : 2;
            _giftAmount = Math.Max(0, _proposal.GiftAmount);
            DirectionSelector.SelectedIndex = _direction == MarriageDirection.MaleJoinsFemaleClan ? 1 : 0;
            GiftSelector.SelectedIndex = _giftMode == 2 ? 1 : _giftMode == 1 ? 2 : 0;
            RefreshTerms();
        }

        private void Finish(FamilyResult result)
        {
            if (result != null && result.IsAllowed) FamilyActionScreenHost.Close();
            else
            {
                StatusText = new TextObject("{=MF_UI_RequestFailed}Request Failed").ToString();
                FamilyDiagnostics.Warn($"Marriage proposal dialog action failed: proposal={_proposal.ProposalId}, reason={result?.ReasonCode.ToString() ?? "NoResult"}.");
            }
        }
        private void ExecuteResolution(string actionName, Func<FamilyResult> action)
        {
            FamilyDiagnostics.DebugLog($"Marriage proposal dialog received {actionName} click: proposal={_proposal.ProposalId}.");
            try { Finish(action()); }
            catch (Exception exception)
            {
                StatusText = new TextObject("{=MF_UI_RequestFailed}Request Failed").ToString();
                FamilyDiagnostics.Error($"Marriage proposal dialog failed to execute {actionName}: proposal={_proposal.ProposalId}.", exception);
            }
        }
        private static MarriageDirection GetDirection(FamilyProposal proposal)
        {
            Hero female = proposal.HeroA.IsFemale ? proposal.HeroA : proposal.HeroB;
            return proposal.DestinationClan == female.Clan ? MarriageDirection.MaleJoinsFemaleClan : MarriageDirection.FemaleJoinsMaleClan;
        }
        private static MarriageDirection Opposite(MarriageDirection value) => value == MarriageDirection.FemaleJoinsMaleClan ? MarriageDirection.MaleJoinsFemaleClan : MarriageDirection.FemaleJoinsMaleClan;
        private static void OpenHero(Hero hero)
        {
            if (hero == null) return;
            FamilyDiagnostics.DebugLog($"Marriage proposal dialog received hero portrait click: hero={hero.StringId}/{hero.Name}, encyclopediaLink={hero.EncyclopediaLink}.");
            try
            {
                TaleWorlds.CampaignSystem.Encyclopedia.EncyclopediaManager encyclopediaManager = TaleWorlds.CampaignSystem.Campaign.Current?.EncyclopediaManager;
                if (encyclopediaManager == null || string.IsNullOrEmpty(hero.EncyclopediaLink))
                {
                    FamilyDiagnostics.Warn($"Could not open hero encyclopedia from marriage proposal dialog: hero={hero.StringId}, manager or link unavailable.");
                    return;
                }
                FamilyActionScreenHost.Close();
                encyclopediaManager.GoToLink(hero.EncyclopediaLink);
            }
            catch (Exception exception)
            {
                FamilyDiagnostics.Error($"Failed to open hero encyclopedia from marriage proposal dialog: hero={hero.StringId}.", exception);
            }
        }
    }
}
