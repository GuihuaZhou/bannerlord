using ModifiedPolitics.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;

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

        private string _warPotentialLabelText;
        private string _fiefTroopsLabelText;
        private string _garrisonTroopsLabelText;
        private string _fieldTroopsLabelText;
        private string _clanWealthLabelText;
        private string _dailyIncomeLabelText;
        private string _partyWageLabelText;

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

        [DataSourceProperty]
        public string WarPotentialLabelText
        {
            get => _warPotentialLabelText;
            set => SetWarPotentialProperty(
                ref _warPotentialLabelText,
                value,
                nameof(WarPotentialLabelText));
        }

        [DataSourceProperty]
        public string FiefTroopsLabelText
        {
            get => _fiefTroopsLabelText;
            set => SetWarPotentialProperty(
                ref _fiefTroopsLabelText,
                value,
                nameof(FiefTroopsLabelText));
        }

        [DataSourceProperty]
        public string GarrisonTroopsLabelText
        {
            get => _garrisonTroopsLabelText;
            set => SetWarPotentialProperty(
                ref _garrisonTroopsLabelText,
                value,
                nameof(GarrisonTroopsLabelText));
        }

        [DataSourceProperty]
        public string FieldTroopsLabelText
        {
            get => _fieldTroopsLabelText;
            set => SetWarPotentialProperty(
                ref _fieldTroopsLabelText,
                value,
                nameof(FieldTroopsLabelText));
        }

        [DataSourceProperty]
        public string ClanWealthLabelText
        {
            get => _clanWealthLabelText;
            set => SetWarPotentialProperty(
                ref _clanWealthLabelText,
                value,
                nameof(ClanWealthLabelText));
        }

        [DataSourceProperty]
        public string DailyIncomeLabelText
        {
            get => _dailyIncomeLabelText;
            set => SetWarPotentialProperty(
                ref _dailyIncomeLabelText,
                value,
                nameof(DailyIncomeLabelText));
        }

        [DataSourceProperty]
        public string PartyWageLabelText
        {
            get => _partyWageLabelText;
            set => SetWarPotentialProperty(
                ref _partyWageLabelText,
                value,
                nameof(PartyWageLabelText));
        }

        private void RefreshWarPotentialLocalization()
        {
            WarPotentialLabelText =
                new TextObject("{=ModifiedPolitics_WarPotential}War Potential").ToString();

            FiefTroopsLabelText =
                new TextObject("{=ModifiedPolitics_FiefTroops}Fief Troops").ToString();

            GarrisonTroopsLabelText =
                new TextObject("{=ModifiedPolitics_GarrisonTroops}Garrison Troops").ToString();

            FieldTroopsLabelText =
                new TextObject("{=ModifiedPolitics_FieldTroops}Field Troops").ToString();

            ClanWealthLabelText =
                new TextObject("{=ModifiedPolitics_ClanWealth}Clan Wealth").ToString();

            DailyIncomeLabelText =
                new TextObject("{=ModifiedPolitics_DailyIncome}Daily Income").ToString();

            PartyWageLabelText =
                new TextObject("{=ModifiedPolitics_PartyWage}Party Wage").ToString();
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

            // 小家族作为雇佣兵派系活动, 不参与王国战争潜力评价.
            if (clan.IsMinorFaction && clan != Clan.PlayerClan)
            {
                ResetWarPotentialValues();
                WarPotentialText = new TextObject(
                    "{=ModifiedPolitics_NotApplicable}Not Applicable").ToString();
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
