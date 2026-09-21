using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Purchases real food from a town market when the military granary falls
    /// below its replenishment threshold. Civilian FoodStocks are not
    /// converted into physical items by this behavior.
    /// </summary>
    public class GarrisonLocalMarketPurchaseBehavior : CampaignBehaviorBase
    {
        private const int MaximumPurchaseSupplyDaysPerDay = 5;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent
                .AddNonSerializedListener(this, OnDailySettlementTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnDailySettlementTick(Settlement settlement)
        {
            Town town = settlement?.Town;

            if (settlement?.IsTown != true
                || town?.GarrisonParty == null)
            {
                return;
            }

            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(town);

            if (!GarrisonLogisticsModel.NeedsReplenishment(status)
                || status.FoodDeficit <= 0)
            {
                return;
            }

            int dailyPurchaseLimit = (int)Math.Ceiling(
                status.DailyConsumption
                * MaximumPurchaseSupplyDaysPerDay);
            int purchaseDemand = Math.Min(
                status.FoodDeficit,
                dailyPurchaseLimit);

            if (purchaseDemand > 0)
            {
                PurchaseFoodFromLocalMarket(
                    settlement,
                    purchaseDemand);
            }
        }

        /// <summary>
        /// Transfers food items out of the real market roster. The clan leader
        /// pays the current settlement price for every purchased item.
        /// </summary>
        private static void PurchaseFoodFromLocalMarket(
            Settlement settlement,
            int foodDeficit)
        {
            Town town = settlement.Town;
            Hero payer = settlement.OwnerClan?.Leader;

            if (payer == null
                || payer.Gold <= 0
                || settlement.ItemRoster == null)
            {
                return;
            }

            int remainingDeficit = foodDeficit;
            int remainingGold = payer.Gold;
            int purchasedFood = 0;
            int totalCost = 0;

            // Iterate backwards because purchased entries may be removed from
            // the market roster when their count reaches zero.
            for (int index = settlement.ItemRoster.Count - 1;
                index >= 0 && remainingDeficit > 0 && remainingGold > 0;
                index--)
            {
                ItemRosterElement marketElement =
                    settlement.ItemRoster[index];
                ItemObject item = marketElement.EquipmentElement.Item;

                if (item?.IsFood != true || marketElement.Amount <= 0)
                {
                    continue;
                }

                int unitPrice = Math.Max(
                    1,
                    town.GetItemPrice(
                        marketElement.EquipmentElement,
                        null,
                        false));

                int affordableAmount = remainingGold / unitPrice;
                int purchaseAmount = Math.Min(
                    Math.Min(marketElement.Amount, remainingDeficit),
                    affordableAmount);

                if (purchaseAmount <= 0)
                {
                    continue;
                }

                settlement.ItemRoster.AddToCounts(
                    marketElement.EquipmentElement,
                    -purchaseAmount);
                town.GarrisonParty.ItemRoster.AddToCounts(
                    marketElement.EquipmentElement,
                    purchaseAmount);

                int itemCost = purchaseAmount * unitPrice;
                purchasedFood += purchaseAmount;
                totalCost += itemCost;
                remainingDeficit -= purchaseAmount;
                remainingGold -= itemCost;
            }

            if (totalCost <= 0)
            {
                return;
            }

            GiveGoldAction.ApplyForCharacterToSettlement(
                payer,
                settlement,
                totalCost,
                true);

            if (settlement.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_garrison_market_purchase");
                message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
                message.SetTextVariable("PURCHASED", purchasedFood);
                message.SetTextVariable("COST", totalCost);
                message.SetTextVariable(
                    "CURRENT_FOOD",
                    town.GarrisonParty.ItemRoster.TotalFood);
                ModLogger.Info(message.ToString());
            }
        }
    }
}
