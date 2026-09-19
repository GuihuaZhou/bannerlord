using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Requisitions civilian FoodStocks into the real garrison granary when
    /// military supplies fall below thirty days. Requisition has no civilian
    /// reserve floor and may reduce FoodStocks to zero.
    /// </summary>
    public class GarrisonLocalRequisitionBehavior : CampaignBehaviorBase
    {
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

            if (settlement?.IsFortification != true
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

            int availableCivilianFood =
                Math.Max(0, (int)Math.Floor(town.FoodStocks));

            int requisitionAmount = Math.Min(
                availableCivilianFood,
                status.FoodDeficit);

            if (requisitionAmount <= 0)
            {
                return;
            }

            town.FoodStocks -= requisitionAmount;
            town.GarrisonParty.ItemRoster.AddToCounts(
                DefaultItems.Grain,
                requisitionAmount);

            if (settlement.OwnerClan == Clan.PlayerClan)
            {
                ModLogger.Notice(
                    $"[GarrisonLogistics] Local requisition | " +
                    $"Settlement='{settlement.Name}' | " +
                    $"Transferred={requisitionAmount} | " +
                    $"FoodStocks={town.FoodStocks:0.##} | " +
                    $"MilitaryFood=" +
                    $"{town.GarrisonParty.ItemRoster.TotalFood}");
            }
        }
    }
}
