using ModifiedPolitics.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

namespace ModifiedPolitics.UI.KingdomClan
{
    public partial class KingdomClanItemVMMixin
    {
        private int _warPotential;
        private string _warPotentialText;
        private int _fiefTroops;
        private int _garrisonTroops;
        private int _fieldTroops;
        private int _clanWealth;
        private int _dailyIncome;
        private int _partyDailyWage;

        [DataSourceProperty]
        public int WarPotential
        {
            get => _warPotential;
            set => SetWarPotentialProperty(ref _warPotential, value, nameof(WarPotential));
        }

        [DataSourceProperty]
        public string WarPotentialText
        {
            get => _warPotentialText;
            set => SetWarPotentialProperty(ref _warPotentialText, value, nameof(WarPotentialText));
        }

        [DataSourceProperty]
        public int FiefTroops
        {
            get => _fiefTroops;
            set => SetWarPotentialProperty(ref _fiefTroops, value, nameof(FiefTroops));
        }

        [DataSourceProperty]
        public int GarrisonTroops
        {
            get => _garrisonTroops;
            set => SetWarPotentialProperty(ref _garrisonTroops, value, nameof(GarrisonTroops));
        }

        [DataSourceProperty]
        public int FieldTroops
        {
            get => _fieldTroops;
            set => SetWarPotentialProperty(ref _fieldTroops, value, nameof(FieldTroops));
        }

        [DataSourceProperty]
        public int ClanWealth
        {
            get => _clanWealth;
            set => SetWarPotentialProperty(ref _clanWealth, value, nameof(ClanWealth));
        }

        [DataSourceProperty]
        public int DailyIncome
        {
            get => _dailyIncome;
            set => SetWarPotentialProperty(ref _dailyIncome, value, nameof(DailyIncome));
        }

        [DataSourceProperty]
        public int PartyDailyWage
        {
            get => _partyDailyWage;
            set => SetWarPotentialProperty(ref _partyDailyWage, value, nameof(PartyDailyWage));
        }

        private void SetWarPotentialProperty(
            ref int field,
            int value,
            string propertyName)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _vm.OnPropertyChangedWithValue(value, propertyName);
        }

        private void SetWarPotentialProperty(
            ref string field,
            string value,
            string propertyName)
        {
            if (field == value)
            {
                return;
            }

            field = value;
            _vm.OnPropertyChangedWithValue(value, propertyName);
        }

        private void RefreshWarPotentialData()
        {
            Clan clan = GetClan();
            WarPotentialModel model = WarPotentialModel.Instance;

            if (clan == null || model == null)
            {
                ResetWarPotentialValues();
                return;
            }

            WarPotentialResult result = model.CalculateWarPotential(clan);

            if (result == null)
            {
                ResetWarPotentialValues();
                return;
            }

            WarPotential = result.WarPotential;
            WarPotentialText = WarPotentialModel.GetWarPotentialDisplayText(result.WarPotential);
            FiefTroops = result.FiefTroops;
            GarrisonTroops = result.GarrisonTroops;
            FieldTroops = result.FieldTroops;
            ClanWealth = result.ClanWealth;
            DailyIncome = (int)Math.Round(result.DailyIncome, MidpointRounding.AwayFromZero);
            PartyDailyWage = result.PartyDailyWage;
        }

        private void ResetWarPotentialValues()
        {
            WarPotential = 0;
            WarPotentialText = WarPotentialModel.GetWarPotentialDisplayText(0);
            FiefTroops = 0;
            GarrisonTroops = 0;
            FieldTroops = 0;
            ClanWealth = 0;
            DailyIncome = 0;
            PartyDailyWage = 0;
        }
    }
}
