// Owns the single-movie marriage workflow and builds the authoritative target-clan index.
using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedFamily.Campaign;
using ModifiedFamily.Core;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using CampaignClan = TaleWorlds.CampaignSystem.Clan;
using GameCampaign = TaleWorlds.CampaignSystem.Campaign;

namespace ModifiedFamily.UI.Marriage
{
    internal enum MarriageUiPage
    {
        ClanList,
        ClanDetail,
        HeroDetail
    }

    internal sealed class MarriageNavigationVM : ViewModel
    {
        private readonly FamilyProposalService _service;
        private MarriageUiPage _page;
        private string _breadcrumbText;
        private string _emptyStateText;
        private MarriageClanDetailsVM _clanDetails;
        private MarriageHeroDetailsVM _heroDetails;
        private bool _isTermsOpen;
        private bool _isSubmitting;
        private MarriageDirection _selectedDirection;
        private int _giftMode;
        private int _giftAmount;
        private string _termsValidationText;

        internal MarriageNavigationVM(Hero ownHero)
        {
            OwnHero = ownHero;
            _service = GameCampaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service;
            Clans = new MBBindingList<MarriageClanItemVM>();
            Title = new TextObject("{=MF_UI_MarriageScreenTitle}Marriage Screen Title").ToString();
            ClanListTitle = new TextObject("{=MF_UI_MarriageClanListTitle}Marriage Clan List Title").ToString();
            OwnHeroSummaryText = new TextObject("{=MF_UI_MarriageForHero}Marriage For Hero {HERO}")
                .SetTextVariable("HERO", ownHero?.Name ?? new TextObject(string.Empty)).ToString();
            OwnHeroVisual = ownHero == null ? null : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(ownHero.CharacterObject));
            OwnHeroCharacter = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            OwnHeroTraits = new MBBindingList<EncyclopediaTraitItemVM>();
            OwnHeroSkills = new MBBindingList<EncyclopediaSkillVM>();
            MarriageDirectionSelector = new SelectorVM<SelectorItemVM>(0, OnMarriageDirectionChanged);
            MarriageDirectionSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_NormalMarriageDirection}Normal Marriage Direction")));
            MarriageDirectionSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_MatrilocalMarriageDirection}Matrilocal Marriage Direction")));
            MarriageGiftSelector = new SelectorVM<SelectorItemVM>(0, OnMarriageGiftChanged);
            MarriageGiftSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_NoMarriageGift}No Marriage Gift")));
            MarriageGiftSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_OtherPaysMarriageGift}Other Pays Marriage Gift")));
            MarriageGiftSelector.AddItem(new SelectorItemVM(new TextObject("{=MF_UI_PlayerPaysMarriageGift}Player Pays Marriage Gift")));
            if (ownHero != null)
            {
                // Reuse the native encyclopedia character source so the tableau displays the exact live hero.
                // Mirror EncyclopediaHeroPageVM exactly: load the hero, then remove the head and mount
                // slots before the tableau is created. This keeps the presentation focused on the hero
                // and prevents humanoid idle actions from being forwarded to a horse entity.
                OwnHeroCharacter.FillFrom(ownHero, -1, ownHero.IsNotable, true);
                OwnHeroCharacter.SetEquipment(EquipmentIndex.Horse, default(EquipmentElement));
                OwnHeroCharacter.SetEquipment(EquipmentIndex.HorseHarness, default(EquipmentElement));
                OwnHeroCharacter.SetEquipment(EquipmentIndex.Head, default(EquipmentElement));
                foreach (TraitObject trait in DefaultTraits.Personality)
                {
                    int value = ownHero.GetTraitLevel(trait);
                    if (value != 0) OwnHeroTraits.Add(new EncyclopediaTraitItemVM(trait, value));
                }

                foreach (SkillObject skill in MBObjectManager.Instance.GetObjectTypeList<SkillObject>())
                {
                    int value = ownHero.GetSkillValue(skill);
                    if (value > 0) OwnHeroSkills.Add(new EncyclopediaSkillVM(skill, value));
                }
            }
            OwnHeroNameText = ownHero?.Name?.ToString() ?? "-";
            OwnHeroClanText = ownHero?.Clan?.Name?.ToString() ?? "-";
            OwnHeroCultureText = ownHero?.Clan?.Culture?.Name?.ToString() ?? "-";
            OwnHeroAgeText = ownHero == null ? "-" : ((int)ownHero.Age).ToString();
            OwnHeroRoleText = ownHero?.Clan?.Leader == ownHero
                ? new TextObject("{=MF_UI_ClanLeaderRole}Clan Leader Role").ToString()
                : new TextObject("{=MF_UI_ClanMemberRole}Clan Member Role").ToString();
            CloseText = new TextObject("{=MF_UI_Close}Close").ToString();
            KingdomColumnText = new TextObject("{=MF_UI_MarriageKingdomColumn}Marriage Kingdom Column").ToString();
            CultureColumnText = new TextObject("{=MF_UI_MarriageCultureColumn}Marriage Culture Column").ToString();
            MatchColumnText = new TextObject("{=MF_UI_MarriageMatchColumn}Marriage Match Column").ToString();
            RelationColumnText = new TextObject("{=MF_UI_MarriageRelationColumn}Marriage Relation Column").ToString();
            CandidateColumnText = new TextObject("{=MF_UI_MarriageCandidateColumn}Marriage Candidate Column").ToString();
            EmptyStateText = new TextObject("{=MF_UI_NoMarriageCandidates}No Marriage Candidates").ToString();
            LoadClans();
            SetPage(MarriageUiPage.ClanList);
        }

        internal Hero OwnHero { get; }
        internal CampaignClan SelectedTargetClan { get; private set; }
        internal Hero SelectedTargetHero { get; private set; }

        [DataSourceProperty] public MBBindingList<MarriageClanItemVM> Clans { get; }
        [DataSourceProperty] public MarriageClanDetailsVM ClanDetails { get => _clanDetails; private set { _clanDetails = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public MarriageHeroDetailsVM HeroDetails { get => _heroDetails; private set { _heroDetails = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string ClanListTitle { get; }
        [DataSourceProperty] public string OwnHeroSummaryText { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM OwnHeroVisual { get; }
        [DataSourceProperty] public HeroViewModel OwnHeroCharacter { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaTraitItemVM> OwnHeroTraits { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaSkillVM> OwnHeroSkills { get; }
        [DataSourceProperty] public SelectorVM<SelectorItemVM> MarriageDirectionSelector { get; }
        [DataSourceProperty] public SelectorVM<SelectorItemVM> MarriageGiftSelector { get; }
        [DataSourceProperty] public bool OwnHeroHasSkills => OwnHeroSkills.Count > 0;
        [DataSourceProperty] public string SkillsText => new TextObject("{=hVve5rNi}Skills").ToString();
        [DataSourceProperty] public string OwnHeroNameText { get; }
        [DataSourceProperty] public string OwnHeroClanText { get; }
        [DataSourceProperty] public string OwnHeroCultureText { get; }
        [DataSourceProperty] public string OwnHeroAgeText { get; }
        [DataSourceProperty] public string OwnHeroRoleText { get; }
        [DataSourceProperty] public string CloseText { get; }
        [DataSourceProperty] public string KingdomColumnText { get; }
        [DataSourceProperty] public string CultureColumnText { get; }
        [DataSourceProperty] public string MatchColumnText { get; }
        [DataSourceProperty] public string RelationColumnText { get; }
        [DataSourceProperty] public string CandidateColumnText { get; }
        [DataSourceProperty] public bool HasClans => Clans.Count > 0;
        [DataSourceProperty] public bool IsClanListVisible => _page == MarriageUiPage.ClanList;
        [DataSourceProperty] public bool IsClanDetailVisible => _page == MarriageUiPage.ClanDetail;
        [DataSourceProperty] public bool IsHeroDetailVisible => _page == MarriageUiPage.HeroDetail;
        [DataSourceProperty] public bool IsTermsOpen { get => _isTermsOpen; private set { _isTermsOpen = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool IsSubmitting { get => _isSubmitting; private set { _isSubmitting = value; OnPropertyChangedWithValue(value); OnPropertyChanged(nameof(CanSubmitTerms)); } }
        [DataSourceProperty] public bool IsNormalDirection => _selectedDirection == MarriageDirection.FemaleJoinsMaleClan;
        [DataSourceProperty]
        public bool IsMatrilocalDirection
        {
            get => _selectedDirection == MarriageDirection.MaleJoinsFemaleClan;
            set
            {
                MarriageDirection direction = value
                    ? MarriageDirection.MaleJoinsFemaleClan
                    : MarriageDirection.FemaleJoinsMaleClan;
                if (_selectedDirection == direction) return;
                _selectedDirection = direction;
                OnPropertyChangedWithValue(value);
                OnPropertyChanged(nameof(IsNormalDirection));
                RefreshTermsValidation();
            }
        }
        [DataSourceProperty] public bool IsNoGift => _giftMode == 0;
        [DataSourceProperty] public bool IsPlayerPays => _giftMode == 1;
        [DataSourceProperty] public bool IsOtherPays => _giftMode == 2;
        [DataSourceProperty] public int GiftAmount { get => _giftAmount; set { _giftAmount = Math.Max(0, value); OnPropertyChangedWithValue(_giftAmount); RefreshTermsValidation(); } }
        [DataSourceProperty] public string TermsValidationText { get => _termsValidationText; private set { _termsValidationText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public bool CanSubmitTerms => !_isSubmitting && _page == MarriageUiPage.HeroDetail && BuildDraft(out FamilyResult result) != null && result.IsAllowed;
        [DataSourceProperty] public bool IsGiftAmountEnabled => _giftMode != 0;
        [DataSourceProperty] public string TermsTitle => new TextObject("{=MF_UI_MarriageTermsTitle}Marriage Terms Title").ToString();
        [DataSourceProperty] public string DirectionTitle => new TextObject("{=MF_UI_MarriageDirectionTitle}Marriage Direction Title").ToString();
        [DataSourceProperty] public string NormalDirectionText => new TextObject("{=MF_UI_NormalMarriageDirection}Normal Marriage Direction").ToString();
        [DataSourceProperty] public string MatrilocalDirectionText => new TextObject("{=MF_UI_MatrilocalMarriageDirection}Matrilocal Marriage Direction").ToString();
        [DataSourceProperty] public string GiftTitle => new TextObject("{=MF_UI_MarriageGiftTitle}Marriage Gift Title").ToString();
        [DataSourceProperty] public string NoGiftText => new TextObject("{=MF_UI_NoMarriageGift}No Marriage Gift").ToString();
        [DataSourceProperty] public string PlayerPaysText => new TextObject("{=MF_UI_PlayerPaysMarriageGift}Player Pays Marriage Gift").ToString();
        [DataSourceProperty] public string OtherPaysText => new TextObject("{=MF_UI_OtherPaysMarriageGift}Other Pays Marriage Gift").ToString();
        [DataSourceProperty] public string GiftAmountText => new TextObject("{=MF_UI_MarriageGiftAmount}Marriage Gift Amount").ToString();
        [DataSourceProperty] public string OpenTermsText => new TextObject("{=MF_UI_OpenMarriageTerms}Open Marriage Terms").ToString();
        [DataSourceProperty] public string SubmitTermsText => new TextObject("{=MF_UI_SubmitMarriageProposal}Submit Marriage Proposal").ToString();
        [DataSourceProperty] public string CancelTermsText => new TextObject("{=MF_UI_Cancel}Cancel").ToString();
        [DataSourceProperty] public bool CanGoBack => _page != MarriageUiPage.ClanList;
        [DataSourceProperty] public string BreadcrumbText { get => _breadcrumbText; private set { _breadcrumbText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string EmptyStateText { get => _emptyStateText; private set { _emptyStateText = value; OnPropertyChangedWithValue(value); } }

        public void ExecuteBack()
        {
            if (_page == MarriageUiPage.HeroDetail) SetPage(MarriageUiPage.ClanDetail);
            else if (_page == MarriageUiPage.ClanDetail) SetPage(MarriageUiPage.ClanList);
        }

        public void ExecuteClose() => FamilyActionScreenHost.Close();

        public void ExecuteOpenTerms()
        {
            if (SelectedTargetHero == null) return;
            FamilyResult normal = ValidateDraft(SelectedTargetHero, MarriageDirection.FemaleJoinsMaleClan);
            _selectedDirection = normal.IsAllowed ? MarriageDirection.FemaleJoinsMaleClan : MarriageDirection.MaleJoinsFemaleClan;
            _giftMode = 0;
            GiftAmount = 0;
            IsTermsOpen = true;
            NotifyTermsSelectionChanged();
            RefreshTermsValidation();
        }

        public void ExecuteCloseTerms() => IsTermsOpen = false;
        public void ExecuteSelectNormalDirection() { _selectedDirection = MarriageDirection.FemaleJoinsMaleClan; NotifyTermsSelectionChanged(); RefreshTermsValidation(); }
        public void ExecuteSelectMatrilocalDirection() { _selectedDirection = MarriageDirection.MaleJoinsFemaleClan; NotifyTermsSelectionChanged(); RefreshTermsValidation(); }
        public void ExecuteSelectNoGift() { _giftMode = 0; GiftAmount = 0; NotifyTermsSelectionChanged(); RefreshTermsValidation(); }
        public void ExecuteSelectPlayerPays() { _giftMode = 1; NotifyTermsSelectionChanged(); RefreshTermsValidation(); }
        public void ExecuteSelectOtherPays() { _giftMode = 2; NotifyTermsSelectionChanged(); RefreshTermsValidation(); }
        public void ExecuteDecreaseGiftAmount() => GiftAmount = Math.Max(0, GiftAmount - 100);
        public void ExecuteIncreaseGiftAmount() => GiftAmount = Math.Min(100000000, GiftAmount + 100);

        public void ExecuteSubmitTerms()
        {
            FamilyDiagnostics.DebugLog($"Marriage screen received submit command: ownHero={OwnHero?.StringId ?? "none"}, target={SelectedTargetHero?.StringId ?? "none"}, direction={_selectedDirection}, giftMode={_giftMode}, amount={GiftAmount}.");
            MarriageProposalDraft draft = BuildDraft(out FamilyResult validation);
            FamilyDiagnostics.DebugLog($"Marriage submission draft built: draft={(draft == null ? "null" : "ok")}, validation={validation.IsAllowed}/{validation.ReasonCode}.");
            if (draft == null || !validation.IsAllowed)
            {
                FamilyDiagnostics.Warn($"Marriage screen rejected submission: {validation.ReasonCode}.");
                RefreshTermsValidation();
                return;
            }
            SubmitConfirmed();
        }

        private void SubmitConfirmed()
        {
            if (IsSubmitting) return;
            MarriageProposalDraft draft = BuildDraft(out FamilyResult validation);
            if (draft == null || !validation.IsAllowed) { RefreshTermsValidation(); return; }
            IsSubmitting = true;
            FamilyDiagnostics.DebugLog("Marriage screen is calling FamilyProposalService.TryCreateMarriageProposal.");
            FamilyResult result;
            try
            {
                result = _service.TryCreateMarriageProposal(draft);
            }
            catch (Exception exception)
            {
                IsSubmitting = false;
                FamilyDiagnostics.Error("An exception occurred while submitting from the marriage screen.", exception);
                TermsValidationText = new TextObject("{=MF_UI_MarriageSubmitFailed}Marriage Submit Failed").ToString();
                HeroDetails?.RefreshArrangement();
                return;
            }
            IsSubmitting = false;
            FamilyDiagnostics.DebugLog($"Marriage service returned: allowed={result.IsAllowed}, reason={result.ReasonCode}, proposal={result.ProposalId ?? "-"}.");
            if (!result.IsAllowed)
            {
                TermsValidationText = DenialText(result.ReasonCode);
                OnPropertyChanged(nameof(CanSubmitTerms));
                return;
            }
            InformationManager.DisplayMessage(new InformationMessage(new TextObject("{=MF_UI_MarriageProposalSent}Marriage Proposal Sent").ToString(), Colors.Green));
            FamilyDiagnostics.Notice($"Player submitted a marriage request from the marriage screen: {OwnHero?.Name} and {SelectedTargetHero?.Name}, proposal={result.ProposalId ?? "-"}.");
            FamilyDiagnostics.DebugLog("Marriage proposal created successfully; closing the marriage screen.");
            FamilyActionScreenHost.Close();
        }

        public override void OnFinalize()
        {
            HeroDetails?.OnFinalize();
            OwnHeroCharacter?.OnFinalize();
            base.OnFinalize();
        }

        private void LoadClans()
        {
            int examinedClans = 0;
            int usableClans = 0;
            int oppositeGenderHeroes = 0;
            int negotiableHeroes = 0;
            int eligibleClans = 0;
            int eligibleHeroes = 0;
            var rejectionCounts = new Dictionary<FamilyReasonCode, int>();
            var rows = new List<MarriageClanItemVM>();
            if (OwnHero == null || _service == null)
            {
                FamilyDiagnostics.Warn("Could not build the marriage clan list because the current hero or proposal service is unavailable.");
                return;
            }

            FamilyProposal activeProposal = _service.GetActiveProposalFor(OwnHero);
            if (activeProposal != null)
            {
                EmptyStateText = new TextObject("{=MF_UI_OwnHeroProposalLocked}Own Hero Proposal Locked {HERO} {ID}")
                    .SetTextVariable("HERO", OwnHero.Name)
                    .SetTextVariable("ID", activeProposal.ProposalId ?? "-")
                    .ToString();
                FamilyDiagnostics.DebugLog($"Marriage screen [ClanList] produced no candidates: ownHero={OwnHero.StringId}/{OwnHero.Name} is locked by active proposal {activeProposal.ProposalId}, type={activeProposal.ProposalType}, status={activeProposal.Status}.");
                return;
            }

            foreach (CampaignClan clan in CampaignClan.All)
            {
                if (clan == CampaignClan.PlayerClan) continue;
                examinedClans++;
                FamilyResult clanResult = MarriageEligibilityModel.CanUseClan(clan);
                if (!clanResult.IsAllowed)
                {
                    CountRejection(rejectionCounts, clanResult.ReasonCode);
                    continue;
                }
                usableClans++;
                int candidateCount = 0;
                foreach (Hero target in clan.Heroes)
                {
                    if (target == null || target.Clan == null || target.IsFemale == OwnHero.IsFemale) continue;
                    oppositeGenderHeroes++;
                    FamilyResult heroResult = MarriageEligibilityModel.CanNegotiateHero(target);
                    if (!heroResult.IsAllowed)
                    {
                        CountRejection(rejectionCounts, heroResult.ReasonCode);
                        continue;
                    }
                    negotiableHeroes++;
                    FamilyResult normal = ValidateDraft(target, MarriageDirection.FemaleJoinsMaleClan);
                    FamilyResult matrilocal = normal.IsAllowed ? normal : ValidateDraft(target, MarriageDirection.MaleJoinsFemaleClan);
                    if (normal.IsAllowed || matrilocal.IsAllowed) candidateCount++;
                    else
                    {
                        CountRejection(rejectionCounts, normal.ReasonCode);
                        if (matrilocal.ReasonCode != normal.ReasonCode) CountRejection(rejectionCounts, matrilocal.ReasonCode);
                    }
                }
                if (candidateCount <= 0) continue;
                int tier = GetMatchTier(CampaignClan.PlayerClan, clan);
                int relation = CampaignClan.PlayerClan.GetRelationWithClan(clan);
                rows.Add(new MarriageClanItemVM(clan, candidateCount, tier, relation, SelectClan));
                eligibleClans++;
                eligibleHeroes += candidateCount;
            }

            foreach (MarriageClanItemVM row in rows
                .OrderBy(x => x.MatchTier)
                .ThenByDescending(x => x.RelationValue)
                .ThenByDescending(x => x.CandidateCount)
                .ThenBy(x => x.Name, StringComparer.CurrentCulture))
                Clans.Add(row);

            string reasons = rejectionCounts.Count == 0 ? "none" : string.Join(", ", rejectionCounts.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value));
            FamilyDiagnostics.DebugLog($"Marriage screen [ClanList]: ownHero={OwnHero.StringId}/{OwnHero.Name}; examinedClans={examinedClans}, usableClans={usableClans}, oppositeGenderHeroes={oppositeGenderHeroes}, negotiableHeroes={negotiableHeroes}, displayedClans={eligibleClans}, displayedHeroes={eligibleHeroes}; rejectionReasons={reasons}.");
        }

        private FamilyResult ValidateDraft(Hero target, MarriageDirection direction) => _service.CanCreateMarriageProposal(new MarriageProposalDraft
        {
            ProposerClan = CampaignClan.PlayerClan,
            ReceiverClan = target.Clan,
            HeroA = OwnHero,
            HeroB = target,
            Direction = direction,
            GiftAmount = 0
        });

        private static void CountRejection(IDictionary<FamilyReasonCode, int> counts, FamilyReasonCode reason)
        {
            if (reason == FamilyReasonCode.None) return;
            counts[reason] = counts.TryGetValue(reason, out int count) ? count + 1 : 1;
        }

        private static int GetMatchTier(CampaignClan ownClan, CampaignClan targetClan)
        {
            bool sameKingdom = ownClan?.Kingdom != null && ownClan.Kingdom == targetClan?.Kingdom;
            bool sameCulture = ownClan?.Culture != null && ownClan.Culture == targetClan?.Culture;
            if (sameKingdom && sameCulture) return 0;
            if (sameCulture) return 1;
            if (sameKingdom) return 2;
            return 3;
        }

        private void SelectClan(MarriageClanItemVM item)
        {
            SelectedTargetClan = item?.Clan;
            SelectedTargetHero = null;
            if (SelectedTargetClan == null) return;

            List<Hero> eligibleHeroes = GetEligibleHeroes(SelectedTargetClan).ToList();
            ClanDetails = new MarriageClanDetailsVM(SelectedTargetClan, eligibleHeroes, SelectHero);
            FamilyDiagnostics.DebugLog($"Marriage screen [ClanDetail]: targetClan={SelectedTargetClan.StringId}/{SelectedTargetClan.Name}, liveEligibleCandidates={eligibleHeroes.Count}.");
            SetPage(MarriageUiPage.ClanDetail);
        }

        private IEnumerable<Hero> GetEligibleHeroes(CampaignClan clan)
        {
            if (clan == null || OwnHero == null || _service == null) yield break;
            foreach (Hero target in clan.Heroes.OrderByDescending(x => x?.Age ?? 0f))
            {
                if (target == null || target.Clan != clan || target.IsFemale == OwnHero.IsFemale) continue;
                if (!MarriageEligibilityModel.CanNegotiateHero(target).IsAllowed) continue;
                FamilyResult normal = ValidateDraft(target, MarriageDirection.FemaleJoinsMaleClan);
                FamilyResult matrilocal = normal.IsAllowed ? normal : ValidateDraft(target, MarriageDirection.MaleJoinsFemaleClan);
                if (normal.IsAllowed || matrilocal.IsAllowed) yield return target;
            }
        }

        private void SelectHero(MarriageHeroItemVM item)
        {
            SelectedTargetHero = item?.Hero;
            if (SelectedTargetHero == null) return;
            HeroDetails?.OnFinalize();
            HeroDetails = new MarriageHeroDetailsVM(this, OwnHero, SelectedTargetHero);
            InitializeTerms();
            FamilyDiagnostics.DebugLog($"Marriage screen [HeroDetail]: ownHero={OwnHero?.StringId ?? "none"}/{OwnHero?.Name?.ToString() ?? "none"}, target={SelectedTargetHero.StringId}/{SelectedTargetHero.Name}, targetClan={SelectedTargetClan?.StringId ?? "none"}.");
            SetPage(MarriageUiPage.HeroDetail);
            RefreshTermsValidation();
        }

        private void InitializeTerms()
        {
            FamilyResult normal = ValidateDraft(SelectedTargetHero, MarriageDirection.FemaleJoinsMaleClan);
            _selectedDirection = normal.IsAllowed ? MarriageDirection.FemaleJoinsMaleClan : MarriageDirection.MaleJoinsFemaleClan;
            MarriageDirectionSelector.SelectedIndex = _selectedDirection == MarriageDirection.MaleJoinsFemaleClan ? 1 : 0;
            _giftMode = 0;
            MarriageGiftSelector.SelectedIndex = 0;
            _giftAmount = 0;
            NotifyTermsSelectionChanged();
            OnPropertyChanged(nameof(GiftAmount));
        }

        private void OnMarriageDirectionChanged(SelectorVM<SelectorItemVM> selector)
        {
            if (selector == null) return;
            _selectedDirection = selector.SelectedIndex == 1
                ? MarriageDirection.MaleJoinsFemaleClan
                : MarriageDirection.FemaleJoinsMaleClan;
            NotifyTermsSelectionChanged();
            RefreshTermsValidation();
        }

        private void OnMarriageGiftChanged(SelectorVM<SelectorItemVM> selector)
        {
            if (selector == null) return;
            _giftMode = selector.SelectedIndex == 1 ? 2 : selector.SelectedIndex == 2 ? 1 : 0;
            if (_giftMode == 0)
            {
                _giftAmount = 0;
                OnPropertyChanged(nameof(GiftAmount));
            }
            NotifyTermsSelectionChanged();
            RefreshTermsValidation();
        }

        private void SetPage(MarriageUiPage page)
        {
            _page = page;
            BreadcrumbText = page == MarriageUiPage.ClanList
                ? new TextObject("{=MF_UI_MarriageBreadcrumbClans}Marriage Breadcrumb Clans").ToString()
                : page == MarriageUiPage.ClanDetail
                    ? new TextObject("{=MF_UI_MarriageBreadcrumbClanDetail}Marriage Breadcrumb Clan Detail {CLAN}").SetTextVariable("CLAN", SelectedTargetClan?.Name ?? new TextObject("-")).ToString()
                    : new TextObject("{=MF_UI_MarriageBreadcrumbHeroDetail}Marriage Breadcrumb Hero Detail {CLAN} {HERO}")
                        .SetTextVariable("CLAN", SelectedTargetClan?.Name ?? new TextObject("-"))
                        .SetTextVariable("HERO", SelectedTargetHero?.Name ?? new TextObject("-"))
                        .ToString();
            OnPropertyChanged(nameof(IsClanListVisible));
            OnPropertyChanged(nameof(IsClanDetailVisible));
            OnPropertyChanged(nameof(IsHeroDetailVisible));
            OnPropertyChanged(nameof(IsTermsOpen));
            OnPropertyChanged(nameof(CanGoBack));
            FamilyDiagnostics.DebugLog($"Marriage screen changed to [{page}]: ownHero={OwnHero?.StringId ?? "none"}/{OwnHero?.Name?.ToString() ?? "none"}.");
        }

        private MarriageProposalDraft BuildDraft(out FamilyResult validation)
        {
            validation = FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (_service == null || OwnHero == null || SelectedTargetHero == null || SelectedTargetHero.Clan == null) return null;
            CampaignClan payer = _giftMode == 1 ? CampaignClan.PlayerClan : _giftMode == 2 ? SelectedTargetHero.Clan : null;
            int amount = _giftMode == 0 ? 0 : Math.Max(0, GiftAmount);
            if (amount > 0 && payer?.Leader != null && payer.Leader.Gold < amount)
            {
                validation = FamilyResult.Deny(FamilyReasonCode.InsufficientFunds);
                return new MarriageProposalDraft();
            }
            var draft = new MarriageProposalDraft
            {
                ProposerClan = CampaignClan.PlayerClan,
                ReceiverClan = SelectedTargetHero.Clan,
                HeroA = OwnHero,
                HeroB = SelectedTargetHero,
                Direction = _selectedDirection,
                GoldPayerClan = amount > 0 ? payer : null,
                GiftAmount = amount
            };
            validation = _service.CanCreateMarriageProposal(draft);
            return draft;
        }

        private void RefreshTermsValidation()
        {
            if (SelectedTargetHero == null) return;
            BuildDraft(out FamilyResult result);
            TermsValidationText = result.IsAllowed
                ? new TextObject("{=MF_UI_MarriageTermsValid}Marriage Terms Valid").ToString()
                : DenialText(result.ReasonCode);
            OnPropertyChanged(nameof(CanSubmitTerms));
            HeroDetails?.RefreshArrangement();
        }

        private void NotifyTermsSelectionChanged()
        {
            OnPropertyChanged(nameof(IsNormalDirection));
            OnPropertyChanged(nameof(IsMatrilocalDirection));
            OnPropertyChanged(nameof(IsNoGift));
            OnPropertyChanged(nameof(IsPlayerPays));
            OnPropertyChanged(nameof(IsOtherPays));
            OnPropertyChanged(nameof(IsGiftAmountEnabled));
        }

        private static string DenialText(FamilyReasonCode reason)
        {
            switch (reason)
            {
                case FamilyReasonCode.LeaderCannotLeave: return new TextObject("{=MF_UI_DenialLeaderCannotLeave}Denial Leader Cannot Leave").ToString();
                case FamilyReasonCode.InsufficientFunds: return new TextObject("{=MF_UI_DenialInsufficientFunds}Denial Insufficient Funds").ToString();
                case FamilyReasonCode.AtWar: return new TextObject("{=MF_UI_DenialAtWar}Denial At War").ToString();
                case FamilyReasonCode.CultureForbidden: return new TextObject("{=MF_UI_DenialCultureForbidden}Denial Culture Forbidden").ToString();
                case FamilyReasonCode.HeroLocked: return new TextObject("{=MF_UI_DenialHeroLocked}Denial Hero Locked").ToString();
                case FamilyReasonCode.PairCooldown: return new TextObject("{=MF_UI_DenialPairCooldown}Denial Pair Cooldown").ToString();
                default: return new TextObject("{=MF_UI_DenialMarriageInvalid}Denial Marriage Invalid").ToString();
            }
        }
    }

    internal sealed class MarriageClanItemVM : ViewModel
    {
        private readonly Action<MarriageClanItemVM> _select;

        internal MarriageClanItemVM(CampaignClan clan, int candidateCount, int matchTier, int relationValue, Action<MarriageClanItemVM> select)
        {
            Clan = clan;
            CandidateCount = candidateCount;
            MatchTier = matchTier;
            RelationValue = relationValue;
            _select = select;
            Name = clan.Name.ToString();
            KingdomName = clan.Kingdom?.Name?.ToString() ?? new TextObject("{=MF_UI_IndependentClan}Independent Clan").ToString();
            CultureName = clan.Culture?.Name?.ToString() ?? "-";
            RelationText = relationValue > 0 ? "+" + relationValue : relationValue.ToString();
            CandidateCountText = candidateCount.ToString();
            MatchTierText = GetTierText(matchTier);
            LeaderName = clan.Leader?.Name?.ToString() ?? "-";
            KingdomFieldText = FormatField("{=MF_UI_MarriageKingdomField}Marriage Kingdom Field {VALUE}", KingdomName);
            CultureFieldText = FormatField("{=MF_UI_MarriageCultureField}Marriage Culture Field {VALUE}", CultureName);
            LeaderFieldText = FormatField("{=MF_UI_MarriageLeaderField}Marriage Leader Field {VALUE}", LeaderName);
            RelationFieldText = FormatField("{=MF_UI_MarriageRelationField}Marriage Relation Field {VALUE}", RelationText);
            CandidateFieldText = FormatField("{=MF_UI_MarriageCandidateField}Marriage Candidate Field {VALUE}", CandidateCountText);
            MatchFieldText = FormatField("{=MF_UI_MarriageMatchField}Marriage Match Field {VALUE}", MatchTierText);
            Banner = new BannerImageIdentifierVM(clan.Banner, true);
            LeaderVisual = clan.Leader == null
                ? null
                : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(clan.Leader.CharacterObject));
        }

        internal CampaignClan Clan { get; }
        internal int CandidateCount { get; }
        internal int MatchTier { get; }
        internal int RelationValue { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string KingdomName { get; }
        [DataSourceProperty] public string CultureName { get; }
        [DataSourceProperty] public string RelationText { get; }
        [DataSourceProperty] public string CandidateCountText { get; }
        [DataSourceProperty] public string MatchTierText { get; }
        [DataSourceProperty] public string LeaderName { get; }
        [DataSourceProperty] public string KingdomFieldText { get; }
        [DataSourceProperty] public string CultureFieldText { get; }
        [DataSourceProperty] public string LeaderFieldText { get; }
        [DataSourceProperty] public string RelationFieldText { get; }
        [DataSourceProperty] public string CandidateFieldText { get; }
        [DataSourceProperty] public string MatchFieldText { get; }
        [DataSourceProperty] public BannerImageIdentifierVM Banner { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM LeaderVisual { get; }
        public void ExecuteSelect() => _select?.Invoke(this);

        private static string FormatField(string template, string value) => new TextObject(template)
            .SetTextVariable("VALUE", value ?? "-")
            .ToString();

        private static string GetTierText(int tier)
        {
            if (tier == 0) return new TextObject("{=MF_UI_MatchSameRealmCulture}Match Same Realm Culture").ToString();
            if (tier == 1) return new TextObject("{=MF_UI_MatchSameCulture}Match Same Culture").ToString();
            if (tier == 2) return new TextObject("{=MF_UI_MatchSameRealm}Match Same Realm").ToString();
            return "-";
        }
    }
}
