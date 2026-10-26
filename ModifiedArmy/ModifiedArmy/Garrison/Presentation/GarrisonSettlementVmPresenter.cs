using ModifiedArmy.Garrison.Models;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Presentation
{
    public static class GarrisonSettlementVmPresenter
    {
        public static void AppendProperties(KingdomSettlementItemVM item)
        {
            if (item?.Settlement == null || !item.Settlement.IsFortification ||
                item.ItemProperties == null) return;

            int wageLimit = item.Settlement.GarrisonWagePaymentLimit;
            GarrisonWageLimitModel.CalculateWagePaymentLimit(
                item.Settlement.Town, out int desiredSize, out _,
                out float averageWage, out _, out bool isBorder);
            int currentWage = item.Settlement.Town.GarrisonParty?.TotalWage ?? 0;
            GarrisonLogisticsStatus logistics =
                GarrisonLogisticsModel.Calculate(item.Settlement.Town);
            TextObject type = new TextObject(
                "{=ModifiedArmy_GarrisonSettlementTypeValue}{LOCATION} {TYPE}");
            type.SetTextVariable("LOCATION", new TextObject(isBorder
                ? "{=ModifiedArmy_GarrisonBorder}Border"
                : "{=ModifiedArmy_GarrisonInterior}Interior"));
            type.SetTextVariable("TYPE", new TextObject(item.Settlement.IsCastle
                ? "{=ModifiedArmy_GarrisonCastle}Castle"
                : "{=ModifiedArmy_GarrisonTown}Town"));
            string wealth = CampaignUIHelper.GetClanWealthStatusText(
                item.Settlement.OwnerClan);
            int leaderGold = item.Settlement.OwnerClan?.Leader?.Gold ?? 0;

            BasicTooltipViewModel wageHint = Tooltip(
                Row("{=ModifiedArmy_GarrisonWageLimit}Wage Limit",
                    wageLimit, true),
                Row("{=ModifiedArmy_GarrisonSettlementType}Settlement Type",
                    type),
                Row("{=ModifiedArmy_GarrisonMaximumTroops}Maximum Troops",
                    desiredSize),
                Row("{=ModifiedArmy_GarrisonAverageWage}Average Wage",
                    averageWage.ToString("F2")),
                Row("{=ModifiedArmy_GarrisonLeaderWealth}Leader Wealth",
                    leaderGold),
                Row("{=ModifiedArmy_GarrisonWealthLevel}Wealth Level", wealth),
                Row("{=ModifiedArmy_GarrisonCurrentWage}Current Wage",
                    currentWage));
            Add(item, "{=ModifiedArmy_GarrisonWageLimit}Wage Limit",
                wageLimit, wageHint);

            BasicTooltipViewModel foodHint = Tooltip(
                Row("{=ModifiedArmy_GarrisonMilitaryFood}Military Granary",
                    logistics.CurrentFood, true),
                Row("{=ModifiedArmy_GarrisonDailyFoodConsumption}Daily Consumption",
                    logistics.DailyConsumption.ToString("0.##")),
                Row("{=ModifiedArmy_GarrisonFoodSupplyDays}Supply Days",
                    logistics.FoodSupplyDays.ToString("0.##")),
                Row("{=ModifiedArmy_GarrisonTargetFood}Target Food",
                    logistics.TargetFood),
                Row("{=ModifiedArmy_GarrisonFoodDeficit}Food Deficit",
                    logistics.FoodDeficit));
            Add(item, "{=ModifiedArmy_GarrisonMilitaryFood}Military Granary",
                logistics.CurrentFood, foodHint);
        }

        private static TooltipProperty Row(
            string label, object value, bool title = false) =>
            new TooltipProperty(Text(label), value?.ToString() ?? string.Empty,
                0, false, title ? TooltipProperty.TooltipPropertyFlags.Title :
                    TooltipProperty.TooltipPropertyFlags.None);

        private static BasicTooltipViewModel Tooltip(
            params TooltipProperty[] rows) =>
            new BasicTooltipViewModel(() => new List<TooltipProperty>(rows));

        private static void Add(KingdomSettlementItemVM item, string label,
            object value, BasicTooltipViewModel hint) =>
            item.ItemProperties.Add(new SelectableFiefItemPropertyVM(
                Text(label), value?.ToString() ?? string.Empty, 0,
                SelectableItemPropertyVM.PropertyType.Garrison, hint, false));

        private static string Text(string taggedText) =>
            new TextObject(taggedText).ToString();
    }
}
