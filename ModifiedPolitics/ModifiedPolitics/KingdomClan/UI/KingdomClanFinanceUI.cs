using System.Collections.Generic;
using ModifiedArmy.Recruitment.Finance;
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
        private string _recruitmentBudgetText;
        private string _expectedReserveText;
        private int _totalIncomeValue;
        private int _totalExpensesValue;
        private int _dailyChangeValue;
        private int _currentGold;
        private int _recruitmentBudget;
        private int _expectedReserve;
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
        public string RecruitmentBudgetText
        {
            get => _recruitmentBudgetText;
            set => SetFinanceProperty(
                ref _recruitmentBudgetText,
                value,
                nameof(RecruitmentBudgetText));
        }

        [DataSourceProperty]
        public string ExpectedReserveText
        {
            get => _expectedReserveText;
            set => SetFinanceProperty(
                ref _expectedReserveText,
                value,
                nameof(ExpectedReserveText));
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
        public int RecruitmentBudget
        {
            get => _recruitmentBudget;
            set => SetFinanceProperty(
                ref _recruitmentBudget,
                value,
                nameof(RecruitmentBudget));
        }

        [DataSourceProperty]
        public int ExpectedReserve
        {
            get => _expectedReserve;
            set => SetFinanceProperty(
                ref _expectedReserve,
                value,
                nameof(ExpectedReserve));
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
            RecruitmentBudgetText = new TextObject(
                "{=ModifiedPolitics_RecruitmentBudget}Recruitment Budget")
                .ToString();
            ExpectedReserveText = new TextObject(
                "{=ModifiedPolitics_ExpectedReserve}Expected Reserve")
                .ToString();

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
                RecruitmentBudget = 0;
                ExpectedReserve = 0;
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
            ClanRecruitmentBudgetSnapshot recruitmentSnapshot =
                ClanRecruitmentBudgetManager.GetSnapshot(clan);

            // The displayed budget is the amount still available today, so
            // the panel immediately reflects recruitment by another party of
            // the same clan. Expected reserve is the protected treasury floor.
            RecruitmentBudget = (int)recruitmentSnapshot
                .RemainingCommitmentBudget;
            ExpectedReserve = (int)recruitmentSnapshot.ExpectedReserve;
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
