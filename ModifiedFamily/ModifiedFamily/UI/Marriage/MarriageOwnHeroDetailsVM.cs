using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Items;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedFamily.UI.Marriage
{
    internal sealed class MarriageOwnHeroDetailsVM : ViewModel
    {
        internal MarriageOwnHeroDetailsVM(Hero hero)
        {
            Hero = hero;
            Character = new HeroViewModel(CharacterViewModel.StanceTypes.EmphasizeFace);
            Traits = new MBBindingList<EncyclopediaTraitItemVM>();
            Skills = new MBBindingList<EncyclopediaSkillVM>();
            if (hero != null)
            {
                Character.FillFrom(hero, -1, hero.IsNotable, true);
                Character.SetEquipment(EquipmentIndex.Horse, default(EquipmentElement));
                Character.SetEquipment(EquipmentIndex.HorseHarness, default(EquipmentElement));
                Character.SetEquipment(EquipmentIndex.Head, default(EquipmentElement));
                foreach (TraitObject trait in DefaultTraits.Personality)
                {
                    int value = hero.GetTraitLevel(trait);
                    if (value != 0) Traits.Add(new EncyclopediaTraitItemVM(trait, value));
                }

                foreach (SkillObject skill in MBObjectManager.Instance.GetObjectTypeList<SkillObject>())
                {
                    int value = hero.GetSkillValue(skill);
                    if (value > 0) Skills.Add(new EncyclopediaSkillVM(skill, value));
                }
            }

            NameText = hero?.Name?.ToString() ?? "-";
            RoleText = hero?.Clan?.Leader == hero
                ? new TextObject("{=MF_UI_ClanLeaderRole}Clan Leader Role").ToString()
                : new TextObject("{=MF_UI_ClanMemberRole}Clan Member Role").ToString();
            SkillsText = new TextObject("{=MF_UI_Skills}Skills").ToString();
        }

        internal Hero Hero { get; }
        [DataSourceProperty] public HeroViewModel Character { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaTraitItemVM> Traits { get; }
        [DataSourceProperty] public MBBindingList<EncyclopediaSkillVM> Skills { get; }
        [DataSourceProperty] public string NameText { get; }
        [DataSourceProperty] public string RoleText { get; }
        [DataSourceProperty] public string SkillsText { get; }
        [DataSourceProperty] public bool HasSkills => Skills.Count > 0;

        public override void OnFinalize()
        {
            Character?.OnFinalize();
            base.OnFinalize();
        }
    }
}
