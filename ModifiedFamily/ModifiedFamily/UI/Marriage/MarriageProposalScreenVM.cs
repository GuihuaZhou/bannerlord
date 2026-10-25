using System;
using ModifiedFamily.Core;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
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
            RefreshTerms();
        }

        [DataSourceProperty] public string Title => new TextObject("{=MF_UI_IncomingMarriageTitle}收到家族联姻提议").ToString();
        [DataSourceProperty] public string ConditionsTitle => new TextObject("{=MF_UI_MarriageConditions}联姻条件").ToString();
        [DataSourceProperty] public string PlayerSideText => new TextObject("{=MF_UI_OurHero}我方成员").ToString();
        [DataSourceProperty] public string OtherSideText => new TextObject("{=MF_UI_OtherHero}对方成员").ToString();
        [DataSourceProperty] public string PlayerHeroName { get; }
        [DataSourceProperty] public string OtherHeroName { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM PlayerHeroVisual { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM OtherHeroVisual { get; }
        [DataSourceProperty] public string DirectionLabel => new TextObject("{=MF_UI_DirectionLabel}归属").ToString();
        [DataSourceProperty] public string GiftLabel => new TextObject("{=MF_UI_GiftLabel}礼金").ToString();
        [DataSourceProperty] public string GiftAmountLabel => new TextObject("{=MF_UI_GiftAmount}礼金金额").ToString();
        [DataSourceProperty] public string AcceptText => new TextObject("{=MF_UI_Accept}接受").ToString();
        [DataSourceProperty] public string RejectText => new TextObject("{=MF_UI_Reject}拒绝").ToString();
        [DataSourceProperty] public string NegotiateText => new TextObject("{=MF_UI_Negotiate}协商").ToString();
        [DataSourceProperty] public string SubmitCounterText => new TextObject("{=MF_UI_SubmitCounter}提交反提议").ToString();
        [DataSourceProperty] public string BackText => new TextObject("{=MF_UI_Back}返回").ToString();
        [DataSourceProperty] public string DirectionText { get => _directionText; private set { _directionText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string GiftText { get => _giftText; private set { _giftText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string GiftAmountText { get => _giftAmountText; private set { _giftAmountText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsViewing { get => _isViewing; private set { _isViewing = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsNegotiating { get => _isNegotiating; private set { _isNegotiating = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool CanNegotiate => _proposal.CounterCount == 0;
        [DataSourceProperty] public bool HasGift => _giftMode != 0;
        [DataSourceProperty] public bool CanSubmitCounter { get => _canSubmitCounter; private set { _canSubmitCounter = value; OnPropertyChangedWithValue(value); } }

        public void ExecuteAccept() { FamilyResult result = _service.TryAccept(_proposal.ProposalId, TaleWorlds.CampaignSystem.Clan.PlayerClan); Finish(result); }
        public void ExecuteReject() { FamilyResult result = _service.TryReject(_proposal.ProposalId, TaleWorlds.CampaignSystem.Clan.PlayerClan, FamilyReasonCode.PlayerDeclined); Finish(result); }
        public void ExecuteNegotiate()
        {
            if (!CanNegotiate) return;
            IsViewing = false; IsNegotiating = true;
            StatusText = new TextObject("{=MF_UI_AdjustCounterTerms}调整条件后提交；每条谈判链只能协商一次。").ToString();
            RefreshTerms();
        }
        public void ExecuteBack() { IsNegotiating = false; IsViewing = true; StatusText = string.Empty; RefreshTerms(); }
        public void ExecuteClose() => FamilyActionScreenHost.Close();
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
                ? new TextObject("{=MF_UI_DirectionNormal}女方嫁入男方家族").ToString()
                : new TextObject("{=MF_UI_DirectionMatrilocal}男方入赘女方家族").ToString();
            GiftText = _giftMode == 0 ? new TextObject("{=MF_UI_NoGiftShort}无礼金").ToString()
                : _giftMode == 1 ? new TextObject("{=MF_UI_WePayShort}我方支付").ToString()
                : new TextObject("{=MF_UI_OtherPaysShort}对方支付").ToString();
            GiftAmountText = _giftAmount.ToString("N0");
            OnPropertyChanged(nameof(HasGift));
            bool changed = _direction != GetDirection(_proposal) || _giftMode != (_proposal.GiftAmount <= 0 ? 0 : _proposal.GoldPayerClan == TaleWorlds.CampaignSystem.Clan.PlayerClan ? 1 : 2) || (_giftMode != 0 && _giftAmount != _proposal.GiftAmount);
            TaleWorlds.CampaignSystem.Clan payer = _giftMode == 1 ? TaleWorlds.CampaignSystem.Clan.PlayerClan : _giftMode == 2 ? _otherHero.Clan : null;
            CanSubmitCounter = CanNegotiate && changed && (_giftMode == 0 || (_giftAmount > 0 && payer?.Leader != null && payer.Leader.Gold >= _giftAmount));
        }

        private void Finish(FamilyResult result)
        {
            if (result != null && result.IsAllowed) FamilyActionScreenHost.Close();
            else StatusText = new TextObject("{=MF_UI_RequestFailed}提议未能提交，请检查人物状态、家族关系、冷却或礼金条件。").ToString();
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
            FamilyActionScreenHost.Close();
            TaleWorlds.CampaignSystem.Campaign.Current?.EncyclopediaManager?.GoToLink(hero.EncyclopediaLink);
        }
    }
}
