// Presents one eligible professional troop as an isolated weight draft; it
// never writes campaign state and delegates navigation to the popup owner.
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Preferences.Presentation
{
    internal sealed class RecruitmentWeightTroopItemVM : ViewModel
    {
        private readonly Action<RecruitmentWeightTroopItemVM> _openEncyclopedia;
        private int _weight;

        internal RecruitmentWeightTroopItemVM(CharacterObject troop,
            int weight, int defaultWeight,
            Action<RecruitmentWeightTroopItemVM> openEncyclopedia)
        {
            Troop = troop ?? throw new ArgumentNullException(nameof(troop));
            DefaultWeight = Behaviors.SettlementProfessionalPreferenceBehavior
                .NormalizeWeight(defaultWeight);
            _weight = Behaviors.SettlementProfessionalPreferenceBehavior
                .NormalizeWeight(weight);
            _openEncyclopedia = openEncyclopedia;
            NameText = troop.Name.ToString();
            Visual = new CharacterImageIdentifierVM(
                CampaignUIHelper.GetCharacterCode(troop, false));
            TierIconData = CampaignUIHelper.GetCharacterTierData(troop, false);
            TypeIconData = CampaignUIHelper.GetCharacterTypeData(troop, false);
        }

        internal CharacterObject Troop { get; }
        internal int DefaultWeight { get; }
        internal string TroopId => Troop.StringId;

        [DataSourceProperty] public string NameText { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM Visual { get; }
        [DataSourceProperty] public StringItemWithHintVM TierIconData { get; }
        [DataSourceProperty] public StringItemWithHintVM TypeIconData { get; }

        [DataSourceProperty]
        public int Weight
        {
            get => _weight;
            set
            {
                int normalized = Behaviors.SettlementProfessionalPreferenceBehavior
                    .NormalizeWeight(value);
                if (_weight == normalized) return;
                _weight = normalized;
                OnPropertyChangedWithValue(normalized, nameof(Weight));
                OnPropertyChangedWithValue(CanDecrease, nameof(CanDecrease));
                OnPropertyChangedWithValue(CanIncrease, nameof(CanIncrease));
            }
        }

        [DataSourceProperty] public bool CanDecrease => Weight > 0;
        [DataSourceProperty] public bool CanIncrease => Weight <
            Behaviors.SettlementProfessionalPreferenceBehavior.MaximumCustomWeight;

        /// <summary>Decreases the draft by one without crossing zero.</summary>
        public void ExecuteDecreaseWeight()
        {
            if (CanDecrease) Weight--;
        }

        /// <summary>Increases the draft by one without crossing the configured maximum.</summary>
        public void ExecuteIncreaseWeight()
        {
            if (CanIncrease) Weight++;
        }

        /// <summary>
        /// Requests native encyclopedia navigation while the owning popup keeps
        /// its in-memory draft alive beneath the encyclopedia screen.
        /// </summary>
        public void ExecuteOpenEncyclopedia() => _openEncyclopedia?.Invoke(this);
    }
}
