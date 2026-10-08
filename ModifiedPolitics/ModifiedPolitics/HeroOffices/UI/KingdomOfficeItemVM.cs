using System;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.Library;

namespace ModifiedPolitics.HeroOffices.UI
{
    /// <summary>
    /// Represents one selectable office row and its read-only detail content.
    /// </summary>
    public sealed class KingdomOfficeItemVM : ViewModel
    {
        private readonly Action<KingdomOfficeItemVM> _onSelect;
        private bool _isSelected;

        public KingdomOfficeItemVM(
            OfficeType officeType,
            string name,
            string seatText,
            string description,
            string effectsText,
            string holdersText,
            Action<KingdomOfficeItemVM> onSelect)
        {
            OfficeType = officeType;
            Name = name;
            SeatText = seatText;
            Description = description;
            EffectsText = effectsText;
            HoldersText = holdersText;
            _onSelect = onSelect;
        }

        public OfficeType OfficeType { get; }

        [DataSourceProperty]
        public string Name { get; }

        [DataSourceProperty]
        public string SeatText { get; }

        [DataSourceProperty]
        public string Description { get; }

        [DataSourceProperty]
        public string EffectsText { get; }

        [DataSourceProperty]
        public string HoldersText { get; }

        [DataSourceProperty]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value == _isSelected)
                    return;

                _isSelected = value;
                OnPropertyChangedWithValue(value, nameof(IsSelected));
            }
        }

        public void ExecuteSelect()
        {
            _onSelect?.Invoke(this);
        }
    }
}
