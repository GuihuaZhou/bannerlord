using Helpers;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Supply
{
    /// <summary>
    /// Creates and controls temporary supply parties for fortifications whose
    /// military granaries cannot be restored by local sources alone.
    /// </summary>
    public partial class SupplyPartyBehavior : CampaignBehaviorBase
    {
        public const int EscortSize = 50;

        public const int MaximumRequestedFood = 500;

        private const float MaximumSourceDistance = 150f;

        private const int VillagePurchaseBatchSize = 100;

        private const int TownPurchaseBatchSize = 75;

        private const int MinimumTownMarketFoodReserve = 100;

        private const int EmergencySupplyDays = 15;

        private const int OutboundProvisionDays = 5;

        private const int DestroyedPartyCooldownDays = 10;

        private Dictionary<Settlement, CampaignTime>
            _nextDispatchTimes =
                new Dictionary<Settlement, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent
                .AddNonSerializedListener(this, OnDailySettlementTick);
            CampaignEvents.HourlyTickPartyEvent
                .AddNonSerializedListener(this, OnHourlyPartyTick);
            CampaignEvents.SettlementEntered
                .AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.MobilePartyDestroyed
                .AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.OnSettlementOwnerChangedEvent
                .AddNonSerializedListener(this, OnSettlementOwnerChanged);
            CampaignEvents.OnGameLoadFinishedEvent
                .AddNonSerializedListener(this, OnGameLoadFinished);
            CampaignEvents.OnSessionLaunchedEvent
                .AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "_modifiedArmySupplyNextDispatchTimes",
                ref _nextDispatchTimes);

            _nextDispatchTimes ??=
                new Dictionary<Settlement, CampaignTime>();
        }

        private void OnDailySettlementTick(Settlement settlement)
        {
            if (!CanDispatchFrom(settlement)
                || FindSupplyPartyForHome(settlement) != null
                || IsDispatchCoolingDown(settlement))
            {
                return;
            }

            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(settlement.Town);

            if (!GarrisonLogisticsModel.NeedsReplenishment(status)
                || !ShouldDispatchAfterLocalSupply(settlement, status))
            {
                return;
            }

            SourceSelection source = FindBestSource(
                settlement,
                status.FoodDeficit,
                null);

            if (source == null)
            {
                SetDispatchCooldown(settlement, 1f);
                return;
            }

            TryCreateSupplyParty(
                settlement,
                source,
                Math.Min(status.FoodDeficit, MaximumRequestedFood));
        }

        private void OnHourlyPartyTick(MobileParty party)
        {
            SupplyPartyComponent component = GetComponent(party);

            if (component == null
                || !party.IsActive
                || party.MapEvent != null)
            {
                return;
            }

            if (component.MissionState
                == SupplyPartyMissionState.TravelingToSource)
            {
                if (!IsValidSource(
                    component.HomeSettlement,
                    component.SourceSettlement))
                {
                    SourceSelection replacement = FindBestSource(
                        component.HomeSettlement,
                        component.RequestedFood,
                        party);

                    if (replacement == null)
                    {
                        component.BeginReturnJourney();
                        MoveToSettlement(
                            party,
                            component.HomeSettlement);
                        return;
                    }

                    component.ChangeSource(replacement.Settlement);
                }

                EnsureMovingTo(
                    party,
                    component.SourceSettlement);
                return;
            }

            if (component.MissionState
                == SupplyPartyMissionState.ReturningHome)
            {
                EnsureMovingTo(party, component.HomeSettlement);
            }
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            SupplyPartyComponent component = GetComponent(party);

            if (component == null || !party.IsActive)
            {
                return;
            }

            if (component.MissionState
                    == SupplyPartyMissionState.TravelingToSource
                && settlement == component.SourceSettlement)
            {
                SupplyAcquisitionResult acquisition =
                    AcquireFoodAtSource(party, component, settlement);
                component.RecordSourceVisit(
                    settlement,
                    acquisition.FoodAcquired);

                if (component.HomeSettlement.OwnerClan == Clan.PlayerClan)
                {
                    TextObject message = GameTexts.FindText(
                        "str_modifiedarmy_supply_party_loaded");
                    message.SetTextVariable(
                        "HOME_NAME",
                        component.HomeSettlement.Name);
                    message.SetTextVariable("SOURCE_NAME", settlement.Name);
                    message.SetTextVariable("FOOD", acquisition.FoodAcquired);
                    message.SetTextVariable("COST", acquisition.GoldCost);
                    message.SetTextVariable(
                        "MOUNTS",
                        acquisition.MountsPurchased);
                    message.SetTextVariable(
                        "MOUNT_COST",
                        acquisition.MountGoldCost);
                    message.SetTextVariable(
                        "CARGO_FOOD",
                        party.ItemRoster.TotalFood);
                    ModLogger.Info(message.ToString());
                }

                Hero payer = component.HomeSettlement.OwnerClan?.Leader;
                bool canContinue = component.RequestedFood > 0
                    && party.TotalWeightCarried < party.InventoryCapacity
                    && payer?.Gold > 0;
                SourceSelection nextSource = canContinue
                    ? FindBestSource(
                        component.HomeSettlement,
                        component.RequestedFood,
                        party)
                    : null;

                if (nextSource != null)
                {
                    component.ChangeSource(nextSource.Settlement);
                    MoveToSettlement(party, nextSource.Settlement);
                }
                else
                {
                    component.BeginReturnJourney();
                    MoveToSettlement(party, component.HomeSettlement);
                }

                return;
            }

            if (component.MissionState
                    == SupplyPartyMissionState.ReturningHome
                && settlement == component.HomeSettlement)
            {
                CompleteMission(party, component);
            }
        }

        private void OnMobilePartyDestroyed(
            MobileParty party,
            PartyBase destroyerParty)
        {
            SupplyPartyComponent component = GetComponent(party);

            if (component == null
                || component.MissionState
                    == SupplyPartyMissionState.Completed)
            {
                return;
            }

            // Losing the escort, animals, and cargo temporarily interrupts the
            // home settlement's ability to organize another supply mission.
            SetDispatchCooldown(
                component.HomeSettlement,
                DestroyedPartyCooldownDays);

            if (component.HomeSettlement?.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_destroyed");
                message.SetTextVariable(
                    "HOME_NAME",
                    component.HomeSettlement.Name);
                message.SetTextVariable(
                    "COOLDOWN_DAYS",
                    DestroyedPartyCooldownDays);
                ModLogger.Notice(message.ToString());
            }
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = GetComponent(party);

                if (component == null)
                {
                    continue;
                }

                if (component.HomeSettlement == settlement)
                {
                    component.SynchronizeOwnership();
                }

                if (component.HomeSettlement == settlement
                    || component.SourceSettlement == settlement)
                {
                    if (component.MissionState
                            == SupplyPartyMissionState.TravelingToSource
                        && !IsValidSource(
                            component.HomeSettlement,
                            component.SourceSettlement))
                    {
                        SourceSelection replacement = FindBestSource(
                            component.HomeSettlement,
                            component.RequestedFood,
                            party);

                        if (replacement != null)
                        {
                            component.ChangeSource(replacement.Settlement);
                            MoveToSettlement(
                                party,
                                replacement.Settlement);
                        }
                        else
                        {
                            component.BeginReturnJourney();
                            MoveToSettlement(
                                party,
                                component.HomeSettlement);
                        }
                    }
                }
            }
        }

        private void OnGameLoadFinished()
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = GetComponent(party);

                if (component == null)
                {
                    continue;
                }

                component.SynchronizeOwnership();
                // This flag is not saveable in every game version. Restore
                // it after loading so native AI cannot compete with the
                // supply mission route controller.
                party.Ai.SetDoNotMakeNewDecisions(true);

                Settlement destination = component.MissionState
                    == SupplyPartyMissionState.TravelingToSource
                        ? component.SourceSettlement
                        : component.HomeSettlement;

                if (destination != null)
                {
                    MoveToSettlement(party, destination);
                }
            }
        }

        private bool TryCreateSupplyParty(
            Settlement home,
            SourceSelection source,
            int requestedFood)
        {
            MobileParty garrison = home.Town.GarrisonParty;
            TroopRoster escortRoster = ExtractEscortRoster(garrison);

            if (escortRoster.TotalHealthyCount < EscortSize)
            {
                RestoreTroops(escortRoster, garrison.MemberRoster);
                SetDispatchCooldown(home, 1f);
                return false;
            }

            MobileParty supplyParty = null;
            PackAnimalPreparationResult packAnimals =
                new PackAnimalPreparationResult();
            int ridingMountsFromGranary = 0;

            try
            {
                supplyParty = SupplyPartyComponent.CreateSupplyParty(
                    home,
                    source.Settlement,
                    requestedFood,
                    escortRoster);

                EnterSettlementAction.ApplyForParty(supplyParty, home);
                packAnimals = PreparePackAnimals(
                    garrison,
                    supplyParty,
                    home.OwnerClan?.Leader);
                ridingMountsFromGranary =
                    TransferRidingMountsFromGranary(
                        garrison,
                        supplyParty);
                TransferOutboundProvisions(garrison, supplyParty);
                MoveToSettlement(supplyParty, source.Settlement);
            }
            catch (Exception exception)
            {
                if (supplyParty != null && supplyParty.IsActive)
                {
                    RestoreTroops(
                        supplyParty.MemberRoster,
                        garrison.MemberRoster);
                    TransferAllItems(
                        supplyParty.ItemRoster,
                        garrison.ItemRoster);
                    GetComponent(supplyParty)?.CompleteMission();
                    DestroyPartyAction.Apply(null, supplyParty);
                }
                else
                {
                    RestoreTroops(
                        escortRoster,
                        garrison.MemberRoster);
                }

                SetDispatchCooldown(home, 1f);
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_creation_failed");
                message.SetTextVariable("HOME_NAME", home.Name);
                message.SetTextVariable("ERROR", exception.Message);
                ModLogger.Error(message.ToString());
                return false;
            }

            if (home.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_dispatched");
                message.SetTextVariable("HOME_NAME", home.Name);
                message.SetTextVariable(
                    "SOURCE_NAME",
                    source.Settlement.Name);
                message.SetTextVariable("ESCORT", EscortSize);
                message.SetTextVariable(
                    "PACK_ANIMALS",
                    supplyParty.ItemRoster.NumberOfPackAnimals);
                message.SetTextVariable(
                    "FROM_GRANARY",
                    packAnimals.FromGranary);
                message.SetTextVariable("PURCHASED", packAnimals.Purchased);
                message.SetTextVariable("ANIMAL_COST", packAnimals.GoldCost);
                message.SetTextVariable(
                    "MOUNTS_FROM_GRANARY",
                    ridingMountsFromGranary);
                message.SetTextVariable("REQUESTED", requestedFood);
                ModLogger.Notice(message.ToString());
            }

            return true;
        }

        private static TroopRoster ExtractEscortRoster(
            MobileParty garrison)
        {
            TroopRoster escort = TroopRoster.CreateDummyTroopRoster();
            int remaining = EscortSize;

            IEnumerable<TroopRosterElement> candidates = garrison
                .MemberRoster
                .GetTroopRoster()
                .Where(element =>
                    !element.Character.IsHero
                    && element.Number - element.WoundedNumber > 0)
                .OrderBy(element => element.Character.Tier);

            foreach (TroopRosterElement element in candidates)
            {
                int healthy = element.Number - element.WoundedNumber;
                int count = Math.Min(healthy, remaining);

                if (count <= 0)
                {
                    continue;
                }

                garrison.MemberRoster.AddToCounts(
                    element.Character,
                    -count,
                    false,
                    0,
                    0,
                    true,
                    -1);
                escort.AddToCounts(
                    element.Character,
                    count,
                    false,
                    0,
                    0,
                    true,
                    -1);
                remaining -= count;

                if (remaining <= 0)
                {
                    break;
                }
            }

            return escort;
        }

        private static void CompleteMission(
            MobileParty party,
            SupplyPartyComponent component)
        {
            MobileParty garrison =
                component.HomeSettlement.Town.GarrisonParty;

            if (garrison == null)
            {
                return;
            }

            int returnedTroops = party.MemberRoster.TotalManCount;
            int returnedFood = party.ItemRoster.TotalFood;
            int returnedPackAnimals =
                party.ItemRoster.NumberOfPackAnimals;
            int returnedMounts = party.ItemRoster.NumberOfMounts;

            RestoreTroops(party.MemberRoster, garrison.MemberRoster);
            TransferAllItems(party.ItemRoster, garrison.ItemRoster);
            component.CompleteMission();

            if (component.HomeSettlement.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_returned");
                message.SetTextVariable(
                    "HOME_NAME",
                    component.HomeSettlement.Name);
                message.SetTextVariable("ESCORT", returnedTroops);
                message.SetTextVariable("FOOD", returnedFood);
                message.SetTextVariable(
                    "PACK_ANIMALS",
                    returnedPackAnimals);
                message.SetTextVariable("MOUNTS", returnedMounts);
                ModLogger.Notice(message.ToString());
            }

            DestroyPartyAction.Apply(null, party);
        }

        private static void RestoreTroops(
            TroopRoster source,
            TroopRoster destination)
        {
            foreach (TroopRosterElement element in
                source.GetTroopRoster())
            {
                destination.AddToCounts(
                    element.Character,
                    element.Number,
                    false,
                    element.WoundedNumber,
                    0,
                    true,
                    -1);
            }
        }

        private static void TransferAllItems(
            ItemRoster source,
            ItemRoster destination)
        {
            for (int index = source.Count - 1; index >= 0; index--)
            {
                ItemRosterElement element = source[index];
                destination.AddToCounts(
                    element.EquipmentElement,
                    element.Amount);
                source.AddToCounts(
                    element.EquipmentElement,
                    -element.Amount);
            }
        }

        private static void TransferOutboundProvisions(
            MobileParty garrison,
            MobileParty supplyParty)
        {
            int provisionTarget = (int)Math.Ceiling(
                Math.Abs(supplyParty.FoodChange)
                * OutboundProvisionDays);
            TransferFood(
                garrison.ItemRoster,
                supplyParty.ItemRoster,
                provisionTarget);
        }

        private static int TransferFood(
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

                if (element.EquipmentElement.Item?.IsFood != true
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

        private SourceSelection FindBestSource(
            Settlement home,
            int requestedFood,
            MobileParty requestingParty)
        {
            SourceSelection best = null;

            foreach (Settlement candidate in Settlement.All)
            {
                SupplyPartyComponent requestingComponent =
                    GetComponent(requestingParty);

                if (!IsValidSource(home, candidate)
                    || requestingComponent?.HasVisited(candidate) == true)
                {
                    continue;
                }

                int availableFood = CalculateAvailableFood(candidate)
                    - CalculateReservedFood(candidate, requestingParty);

                if (availableFood <= 0)
                {
                    continue;
                }

                float distance = Campaign.Current.Models.MapDistanceModel
                    .GetDistance(
                        home,
                        candidate,
                        false,
                        false,
                        MobileParty.NavigationType.Default);

                if (distance <= 0f || float.IsNaN(distance))
                {
                    continue;
                }

                if (distance > MaximumSourceDistance)
                {
                    continue;
                }

                int batchSize = candidate.IsVillage
                    ? VillagePurchaseBatchSize
                    : TownPurchaseBatchSize;
                int request = Math.Min(
                    Math.Min(
                        Math.Min(availableFood, requestedFood),
                        MaximumRequestedFood),
                    batchSize);
                int priority = candidate.IsVillage ? 2 : 1;
                float score = request / distance;

                if (best == null
                    || priority > best.Priority
                    || priority == best.Priority && score > best.Score)
                {
                    best = new SourceSelection(
                        candidate,
                        request,
                        priority,
                        score);
                }
            }

            return best;
        }

        private static bool IsValidSource(
            Settlement home,
            Settlement candidate)
        {
            if (home == null
                || candidate == null
                || candidate == home
                || (!candidate.IsTown
                    && !IsFoodProducingVillage(candidate))
                || candidate.IsUnderSiege
                || GetSourceOwnerClan(candidate) == null
                || home.OwnerClan == null
                || home.MapFaction == null
                || candidate.MapFaction == null
                || FactionManager.IsAtWarAgainstFaction(
                    home.MapFaction,
                    candidate.MapFaction))
            {
                return false;
            }

            return true;
        }

        private static int CalculateAvailableFood(Settlement source)
        {
            if (source.IsTown)
            {
                return Math.Max(
                    0,
                    (source.ItemRoster?.TotalFood ?? 0)
                        - MinimumTownMarketFoodReserve);
            }

            if (IsFoodProducingVillage(source))
            {
                return source.ItemRoster?.TotalFood ?? 0;
            }

            return 0;
        }

        private static bool IsFoodProducingVillage(Settlement settlement)
        {
            return settlement?.IsVillage == true
                && settlement.Village?.VillageType?.PrimaryProduction?.IsFood
                    == true;
        }

        private static Clan GetSourceOwnerClan(Settlement settlement)
        {
            return settlement?.OwnerClan
                ?? settlement?.Village?.Bound?.OwnerClan;
        }

        private static int CalculateReservedFood(
            Settlement source,
            MobileParty requestingParty)
        {
            int reserved = 0;

            foreach (MobileParty party in MobileParty.All)
            {
                if (party == requestingParty)
                {
                    continue;
                }

                SupplyPartyComponent component = GetComponent(party);

                if (component?.MissionState
                        == SupplyPartyMissionState.TravelingToSource
                    && component.SourceSettlement == source)
                {
                    reserved += Math.Min(
                        component.RequestedFood,
                        CalculateAvailableFood(source));
                }
            }

            return reserved;
        }

        private static bool CanDispatchFrom(Settlement settlement)
        {
            return settlement?.IsFortification == true
                && !settlement.IsUnderSiege
                && settlement.OwnerClan != null
                && settlement.Town?.GarrisonParty != null;
        }

        private static bool ShouldDispatchAfterLocalSupply(
            Settlement settlement,
            GarrisonLogisticsStatus status)
        {
            if (settlement.IsCastle
                || status.FoodSupplyDays < EmergencySupplyDays)
            {
                return true;
            }

            int localMarketFood =
                settlement.ItemRoster?.TotalFood ?? 0;
            int fiveDayDemand = (int)Math.Ceiling(
                status.DailyConsumption * 5f);
            Hero payer = settlement.OwnerClan?.Leader;

            return localMarketFood < fiveDayDemand
                || payer == null
                || payer.Gold <= 0;
        }

        private static MobileParty FindSupplyPartyForHome(
            Settlement home)
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = GetComponent(party);

                if (component?.HomeSettlement == home
                    && component.MissionState
                        != SupplyPartyMissionState.Completed)
                {
                    return party;
                }
            }

            return null;
        }

        private bool IsDispatchCoolingDown(Settlement home)
        {
            if (!_nextDispatchTimes.TryGetValue(
                home,
                out CampaignTime nextTime))
            {
                return false;
            }

            if (nextTime.IsPast)
            {
                _nextDispatchTimes.Remove(home);
                return false;
            }

            return true;
        }

        private void SetDispatchCooldown(
            Settlement home,
            float days)
        {
            if (home != null)
            {
                _nextDispatchTimes[home] =
                    CampaignTime.Now + CampaignTime.Days(days);
            }
        }

        private static void EnsureMovingTo(
            MobileParty party,
            Settlement destination)
        {
            if (destination == null
                || party.CurrentSettlement == destination
                || (party.DefaultBehavior == AiBehavior.GoToSettlement
                    && party.TargetSettlement == destination))
            {
                return;
            }

            MoveToSettlement(party, destination);
        }

        private static void MoveToSettlement(
            MobileParty party,
            Settlement destination)
        {
            if (party == null || destination == null)
            {
                return;
            }

            AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(
                party,
                destination,
                false,
                out MobileParty.NavigationType navigationType,
                out _,
                out bool isFromPort);

            SetPartyAiAction.GetActionForVisitingSettlement(
                party,
                destination,
                navigationType,
                isFromPort,
                false);
        }

        private static SupplyPartyComponent GetComponent(
            MobileParty party)
        {
            return party?.PartyComponent as SupplyPartyComponent;
        }

        private sealed class SourceSelection
        {
            public SourceSelection(
                Settlement settlement,
                int requestedFood,
                int priority,
                float score)
            {
                Settlement = settlement;
                RequestedFood = requestedFood;
                Priority = priority;
                Score = score;
            }

            public Settlement Settlement { get; }

            public int RequestedFood { get; }

            public int Priority { get; }

            public float Score { get; }
        }
    }
}
