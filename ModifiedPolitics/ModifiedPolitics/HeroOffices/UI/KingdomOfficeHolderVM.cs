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
        private readonly Action _onAppoint;

        public KingdomOfficeHolderVM(
            Hero hero,
            string assignmentText,
            bool canDismiss,
            Action<Hero> onDismiss,
            bool canAppoint = false,
            Action onAppoint = null)
        {
            Hero = hero;
            HeroVisual = new HeroVM(hero, true);
            IsVacant = hero == null;
            HasHolder = !IsVacant;
            Name = hero?.Name?.ToString()
                   ?? new TextObject("{=MP_OfficeVacantSlot}Vacant").ToString();
            ClanName = hero?.Clan?.Name?.ToString() ?? string.Empty;
            AssignmentText = assignmentText ?? string.Empty;
            HasAssignmentText = !string.IsNullOrWhiteSpace(AssignmentText);
            CanDismiss = canDismiss;
            CanAppoint = canAppoint;
            DismissText = new TextObject("{=MP_OfficeDismissButton}Dismiss").ToString();
            _onDismiss = onDismiss;
            _onAppoint = onAppoint;
        }

        public Hero Hero { get; }

        [DataSourceProperty]
        public bool IsVacant { get; }

        [DataSourceProperty]
        public bool HasHolder { get; }

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
        public bool CanAppoint { get; }

        [DataSourceProperty]
        public string DismissText { get; }

        public void ExecuteDismiss()
        {
            if (CanDismiss && Hero != null)
                _onDismiss?.Invoke(Hero);
        }

        public void ExecuteAppoint()
        {
            if (IsVacant && CanAppoint)
                _onAppoint?.Invoke();
        }
    }
}
