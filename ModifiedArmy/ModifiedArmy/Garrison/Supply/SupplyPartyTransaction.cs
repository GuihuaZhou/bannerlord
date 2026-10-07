using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Garrison.Supply
{
    /// <summary>
    /// Handles physical cargo, market payment, and castle granary transfers
    /// for supply-party missions.
    /// </summary>
    public partial class SupplyPartyBehavior
    {
        private const float PackAnimalsPerEscort = 0.5f;

        private static SupplyAcquisitionResult AcquireFoodAtSource(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement source)
        {
            if (!IsValidSource(component.HomeSettlement, source))
            {
                return new SupplyAcquisitionResult();
            }

            int availableFood = CalculateAvailableFood(source);
            int requestedFood = Math.Min(
                component.RequestedFood,
                availableFood);

            if (requestedFood <= 0)
            {
                return new SupplyAcquisitionResult();
            }

            if (source.IsTown)
            {
                return PurchaseFoodFromTown(
                    party,
                    component,
                    source,
                    requestedFood);
            }

            if (IsFoodProducingVillage(source))
            {
                return PurchaseFoodFromVillage(
                    party,
                    component,
                    source,
                    requestedFood);
            }

            return new SupplyAcquisitionResult();
        }

        private static SupplyAcquisitionResult PurchaseFoodFromVillage(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement source,
            int requestedFood)
        {
            Hero payer = component.HomeSettlement.OwnerClan?.Leader;

            if (payer == null || payer.Gold <= 0)
            {
                return new SupplyAcquisitionResult();
            }

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

                if (item?.IsFood != true || element.Amount <= 0)
                {
                    continue;
                }

                int unitPrice = Math.Max(1, item.Value);
                int amount = Math.Min(
                    Math.Min(
                        Math.Min(element.Amount, remainingFood),
                        remainingGold / unitPrice),
                    CalculateCapacityForItem(party, item));

                if (amount <= 0)
                {
                    continue;
                }

                source.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    -amount);
                party.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    amount);

                int cost = amount * unitPrice;
                remainingFood -= amount;
                remainingGold -= cost;
                acquiredFood += amount;
                totalCost += cost;
            }

            if (totalCost > 0)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(
                    payer,
                    source,
                    totalCost,
                    true);
            }

            return new SupplyAcquisitionResult(acquiredFood, totalCost);
        }

        private static SupplyAcquisitionResult PurchaseFoodFromTown(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement source,
            int requestedFood)
        {
            Hero payer = component.HomeSettlement.OwnerClan?.Leader;

            if (payer == null || payer.Gold <= 0)
            {
                return new SupplyAcquisitionResult();
            }

            SupplyMountPurchaseResult mountPurchase =
                PurchaseRidingMountsFromTown(party, source, payer);

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

                if (item?.IsFood != true || element.Amount <= 0)
                {
                    continue;
                }

                int unitPrice = Math.Max(
                    1,
                    source.Town.GetItemPrice(
                        element.EquipmentElement,
                        null,
                        false));
                int affordableAmount = remainingGold / unitPrice;
                int capacityAmount = CalculateCapacityForItem(
                    party,
                    item);
                int amount = Math.Min(
                    Math.Min(
                        Math.Min(element.Amount, remainingFood),
                        affordableAmount),
                    capacityAmount);

                if (amount <= 0)
                {
                    continue;
                }

                source.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    -amount);
                party.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    amount);

                int cost = amount * unitPrice;
                remainingFood -= amount;
                remainingGold -= cost;
                acquiredFood += amount;
                totalCost += cost;
            }

            if (totalCost > 0)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(
                    payer,
                    source,
                    totalCost,
                    true);
            }

            return new SupplyAcquisitionResult(
                acquiredFood,
                totalCost + mountPurchase.GoldCost,
                mountPurchase.Amount,
                mountPurchase.GoldCost);
        }

        /// <summary>
        /// Buys real riding animals from the source town market for foot
        /// escorts. This lets the native speed model calculate mounted-foot
        /// travel naturally. No riding animals are created from thin air.
        /// </summary>
        private static SupplyMountPurchaseResult
            PurchaseRidingMountsFromTown(
                MobileParty party,
                Settlement source,
                Hero payer)
        {
            int footTroops = party.MemberRoster
                .GetTroopRoster()
                .Where(element => !element.Character.IsMounted)
                .Sum(element => element.Number);
            int requestedMounts = Math.Max(
                0,
                footTroops - party.ItemRoster.NumberOfMounts);

            if (requestedMounts <= 0 || payer.Gold <= 0)
            {
                return new SupplyMountPurchaseResult();
            }

            List<ItemRosterElement> candidates = source.ItemRoster
                .Where(element =>
                    element.Amount > 0
                    && element.EquipmentElement.Item?.IsMountable == true
                    && element.EquipmentElement.Item.ItemCategory
                        == DefaultItemCategories.Horse)
                .OrderBy(element => source.Town.GetItemPrice(
                    element.EquipmentElement,
                    null,
                    false))
                .ToList();

            int remainingMounts = requestedMounts;
            int remainingGold = payer.Gold;
            int purchased = 0;
            int totalCost = 0;

            foreach (ItemRosterElement element in candidates)
            {
                if (remainingMounts <= 0 || remainingGold <= 0)
                {
                    break;
                }

                int unitPrice = Math.Max(
                    1,
                    source.Town.GetItemPrice(
                        element.EquipmentElement,
                        null,
                        false));
                int amount = Math.Min(
                    Math.Min(element.Amount, remainingMounts),
                    remainingGold / unitPrice);

                if (amount <= 0)
                {
                    continue;
                }

                source.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    -amount);
                party.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    amount);

                int cost = amount * unitPrice;
                remainingMounts -= amount;
                remainingGold -= cost;
                purchased += amount;
                totalCost += cost;
            }

            if (totalCost > 0)
            {
                GiveGoldAction.ApplyForCharacterToSettlement(
                    payer,
                    source,
                    totalCost,
                    true);
            }

            return new SupplyMountPurchaseResult(
                purchased,
                totalCost);
        }

        /// <summary>
        /// Reuses riding animals returned by earlier missions before the party
        /// purchases any shortage at its source town.
        /// </summary>
        private static int TransferRidingMountsFromGranary(
            MobileParty garrison,
            MobileParty party)
        {
            int footTroops = party.MemberRoster
                .GetTroopRoster()
                .Where(element => !element.Character.IsMounted)
                .Sum(element => element.Number);
            int remaining = Math.Max(
                0,
                footTroops - party.ItemRoster.NumberOfMounts);
            int requested = remaining;

            for (int index = garrison.ItemRoster.Count - 1;
                index >= 0 && remaining > 0;
                index--)
            {
                ItemRosterElement element = garrison.ItemRoster[index];
                ItemObject item = element.EquipmentElement.Item;

                if (element.Amount <= 0
                    || item?.IsMountable != true
                    || item.ItemCategory != DefaultItemCategories.Horse)
                {
                    continue;
                }

                int amount = Math.Min(element.Amount, remaining);
                garrison.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    -amount);
                party.ItemRoster.AddToCounts(
                    element.EquipmentElement,
                    amount);
                remaining -= amount;
            }

            return requested - remaining;
        }

        private static int CalculateCapacityForItem(
            MobileParty party,
            ItemObject item)
        {
            float remainingCapacity = Math.Max(
                0f,
                party.InventoryCapacity - party.TotalWeightCarried);

            if (item.Weight <= 0f)
            {
                return int.MaxValue;
            }

            return Math.Max(
                0,
                (int)Math.Floor(remainingCapacity / item.Weight));
        }

        /// <summary>
        /// Takes pack animals from the military granary first. Any shortage is
        /// purchased by the settlement owner and created as real inventory.
        /// All surviving animals return to the granary with the party.
        /// </summary>
        private static PackAnimalPreparationResult PreparePackAnimals(
            MobileParty garrison,
            MobileParty party,
            Hero payer)
        {
            int targetAmount = Math.Max(
                1,
                (int)Math.Floor(
                    party.MemberRoster.TotalManCount
                    * PackAnimalsPerEscort));
            int fromGranary = TransferPackAnimals(
                garrison.ItemRoster,
                party.ItemRoster,
                targetAmount);
            int shortage = targetAmount - fromGranary;

            if (shortage <= 0 || payer == null || payer.Gold <= 0)
            {
                return new PackAnimalPreparationResult(
                    fromGranary,
                    0,
                    0);
            }

            ItemObject packAnimal = FindCheapestPackAnimal();

            if (packAnimal == null)
            {
                return new PackAnimalPreparationResult(
                    fromGranary,
                    0,
                    0);
            }

            int unitPrice = Math.Max(1, packAnimal.Value);
            int purchased = Math.Min(shortage, payer.Gold / unitPrice);
            int goldCost = purchased * unitPrice;

            if (purchased > 0)
            {
                party.ItemRoster.AddToCounts(packAnimal, purchased);
                GiveGoldAction.ApplyBetweenCharacters(
                    payer,
                    null,
                    goldCost,
                    true);
            }

            return new PackAnimalPreparationResult(
                fromGranary,
                purchased,
                goldCost);
        }

        private static int TransferPackAnimals(
            ItemRoster source,
            ItemRoster destination,
            int requestedAmount)
        {
            int remaining = requestedAmount;

            for (int index = source.Count - 1;
                index >= 0 && remaining > 0;
                index--)
            {
                ItemRosterElement element = source[index];
                ItemObject item = element.EquipmentElement.Item;

                if (item?.ItemCategory != DefaultItemCategories.PackAnimal
                    || element.Amount <= 0)
                {
                    continue;
                }

                int amount = Math.Min(element.Amount, remaining);
                source.AddToCounts(element.EquipmentElement, -amount);
                destination.AddToCounts(element.EquipmentElement, amount);
                remaining -= amount;
            }

            return requestedAmount - remaining;
        }

        private static ItemObject FindCheapestPackAnimal()
        {
            ItemObject packAnimal = null;
            int lowestValue = int.MaxValue;

            foreach (ItemObject item in
                MBObjectManager.Instance.GetObjectTypeList<ItemObject>())
            {
                if (item.ItemCategory == DefaultItemCategories.PackAnimal
                    && !item.NotMerchandise
                    && item.Value < lowestValue)
                {
                    packAnimal = item;
                    lowestValue = item.Value;
                }
            }

            return packAnimal;
        }

        private sealed class SupplyAcquisitionResult
        {
            public SupplyAcquisitionResult(
                int foodAcquired = 0,
                int goldCost = 0,
                int mountsPurchased = 0,
                int mountGoldCost = 0)
            {
                FoodAcquired = foodAcquired;
                GoldCost = goldCost;
                MountsPurchased = mountsPurchased;
                MountGoldCost = mountGoldCost;
            }

            public int FoodAcquired { get; }

            public int GoldCost { get; }

            public int MountsPurchased { get; }

            public int MountGoldCost { get; }
        }

        private sealed class SupplyMountPurchaseResult
        {
            public SupplyMountPurchaseResult(
                int amount = 0,
                int goldCost = 0)
            {
                Amount = amount;
                GoldCost = goldCost;
            }

            public int Amount { get; }

            public int GoldCost { get; }
        }

        private sealed class PackAnimalPreparationResult
        {
            public PackAnimalPreparationResult(
                int fromGranary = 0,
                int purchased = 0,
                int goldCost = 0)
            {
                FromGranary = fromGranary;
                Purchased = purchased;
                GoldCost = goldCost;
            }

            public int FromGranary { get; }

            public int Purchased { get; }

            public int GoldCost { get; }
        }
    }
}
