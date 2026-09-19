using System;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Calculates local virtual food production that is routed into the real
    /// military granary before any remainder reaches civilian FoodStocks.
    /// </summary>
    public static class GarrisonLocalProductionSupplyModel
    {
        private const int TownSurroundingLandsProduction = 15;

        private const int CastleSurroundingLandsProduction = 10;

        private const int FarmlandsProductionPerLevel = 6;

        /// <summary>
        /// Allocates all available surrounding-lands and castle Farmlands
        /// production without exceeding the military granary target.
        /// </summary>
        public static int CalculateDailyAllocation(Town town)
        {
            if (town == null
                || town.IsUnderSiege
                || town.GarrisonParty == null)
            {
                return 0;
            }

            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(town);

            if (status.FoodDeficit <= 0)
            {
                return 0;
            }

            int surroundingLandsProduction = town.IsTown
                ? TownSurroundingLandsProduction
                : CastleSurroundingLandsProduction;
            int farmlandsProduction = town.IsCastle
                ? GetFarmlandLevel(town) * FarmlandsProductionPerLevel
                : 0;
            int totalLocalProduction =
                surroundingLandsProduction + farmlandsProduction;

            return Math.Min(totalLocalProduction, status.FoodDeficit);
        }

        private static int GetFarmlandLevel(Town town)
        {
            foreach (Building building in town.Buildings)
            {
                if (building.BuildingType
                    == DefaultBuildingTypes.CastleFarmlands)
                {
                    return building.CurrentLevel;
                }
            }

            return 0;
        }
    }
}
