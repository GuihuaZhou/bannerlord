using ModifiedArmy.ForceManagement.Contracts;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Services
{
    public sealed class FoodTransferService
    {
        private const int MinimumSourceFoodReserve = 100;
        private const int CarrierFoodReserveDays = 5;

        public int RegisterExistingCarrierFood(ReinforcementMission mission)
        {
            MobileParty carrier = mission?.Carrier;
            if (carrier == null) return 0;
            int amount = Math.Min(Math.Max(0, mission.RequestedFoodCount),
                GetDeliverableFood(carrier));
            mission.RequestedFoodCount -= amount;
            return amount;
        }

        public int Load(ReinforcementMission mission)
        {
            MobileParty carrier = mission?.Carrier;
            ItemRoster source = mission?.FoodSource?.ItemRoster;
            if (carrier == null || source == null ||
                carrier.CurrentSettlement != mission.FoodSource ||
                mission.FoodSource.OwnerClan != carrier.ActualClan)
                return 0;

            int sourceFood = source.Where(x =>
                x.EquipmentElement.Item?.IsFood == true).Sum(x => x.Amount);
            int remaining = Math.Min(Math.Max(0, mission.RequestedFoodCount),
                Math.Max(0, sourceFood - MinimumSourceFoodReserve));
            int loaded = 0;
            foreach (ItemRosterElement element in source.ToList())
            {
                ItemObject item = element.EquipmentElement.Item;
                if (item?.IsFood != true || remaining <= 0) continue;
                int capacity = item.Weight <= 0f ? int.MaxValue :
                    Math.Max(0, (int)Math.Floor((carrier.InventoryCapacity -
                        carrier.TotalWeightCarried) / item.Weight));
                int amount = Math.Min(remaining,
                    Math.Min(element.Amount, capacity));
                if (amount <= 0) continue;
                source.AddToCounts(item, -amount);
                carrier.ItemRoster.AddToCounts(item, amount);
                loaded += amount;
                remaining -= amount;
            }
            mission.RequestedFoodCount = Math.Max(0,
                mission.RequestedFoodCount - loaded);
            return loaded;
        }

        public int Unload(ReinforcementMission mission)
        {
            MobileParty carrier = mission?.Carrier;
            ItemRoster target = mission?.Destination?.ItemRoster;
            if (carrier == null || target == null ||
                carrier.CurrentSettlement != mission.Destination ||
                mission.Destination.OwnerClan != carrier.ActualClan)
                return 0;

            int remaining = GetDeliverableFood(carrier);
            int delivered = 0;
            foreach (ItemRosterElement element in carrier.ItemRoster.ToList())
            {
                ItemObject item = element.EquipmentElement.Item;
                if (item?.IsFood != true || remaining <= 0) continue;
                int amount = Math.Min(remaining, element.Amount);
                if (amount <= 0) continue;
                carrier.ItemRoster.AddToCounts(item, -amount);
                target.AddToCounts(item, amount);
                delivered += amount;
                remaining -= amount;
            }
            return delivered;
        }

        private static int GetDeliverableFood(MobileParty carrier)
        {
            int reserve = Math.Max(0, (int)Math.Ceiling(
                Math.Max(0f, -carrier.FoodChange) * CarrierFoodReserveDays));
            return Math.Max(0, carrier.ItemRoster.TotalFood - reserve);
        }
    }
}
