using System;
using Bannerlord.UIExtenderEx.Attributes;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ModifiedArmy.Models.Fief.Presentation
{
    public sealed class FiefManagementSourceVM : ViewModel
    {
        private readonly Action<FiefManagementSourceVM> _select;
        private bool _isSelected;

        internal FiefManagementSourceVM(Settlement village, string name,
            int count, Action<FiefManagementSourceVM> select)
        {
            Village = village;
            Name = name;
            Count = count.ToString();
            _select = select;
        }

        internal Settlement Village { get; }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public string Count { get; private set; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
            }
        }

        internal void SetCount(int count)
        {
            Count = count.ToString();
            OnPropertyChangedWithValue(Count, nameof(Count));
        }

        [DataSourceMethod]
        public void ExecuteSelect() => _select?.Invoke(this);
    }
}
