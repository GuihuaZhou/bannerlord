using Helpers;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Separates civilian FoodStocks from military food. Garrison troops no
    /// longer consume FoodStocks, while castle Farmlands production assigned
    /// to the military granary is removed from civilian production.
    /// </summary>
    public class NewSettlementFoodModel : SettlementFoodModel
    {
        private static readonly Dictionary<Town, int>
            LocalProductionAllocations = new Dictionary<Town, int>();

        private readonly TextObject _prosperityText =
            GameTexts.FindText("str_prosperity");

        private readonly TextObject _landsAroundSettlementText =
            GameTexts.FindText("str_lands_around_settlement");

        private readonly TextObject _militaryAllocationText = new TextObject(
            "{=ModifiedArmy_GarrisonLocalProductionAllocation}" +
            "Military granary allocation");

        public override int FoodStocksUpperLimit => 300;

        public override int NumberOfProsperityToEatOneFood => 40;

        public override int NumberOfMenOnGarrisonToEatOneFood => 20;

        public override int CastleFoodStockUpperLimitBonus => 150;

        /// <summary>
        /// Stores today's real Farmlands transfer so every food-model query
        /// uses the same value after the garrison inventory has changed.
        /// </summary>
        public static void SetLocalProductionAllocation(
            Town town,
            int amount)
        {
            if (town != null)
            {
                LocalProductionAllocations[town] = amount;
            }
        }

        public override ExplainedNumber CalculateTownFoodStocksChange(
            Town town,
            bool includeMarketStocks = true,
            bool includeDescriptions = false)
        {
            ExplainedNumber production = new ExplainedNumber(
                0f,
                includeDescriptions,
                null);
            ExplainedNumber consumption = new ExplainedNumber(
                0f,
                includeDescriptions,
                null);

            // Civilian prosperity remains a FoodStocks expense. Garrison
            // consumption is deliberately omitted because it is paid from the
            // garrison party's real food inventory.
            ExplainedNumber prosperityConsumption = new ExplainedNumber(
                town.Prosperity / NumberOfProsperityToEatOneFood,
                false,
                null);

            PerkHelper.AddPerkBonusForTown(
                DefaultPerks.Steward.MasterOfWarcraft,
                town,
                ref prosperityConsumption);

            consumption.Add(
                prosperityConsumption.ResultNumber,
                _prosperityText,
                null);
            town.AddEffectOfBuildings(
                BuildingEffectEnum.FoodConsumption,
                ref consumption);

            Clan ownerClan = town.Settlement.OwnerClan;
            Kingdom kingdom = ownerClan?.Kingdom;

            if (kingdom != null
                && kingdom.HasPolicy(DefaultPolicies.HuntingRights))
            {
                production.Add(
                    2f,
                    DefaultPolicies.HuntingRights.Name,
                    null);
            }

            if (!town.IsUnderSiege)
            {
                production.Add(
                    town.IsTown ? 15f : 10f,
                    _landsAroundSettlementText,
                    null);

                foreach (Village village in
                    town.Owner.Settlement.BoundVillages)
                {
                    float villageProduction = 0f;

                    if (village.VillageState
                        == Village.VillageStates.Normal)
                    {
                        villageProduction =
                            (village.GetHearthLevel() + 1) * 6f;
                    }

                    production.Add(
                        villageProduction,
                        village.Name,
                        null);
                }

                town.AddEffectOfBuildings(
                    BuildingEffectEnum.FoodProduction,
                    ref production);

                int localProductionAllocation =
                    GetLocalProductionAllocation(town);

                if (localProductionAllocation > 0)
                {
                    production.Add(
                        -localProductionAllocation,
                        _militaryAllocationText,
                        null);
                }
            }
            else
            {
                PerkHelper.AddPerkBonusForTown(
                    DefaultPerks.Roguery.DirtyFighting,
                    town,
                    ref production);
            }

            if (includeMarketStocks)
            {
                foreach (Town.SellLog sellLog in town.SoldItems)
                {
                    if (sellLog.Category.Properties
                        == ItemCategory.Property.BonusToFoodStores)
                    {
                        production.Add(
                            sellLog.Number,
                            includeDescriptions
                                ? sellLog.Category.GetName()
                                : null,
                            null);
                    }
                }
            }

            ExplainedNumber result = new ExplainedNumber(
                0f,
                includeDescriptions,
                null);
            result.AddFromExplainedNumber(production, null);
            result.SubtractFromExplainedNumber(consumption, null);

            Campaign.Current.Models.IssueModel
                .GetIssueEffectsOfSettlement(
                    DefaultIssueEffects.SettlementFood,
                    town.Settlement,
                    ref result);

            return result;
        }

        private static int GetLocalProductionAllocation(Town town)
        {
            return LocalProductionAllocations.TryGetValue(
                town,
                out int amount)
                ? amount
                : 0;
        }
    }
}
