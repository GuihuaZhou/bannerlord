using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.LinQuick;

namespace ModifiedArmy.Models
{
    public class NewBuildingConstructionModel : DefaultBuildingScoreCalculationModel
    {
        public override Building GetNextBuilding(Town town)
        {
            // 获取所有符合条件的候选建筑（非日常项目、未满级、不在建造队列中）
            var candidates = town.Buildings.WhereQ((Building x) =>
                !x.BuildingType.IsDailyProject &&
                x.CurrentLevel < 3 && // MaxBuildingLevel is 3
                !town.BuildingsInProgress.Contains(x)
            );

            if (candidates.IsEmpty<Building>())
                return null;

            // 定义优先级列表
            var priorityList = new BuildingType[]
            {
                // 军营
                DefaultBuildingTypes.SettlementBarracks,
                DefaultBuildingTypes.CastleBarracks,
                // 城墙
                DefaultBuildingTypes.SettlementFortifications,
                DefaultBuildingTypes.CastleFortifications,
                // 训练场
                DefaultBuildingTypes.SettlementTrainingFields,
                DefaultBuildingTypes.CastleTrainingFields,
                // 粮仓
                DefaultBuildingTypes.SettlementWarehouse,
                DefaultBuildingTypes.CastleGranary,
                // 水利
                DefaultBuildingTypes.SettlementWaterworks,
                DefaultBuildingTypes.CastleFortifications,
                // 市场
                DefaultBuildingTypes.SettlementMarketplace,
                DefaultBuildingTypes.CastleFortifications,
                // 税务/农田
                DefaultBuildingTypes.SettlementTaxOffice,
                DefaultBuildingTypes.CastleFarmlands
            };

            // 按优先级顺序查找
            foreach (var buildingType in priorityList)
            {
                var building = candidates.FirstOrDefault(b => b.BuildingType == buildingType);
                if (building != null)
                    return building;
            }

            // 如果没有高优先级建筑，则随机选择一个
            return candidates.GetRandomElementInefficiently<Building>();
        }

        // 重写日常项目选择逻辑
        public override Building GetNextDailyBuilding(Town town)
        {
            // --- 关键逻辑：检查是否存在未满级的主建筑 ---
            bool hasUnfinishedMainBuilding = town.Buildings.Any(b =>
                !b.BuildingType.IsDailyProject && b.CurrentLevel < 3
            );

            // 如果有未完成的主建筑，则不进行任何日常项目
            if (hasUnfinishedMainBuilding)
                return null;

            // 否则，沿用默认的随机选择逻辑
            return town.Buildings.GetRandomElementWithPredicate((Building b) => b.BuildingType.IsDailyProject);
        }
    }
}
