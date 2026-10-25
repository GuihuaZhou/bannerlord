// Supplies the M3 encyclopedia-style target-hero page without opening another movie.
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedFamily.UI.Marriage
{
    internal sealed class MarriageHeroDetailsVM : ViewModel
    {
        private readonly Action _openTerms;

        internal MarriageHeroDetailsVM(Hero ownHero, Hero targetHero, Action openTerms)
        {
            _openTerms = openTerms;
            TargetHero = targetHero;
            Character = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            Traits = new MBBindingList<EncyclopediaTraitItemVM>();
            Skills = new MBBindingList<EncyclopediaSkillVM>();
            Family = new MBBindingList<MarriageRelativeHeroVM>();
            Stats = new MBBindingList<StringPairItemVM>();
            if (targetHero != null)
            {
                // Follow the native encyclopedia tableau setup and omit headgear and mounts.
                Character.FillFrom(targetHero, -1, targetHero.IsNotable, true);
                Character.SetEquipment(EquipmentIndex.Horse, default(EquipmentElement));
                Character.SetEquipment(EquipmentIndex.HorseHarness, default(EquipmentElement));
                Character.SetEquipment(EquipmentIndex.Head, default(EquipmentElement));
                foreach (TraitObject trait in DefaultTraits.Personality)
                {
                    int value = targetHero.GetTraitLevel(trait);
                    if (value != 0) Traits.Add(new EncyclopediaTraitItemVM(trait, value));
                }
                foreach (SkillObject skill in MBObjectManager.Instance.GetObjectTypeList<SkillObject>())
                {
                    int value = targetHero.GetSkillValue(skill);
                    if (value > 0) Skills.Add(new EncyclopediaSkillVM(skill, value));
                }
                AddRelative(targetHero.Father, "{=MF_UI_RelativeFather}父亲");
                AddRelative(targetHero.Mother, "{=MF_UI_RelativeMother}母亲");
                AddRelative(targetHero.Spouse, "{=MF_UI_RelativeSpouse}配偶");
                foreach (Hero sibling in targetHero.Siblings) AddRelative(sibling, "{=MF_UI_RelativeSibling}兄弟姐妹");
                foreach (Hero child in targetHero.Children) AddRelative(child, "{=MF_UI_RelativeChild}子女");
            }

            Name = targetHero?.Name?.ToString() ?? "-";
            RoleText = targetHero?.Clan?.Leader == targetHero
                ? new TextObject("{=MF_UI_ClanLeaderRole}家族领袖").ToString()
                : new TextObject("{=MF_UI_ClanMemberRole}家族成员").ToString();
            RoleFieldText = Format("{=MF_UI_MarriageHeroRole}身份：{ROLE}", "ROLE", RoleText);
            AgeText = Format("{=MF_UI_MarriageHeroAge}年龄：{AGE}", "AGE", targetHero == null ? "-" : ((int)targetHero.Age).ToString());
            ClanText = Format("{=MF_UI_MarriageHeroClan}所属家族：{CLAN}", "CLAN", targetHero?.Clan?.Name?.ToString() ?? "-");
            CultureText = Format("{=MF_UI_MarriageCultureField}文化：{VALUE}", "VALUE", targetHero?.Culture?.Name?.ToString() ?? "-");
            ClanBanner = targetHero?.Clan == null ? null : new BannerImageIdentifierVM(targetHero.Clan.Banner, true);
            Faction = targetHero?.Clan == null ? null : new EncyclopediaFactionVM(targetHero.Clan);
            int relation = ownHero == null || targetHero == null ? 0 : ownHero.GetRelation(targetHero);
            RelationText = Format("{=MF_UI_MarriageHeroRelation}与己方人物关系：{RELATION}", "RELATION", relation > 0 ? "+" + relation : relation.ToString());
            Stats.Add(new StringPairItemVM(new TextObject("{=MF_UI_StatCulture}文化：").ToString(), targetHero?.Culture?.Name?.ToString() ?? "-", null));
            Stats.Add(new StringPairItemVM(new TextObject("{=MF_UI_StatAge}年龄：").ToString(), targetHero == null ? "-" : ((int)targetHero.Age).ToString(), null));
            Stats.Add(new StringPairItemVM(new TextObject("{=MF_UI_StatRelation}关系：").ToString(), relation > 0 ? "+" + relation : relation.ToString(), null));
            BiographyText = targetHero?.EncyclopediaText?.ToString();
            if (string.IsNullOrWhiteSpace(BiographyText)) BiographyText = new TextObject("{=MF_UI_NoHeroBiography}当前没有可显示的人物生平资料。").ToString();
            InformationTitle = new TextObject("{=MF_UI_HeroInformation}信息").ToString();
            BiographyTitle = new TextObject("{=MF_UI_HeroBiography}生平").ToString();
            FamilyTitle = new TextObject("{=MF_UI_HeroFamily}亲属关系").ToString();
            PairingText = new TextObject("{=MF_UI_CurrentMarriagePair}当前联姻对象：{OWN}  ↔  {TARGET}")
                .SetTextVariable("OWN", ownHero?.Name ?? new TextObject("-"))
                .SetTextVariable("TARGET", targetHero?.Name ?? new TextObject("-"))
                .ToString();
            SkillsText = new TextObject("{=hVve5rNi}技能").ToString();
        }

        internal Hero TargetHero { get; }
        [DataSourceProperty] public HeroViewModel Character { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaTraitItemVM> Traits { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaSkillVM> Skills { get; }
        [DataSourceProperty] public MBBindingList<MarriageRelativeHeroVM> Family { get; }
        [DataSourceProperty] public MBBindingList<StringPairItemVM> Stats { get; }
        [DataSourceProperty] public string Name { get; }
        [DataSourceProperty] public string RoleText { get; }
        [DataSourceProperty] public string RoleFieldText { get; }
        [DataSourceProperty] public string AgeText { get; }
        [DataSourceProperty] public string ClanText { get; }
        [DataSourceProperty] public string CultureText { get; }
        [DataSourceProperty] public BannerImageIdentifierVM ClanBanner { get; }
        [DataSourceProperty] public string ClanName => TargetHero?.Clan?.Name?.ToString() ?? "-";
        [DataSourceProperty] public string RelationText { get; }
        [DataSourceProperty] public string BiographyText { get; }
        [DataSourceProperty] public string InformationText => BiographyText;
        [DataSourceProperty] public string InformationTitle { get; }
        [DataSourceProperty] public string InfoText => InformationTitle;
        [DataSourceProperty] public string BiographyTitle { get; }
        [DataSourceProperty] public string FamilyTitle { get; }
        [DataSourceProperty] public string PairingText { get; }
        [DataSourceProperty] public string OpenTermsText => new TextObject("{=MF_UI_OpenMarriageTerms}安排联姻").ToString();
        [DataSourceProperty] public string SkillsText { get; }
        [DataSourceProperty] public string FamilyText => FamilyTitle;
        [DataSourceProperty] public EncyclopediaFactionVM Faction { get; }
        [DataSourceProperty] public bool HasNeutralClan => TargetHero?.Clan == null;
        [DataSourceProperty] public bool HasSkills => Skills.Count > 0;
        [DataSourceProperty] public bool HasFamily => Family.Count > 0;

        public void ExecuteOpenTerms() => _openTerms?.Invoke();

        public override void OnFinalize()
        {
            Character?.OnFinalize();
            base.OnFinalize();
        }

        private void AddRelative(Hero hero, string roleText)
        {
            if (hero == null || Family.Any(x => x.Hero == hero)) return;
            Family.Add(new MarriageRelativeHeroVM(hero, new TextObject(roleText).ToString()));
        }

        private static string Format(string template, string key, string value) => new TextObject(template).SetTextVariable(key, value ?? "-").ToString();
    }

    internal sealed class MarriageRelativeHeroVM : HeroVM
    {
        internal MarriageRelativeHeroVM(Hero hero, string role) : base(hero, true)
        {
            Role = role ?? "-";
        }

        [DataSourceProperty] public string Role { get; }
    }
}
