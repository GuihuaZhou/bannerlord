using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Models;
using ModifiedPolitics.KingdomDiplomacy.Negotiation.Services;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation
{
    /// <summary>
    /// Owns a complete kingdom-to-kingdom negotiation session. It mirrors the
    /// useful left inventory, central offer and right inventory interaction of
    /// BarterVM, but does not inherit from it or use any hero barter state.
    /// </summary>
    public sealed class KingdomNegotiationVM : ViewModel
    {
        private readonly Action _close;
        private int _resultBarOtherPercentage;
        private int _resultBarOffererPercentage;
        private bool _isTargetSupportInsufficient = true;
        private bool _isPlayerSupportInsufficient = true;
        private bool _deferOfferRefresh;

        public KingdomNegotiationVM(
            Kingdom playerKingdom,
            Kingdom targetKingdom,
            Action close)
        {
            PlayerKingdom = playerKingdom;
            TargetKingdom = targetKingdom;
            _close = close;

            LeftFiefList = NewList();
            RightFiefList = NewList();
            LeftPrisonerList = NewList();
            RightPrisonerList = NewList();
            LeftItemList = NewList();
            RightItemList = NewList();
            LeftOtherList = NewList();
            RightOtherList = NewList();
            LeftDiplomaticList = NewList();
            RightDiplomaticList = NewList();
            LeftGoldList = NewList();
            RightGoldList = NewList();
            LeftOfferList = NewList();
            RightOfferList = NewList();

            LeftHero = targetKingdom?.Leader == null
                ? null
                : new HeroVM(targetKingdom.Leader, false);
            RightHero = playerKingdom?.Leader == null
                ? null
                : new HeroVM(playerKingdom.Leader, false);
            AutoBalanceHint = new HintViewModel();

            BuildSide(targetKingdom, false);
            BuildSide(playerKingdom, true);
            RefreshValues();
        }

        public Kingdom PlayerKingdom { get; }

        public Kingdom TargetKingdom { get; }

        [DataSourceProperty]
        public bool IsKingdomNegotiation => true;

        [DataSourceProperty] public string LeftNameLbl { get; private set; }
        [DataSourceProperty] public string RightNameLbl { get; private set; }
        [DataSourceProperty] public int LeftMaxGold { get; private set; }
        [DataSourceProperty] public int RightMaxGold { get; private set; }
        [DataSourceProperty] public HeroVM LeftHero { get; }
        [DataSourceProperty] public HeroVM RightHero { get; }
        [DataSourceProperty] public string FiefLbl { get; private set; }
        [DataSourceProperty] public string PrisonerLbl { get; private set; }
        [DataSourceProperty] public string ItemLbl { get; private set; }
        [DataSourceProperty] public string OtherLbl { get; private set; }
        [DataSourceProperty] public string DiplomaticLbl { get; private set; }
        [DataSourceProperty] public string CancelLbl { get; private set; }
        [DataSourceProperty] public string ResetLbl { get; private set; }
        [DataSourceProperty] public string OfferLbl { get; private set; }
        [DataSourceProperty]
        public bool IsOfferDisabled => LeftOfferList.Count == 0
            && RightOfferList.Count == 0;
        /// <summary>
        /// Shows the target kingdom's influence-weighted council support for
        /// the complete draft. This is refreshed whenever a term moves in or
        /// out of the central offer.
        /// </summary>
        [DataSourceProperty]
        public int ResultBarOtherPercentage
        {
            get => _resultBarOtherPercentage;
            private set
            {
                if (_resultBarOtherPercentage == value)
                {
                    return;
                }

                _resultBarOtherPercentage = value;
                OnPropertyChangedWithValue(
                    value,
                    nameof(ResultBarOtherPercentage));
            }
        }

        [DataSourceProperty]
        public bool IsTargetSupportInsufficient
        {
            get => _isTargetSupportInsufficient;
            private set
            {
                if (_isTargetSupportInsufficient == value)
                {
                    return;
                }

                _isTargetSupportInsufficient = value;
                OnPropertyChangedWithValue(
                    value,
                    nameof(IsTargetSupportInsufficient));
            }
        }

        [DataSourceProperty]
        public int ResultBarOffererPercentage
        {
            get => _resultBarOffererPercentage;
            private set
            {
                if (_resultBarOffererPercentage == value)
                {
                    return;
                }

                _resultBarOffererPercentage = value;
                OnPropertyChangedWithValue(
                    value,
                    nameof(ResultBarOffererPercentage));
            }
        }

        [DataSourceProperty]
        public bool IsPlayerSupportInsufficient
        {
            get => _isPlayerSupportInsufficient;
            private set
            {
                if (_isPlayerSupportInsufficient == value)
                {
                    return;
                }

                _isPlayerSupportInsufficient = value;
                OnPropertyChangedWithValue(
                    value,
                    nameof(IsPlayerSupportInsufficient));
            }
        }
        [DataSourceProperty] public HintViewModel AutoBalanceHint { get; }
        [DataSourceProperty] public InputKeyItemVM ResetInputKey { get; set; }
        [DataSourceProperty] public InputKeyItemVM DoneInputKey { get; set; }
        [DataSourceProperty] public InputKeyItemVM CancelInputKey { get; set; }

        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> LeftFiefList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> RightFiefList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> LeftPrisonerList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> RightPrisonerList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> LeftItemList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> RightItemList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> LeftOtherList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> RightOtherList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> LeftDiplomaticList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> RightDiplomaticList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> LeftGoldList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> RightGoldList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> LeftOfferList { get; }
        [DataSourceProperty] public MBBindingList<KingdomNegotiationItemVM> RightOfferList { get; }

        public override void RefreshValues()
        {
            base.RefreshValues();
            // The native barter header identifies the two negotiators. The
            // kingdom remains visible through the ruler's banner and portrait.
            LeftNameLbl = TargetKingdom?.Leader?.Name?.ToString()
                ?? TargetKingdom?.Name?.ToString()
                ?? string.Empty;
            RightNameLbl = PlayerKingdom?.Leader?.Name?.ToString()
                ?? PlayerKingdom?.Name?.ToString()
                ?? string.Empty;
            LeftMaxGold = TargetKingdom?.Leader?.Gold ?? 0;
            RightMaxGold = PlayerKingdom?.Leader?.Gold ?? 0;
            FiefLbl = new TextObject("{=MP_KingdomNegotiationFiefsTitle}Towns and Castles").ToString();
            PrisonerLbl = new TextObject("{=MP_KingdomNegotiationPrisonersTitle}Hero Prisoners").ToString();
            ItemLbl = string.Empty;
            OtherLbl = string.Empty;
            DiplomaticLbl = new TextObject("{=MP_KingdomNegotiationDiplomacyTitle}Diplomatic Treaties").ToString();
            CancelLbl = new TextObject("{=str_cancel}Cancel").ToString();
            ResetLbl = new TextObject("{=str_reset}Reset").ToString();
            OfferLbl = new TextObject(
                "{=MP_KingdomNegotiationSubmit}Submit Proposal")
                .ToString();
        }

        public override void OnFinalize()
        {
            ResetInputKey?.OnFinalize();
            DoneInputKey?.OnFinalize();
            CancelInputKey?.OnFinalize();
            LeftHero?.OnFinalize();
            RightHero?.OnFinalize();
            base.OnFinalize();
        }

        public void ExecuteCancel()
        {
            _close?.Invoke();
        }

        public void ExecuteOffer()
        {
            KingdomNegotiationDraft draft = CreateDraft();
            if (!KingdomNegotiationDraftValidator.TryValidate(
                    draft,
                    out TextObject reason))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(reason.ToString()));
                return;
            }

            KingdomNegotiationEvaluation playerEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    PlayerKingdom);
            KingdomNegotiationEvaluation targetEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    TargetKingdom);
            KingdomNegotiationEvaluationService.Log(playerEvaluation);
            KingdomNegotiationEvaluationService.Log(targetEvaluation);

            if (!KingdomNegotiationProposalService.SubmitPlayerProposal(
                    draft,
                    _close,
                    out reason))
            {
                InformationManager.DisplayMessage(
                    new InformationMessage(reason.ToString()));
                return;
            }

            TextObject result = new TextObject(
                "{=MP_KingdomNegotiationSubmitted}The compound proposal has been submitted to your council.");
            InformationManager.DisplayMessage(
                new InformationMessage(result.ToString()));
        }

        public void ExecuteReset()
        {
            ResetList(LeftOfferList);
            ResetList(RightOfferList);
            RefreshOfferState();
        }

        public void ExecuteAutoBalance()
        {
        }

        /// <summary>
        /// Captures the current central offer without exposing any UI objects
        /// to the later evaluation, voting and execution stages.
        /// </summary>
        public KingdomNegotiationDraft CreateDraft()
        {
            return new KingdomNegotiationDraft(
                PlayerKingdom,
                TargetKingdom,
                LeftOfferList.Concat(RightOfferList).Select(item =>
                    new KingdomNegotiationDraftTerm(
                        item.Type,
                        item.IsPlayerOwned
                            ? PlayerKingdom
                            : TargetKingdom,
                        item.Subject,
                        item.CurrentOfferedAmount)));
        }

        public void ExecuteTransferAllLeftFief() => TransferAll(LeftFiefList);
        public void ExecuteTransferAllRightFief() => TransferAll(RightFiefList);
        public void ExecuteTransferAllLeftPrisoner() => TransferAll(LeftPrisonerList);
        public void ExecuteTransferAllRightPrisoner() => TransferAll(RightPrisonerList);
        public void ExecuteTransferAllLeftItem() => TransferAll(LeftItemList);
        public void ExecuteTransferAllRightItem() => TransferAll(RightItemList);
        public void ExecuteTransferAllLeftOther() => TransferAll(LeftOtherList);
        public void ExecuteTransferAllRightOther() => TransferAll(RightOtherList);
        public void ExecuteTransferAllLeftDiplomatic() => TransferAll(LeftDiplomaticList);
        public void ExecuteTransferAllRightDiplomatic() => TransferAll(RightDiplomaticList);

        public void ExecuteTransferWithParameters(object[] parameters)
        {
        }

        public void SetInputKeys(HotKey reset, HotKey done, HotKey cancel)
        {
            ResetInputKey = InputKeyItemVM.CreateFromHotKey(reset, true);
            DoneInputKey = InputKeyItemVM.CreateFromHotKey(done, true);
            CancelInputKey = InputKeyItemVM.CreateFromHotKey(cancel, true);
            OnPropertyChanged(nameof(ResetInputKey));
            OnPropertyChanged(nameof(DoneInputKey));
            OnPropertyChanged(nameof(CancelInputKey));
        }

        private static MBBindingList<KingdomNegotiationItemVM> NewList()
        {
            return new MBBindingList<KingdomNegotiationItemVM>();
        }

        private void BuildSide(Kingdom kingdom, bool isPlayerSide)
        {
            if (kingdom == null)
            {
                return;
            }

            MBBindingList<KingdomNegotiationItemVM> gold = isPlayerSide
                ? RightGoldList
                : LeftGoldList;
            MBBindingList<KingdomNegotiationItemVM> fiefs = isPlayerSide
                ? RightFiefList
                : LeftFiefList;
            MBBindingList<KingdomNegotiationItemVM> prisoners = isPlayerSide
                ? RightPrisonerList
                : LeftPrisonerList;
            MBBindingList<KingdomNegotiationItemVM> diplomacy = isPlayerSide
                ? RightDiplomaticList
                : LeftDiplomaticList;

            int leaderGold = Math.Max(0, kingdom.Leader?.Gold ?? 0);
            if (leaderGold > 0)
            {
                gold.Add(CreateItem(
                    KingdomNegotiationTermType.Gold,
                    kingdom.StringId + ":gold",
                    new TextObject(
                        "{=MP_KingdomNegotiationDenars}Denars")
                        .ToString(),
                    kingdom.Leader,
                    isPlayerSide,
                    true,
                    leaderGold,
                    "gold_barterable"));
            }

            foreach (Town town in kingdom.Fiefs
                .OrderByDescending(x => x.IsTown)
                .ThenBy(x => x.Name.ToString()))
            {
                fiefs.Add(CreateItem(
                    KingdomNegotiationTermType.Settlement,
                    kingdom.StringId + ":settlement:" + town.Settlement.StringId,
                    town.Name.ToString(),
                    town.Settlement,
                    isPlayerSide,
                    false,
                    1,
                    "fief_barterable",
                    town.Settlement.SettlementComponent == null
                        ? "placeholder"
                        : town.Settlement.SettlementComponent
                            .BackgroundMeshName));
            }

            foreach (Hero hero in Hero.AllAliveHeroes.Where(x =>
                x.IsPrisoner
                && x.PartyBelongedToAsPrisoner?.MapFaction == kingdom))
            {
                prisoners.Add(CreateItem(
                    KingdomNegotiationTermType.PrisonerHero,
                    kingdom.StringId + ":prisoner:" + hero.StringId,
                    hero.Name.ToString(),
                    hero,
                    isPlayerSide,
                    false,
                    1,
                    "transfer_prisoner_barterable",
                    string.Empty,
                    new GenericImageIdentifierVM(
                        new CharacterImageIdentifier(
                            CharacterCode.CreateFrom(
                                hero.CharacterObject)))));
            }

            AddDiplomacyItems(diplomacy, kingdom, isPlayerSide);
        }

        private void AddDiplomacyItems(
            MBBindingList<KingdomNegotiationItemVM> list,
            Kingdom kingdom,
            bool isPlayerSide)
        {
            Kingdom otherKingdom = kingdom == PlayerKingdom
                ? TargetKingdom
                : PlayerKingdom;
            if (kingdom.IsAtWarWith(otherKingdom))
            {
                AddDiplomacyItem(list, kingdom, isPlayerSide,
                    KingdomNegotiationTermType.Peace, "peace",
                    "{=MP_NegotiationTermPeace}Peace");
            }
            else
            {
                ITradeAgreementsCampaignBehavior tradeBehavior =
                    Campaign.Current.GetCampaignBehavior<
                        ITradeAgreementsCampaignBehavior>();
                if (tradeBehavior == null
                    || !tradeBehavior.HasTradeAgreement(
                        kingdom,
                        otherKingdom,
                        out _))
                {
                    AddDiplomacyItem(list, kingdom, isPlayerSide,
                        KingdomNegotiationTermType.TradeAgreement, "trade",
                        "{=MP_NegotiationTermTrade}Trade Agreement");
                }

                IAllianceCampaignBehavior allianceBehavior =
                    Campaign.Current.GetCampaignBehavior<
                        IAllianceCampaignBehavior>();
                if (allianceBehavior == null
                    || !allianceBehavior.IsAllyWithKingdom(
                        kingdom,
                        otherKingdom))
                {
                    AddDiplomacyItem(list, kingdom, isPlayerSide,
                        KingdomNegotiationTermType.Alliance, "alliance",
                        "{=MP_NegotiationTermAlliance}Alliance");
                }
            }
            AddDiplomacyItem(list, kingdom, isPlayerSide,
                isPlayerSide
                    ? KingdomNegotiationTermType.PlayerBecomesVassal
                    : KingdomNegotiationTermType.TargetBecomesVassal,
                "vassal",
                isPlayerSide
                    ? "{=MP_NegotiationTermPlayerVassal}Our Kingdom Becomes Vassal"
                    : "{=MP_NegotiationTermTargetVassal}Target Becomes Vassal");
            AddDiplomacyItem(list, kingdom, isPlayerSide,
                isPlayerSide
                    ? KingdomNegotiationTermType.PlayerBecomesPuppet
                    : KingdomNegotiationTermType.TargetBecomesPuppet,
                "puppet",
                isPlayerSide
                    ? "{=MP_NegotiationTermPlayerPuppet}Our Kingdom Becomes Puppet"
                    : "{=MP_NegotiationTermTargetPuppet}Target Becomes Puppet");
        }

        private void AddDiplomacyItem(
            MBBindingList<KingdomNegotiationItemVM> list,
            Kingdom kingdom,
            bool isPlayerSide,
            KingdomNegotiationTermType type,
            string key,
            string label)
        {
            list.Add(CreateItem(type,
                kingdom.StringId + ":diplomacy:" + key,
                new TextObject(label).ToString(),
                kingdom,
                isPlayerSide,
                false,
                1,
                "peace_barterable"));
        }

        private KingdomNegotiationItemVM CreateItem(
            KingdomNegotiationTermType type,
            string key,
            string label,
            object subject,
            bool isPlayerSide,
            bool isMultiple,
            int total,
            string barterableType,
            string fiefFileName = "",
            ImageIdentifierVM visualIdentifier = null)
        {
            return new KingdomNegotiationItemVM(
                type,
                key,
                label,
                subject,
                isPlayerSide,
                isMultiple,
                total,
                barterableType,
                fiefFileName,
                visualIdentifier,
                TransferItem);
        }

        private void TransferItem(KingdomNegotiationItemVM item)
        {
            MBBindingList<KingdomNegotiationItemVM> offer =
                item.IsPlayerOwned ? RightOfferList : LeftOfferList;

            if (item.IsOffered)
            {
                offer.Remove(item);
                item.IsOffered = false;
                RefreshOfferState();
                return;
            }

            if (KingdomNegotiationTermRules.IsSubjectTerm(item.Type))
            {
                RemoveSubjectTerms(LeftOfferList);
                RemoveSubjectTerms(RightOfferList);
            }
            else if (KingdomNegotiationTermRules.IsBilateralTreaty(
                item.Type))
            {
                RemoveTermType(LeftOfferList, item.Type);
                RemoveTermType(RightOfferList, item.Type);
            }
            else if (item.Type == KingdomNegotiationTermType.Gold)
            {
                MBBindingList<KingdomNegotiationItemVM> oppositeOffer =
                    item.IsPlayerOwned ? LeftOfferList : RightOfferList;
                RemoveTermType(
                    oppositeOffer,
                    KingdomNegotiationTermType.Gold);
            }

            item.IsOffered = true;
            offer.Add(item);
            RefreshOfferState();
        }

        private static void RemoveSubjectTerms(
            MBBindingList<KingdomNegotiationItemVM> offer)
        {
            foreach (KingdomNegotiationItemVM item in offer
                .Where(x => KingdomNegotiationTermRules.IsSubjectTerm(x.Type))
                .ToList())
            {
                item.IsOffered = false;
                offer.Remove(item);
            }
        }

        private static void RemoveTermType(
            MBBindingList<KingdomNegotiationItemVM> offer,
            KingdomNegotiationTermType type)
        {
            foreach (KingdomNegotiationItemVM item in offer
                .Where(x => x.Type == type)
                .ToList())
            {
                item.IsOffered = false;
                offer.Remove(item);
            }
        }

        private void TransferAll(
            MBBindingList<KingdomNegotiationItemVM> source)
        {
            _deferOfferRefresh = true;
            try
            {
                foreach (KingdomNegotiationItemVM item in source
                    .Where(x => !x.IsOffered)
                    .ToList())
                {
                    TransferItem(item);
                }
            }
            finally
            {
                _deferOfferRefresh = false;
                RefreshOfferState();
            }
        }

        private static void ResetList(
            MBBindingList<KingdomNegotiationItemVM> offer)
        {
            foreach (KingdomNegotiationItemVM item in offer.ToList())
            {
                item.IsOffered = false;
                offer.Remove(item);
            }
        }

        /// <summary>
        /// Re-evaluates the draft from both councils' perspectives and exposes
        /// their influence-weighted acceptance shares to the two barter-style
        /// progress bars. The proposal can still be assembled while support
        /// is low; final submission performs authoritative validation again.
        /// </summary>
        private void RefreshOfferState()
        {
            if (_deferOfferRefresh)
            {
                return;
            }

            OnPropertyChanged(nameof(IsOfferDisabled));

            if (LeftOfferList.Count == 0 && RightOfferList.Count == 0)
            {
                ResultBarOtherPercentage = 0;
                ResultBarOffererPercentage = 0;
                IsTargetSupportInsufficient = true;
                IsPlayerSupportInsufficient = true;
                return;
            }

            KingdomNegotiationDraft draft = CreateDraft();
            KingdomNegotiationEvaluation targetEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    TargetKingdom);
            KingdomNegotiationEvaluation playerEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    draft,
                    PlayerKingdom);

            ResultBarOtherPercentage = ToPercentage(
                targetEvaluation.AcceptShare);
            ResultBarOffererPercentage = ToPercentage(
                playerEvaluation.AcceptShare);
            IsTargetSupportInsufficient = !targetEvaluation.WouldAccept;
            IsPlayerSupportInsufficient = !playerEvaluation.WouldAccept;
        }

        private static int ToPercentage(float share)
        {
            return (int)Math.Round(
                Math.Max(0f, Math.Min(1f, share)) * 100f);
        }
    }
}
