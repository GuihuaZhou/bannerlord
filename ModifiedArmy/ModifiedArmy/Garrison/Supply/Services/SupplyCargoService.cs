using System;
using TaleWorlds.CampaignSystem.Roster;
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
    }
}
