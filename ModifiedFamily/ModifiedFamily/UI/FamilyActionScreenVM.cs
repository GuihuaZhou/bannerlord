// Supplies the selected hero and the single confirmed action for a family movie.
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedFamily.UI
{
    internal sealed class FamilyActionScreenVM : ViewModel
    {
        private readonly Hero _hero;
        private readonly Action<Hero> _continueAction;
        private string _title;
        private string _description;
        private string _heroName;
        private string _continueText;
        private string _closeText;

        internal FamilyActionScreenVM(Hero hero, bool marriage, Action<Hero> continueAction)
        {
            _hero = hero;
            _continueAction = continueAction;
            Title = new TextObject(marriage ? "{=MF_UI_MarriageScreenTitle}Marriage Screen Title" : "{=MF_UI_AdoptionScreenTitle}Adoption Screen Title").ToString();
            Description = new TextObject(marriage ? "{=MF_UI_MarriageScreenDescription}Marriage Screen Description" : "{=MF_UI_AdoptionScreenDescription}Adoption Screen Description").ToString();
            HeroName = hero?.Name?.ToString() ?? string.Empty;
            ContinueText = new TextObject("{=MF_UI_Continue}Continue").ToString();
            CloseText = new TextObject("{=MF_UI_Close}Close").ToString();
        }

        [DataSourceProperty] public string Title { get => _title; private set { _title = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string Description { get => _description; private set { _description = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string HeroName { get => _heroName; private set { _heroName = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string ContinueText { get => _continueText; private set { _continueText = value; OnPropertyChangedWithValue(value); } }
        [DataSourceProperty] public string CloseText { get => _closeText; private set { _closeText = value; OnPropertyChangedWithValue(value); } }
        public void ExecuteContinue() { Hero hero = _hero; FamilyActionScreenHost.Close(); _continueAction?.Invoke(hero); }
        public void ExecuteClose() => FamilyActionScreenHost.Close();
    }
}
