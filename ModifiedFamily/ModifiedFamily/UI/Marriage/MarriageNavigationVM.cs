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

        internal MarriageNavigationVM(Hero ownHero)
        {
            OwnHero = ownHero;
            _service = GameCampaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>()?.Service;
            Clans = new MBBindingList<MarriageClanItemVM>();
            Title = new TextObject("{=MF_UI_MarriageScreenTitle}联姻").ToString();
            ClanListTitle = new TextObject("{=MF_UI_MarriageClanListTitle}可联姻家族").ToString();
            OwnHeroSummaryText = new TextObject("{=MF_UI_MarriageForHero}为 {HERO} 寻找联姻对象")
                .SetTextVariable("HERO", ownHero?.Name ?? new TextObject(string.Empty)).ToString();
            OwnHeroVisual = ownHero == null ? null : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(ownHero.CharacterObject));
            OwnHeroCharacter = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            OwnHeroTraits = new MBBindingList<EncyclopediaTraitItemVM>();
            OwnHeroSkills = new MBBindingList<EncyclopediaSkillVM>();
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
                ? new TextObject("{=MF_UI_ClanLeaderRole}家族领袖").ToString()
                : new TextObject("{=MF_UI_ClanMemberRole}家族成员").ToString();
            CloseText = new TextObject("{=MF_UI_Close}关闭").ToString();
            KingdomColumnText = new TextObject("{=MF_UI_MarriageKingdomColumn}所属王国").ToString();
            CultureColumnText = new TextObject("{=MF_UI_MarriageCultureColumn}文化").ToString();
            MatchColumnText = new TextObject("{=MF_UI_MarriageMatchColumn}匹配类型").ToString();
            RelationColumnText = new TextObject("{=MF_UI_MarriageRelationColumn}关系").ToString();
            CandidateColumnText = new TextObject("{=MF_UI_MarriageCandidateColumn}候选人数").ToString();
            EmptyStateText = new TextObject("{=MF_UI_NoMarriageCandidates}当前没有符合全部联姻条件且未被其他提议占用的家族或成员。").ToString();
            LoadClans();
            SetPage(MarriageUiPage.ClanList);
        }

        internal Hero OwnHero { get; }
        internal CampaignClan SelectedTargetClan { get; private set; }
        internal Hero SelectedTargetHero { get; private set; }

        [DataSourceProperty] public MBBindingList<MarriageClanItemVM> Clans { get; }
        [DataSourceProperty] public MarriageClanDetailsVM ClanDetails { get => _clanDetails; private set { _clanDetails = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string ClanListTitle { get; }
        [DataSourceProperty] public string OwnHeroSummaryText { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM OwnHeroVisual { get; }
        [DataSourceProperty] public HeroViewModel OwnHeroCharacter { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaTraitItemVM> OwnHeroTraits { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaSkillVM> OwnHeroSkills { get; }
        [DataSourceProperty] public bool OwnHeroHasSkills => OwnHeroSkills.Count > 0;
        [DataSourceProperty] public string SkillsText => new TextObject("{=hVve5rNi}技能").ToString();
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
        [DataSourceProperty] public bool IsTermsOpen => false;
        [DataSourceProperty] public bool CanGoBack => _page != MarriageUiPage.ClanList;
        [DataSourceProperty] public string BreadcrumbText { get => _breadcrumbText; private set { _breadcrumbText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string EmptyStateText { get => _emptyStateText; private set { _emptyStateText = value; OnPropertyChangedWithValue(value); } }

        public void ExecuteBack()
        {
            if (_page == MarriageUiPage.HeroDetail) SetPage(MarriageUiPage.ClanDetail);
            else if (_page == MarriageUiPage.ClanDetail) SetPage(MarriageUiPage.ClanList);
        }

        public void ExecuteClose() => FamilyActionScreenHost.Close();

        public override void OnFinalize()
        {
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
                FamilyDiagnostics.Warn("联姻家族列表无法建立：当前人物或提议服务不可用。");
                return;
            }

            FamilyProposal activeProposal = _service.GetActiveProposalFor(OwnHero);
            if (activeProposal != null)
            {
                EmptyStateText = new TextObject("{=MF_UI_OwnHeroProposalLocked}{HERO}已有一份正在处理的家族事务，当前不能再次安排联姻。（请求：{ID}）")
                    .SetTextVariable("HERO", OwnHero.Name)
                    .SetTextVariable("ID", activeProposal.ProposalId ?? "-")
                    .ToString();
                FamilyDiagnostics.DebugLog($"联姻界面[ClanList]未建立候选：己方人物={OwnHero.StringId}/{OwnHero.Name}已被活动请求{activeProposal.ProposalId}占用，类型={activeProposal.ProposalType}，状态={activeProposal.Status}。");
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

            string reasons = rejectionCounts.Count == 0 ? "无" : string.Join("、", rejectionCounts.OrderByDescending(x => x.Value).Select(x => x.Key + "=" + x.Value));
            FamilyDiagnostics.DebugLog($"联姻界面[ClanList]：己方={OwnHero.StringId}/{OwnHero.Name}；检查Clan={examinedClans}，合法Clan={usableClans}，异性Hero={oppositeGenderHeroes}，可协商Hero={negotiableHeroes}，最终显示Clan={eligibleClans}、Hero={eligibleHeroes}；淘汰原因：{reasons}。");
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
            FamilyDiagnostics.DebugLog($"联姻界面[ClanDetail]：目标家族={SelectedTargetClan.StringId}/{SelectedTargetClan.Name}，实时合法候选={eligibleHeroes.Count}。");
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
            FamilyDiagnostics.DebugLog($"联姻界面[ClanDetail]选择人物：{SelectedTargetHero?.StringId ?? "none"}/{SelectedTargetHero?.Name?.ToString() ?? "none"}；目标家族={SelectedTargetClan?.StringId ?? "none"}。M3人物详情尚未启用。");
        }

        private void SetPage(MarriageUiPage page)
        {
            _page = page;
            BreadcrumbText = page == MarriageUiPage.ClanList
                ? new TextObject("{=MF_UI_MarriageBreadcrumbClans}联姻  >  选择家族").ToString()
                : page == MarriageUiPage.ClanDetail
                    ? new TextObject("{=MF_UI_MarriageBreadcrumbClanDetail}联姻  >  选择家族  >  {CLAN}").SetTextVariable("CLAN", SelectedTargetClan?.Name ?? new TextObject("-")).ToString()
                    : new TextObject("{=MF_UI_MarriageScreenTitle}联姻").ToString();
            OnPropertyChanged(nameof(IsClanListVisible));
            OnPropertyChanged(nameof(IsClanDetailVisible));
            OnPropertyChanged(nameof(IsHeroDetailVisible));
            OnPropertyChanged(nameof(IsTermsOpen));
            OnPropertyChanged(nameof(CanGoBack));
            FamilyDiagnostics.DebugLog($"联姻界面切换至[{page}]；己方人物={OwnHero?.StringId ?? "none"}/{OwnHero?.Name?.ToString() ?? "none"}。");
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
            KingdomName = clan.Kingdom?.Name?.ToString() ?? new TextObject("{=MF_UI_IndependentClan}独立家族").ToString();
            CultureName = clan.Culture?.Name?.ToString() ?? "-";
            RelationText = relationValue > 0 ? "+" + relationValue : relationValue.ToString();
            CandidateCountText = candidateCount.ToString();
            MatchTierText = GetTierText(matchTier);
            LeaderName = clan.Leader?.Name?.ToString() ?? "-";
            KingdomFieldText = FormatField("{=MF_UI_MarriageKingdomField}国家：{VALUE}", KingdomName);
            CultureFieldText = FormatField("{=MF_UI_MarriageCultureField}文化：{VALUE}", CultureName);
            LeaderFieldText = FormatField("{=MF_UI_MarriageLeaderField}领袖：{VALUE}", LeaderName);
            RelationFieldText = FormatField("{=MF_UI_MarriageRelationField}关系：{VALUE}", RelationText);
            CandidateFieldText = FormatField("{=MF_UI_MarriageCandidateField}候选人数：{VALUE}", CandidateCountText);
            MatchFieldText = FormatField("{=MF_UI_MarriageMatchField}匹配类型：{VALUE}", MatchTierText);
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
            if (tier == 0) return new TextObject("{=MF_UI_MatchSameRealmCulture}同国同文化").ToString();
            if (tier == 1) return new TextObject("{=MF_UI_MatchSameCulture}异国同文化").ToString();
            if (tier == 2) return new TextObject("{=MF_UI_MatchSameRealm}同国异文化").ToString();
            return "-";
        }
    }
}
