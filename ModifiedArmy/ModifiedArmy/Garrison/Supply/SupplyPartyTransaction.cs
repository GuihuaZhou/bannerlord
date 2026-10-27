using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Garrison.Supply.Services;
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

        private static int DeliverAidFood(
            MobileParty party,
            SupplyPartyComponent component)
        {
            MobileParty target = component?.AidDestination?.Town
                ?.GarrisonParty;
            if (party == null || target == null ||
                component.DispatchClan == null ||
                component.AidDestination.OwnerClan !=
                    component.DispatchClan)
                return 0;
            int deficit = Math.Max(0, GarrisonLogisticsModel
                .Calculate(component.AidDestination.Town).FoodDeficit);
            int deliverable = Math.Max(0,
                party.ItemRoster.TotalFood - CalculateAidReturnFood(party));
            int requested = Math.Min(component.RequestedFood, deficit);
            return TransferFood(party.ItemRoster, target.ItemRoster,
                Math.Min(deliverable, requested));
        }

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

            SupplyFoodPurchaseResult purchase = Cargo.PurchaseFood(
                party, source, payer, requestedFood,
                element => element.EquipmentElement.Item.Value);
            return new SupplyAcquisitionResult(
                purchase.Food, purchase.Gold);
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

            SupplyFoodPurchaseResult purchase = Cargo.PurchaseFood(
                party, source, payer, requestedFood,
                element => source.Town.GetItemPrice(
                    element.EquipmentElement, null, false));

            return new SupplyAcquisitionResult(
                purchase.Food,
                purchase.Gold + mountPurchase.GoldCost,
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
            SupplyAnimalResult result = Cargo.PurchaseRidingMounts(
                party, source, payer);
            return new SupplyMountPurchaseResult(
                result.Purchased, result.GoldCost);
        }

        /// <summary>
        /// Reuses riding animals returned by earlier missions before the party
        /// purchases any shortage at its source town.
        /// </summary>
        private static int TransferRidingMountsFromGranary(
            MobileParty garrison,
            MobileParty party)
        {
            return Cargo.TransferRidingMounts(garrison, party);
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
            SupplyAnimalResult result = Cargo.PreparePackAnimals(
                garrison, party, payer, PackAnimalsPerEscort);
            return new PackAnimalPreparationResult(result.FromGranary,
                result.Purchased, result.GoldCost);
        }

        private static int TransferPackAnimals(
            ItemRoster source,
            ItemRoster destination,
            int requestedAmount)
        {
            return Cargo.TransferPackAnimals(
                source, destination, requestedAmount);
        }

        private static ItemObject FindCheapestPackAnimal()
        {
            return Cargo.FindCheapestPackAnimal();
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
