using Helpers;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;

using ModifiedPolitics.SettlementDevelopment.Diagnostics;

namespace ModifiedPolitics.SettlementDevelopment.Services
{
    internal static class SettlementBuildingStateService
    {
        internal static bool EnsureCurrentBuilding(
            Town town,
            bool logRecovery = true)
        {
            if (town == null || town.CurrentBuilding != null)
            {
                return town != null;
            }

            Building dailyProject = Campaign.Current?
                .Models?
                .BuildingScoreCalculationModel?
                .GetNextDailyBuilding(town);

            if (dailyProject == null)
            {
                foreach (Building building in town.Buildings)
                {
                    if (building?.BuildingType?.IsDailyProject == true)
                    {
                        dailyProject = building;
                        break;
                    }
                }
            }

            if (dailyProject == null)
            {
                ModLogger.Error(
                    SettlementBuildingLogText.RecoveryFailed(
                        town.Settlement?.Name ?? TextObject.GetEmpty()));
                return false;
            }

            BuildingHelper.ChangeDefaultBuilding(dailyProject, town);
            if (town.CurrentBuilding == null)
            {
                ModLogger.Error(
                    SettlementBuildingLogText.RecoveryStillNull(
                        town.Settlement?.Name ?? TextObject.GetEmpty(),
                        dailyProject.BuildingType.Name));
                return false;
            }

            if (logRecovery)
            {
                ModLogger.Warn(
                    SettlementBuildingLogText.Recovered(
                        town.Settlement?.Name ?? TextObject.GetEmpty(),
                        dailyProject.BuildingType.Name));
            }

            return true;
        }

        internal static void ReconcileAllSettlements()
        {
            int checkedCount = 0;
            int repairedCount = 0;
            int failedCount = 0;

            foreach (Settlement settlement in Settlement.All)
            {
                Town town = settlement?.Town;
                if (town == null)
                {
                    continue;
                }

                checkedCount++;
                if (town.CurrentBuilding != null)
                {
                    continue;
                }

                if (EnsureCurrentBuilding(town, false))
                {
                    repairedCount++;
                }
                else
                {
                    failedCount++;
                }
            }

            ModLogger.Info(
                SettlementBuildingLogText.ReconciliationSummary(
                    checkedCount,
                    repairedCount,
                    failedCount));
        }

        internal static bool TryInvestConstructionBoost(
            Town town,
            Clan ownerClan,
            int amount)
        {
            if (town == null
                || ownerClan?.Leader == null
                || amount <= 0
                || town.BoostBuildingProcess > 0
                || town.BuildingsInProgress.Count == 0
                || ownerClan.Gold < amount)
            {
                return false;
            }

            int goldBefore = ownerClan.Gold;
            GiveGoldAction.ApplyBetweenCharacters(
                ownerClan.Leader,
                null,
                amount,
                false);
            town.BoostBuildingProcess = amount;

            ModLogger.Info(
                SettlementBuildingLogText.BoostInvested(
                    town.Settlement?.Name ?? TextObject.GetEmpty(),
                    ownerClan.Name,
                    amount,
                    goldBefore,
                    ownerClan.Gold,
                    town.BoostBuildingProcess));
            return true;
        }
    }
}
