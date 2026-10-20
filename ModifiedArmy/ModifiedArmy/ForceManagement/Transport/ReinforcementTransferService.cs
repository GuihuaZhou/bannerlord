using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Transport
{
    public sealed class ReinforcementTransferService
    {
        private const int SourceFoodReserve = 100;
        private const int MinimumTravelFood = 10;

        public MobileParty TryCreate(ReinforcementDispatchPlan plan)
        {
            if (plan?.IsApproved != true) return null;
            MobileParty garrison = plan.Source.Town?.GarrisonParty;
            if (garrison == null || plan.Source.OwnerClan != plan.Clan ||
                plan.Destination.OwnerClan != plan.Clan) return null;

            TroopRoster roster = ExtractHealthyOrdinaryTroops(
                garrison, plan.TroopCount);
            if (roster.TotalManCount < plan.TroopCount)
            {
                TransferTroops(roster, garrison.MemberRoster,
                    roster.TotalManCount);
                return null;
            }

            MobileParty party = null;
            try
            {
                party = ReinforcementPartyComponent.Create(
                    plan.Source, plan.Destination, plan.Clan,
                    plan.TroopCount, plan.FoodCount, roster);
                EnterSettlementAction.ApplyForParty(party, plan.Source);
                int food = TransferFood(garrison.ItemRoster,
                    party.ItemRoster,
                    Math.Max(MinimumTravelFood, plan.FoodCount));
                if (food < MinimumTravelFood)
                    throw new InvalidOperationException(
                        "InsufficientTravelFood");
                return party;
            }
            catch
            {
                if (party?.IsActive == true)
                {
                    TransferTroops(party.MemberRoster,
                        garrison.MemberRoster,
                        party.MemberRoster.TotalManCount);
                    TransferAllItems(party.ItemRoster,
                        garrison.ItemRoster);
                    DestroyPartyAction.Apply(null, party);
                }
                else
                {
                    TransferTroops(roster, garrison.MemberRoster,
                        roster.TotalManCount);
                }
                throw;
            }
        }

        public int DeliverTroops(
            MobileParty party,
            ReinforcementPartyComponent component)
        {
            MobileParty target = component?.Destination?.Town?.GarrisonParty;
            if (party == null || target == null ||
                component.DispatchClan == null ||
                component.Destination.OwnerClan != component.DispatchClan)
                return 0;
            int free = GarrisonRecruitmentCapacity
                .GetTransferFreeSlots(target);
            var need = ForceManagementFacade.Current.ReinforcementNeed
                .Evaluate(ForceManagementFacade.Current.GarrisonDefense
                    .Evaluate(component.Destination));
            int requested = need.IsRequested ? need.RequestedTroops : 0;
            return TransferTroops(party.MemberRoster, target.MemberRoster,
                Math.Min(free, requested));
        }

        public void DeliverItemsAtDestination(
            MobileParty party,
            ReinforcementPartyComponent component,
            bool keepTravelFood)
        {
            MobileParty target = component?.Destination?.Town?.GarrisonParty;
            if (party == null || target == null ||
                component.Destination.OwnerClan != component.DispatchClan)
                return;
            TransferFoodLeavingReserve(party.ItemRoster, target.ItemRoster,
                keepTravelFood ? MinimumTravelFood : 0);
            if (!keepTravelFood)
                TransferAllItems(party.ItemRoster, target.ItemRoster);
        }

        public int RecoverTroops(
            MobileParty party,
            ReinforcementPartyComponent component,
            Settlement recovery)
        {
            MobileParty target = recovery?.Town?.GarrisonParty;
            if (party == null || target == null ||
                recovery.OwnerClan != component?.DispatchClan ||
                recovery.IsUnderSiege) return 0;
            return TransferTroops(party.MemberRoster, target.MemberRoster,
                GarrisonRecruitmentCapacity.GetHardFreeSlots(target));
        }

        public void RecoverAllItems(
            MobileParty party,
            ReinforcementPartyComponent component,
            Settlement recovery)
        {
            MobileParty target = recovery?.Town?.GarrisonParty;
            if (party == null || target == null ||
                recovery.OwnerClan != component?.DispatchClan) return;
            TransferAllItems(party.ItemRoster, target.ItemRoster);
        }

        public bool TryDestroyEmpty(
            MobileParty party,
            ReinforcementPartyComponent component)
        {
            if (party == null || component == null ||
                party.MemberRoster.TotalManCount > 0 ||
                party.ItemRoster.Count > 0) return false;
            component.Complete();
            DestroyPartyAction.Apply(null, party);
            return true;
        }

        private static TroopRoster ExtractHealthyOrdinaryTroops(
            MobileParty source,
            int requested)
        {
            TroopRoster result = TroopRoster.CreateDummyTroopRoster();
            Dictionary<CharacterObject, int> protectedCounts =
                Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                    .GetServingTroopCounts(source) ??
                    new Dictionary<CharacterObject, int>();
            int remaining = Math.Max(0, requested);
            foreach (TroopRosterElement element in source.MemberRoster
                .GetTroopRoster().Where(x => x.Character != null &&
                    !x.Character.IsHero).OrderBy(x => x.Character.Tier)
                .ToList())
            {
                int protectedCount = protectedCounts.TryGetValue(
                    element.Character, out int count) ? count : 0;
                int eligible = Math.Max(0,
                    element.Number - element.WoundedNumber - protectedCount);
                int amount = Math.Min(remaining, eligible);
                if (amount <= 0) continue;
                source.MemberRoster.AddToCounts(element.Character, -amount,
                    false, 0, 0, true, -1);
                result.AddToCounts(element.Character, amount,
                    false, 0, 0, true, -1);
                remaining -= amount;
                if (remaining <= 0) break;
            }
            return result;
        }

        public static int TransferTroops(
            TroopRoster source,
            TroopRoster destination,
            int maximum)
        {
            int remaining = Math.Max(0, maximum);
            int moved = 0;
            foreach (TroopRosterElement element in source.GetTroopRoster()
                .Where(x => x.Character != null && !x.Character.IsHero)
                .ToList())
            {
                int amount = Math.Min(remaining, element.Number);
                if (amount <= 0) continue;
                int wounded = Math.Min(amount, element.WoundedNumber);
                source.AddToCounts(element.Character, -amount, false,
                    -wounded, 0, true, -1);
                destination.AddToCounts(element.Character, amount, false,
                    wounded, 0, true, -1);
                moved += amount;
                remaining -= amount;
                if (remaining <= 0) break;
            }
            return moved;
        }

        private static int TransferFood(
            ItemRoster source,
            ItemRoster destination,
            int requested)
        {
            int available = Math.Max(0, source.TotalFood - SourceFoodReserve);
            int remaining = Math.Min(Math.Max(0, requested), available);
            int moved = 0;
            foreach (ItemRosterElement element in source.ToList())
            {
                ItemObject item = element.EquipmentElement.Item;
                if (item?.IsFood != true || remaining <= 0) continue;
                int amount = Math.Min(remaining, element.Amount);
                source.AddToCounts(item, -amount);
                destination.AddToCounts(item, amount);
                moved += amount;
                remaining -= amount;
            }
            return moved;
        }

        private static int TransferFoodLeavingReserve(
            ItemRoster source,
            ItemRoster destination,
            int reserve)
        {
            int remaining = Math.Max(0, source.TotalFood - reserve);
            int moved = 0;
            foreach (ItemRosterElement element in source.ToList())
            {
                ItemObject item = element.EquipmentElement.Item;
                if (item?.IsFood != true || remaining <= 0) continue;
                int amount = Math.Min(remaining, element.Amount);
                source.AddToCounts(item, -amount);
                destination.AddToCounts(item, amount);
                moved += amount;
                remaining -= amount;
            }
            return moved;
        }

        public static void TransferAllItems(
            ItemRoster source,
            ItemRoster destination)
        {
            foreach (ItemRosterElement element in source.ToList())
            {
                if (element.Amount <= 0) continue;
                source.AddToCounts(element.EquipmentElement, -element.Amount);
                destination.AddToCounts(
                    element.EquipmentElement, element.Amount);
            }
        }
    }
}
