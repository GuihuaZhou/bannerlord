using System;
using ModifiedArmy.Garrison.Models;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
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

        public void RollbackDeparture(
            Settlement home,
            MobileParty garrison,
            MobileParty supplyParty,
            TroopRoster detachedEscort,
            int purchasedPackAnimals,
            int refundableGold,
            Action<Settlement, MobileParty, int, int, int, int, int>
                logCapacity)
        {
            if (supplyParty?.IsActive == true)
            {
                SupplyPartyComponent component = supplyParty.PartyComponent as
                    SupplyPartyComponent;
                int garrisonBefore = garrison.Party.NumberOfAllMembers;
                int returning = supplyParty.Party.NumberOfAllMembers;
                int reservedBefore = GarrisonRecruitmentCapacity
                    .GetActiveSupplyEscortCount(home);
                int limit = garrison.Party.PartySizeLimit;
                int state = component?.MissionStateValue ?? -1;
                component?.CompleteMission();
                RestoreTroops(supplyParty.MemberRoster,
                    garrison.MemberRoster);
                RemovePurchasedPackAnimalsForRecovery(supplyParty.ItemRoster,
                    purchasedPackAnimals);
                TransferAllItems(supplyParty.ItemRoster,
                    garrison.ItemRoster);
                logCapacity?.Invoke(home, supplyParty, garrisonBefore,
                    returning, reservedBefore, limit, state);
                DestroyPartyAction.Apply(null, supplyParty);
            }
            else if (detachedEscort != null)
            {
                RestoreTroops(detachedEscort, garrison.MemberRoster);
            }

            if (refundableGold > 0 && home?.OwnerClan?.Leader != null)
                GiveGoldAction.ApplyBetweenCharacters(null,
                    home.OwnerClan.Leader, refundableGold, true);
        }

        public void RemovePurchasedPackAnimalsForRecovery(
            ItemRoster roster,
            int requested)
        {
            int remaining = Math.Max(0, requested);
            for (int index = roster.Count - 1;
                index >= 0 && remaining > 0; index--)
            {
                ItemRosterElement element = roster[index];
                if (element.EquipmentElement.Item?.ItemCategory !=
                        DefaultItemCategories.PackAnimal ||
                    element.Amount <= 0)
                    continue;
                int amount = Math.Min(element.Amount, remaining);
                roster.AddToCounts(element.EquipmentElement, -amount);
                remaining -= amount;
            }
        }

        public SupplyFoodPurchaseResult PurchaseFood(
            MobileParty party,
            Settlement source,
            Hero payer,
            int requestedFood,
            Func<ItemRosterElement, int> getUnitPrice)
        {
            if (party == null || source == null || payer == null ||
                payer.Gold <= 0 || requestedFood <= 0)
                return new SupplyFoodPurchaseResult(0, 0);
            int remainingFood = requestedFood;
            int remainingGold = payer.Gold;
            int acquiredFood = 0;
            int totalCost = 0;
            for (int index = source.ItemRoster.Count - 1;
                index >= 0 && remainingFood > 0 && remainingGold > 0;
                index--)
            {
                ItemRosterElement element = source.ItemRoster[index];
                ItemObject item = element.EquipmentElement.Item;
                if (item?.IsFood != true || element.Amount <= 0) continue;
                int unitPrice = Math.Max(1, getUnitPrice(element));
                int capacity = CalculateCapacityForItem(party, item);
                int amount = Math.Min(Math.Min(
                    Math.Min(element.Amount, remainingFood),
                    remainingGold / unitPrice), capacity);
                if (amount <= 0) continue;
                source.ItemRoster.AddToCounts(
                    element.EquipmentElement, -amount);
                party.ItemRoster.AddToCounts(
                    element.EquipmentElement, amount);
                int cost = amount * unitPrice;
                remainingFood -= amount;
                remainingGold -= cost;
                acquiredFood += amount;
                totalCost += cost;
            }
            if (totalCost > 0)
                GiveGoldAction.ApplyForCharacterToSettlement(
                    payer, source, totalCost, true);
            return new SupplyFoodPurchaseResult(acquiredFood, totalCost);
        }

        private static int CalculateCapacityForItem(
            MobileParty party,
            ItemObject item)
        {
            if (party == null || item == null) return 0;
            if (item.Weight <= 0f) return int.MaxValue;
            return Math.Max(0, (int)Math.Floor(Math.Max(0f,
                party.InventoryCapacity - party.TotalWeightCarried) /
                item.Weight));
        }
    }

    public readonly struct SupplyFoodPurchaseResult
    {
        public SupplyFoodPurchaseResult(int food, int gold)
        {
            Food = food;
            Gold = gold;
        }
        public int Food { get; }
        public int Gold { get; }
    }
}
