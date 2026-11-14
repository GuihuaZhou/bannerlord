using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using HarmonyLib;
using ModifiedArmy.Population.Contracts;
using ModifiedPolitics.SettlementEconomy.Config;
using ModifiedPolitics.SettlementEconomy.Models;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Overlay;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.SettlementEconomy.Presentation
{
    /// <summary>
    /// Read-only Town/Castle population presentation. Village presentation
    /// remains owned by the native overlay and its Village.Hearth binding.
    /// </summary>
    [ViewModelMixin("UpdateProperties", true)]
    public sealed class SettlementPopulationVMMixin :
        BaseViewModelMixin<SettlementMenuOverlayVM>
    {
        private static readonly AccessTools.FieldRef<
            SettlementMenuOverlayVM,
            Settlement> SettlementField =
                AccessTools.FieldRefAccess<
                    SettlementMenuOverlayVM,
                    Settlement>("_settlement");

        private readonly SettlementMenuOverlayVM _vm;
        private string _populationText = string.Empty;
        private int _populationChangeAmount;
        private bool _isPopulationVisible;
        private BasicTooltipViewModel _populationHint;

        public SettlementPopulationVMMixin(SettlementMenuOverlayVM vm)
            : base(vm)
        {
            _vm = vm;
            _populationHint = new BasicTooltipViewModel(
                BuildPopulationTooltip);
            RefreshPopulation();
        }

        [DataSourceProperty]
        public string PopulationText
        {
            get => _populationText;
            private set
            {
                if (_populationText == value) return;
                _populationText = value;
                _vm.OnPropertyChangedWithValue(value, nameof(PopulationText));
            }
        }

        [DataSourceProperty]
        public int PopulationChangeAmount
        {
            get => _populationChangeAmount;
            private set
            {
                if (_populationChangeAmount == value) return;
                _populationChangeAmount = value;
                _vm.OnPropertyChangedWithValue(
                    value,
                    nameof(PopulationChangeAmount));
            }
        }

        [DataSourceProperty]
        public bool IsPopulationVisible
        {
            get => _isPopulationVisible;
            private set
            {
                if (_isPopulationVisible == value) return;
                _isPopulationVisible = value;
                _vm.OnPropertyChangedWithValue(
                    value,
                    nameof(IsPopulationVisible));
            }
        }

        [DataSourceProperty]
        public BasicTooltipViewModel PopulationHint
        {
            get => _populationHint;
            private set
            {
                if (_populationHint == value) return;
                _populationHint = value;
                _vm.OnPropertyChangedWithValue(value, nameof(PopulationHint));
            }
        }

        public override void OnRefresh()
        {
            RefreshPopulation();
        }

        private void RefreshPopulation()
        {
            Settlement settlement = GetSettlement();
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            IsPopulationVisible = settlement?.Town != null &&
                (settlement.IsTown || settlement.IsCastle) &&
                population != null;
            if (!IsPopulationVisible)
            {
                PopulationText = string.Empty;
                PopulationChangeAmount = 0;
                return;
            }

            float hearth = Math.Max(0f, population.GetHearth(settlement));
            float capacity = population.GetNaturalCapacity(settlement);
            DailyHearthForecast forecast = Forecast(settlement, population,
                hearth, capacity);
            PopulationText = ((int)Math.Floor(hearth)).ToString();
            PopulationChangeAmount = (int)Math.Round(
                forecast.AppliedTotal,
                MidpointRounding.AwayFromZero);
        }

        private List<TooltipProperty> BuildPopulationTooltip()
        {
            var properties = new List<TooltipProperty>();
            Settlement settlement = GetSettlement();
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (settlement?.Town == null || population == null)
                return properties;

            SettlementEconomyConfig config =
                SettlementEconomyConfigManager.Instance.RequireActive();
            float hearth = Math.Max(0f, population.GetHearth(settlement));
            float capacity = population.GetNaturalCapacity(settlement);
            DailyHearthForecast forecast = Forecast(settlement, population,
                hearth, capacity);

            properties.Add(Property(
                new TextObject("{=MP_PopulationHearth}Hearth"),
                ((int)Math.Floor(hearth)).ToString(),
                TooltipProperty.TooltipPropertyFlags.Title));
            AddChangeProperty(properties,
                new TextObject("{=MP_PopulationNaturalChange}Natural growth"),
                forecast.Natural + forecast.CapacityAdjustment);
            AddChangeProperty(properties,
                new TextObject("{=MP_PopulationFoodChange}Food effect"),
                forecast.Food);
            AddChangeProperty(properties,
                new TextObject("{=MP_PopulationProsperityChange}Prosperity effect"),
                forecast.Prosperity);
            properties.Add(Property(
                new TextObject(string.Empty),
                string.Empty,
                TooltipProperty.TooltipPropertyFlags.RundownSeperator));
            properties.Add(Property(
                new TextObject("{=MP_PopulationForecast}Expected change"),
                forecast.AppliedTotal.ToString("+0.00;-0.00;0.00"),
                TooltipProperty.TooltipPropertyFlags.RundownResult));
            return properties;
        }

        private static DailyHearthForecast Forecast(Settlement settlement,
            ISettlementPopulationService population, float hearth,
            float capacity)
        {
            SettlementEconomyConfig config =
                SettlementEconomyConfigManager.Instance.RequireActive();
            float perHearth = settlement.IsCastle
                ? config.CastleProsperityPerHearth
                : config.TownProsperityPerHearth;
            return PopulationGrowthModel.CalculateDailyForecast(hearth,
                capacity,
                settlement.Town.Prosperity, settlement.Town.FoodChange,
                perHearth, config.GetGrowthRate(settlement), config);
        }

        private Settlement GetSettlement()
        {
            return SettlementField(_vm);
        }

        private static TooltipProperty Property(
            TextObject label,
            string value,
            TooltipProperty.TooltipPropertyFlags flags =
                TooltipProperty.TooltipPropertyFlags.None)
        {
            return new TooltipProperty(
                label.ToString(),
                value,
                0,
                false,
                flags);
        }

        private static void AddChangeProperty(
            ICollection<TooltipProperty> properties,
            TextObject label,
            float value)
        {
            if (Math.Round(value, 2, MidpointRounding.AwayFromZero) == 0d)
                return;

            properties.Add(Property(
                label,
                value.ToString("+0.00;-0.00;0.00")));
        }
    }
}
