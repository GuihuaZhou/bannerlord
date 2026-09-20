using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.UI.KingdomClan
{
    /// <summary>
    /// Provides the native Clan screen finance summary for the clan currently
    /// selected in Kingdom Management.
    /// </summary>
    public partial class KingdomClanItemVMMixin
    {
        private string _financeText;
        private string _totalIncomeText;
        private string _totalExpensesText;
        private string _dailyChangeText;
        private string _currentGoldText;
        private string _expectedGoldText;
        private int _totalIncomeValue;
        private int _totalExpensesValue;
        private int _dailyChangeValue;
        private int _currentGold;
        private int _expectedGold;
        private BasicTooltipViewModel _financeHint;

        [DataSourceProperty]
        public string FinanceText
        {
            get => _financeText;
            set => SetFinanceProperty(
                ref _financeText,
                value,
                nameof(FinanceText));
        }

        [DataSourceProperty]
        public string TotalIncomeText
        {
            get => _totalIncomeText;
            set => SetFinanceProperty(
                ref _totalIncomeText,
                value,
                nameof(TotalIncomeText));
        }

        [DataSourceProperty]
        public string TotalExpensesText
        {
            get => _totalExpensesText;
            set => SetFinanceProperty(
                ref _totalExpensesText,
                value,
                nameof(TotalExpensesText));
        }

        [DataSourceProperty]
        public string DailyChangeText
        {
            get => _dailyChangeText;
            set => SetFinanceProperty(
                ref _dailyChangeText,
                value,
                nameof(DailyChangeText));
        }

        [DataSourceProperty]
        public string CurrentGoldText
        {
            get => _currentGoldText;
            set => SetFinanceProperty(
                ref _currentGoldText,
                value,
                nameof(CurrentGoldText));
        }

        [DataSourceProperty]
        public string ExpectedGoldText
        {
            get => _expectedGoldText;
            set => SetFinanceProperty(
                ref _expectedGoldText,
                value,
                nameof(ExpectedGoldText));
        }

        [DataSourceProperty]
        public int TotalIncomeValue
        {
            get => _totalIncomeValue;
            set => SetFinanceProperty(
                ref _totalIncomeValue,
                value,
                nameof(TotalIncomeValue));
        }

        [DataSourceProperty]
        public int TotalExpensesValue
        {
            get => _totalExpensesValue;
            set => SetFinanceProperty(
                ref _totalExpensesValue,
                value,
                nameof(TotalExpensesValue));
        }

        [DataSourceProperty]
        public int DailyChangeValue
        {
            get => _dailyChangeValue;
            set => SetFinanceProperty(
                ref _dailyChangeValue,
                value,
                nameof(DailyChangeValue));
        }

        [DataSourceProperty]
        public int CurrentGold
        {
            get => _currentGold;
            set => SetFinanceProperty(
                ref _currentGold,
                value,
                nameof(CurrentGold));
        }

        [DataSourceProperty]
        public int ExpectedGold
        {
            get => _expectedGold;
            set => SetFinanceProperty(
                ref _expectedGold,
                value,
                nameof(ExpectedGold));
        }

        [DataSourceProperty]
        public BasicTooltipViewModel FinanceHint
        {
            get => _financeHint;
            set
            {
                if (value == _financeHint)
                {
                    return;
                }

                _financeHint = value;
                _vm.OnPropertyChangedWithValue(
                    value,
                    nameof(FinanceHint));
            }
        }

        private void RefreshClanFinanceLocalization()
        {
            FinanceText = GameTexts.FindText("str_finance").ToString();
            TotalIncomeText = GameTexts.FindText(
                "str_clan_finance_total_income").ToString();
            TotalExpensesText = GameTexts.FindText(
                "str_clan_finance_total_expenses").ToString();
            DailyChangeText = GameTexts.FindText(
                "str_clan_finance_daily_change").ToString();
            CurrentGoldText = GameTexts.FindText(
                "str_clan_finance_current_gold").ToString();
            ExpectedGoldText = GameTexts.FindText(
                "str_clan_finance_expected").ToString();

            FinanceHint = new BasicTooltipViewModel(
                BuildClanFinanceTooltip);
        }

        private void RefreshClanFinanceData()
        {
            Clan clan = GetClan();

            if (clan == null || Campaign.Current == null)
            {
                TotalIncomeValue = 0;
                TotalExpensesValue = 0;
                DailyChangeValue = 0;
                CurrentGold = 0;
                ExpectedGold = 0;
                return;
            }

            int totalIncome = (int)Campaign.Current.Models
                .ClanFinanceModel
                .CalculateClanIncome(
                    clan,
                    false,
                    false,
                    true)
                .ResultNumber;

            int totalExpenses = (int)Campaign.Current.Models
                .ClanFinanceModel
                .CalculateClanExpenses(
                    clan,
                    false,
                    false,
                    true)
                .ResultNumber;

            // Use the native combined calculation for the displayed balance.
            // This keeps the summary consistent with the detailed tooltip and
            // with any finance model that adds special income or expense rules.
            int dailyChange = (int)Campaign.Current.Models
                .ClanFinanceModel
                .CalculateClanGoldChange(
                    clan,
                    false,
                    false,
                    true)
                .ResultNumber;

            TotalIncomeValue = totalIncome;
            TotalExpensesValue = totalExpenses;
            DailyChangeValue = dailyChange;
            CurrentGold = clan.Gold;
            ExpectedGold = clan.Gold + dailyChange;
        }

        /// <summary>
        /// Builds the same combined daily gold-change explanation used by the
        /// native Clan screen. Income and expense entries share one list and
        /// the final result is the clan's daily balance.
        /// </summary>
        private List<TooltipProperty> BuildClanFinanceTooltip()
        {
            List<TooltipProperty> properties =
                new List<TooltipProperty>();
            Clan clan = GetClan();

            if (clan == null || Campaign.Current == null)
            {
                return properties;
            }

            ExplainedNumber goldChange = Campaign.Current.Models
                .ClanFinanceModel
                .CalculateClanGoldChange(
                    clan,
                    true,
                    false,
                    true);

            properties.AddRange(
                CampaignUIHelper
                    .GetTooltipForAccumulatingPropertyWithResult(
                        DailyChangeText,
                        goldChange.ResultNumber,
                        ref goldChange));

            return properties;
        }

        private void SetFinanceProperty(
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

        private void SetFinanceProperty(
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
    }
}
