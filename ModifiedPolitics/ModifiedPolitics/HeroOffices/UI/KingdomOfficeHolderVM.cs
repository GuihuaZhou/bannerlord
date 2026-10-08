using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.UI
{
    /// <summary>
    /// Presents an appointed hero and routes dismissal back to the office page.
    /// </summary>
    public sealed class KingdomOfficeHolderVM : ViewModel
    {
        private readonly Action<Hero> _onDismiss;

        public KingdomOfficeHolderVM(
            Hero hero,
            string assignmentText,
            bool canDismiss,
            Action<Hero> onDismiss)
        {
            Hero = hero;
            HeroVisual = new HeroVM(hero, true);
            Name = hero?.Name?.ToString() ?? string.Empty;
            ClanName = hero?.Clan?.Name?.ToString() ?? string.Empty;
            AssignmentText = assignmentText ?? string.Empty;
            HasAssignmentText = !string.IsNullOrWhiteSpace(AssignmentText);
            CanDismiss = canDismiss;
            DismissText = new TextObject("{=MP_OfficeDismissButton}Dismiss").ToString();
            _onDismiss = onDismiss;
        }

        public Hero Hero { get; }

        [DataSourceProperty]
        public HeroVM HeroVisual { get; }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public string ClanName { get; }

        [DataSourceProperty]
        public string AssignmentText { get; }

        [DataSourceProperty]
        public bool HasAssignmentText { get; }

        [DataSourceProperty]
        public bool CanDismiss { get; }

        [DataSourceProperty]
        public string DismissText { get; }

        public void ExecuteDismiss()
        {
            if (CanDismiss && Hero != null)
                _onDismiss?.Invoke(Hero);
        }
    }
}
