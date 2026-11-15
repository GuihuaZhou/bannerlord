// Owns transient collapse state for one read-only service or return group; no
// expansion state or aggregated troop data is written to campaign saves.
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.Fief.Presentation
{
    public sealed class FiefManagementBatchGroupVM : ViewModel
    {
        private bool _isExpanded;

        internal FiefManagementBatchGroupVM(string groupKey, string headerText,
            int totalCount, IEnumerable<FiefManagementTroopRowVM> troops,
            bool isExpanded)
        {
            GroupKey = groupKey;
            HeaderText = headerText;
            TotalCountText = totalCount.ToString();
            Troops = new MBBindingList<FiefManagementTroopRowVM>();
            foreach (FiefManagementTroopRowVM troop in troops)
                Troops.Add(troop);
            _isExpanded = isExpanded;
        }

        internal string GroupKey { get; }
        [DataSourceProperty] public string HeaderText { get; }
        [DataSourceProperty] public string TotalCountText { get; }
        [DataSourceProperty] public MBBindingList<FiefManagementTroopRowVM> Troops { get; }

        [DataSourceProperty]
        public bool IsExpanded
        {
            get => _isExpanded;
            private set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChangedWithValue(value, nameof(IsExpanded));
                OnPropertyChangedWithValue(IndicatorSprite,
                    nameof(IndicatorSprite));
            }
        }

        [DataSourceProperty]
        public string IndicatorSprite => IsExpanded
            ? "StdAssets\\expanded"
            : "StdAssets\\collapsed";

        [DataSourceMethod]
        public void ExecuteToggle() => IsExpanded = !IsExpanded;
    }
}
