// Provides the encyclopedia-style clan summary and the authoritative marriage candidates for M2.
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using CampaignClan = TaleWorlds.CampaignSystem.Clan;

namespace ModifiedFamily.UI.Marriage
{
    internal sealed class MarriageClanDetailsVM : ViewModel
    {
        internal MarriageClanDetailsVM(CampaignClan clan, IEnumerable<Hero> eligibleHeroes, Action<MarriageHeroItemVM> selectHero)
        {
            Clan = clan;
            Name = clan?.Name?.ToString() ?? "-";
            Banner = clan == null ? null : new BannerImageIdentifierVM(clan.Banner, true);
            KingdomText = FormatField("{=MF_UI_MarriageKingdomField}国家：{VALUE}", clan?.Kingdom?.Name?.ToString() ?? new TextObject("{=MF_UI_IndependentClan}独立家族").ToString());
            CultureText = FormatField("{=MF_UI_MarriageCultureField}文化：{VALUE}", clan?.Culture?.Name?.ToString() ?? "-");
            LeaderText = FormatField("{=MF_UI_MarriageLeaderField}领袖：{VALUE}", clan?.Leader?.Name?.ToString() ?? "-");
            TierText = FormatField("{=MF_UI_MarriageTierField}家族等级：{VALUE}", clan == null ? "-" : clan.Tier.ToString());
            int relation = clan == null || CampaignClan.PlayerClan == null ? 0 : CampaignClan.PlayerClan.GetRelationWithClan(clan);
            RelationText = FormatField("{=MF_UI_MarriageRelationField}关系：{VALUE}", relation > 0 ? "+" + relation : relation.ToString());
            FiefText = FormatField("{=MF_UI_MarriageFiefField}封地数量：{VALUE}", clan == null ? "-" : clan.Fiefs.Count.ToString());
            DescriptionText = clan?.EncyclopediaText?.ToString();
            if (string.IsNullOrWhiteSpace(DescriptionText))
                DescriptionText = new TextObject("{=MF_UI_MarriageClanFallbackDescription}{CLAN}是{KINGDOM}境内的一个家族。")
                    .SetTextVariable("CLAN", clan?.Name ?? new TextObject("-"))
                    .SetTextVariable("KINGDOM", clan?.Kingdom?.Name ?? new TextObject("{=MF_UI_IndependentClan}独立家族"))
                    .ToString();

            EligibleHeroes = new MBBindingList<MarriageHeroItemVM>();
            foreach (Hero hero in eligibleHeroes ?? Enumerable.Empty<Hero>()) EligibleHeroes.Add(new MarriageHeroItemVM(hero, selectHero));
            CandidateTitleText = new TextObject("{=MF_UI_MarriageEligibleClanMembers}可联姻的家族成员（{COUNT}）")
                .SetTextVariable("COUNT", EligibleHeroes.Count).ToString();
            EmptyText = new TextObject("{=MF_UI_NoClanMarriageCandidates}该家族目前没有仍符合条件的联姻候选人物，请返回并选择其他家族。").ToString();
        }

        internal CampaignClan Clan { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public BannerImageIdentifierVM Banner { get; }
        [DataSourceProperty] public string KingdomText { get; }
        [DataSourceProperty] public string CultureText { get; }
        [DataSourceProperty] public string LeaderText { get; }
        [DataSourceProperty] public string TierText { get; }
        [DataSourceProperty] public string RelationText { get; }
        [DataSourceProperty] public string FiefText { get; }
        [DataSourceProperty] public string DescriptionText { get; }
        [DataSourceProperty] public string CandidateTitleText { get; }
        [DataSourceProperty] public string EmptyText { get; }
        [DataSourceProperty] public MBBindingList<MarriageHeroItemVM> EligibleHeroes { get; }
        [DataSourceProperty] public bool HasEligibleHeroes => EligibleHeroes.Count > 0;

        private static string FormatField(string template, string value) => new TextObject(template).SetTextVariable("VALUE", value ?? "-").ToString();
    }

    internal sealed class MarriageHeroItemVM : ViewModel
    {
        private readonly Action<MarriageHeroItemVM> _select;

        internal MarriageHeroItemVM(Hero hero, Action<MarriageHeroItemVM> select)
        {
            Hero = hero;
            _select = select;
            Name = hero?.Name?.ToString() ?? "-";
            AgeText = new TextObject("{=MF_UI_MarriageHeroAge}年龄：{AGE}").SetTextVariable("AGE", hero == null ? "-" : ((int)hero.Age).ToString()).ToString();
            RoleText = hero?.Clan?.Leader == hero
                ? new TextObject("{=MF_UI_ClanLeaderRole}家族领袖").ToString()
                : new TextObject("{=MF_UI_ClanMemberRole}家族成员").ToString();
            CultureText = new TextObject("{=MF_UI_MarriageCultureField}文化：{VALUE}").SetTextVariable("VALUE", hero?.Culture?.Name?.ToString() ?? "-").ToString();
            Portrait = hero == null ? null : new CharacterImageIdentifierVM(CharacterCode.CreateFrom(hero.CharacterObject));
            Traits = new MBBindingList<EncyclopediaTraitItemVM>();
            if (hero != null)
            {
                foreach (TraitObject trait in DefaultTraits.Personality)
                {
                    int value = hero.GetTraitLevel(trait);
                    if (value != 0) Traits.Add(new EncyclopediaTraitItemVM(trait, value));
                }
            }
        }

        internal Hero Hero { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string AgeText { get; }
        [DataSourceProperty] public string RoleText { get; }
        [DataSourceProperty] public string CultureText { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM Portrait { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaTraitItemVM> Traits { get; }
        public void ExecuteSelect() => _select?.Invoke(this);
    }
}
