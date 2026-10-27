using System;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.Garrison.Supply.Services
{
    /// <summary>Authoritative low-level cargo and escort roster mutations.</summary>
    public sealed class SupplyCargoService
    {
        public void RestoreTroops(TroopRoster source, TroopRoster destination)
        {
            foreach (TroopRosterElement element in source.GetTroopRoster())
                destination.AddToCounts(element.Character, element.Number,
                    false, element.WoundedNumber, 0, true, -1);
        }

        public void TransferAllItems(ItemRoster source, ItemRoster destination)
        {
            for (int index = source.Count - 1; index >= 0; index--)
            {
                ItemRosterElement element = source[index];
                destination.AddToCounts(
                    element.EquipmentElement, element.Amount);
                source.AddToCounts(
                    element.EquipmentElement, -element.Amount);
            }
        }

        public int TransferFood(
            ItemRoster source,
            ItemRoster destination,
            int requestedAmount)
        {
            int remaining = requestedAmount;
            for (int index = source.Count - 1;
                index >= 0 && remaining > 0; index--)
            {
                ItemRosterElement element = source[index];
                if (element.EquipmentElement.Item?.IsFood != true ||
                    element.Amount <= 0)
                    continue;
                int amount = Math.Min(element.Amount, remaining);
                source.AddToCounts(element.EquipmentElement, -amount);
                destination.AddToCounts(element.EquipmentElement, amount);
                remaining -= amount;
            }
            return requestedAmount - remaining;
        }

        public int CalculateFoodCapacity(ItemRoster source, MobileParty party)
        {
            if (source == null || party == null) return 0;
            float remainingWeight = Math.Max(0f,
                party.InventoryCapacity - party.TotalWeightCarried);
            int capacity = 0;
            for (int index = source.Count - 1; index >= 0; index--)
            {
                ItemRosterElement element = source[index];
                ItemObject item = element.EquipmentElement.Item;
                if (item?.IsFood != true || element.Amount <= 0) continue;
                if (item.Weight <= 0f)
                {
                    capacity += element.Amount;
                    continue;
                }
                int amount = Math.Min(element.Amount,
                    (int)Math.Floor(remainingWeight / item.Weight));
                capacity += Math.Max(0, amount);
                remainingWeight -= Math.Max(0, amount) * item.Weight;
            }
            return capacity;
        }

        public int TransferFoodWithinCapacity(
            ItemRoster source,
            ItemRoster destination,
            int requested,
            MobileParty party)
        {
            int remaining = Math.Max(0, requested);
            float remainingWeight = party == null
                ? float.MaxValue
                : Math.Max(0f,
                    party.InventoryCapacity - party.TotalWeightCarried);
            for (int index = source.Count - 1;
                index >= 0 && remaining > 0; index--)
            {
                ItemRosterElement element = source[index];
                ItemObject item = element.EquipmentElement.Item;
                if (item?.IsFood != true || element.Amount <= 0) continue;
                int itemCapacity = item.Weight <= 0f
                    ? int.MaxValue
                    : (int)Math.Floor(remainingWeight / item.Weight);
                int amount = Math.Min(Math.Min(element.Amount, remaining),
                    Math.Max(0, itemCapacity));
                if (amount <= 0) continue;
                source.AddToCounts(element.EquipmentElement, -amount);
                destination.AddToCounts(element.EquipmentElement, amount);
                remaining -= amount;
                if (item.Weight > 0f)
                    remainingWeight -= amount * item.Weight;
            }
            return requested - remaining;
        }
    }
}
