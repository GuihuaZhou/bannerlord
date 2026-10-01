using System;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ModifiedDiplomacy.KingdomDiplomacy.Negotiation
{
    /// <summary>
    /// Represents one transferable kingdom negotiation clause. The public
    /// binding surface intentionally resembles a barter item so Bannerlord's
    /// familiar barter presentation can render it, while the data and actions
    /// remain completely independent from Barterable and BarterManager.
    /// </summary>
    public sealed class KingdomNegotiationItemVM : ViewModel
    {
        private readonly Action<KingdomNegotiationItemVM> _transfer;
        private int _currentOfferedAmount;
        private bool _isOffered;

        public KingdomNegotiationItemVM(
            KingdomNegotiationTermType type,
            string key,
            string label,
            object subject,
            bool isPlayerOwned,
            bool isMultiple,
            int totalAmount,
            string barterableType,
            string fiefFileName,
            ImageIdentifierVM visualIdentifier,
            Action<KingdomNegotiationItemVM> transfer)
        {
            Type = type;
            Key = key ?? string.Empty;
            ItemLbl = label ?? string.Empty;
            Subject = subject;
            IsPlayerOwned = isPlayerOwned;
            IsMultiple = isMultiple;
            TotalItemCount = Math.Max(1, totalAmount);
            BarterableType = barterableType ?? "Other";
            FiefFileName = fiefFileName ?? string.Empty;
            VisualIdentifier = visualIdentifier;
            _currentOfferedAmount = isMultiple ? 1 : TotalItemCount;
            _transfer = transfer;
        }

        public KingdomNegotiationTermType Type { get; }

        public string Key { get; }

        /// <summary>
        /// Keeps the campaign object represented by this row. The draft and
        /// executor use this reference instead of parsing a settlement or hero
        /// identifier back out of the UI key.
        /// </summary>
        public object Subject { get; }

        public bool IsPlayerOwned { get; }

        [DataSourceProperty]
        public string ItemLbl { get; }

        [DataSourceProperty]
        public bool IsMultiple { get; }

        [DataSourceProperty]
        public int TotalItemCount { get; }

        [DataSourceProperty]
        public string TotalItemCountText => IsMultiple
            ? TotalItemCount.ToString()
            : string.Empty;

        [DataSourceProperty]
        public int ItemCount => CurrentOfferedAmount;

        [DataSourceProperty]
        public bool IsOffered
        {
            get => _isOffered;
            set
            {
                if (_isOffered == value)
                {
                    return;
                }

                _isOffered = value;
                OnPropertyChangedWithValue(value, nameof(IsOffered));
            }
        }

        [DataSourceProperty]
        public int CurrentOfferedAmount
        {
            get => _currentOfferedAmount;
            set
            {
                int clamped = Math.Max(1, Math.Min(TotalItemCount, value));
                if (_currentOfferedAmount == clamped)
                {
                    return;
                }

                _currentOfferedAmount = clamped;
                OnPropertyChangedWithValue(
                    clamped,
                    nameof(CurrentOfferedAmount));
                OnPropertyChanged(nameof(CurrentOfferedAmountText));
                OnPropertyChanged(nameof(ItemCount));
            }
        }

        [DataSourceProperty]
        public string CurrentOfferedAmountText => IsMultiple
            ? CurrentOfferedAmount.ToString()
            : string.Empty;

        [DataSourceProperty]
        public bool IsTransferButtonHighlighted => false;

        [DataSourceProperty]
        public bool IsSelectorActive => false;

        [DataSourceProperty]
        public bool IsItemTransferrable => true;

        [DataSourceProperty]
        public string ActiveLink => string.Empty;

        // These properties satisfy the native visual tuple contract. They
        // carry presentation data only and never create a native Barterable.
        [DataSourceProperty]
        public string BarterableType { get; }

        [DataSourceProperty]
        public string FiefFileName { get; }

        [DataSourceProperty]
        public bool HasVisualIdentifier => VisualIdentifier != null;

        [DataSourceProperty]
        public ImageIdentifierVM VisualIdentifier { get; }

        public void ExecuteAction()
        {
            _transfer?.Invoke(this);
        }

        public void ExecuteActiveLink()
        {
        }

        public void ExecuteAddOffered()
        {
            CurrentOfferedAmount++;
        }

        public void ExecuteRemoveOffered()
        {
            CurrentOfferedAmount--;
        }
    }
}
