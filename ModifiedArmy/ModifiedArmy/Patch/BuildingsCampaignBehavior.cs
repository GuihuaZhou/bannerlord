using HarmonyLib;
using Helpers;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace ModifiedArmy.Patch
{
    [HarmonyPatch(typeof(BuildingsCampaignBehavior), "DailyTickSettlement")]
    public static class BuildingsCampaignBehavior_DailyTickSettlement_Patch
    {
        private static void DecideBuildingQueue(Town town)
        {
            if (town.BuildingsInProgress.IsEmpty<Building>())
            {
                Building nextBuilding = Campaign.Current.Models.BuildingScoreCalculationModel.GetNextBuilding(town);
                if (nextBuilding != null)
                {
                    town.BuildingsInProgress.Enqueue(nextBuilding);
                    string logMessage = $"[AI Build Decision] {town.Settlement.Name} 决定修建: {nextBuilding.BuildingType.Name}";
                    ModLogger.Info(logMessage);
                }
            }
        }

        private static void TickCurrentBuildingForTown(Town town)
        {
            if (town.BuildingsInProgress.Peek().CurrentLevel == 3)
            {
                town.BuildingsInProgress.Dequeue();
            }
            if (!town.Owner.Settlement.IsUnderSiege && !town.BuildingsInProgress.IsEmpty<Building>())
            {
                BuildingConstructionModel buildingConstructionModel = Campaign.Current.Models.BuildingConstructionModel;
                Building building = town.BuildingsInProgress.Peek();
                building.BuildingProgress += town.Construction;
                int num = town.IsCastle ? buildingConstructionModel.CastleBoostCost : buildingConstructionModel.TownBoostCost;
                if (town.BoostBuildingProcess > 0)
                {
                    town.BoostBuildingProcess -= num;
                    if (town.BoostBuildingProcess < 0)
                    {
                        town.BoostBuildingProcess = 0;
                    }
                }
                BuildingHelper.CheckIfBuildingIsComplete(building);
            }
        }

        private static void DecideDailyProject(Town town)
        {
            Building nextDailyBuilding = Campaign.Current.Models.BuildingScoreCalculationModel.GetNextDailyBuilding(town);
            if (nextDailyBuilding != null && nextDailyBuilding != town.CurrentDefaultBuilding)
            {
                BuildingHelper.ChangeDefaultBuilding(nextDailyBuilding, town);
            }
        }

        public static bool Prefix(Settlement settlement)
        {
            if (settlement.IsFortification)
            {
                Town town = settlement.Town;
                foreach (Building building in town.Buildings)
                {
                    if (town.Owner.Settlement.SiegeEvent == null)
                    {
                        building.HitPointChanged(10f);
                    }
                }
                if (town.Owner.Settlement.OwnerClan != Clan.PlayerClan)
                {
                    // 每日判定是否要修建新的建筑
                    BuildingsCampaignBehavior_DailyTickSettlement_Patch.DecideBuildingQueue(town);
                    if (MBRandom.RandomFloat < 0.01f)
                    {
                        BuildingsCampaignBehavior_DailyTickSettlement_Patch.DecideDailyProject(town);
                    }
                }
                if (!town.CurrentBuilding.BuildingType.IsDailyProject)
                {
                    BuildingsCampaignBehavior_DailyTickSettlement_Patch.TickCurrentBuildingForTown(town);
                    return false;
                }
                if (town.Governor != null && town.Governor.GetPerkValue(DefaultPerks.Charm.Virile) && MBRandom.RandomFloat <= DefaultPerks.Charm.Virile.SecondaryBonus)
                {
                    Hero randomElement = settlement.Notables.GetRandomElement<Hero>();
                    if (randomElement != null)
                    {
                        ChangeRelationAction.ApplyRelationChangeBetweenHeroes(town.Governor.Clan.Leader, randomElement, 1, false);
                    }
                }
            }

            return false;
        }
    }
}
