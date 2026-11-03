using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace ModifiedPolitics.Models
{
    public class NewBuildingConstructionModel : DefaultBuildingScoreCalculationModel
    {
        public override Building GetNextBuilding(Town town)
        {
            // Collect unfinished non-daily buildings that are not already queued.
            var candidates = town.Buildings.WhereQ((Building x) =>
                !x.BuildingType.IsDailyProject &&
                x.CurrentLevel < 3 && // MaxBuildingLevel is 3
                !town.BuildingsInProgress.Contains(x)
            );

            if (candidates.IsEmpty<Building>())
                return null;

            // Apply the preferred construction order.
            var priorityList = new BuildingType[]
            {
                // Barracks.
                DefaultBuildingTypes.SettlementBarracks,
                DefaultBuildingTypes.CastleBarracks,
                // Fortifications.
                DefaultBuildingTypes.SettlementFortifications,
                DefaultBuildingTypes.CastleFortifications,
                // Training fields.
                DefaultBuildingTypes.SettlementTrainingFields,
                DefaultBuildingTypes.CastleTrainingFields,
                // Granary.
                DefaultBuildingTypes.SettlementWarehouse,
                DefaultBuildingTypes.CastleGranary,
                // Waterworks or aqueducts.
                DefaultBuildingTypes.SettlementWaterworks,
                DefaultBuildingTypes.CastleFortifications,
                // Marketplace.
                DefaultBuildingTypes.SettlementMarketplace,
                DefaultBuildingTypes.CastleFortifications,
                // Tax office or farmlands.
                DefaultBuildingTypes.SettlementTaxOffice,
                DefaultBuildingTypes.CastleFarmlands
            };

            // Select the first available building in priority order.
            foreach (var buildingType in priorityList)
            {
                var building = candidates.FirstOrDefault(b => b.BuildingType == buildingType);
                if (building != null)
                    return building;
            }

            // Fall back to a random eligible building.
            return candidates.GetRandomElementInefficiently<Building>();
        }

        // Select a daily project only when no normal construction remains.
        public override Building GetNextDailyBuilding(Town town)
        {
            // Check whether any normal building can still be upgraded.
            bool hasUnfinishedMainBuilding = town.Buildings.Any(b =>
                !b.BuildingType.IsDailyProject && b.CurrentLevel < 3
            );

            // Do not select a daily project while normal construction remains.
            if (hasUnfinishedMainBuilding)
                return null;

            // Otherwise use the native daily-project selection logic.
            return town.Buildings.GetRandomElementWithPredicate((Building b) => b.BuildingType.IsDailyProject);
        }
    }
}
