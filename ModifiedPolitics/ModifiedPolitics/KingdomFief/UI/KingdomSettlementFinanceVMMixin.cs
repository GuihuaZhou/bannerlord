using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using ModifiedPolitics.KingdomFief.Models;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomFief.UI
{
    /// <summary>
    /// Exposes the native fief finance breakdown on kingdom settlement items.
    /// The selected fief's actual owner is used for every calculation.
    /// </summary>
    [ViewModelMixin("RefreshValues", true)]
    public class KingdomSettlementFinanceVMMixin
        : BaseViewModelMixin<KingdomSettlementItemVM>
    {
        private readonly KingdomSettlementItemVM _vm;
        private MBBindingList<ProfitItemPropertyVM> _financeItems;
        private ProfitItemPropertyVM _totalProfit;

        public KingdomSettlementFinanceVMMixin(
            KingdomSettlementItemVM vm)
            : base(vm)
        {
            _vm = vm;
            _financeItems =
                new MBBindingList<ProfitItemPropertyVM>();
            _totalProfit = CreateProfitItem(
                GameTexts.FindText("str_profit").ToString(),
                0,
                ProfitItemPropertyVM.PropertyType.None);

            RefreshFinance();
        }

        [DataSourceProperty]
        public MBBindingList<ProfitItemPropertyVM> SettlementFinanceItems
        {
            get => _financeItems;
            set
            {
                if (value == _financeItems)
                {
                    return;
                }

                _financeItems = value;
                _vm.OnPropertyChangedWithValue(
                    value,
                    nameof(SettlementFinanceItems));
            }
        }

        [DataSourceProperty]
        public ProfitItemPropertyVM SettlementTotalProfit
        {
            get => _totalProfit;
            set
            {
                if (value == _totalProfit)
                {
                    return;
                }

                _totalProfit = value;
                _vm.OnPropertyChangedWithValue(
                    value,
                    nameof(SettlementTotalProfit));
            }
        }

        public override void OnRefresh()
        {
            RefreshFinance();
        }

        private void RefreshFinance()
        {
            SettlementFinanceBreakdown finance =
                SettlementFinanceBreakdown.Calculate(_vm.Settlement);

            SettlementFinanceItems.Clear();

            AddIfNonZero(
                new TextObject("{=qeclv74c}Taxes").ToString(),
                finance.Taxes,
                ProfitItemPropertyVM.PropertyType.Tax);

            AddIfNonZero(
                new TextObject("{=eIgC6YGp}Tariffs").ToString(),
                finance.Tariffs,
                ProfitItemPropertyVM.PropertyType.Tariff);

            AddIfNonZero(
                new TextObject("{=5dkPxmZG}Garrison Wages").ToString(),
                -finance.GarrisonWages,
                ProfitItemPropertyVM.PropertyType.Garrison);

            foreach (VillageIncomeEntry villageIncome in
                finance.VillageIncomes)
            {
                AddIfNonZero(
                    villageIncome.Village.Name.ToString(),
                    villageIncome.Income,
                    ProfitItemPropertyVM.PropertyType.Village);
            }

            AddIfNonZero(
                new TextObject("{=J8ddrAOf}Governor Effects").ToString(),
                finance.ProjectIncome,
                ProfitItemPropertyVM.PropertyType.Governor);

            SettlementTotalProfit.Value = finance.Profit;
        }

        private void AddIfNonZero(
            string name,
            int value,
            ProfitItemPropertyVM.PropertyType type)
        {
            if (value == 0)
            {
                return;
            }

            SettlementFinanceItems.Add(
                CreateProfitItem(name, value, type));
        }

        private static ProfitItemPropertyVM CreateProfitItem(
            string name,
            int value,
            ProfitItemPropertyVM.PropertyType type)
        {
            return new ProfitItemPropertyVM(
                name,
                value,
                type,
                null,
                null);
        }
    }
}
