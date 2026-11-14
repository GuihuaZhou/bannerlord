// Presents a collapsible group of read-only manpower facts; expansion is
// transient UI state and is deliberately never persisted into campaign saves.
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Presentation
{
    internal sealed class ManpowerSectionVM : ViewModel
    {
        private bool _isExpanded = true;

        internal ManpowerSectionVM(string title)
        {
            Title = title ?? string.Empty;
            Rows = new MBBindingList<ManpowerRowVM>();
        }

        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public MBBindingList<ManpowerRowVM> Rows { get; }

        [DataSourceProperty]
        public bool IsExpanded
        {
            get => _isExpanded;
            private set
            {
                if (value == _isExpanded) return;
                _isExpanded = value;
                OnPropertyChangedWithValue(value, nameof(IsExpanded));
            }
        }

        public void ExecuteToggle() => IsExpanded = !IsExpanded;
    }
}
