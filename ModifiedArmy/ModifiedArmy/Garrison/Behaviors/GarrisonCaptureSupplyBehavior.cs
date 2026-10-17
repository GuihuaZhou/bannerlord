using ModifiedArmy.Garrison.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Lets AI lord parties provision undersupplied military granaries in
    /// fortifications owned by their kingdom. A conquering army additionally
    /// uses an emergency target while the new garrison is being established.
    /// Every donated item is removed from the donor's real inventory, and each
    /// donor retains a campaign-food reserve.
    /// </summary>
    public sealed class GarrisonCaptureSupplyBehavior : CampaignBehaviorBase
    {
        private const int DonorReserveDays = 10;
        private const int InitialGarrisonSupplyDays = 15;
        private const float PendingCaptureDays = 2f;

        private readonly Dictionary<Settlement, CampaignTime> _pendingCaptures =
            new Dictionary<Settlement, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(
                this,
                OnAfterSiegeCompleted);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(
                this,
                OnSettlementEntered);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // The short retry window is intentionally transient. Loading a save
            // resumes the normal local-purchase and supply-party systems.
        }

        private void OnAfterSiegeCompleted(
            Settlement settlement,
            MobileParty attackerParty,
            bool attackerWon,
            MapEvent.BattleTypes battleType)
        {
            if (!attackerWon ||
                (battleType != MapEvent.BattleTypes.Siege &&
                 battleType != MapEvent.BattleTypes.SallyOut) ||
                settlement?.IsFortification != true ||
                attackerParty?.Army == null ||
                attackerParty.IsMainParty)
            {
                return;
            }

            _pendingCaptures[settlement] = CampaignTime.Now +
                CampaignTime.Days(PendingCaptureDays);
            DonateArmyFood(attackerParty, settlement);
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            if (!CanDonateToSettlement(party, settlement))
            {
                return;
            }

            if (_pendingCaptures.TryGetValue(
                    settlement,
                    out CampaignTime expiry))
            {
                if (expiry.IsPast)
                {
                    _pendingCaptures.Remove(settlement);
                }
                else if (party.Army != null)
                {
                    DonateArmyFood(party.Army.LeaderParty, settlement);
                }
            }

            DonateVisitingPartyFood(party, settlement);
        }

        private static void DonateArmyFood(
            MobileParty armyParty,
            Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (armyParty?.Army == null || garrison == null ||
                !IsSameKingdom(armyParty, settlement))
            {
                return;
            }

            int targetFood = CalculateInitialTargetFood(settlement, garrison);
            int remaining = Math.Max(
                0,
                targetFood - (garrison.ItemRoster?.TotalFood ?? 0));
            if (remaining <= 0)
            {
                return;
            }

            DonateFood(
                armyParty.Army.Parties,
                settlement,
                remaining);
        }

        private static void DonateVisitingPartyFood(
            MobileParty party,
            Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (garrison == null)
            {
                return;
            }

            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(settlement.Town);
            if (!GarrisonLogisticsModel.NeedsReplenishment(status) ||
                status.FoodDeficit <= 0)
            {
                return;
            }

            IEnumerable<MobileParty> donors = party.Army != null
                ? party.Army.Parties
                : new[] { party };
            DonateFood(donors, settlement, status.FoodDeficit);
        }

        private static void DonateFood(
            IEnumerable<MobileParty> donors,
            Settlement settlement,
            int requestedFood)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (garrison == null || requestedFood <= 0)
            {
                return;
            }

            int remaining = requestedFood;
            foreach (MobileParty donor in donors
                .Where(p => CanDonateToSettlement(p, settlement))
                .Distinct()
                .OrderByDescending(p => p.ItemRoster?.TotalFood ?? 0))
            {
                if (remaining <= 0)
                {
                    break;
                }

                int reserve = CalculateDonorFoodReserve(donor);
                int available = Math.Max(
                    0,
                    (donor.ItemRoster?.TotalFood ?? 0) - reserve);
                if (available <= 0)
                {
                    continue;
                }

                remaining -= TransferFood(
                    donor.ItemRoster,
                    garrison.ItemRoster,
                    Math.Min(remaining, available));
            }
        }

        private static int CalculateDonorFoodReserve(MobileParty donor)
        {
            float dailyConsumption = 0f;
            var foodModel = Campaign.Current?.Models?
                .MobilePartyFoodConsumptionModel;
            if (foodModel != null)
            {
                var baseConsumption = foodModel
                    .CalculateDailyBaseFoodConsumptionf(donor, false);
                dailyConsumption = Math.Abs(foodModel
                    .CalculateDailyFoodConsumptionf(
                        donor,
                        baseConsumption)
                    .ResultNumber);
            }

            float dailyReserve = Math.Max(
                dailyConsumption,
                Math.Abs(donor.FoodChange));
            return (int)Math.Ceiling(dailyReserve * DonorReserveDays);
        }

        private static bool CanDonateToSettlement(
            MobileParty party,
            Settlement settlement)
        {
            return party != null && party.IsActive && party.IsLordParty &&
                !party.IsMainParty && settlement?.IsFortification == true &&
                settlement.Town?.GarrisonParty != null &&
                IsSameKingdom(party, settlement);
        }

        private static bool IsSameKingdom(
            MobileParty party,
            Settlement settlement)
        {
            Kingdom partyKingdom = party?.ActualClan?.Kingdom;
            Kingdom settlementKingdom = settlement?.OwnerClan?.Kingdom;
            return partyKingdom != null && partyKingdom == settlementKingdom;
        }

        private static int CalculateInitialTargetFood(
            Settlement settlement,
            MobileParty garrison)
        {
            GarrisonLogisticsStatus current =
                GarrisonLogisticsModel.Calculate(settlement.Town);
            // The garrison can still be empty when the siege-completed event is
            // raised. Use the newly conquered border establishment as the
            // minimum emergency target until troop donation finishes.
            int expectedGarrison = settlement.IsTown ? 400 : 250;
            float peoplePerFood = Campaign.Current.Models
                .MobilePartyFoodConsumptionModel
                .NumberOfMenOnMapToEatOneFood;
            if (peoplePerFood <= 0f)
            {
                peoplePerFood = 10f;
            }
            int emergencyTarget = (int)Math.Ceiling(
                expectedGarrison / peoplePerFood * InitialGarrisonSupplyDays);
            return Math.Max(current.TargetFood, emergencyTarget);
        }

        private static int TransferFood(
            ItemRoster source,
            ItemRoster destination,
            int requested)
        {
            if (source == null || destination == null || requested <= 0)
            {
                return 0;
            }

            int remaining = requested;
            for (int index = source.Count - 1;
                index >= 0 && remaining > 0;
                index--)
            {
                ItemRosterElement element = source[index];
                if (element.EquipmentElement.Item?.IsFood != true ||
                    element.Amount <= 0)
                {
                    continue;
                }

                int amount = Math.Min(element.Amount, remaining);
                source.AddToCounts(element.EquipmentElement, -amount);
                destination.AddToCounts(element.EquipmentElement, amount);
                remaining -= amount;
            }
            return requested - remaining;
        }
    }
}
