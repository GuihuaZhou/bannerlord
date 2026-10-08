using System;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.UI
{
    /// <summary>
    /// Represents one selectable office row and its read-only detail content.
    /// </summary>
    public sealed class KingdomOfficeItemVM : ViewModel
    {
        private readonly Action<KingdomOfficeItemVM> _onSelect;
        private readonly Action<KingdomOfficeItemVM> _onAppoint;
        private bool _isSelected;

        public KingdomOfficeItemVM(
            OfficeType officeType,
            string name,
            string seatText,
            string description,
            string effectsText,
            MBBindingList<KingdomOfficeHolderVM> holders,
            bool canAppoint,
            Action<KingdomOfficeItemVM> onSelect,
            Action<KingdomOfficeItemVM> onAppoint)
        {
            OfficeType = officeType;
            Name = name;
            SeatText = seatText;
            Description = description;
            EffectsText = effectsText;
            Holders = holders;
            HasHolders = holders.Count > 0;
            HasNoHolders = !HasHolders;
            CanAppoint = canAppoint;
            HoldersTitle = new TextObject("{=MP_OfficeHoldersTitle}Office Holders").ToString();
            NoHoldersText = new TextObject(
                "{=MP_OfficeNoHolders}There are currently no office holders.").ToString();
            AppointText = new TextObject("{=MP_OfficeAppointButton}Appoint").ToString();
            _onSelect = onSelect;
            _onAppoint = onAppoint;
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
        public MBBindingList<KingdomOfficeHolderVM> Holders { get; }

        [DataSourceProperty]
        public bool HasHolders { get; }

        [DataSourceProperty]
        public bool HasNoHolders { get; }

        [DataSourceProperty]
        public bool CanAppoint { get; }

        [DataSourceProperty]
        public string HoldersTitle { get; }

        [DataSourceProperty]
        public string NoHoldersText { get; }

        [DataSourceProperty]
        public string AppointText { get; }

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

        public void ExecuteAppoint()
        {
            if (CanAppoint)
                _onAppoint?.Invoke(this);
        }
    }
}
