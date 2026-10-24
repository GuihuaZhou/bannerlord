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
            Title = new TextObject(marriage ? "{=MF_UI_MarriageScreenTitle}安排联姻" : "{=MF_UI_AdoptionScreenTitle}办理收养").ToString();
            Description = new TextObject(marriage ? "{=MF_UI_MarriageScreenDescription}将为所选家族成员选择联姻家族、对象、婚后归属及礼金。" : "{=MF_UI_AdoptionScreenDescription}将由玩家家族成员与所选同伴建立法定收养关系。").ToString();
            HeroName = hero?.Name?.ToString() ?? string.Empty;
            ContinueText = new TextObject("{=MF_UI_Continue}继续").ToString();
            CloseText = new TextObject("{=MF_UI_Close}关闭").ToString();
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
