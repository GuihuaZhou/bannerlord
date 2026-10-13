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
    /// Makes an AI army provision the military granary after capturing a
    /// fortification. Every donated item is removed from an army party's real
    /// inventory, and each donor retains a campaign-food reserve.
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
            if (party?.Army == null || party.IsMainParty ||
                settlement?.IsFortification != true ||
                !_pendingCaptures.TryGetValue(settlement, out CampaignTime expiry))
            {
                return;
            }

            if (expiry.IsPast || party.MapFaction != settlement.MapFaction)
            {
                _pendingCaptures.Remove(settlement);
                return;
            }

            DonateArmyFood(party.Army.LeaderParty, settlement);
        }

        private static void DonateArmyFood(
            MobileParty armyParty,
            Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (armyParty?.Army == null || garrison == null ||
                armyParty.MapFaction != settlement.MapFaction)
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

            foreach (MobileParty donor in armyParty.Army.Parties
                .Where(p => p != null && p.IsActive && !p.IsMainParty &&
                    p.MapFaction == settlement.MapFaction)
                .OrderByDescending(p => p.ItemRoster?.TotalFood ?? 0))
            {
                if (remaining <= 0)
                {
                    break;
                }

                int reserve = (int)Math.Ceiling(
                    Math.Abs(donor.FoodChange) * DonorReserveDays);
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
