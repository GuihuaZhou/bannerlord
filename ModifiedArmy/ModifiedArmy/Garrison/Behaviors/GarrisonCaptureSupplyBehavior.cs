using ModifiedArmy.Garrison.Models;
using ModifiedArmy.ForceManagement.Missions;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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
        private const int InitialTownGarrison = 125;
        private const int InitialCastleGarrison = 75;
        private readonly PartyVisitExecutionService _captureTransfers =
            new PartyVisitExecutionService();

        private Dictionary<Settlement, MobileParty> _pendingCaptures =
            new Dictionary<Settlement, MobileParty>();
        private readonly Dictionary<Settlement, string>
            _lastCaptureDiagnosticStates = new();

        public override void RegisterEvents()
        {
            CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(
                this,
                OnAfterSiegeCompleted);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(
                this,
                OnSettlementEntered);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(
                this,
                OnHourlyPartyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("_modifiedArmyPendingCaptureDonations",
                ref _pendingCaptures);
            _pendingCaptures ??= new Dictionary<Settlement, MobileParty>();
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
                attackerParty?.Army == null)
            {
                return;
            }

            MobileParty leader = attackerParty.Army.LeaderParty;
            _pendingCaptures[settlement] = leader;
            LogCaptureDiagnostic(leader, settlement, "Registered", 0, 0);
            int food = DonateArmyFood(attackerParty, settlement);
            int troops = TryDonateArmyTroops(leader, settlement);
            LogCaptureDiagnostic(leader, settlement, "AfterSiegeCompleted",
                troops, food);
            LogCaptureContribution(leader, settlement, troops, food);
            CompletePendingCaptureIfEstablished(settlement);
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            if (_pendingCaptures.TryGetValue(
                    settlement,
                    out MobileParty capturingArmyLeader) &&
                IsCapturingArmyLeader(party, capturingArmyLeader))
            {
                int food = DonateArmyFood(party, settlement);
                int troops = TryDonateArmyTroops(party, settlement);
                LogCaptureDiagnostic(party, settlement, "SettlementEntered",
                    troops, food);
                LogCaptureContribution(party, settlement, troops, food);
                CompletePendingCaptureIfEstablished(settlement);
            }

            if (CanDonateToSettlement(party, settlement))
                DonateVisitingPartyFood(party, settlement);
        }

        private void OnHourlyPartyTick(MobileParty party)
        {
            if (party?.IsActive != true || party.MapEvent != null ||
                _pendingCaptures.Count == 0)
                return;
            foreach (var pending in _pendingCaptures.ToList())
            {
                if (pending.Value != party) continue;
                Settlement settlement = pending.Key;
                if (!IsCaptureStillValid(party, settlement))
                {
                    LogCaptureDiagnostic(party, settlement,
                        "CaptureNoLongerValid", 0, 0);
                    _pendingCaptures.Remove(settlement);
                    _lastCaptureDiagnosticStates.Remove(settlement);
                    continue;
                }
                if (!CanRetryCaptureDonation(party, settlement))
                {
                    LogCaptureDiagnostic(party, settlement,
                        "WaitingForSettlementReady", 0, 0);
                    continue;
                }
                int troops = TryDonateArmyTroops(party, settlement);
                int food = DonateArmyFood(party, settlement);
                LogCaptureDiagnostic(party, settlement, "HourlyRetry",
                    troops, food);
                LogCaptureContribution(party, settlement, troops, food);
                CompletePendingCaptureIfEstablished(settlement);
            }
        }

        /// <summary>
        /// Purely checks whether the capturing army leader still owns the
        /// one-shot post-conquest donation permit. Harmony prefixes may query
        /// this more than once, so a query must never consume the permit.
        /// </summary>
        public bool CanDonateTroopsAfterCapture(
            MobileParty party,
            Settlement settlement)
        {
            if (party == null || settlement == null ||
                !_pendingCaptures.TryGetValue(
                    settlement,
                    out MobileParty capturingArmyLeader))
            {
                return false;
            }

            if (!party.IsActive || !party.IsLordParty ||
                !settlement.IsFortification ||
                !IsSameKingdom(party, settlement) ||
                !IsCapturingArmyLeader(party, capturingArmyLeader))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Consumes the capture permit immediately before the unified exchange
        /// path attempts the donation. A failed approval still consumes it so
        /// repeated settlement events cannot create a second transfer.
        /// </summary>
        public bool TryConsumeTroopDonationPermit(
            MobileParty party,
            Settlement settlement)
        {
            if (!CanDonateTroopsAfterCapture(party, settlement))
            {
                return false;
            }

            return _pendingCaptures.Remove(settlement);
        }

        private static bool IsCapturingArmyLeader(
            MobileParty party,
            MobileParty capturingArmyLeader)
        {
            return party != null && party == capturingArmyLeader &&
                party.Army?.LeaderParty == party;
        }

        private static int DonateArmyFood(
            MobileParty armyParty,
            Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (armyParty?.Army == null || garrison == null ||
                !IsSameKingdom(armyParty, settlement))
            {
                return 0;
            }

            int targetFood = CalculateInitialTargetFood(settlement, garrison);
            int remaining = Math.Max(
                0,
                targetFood - (garrison.ItemRoster?.TotalFood ?? 0));
            if (remaining <= 0)
            {
                return 0;
            }

            return DonateFood(
                armyParty.Army.Parties,
                settlement,
                remaining,
                true);
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
            DonateFood(donors, settlement, status.FoodDeficit, false);
        }

        private static int DonateFood(
            IEnumerable<MobileParty> donors,
            Settlement settlement,
            int requestedFood,
            bool allowMainParty)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (garrison == null || requestedFood <= 0)
            {
                return 0;
            }

            int remaining = requestedFood;
            foreach (MobileParty donor in donors
                .Where(p => CanDonateToSettlement(p, settlement,
                    allowMainParty))
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
            return requestedFood - remaining;
        }

        private int TryDonateArmyTroops(MobileParty leader,
            Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (!CanRetryCaptureDonation(leader, settlement) ||
                garrison == null) return 0;
            int target = settlement.IsTown
                ? InitialTownGarrison : InitialCastleGarrison;
            int remaining = Math.Max(0,
                target - garrison.MemberRoster.TotalRegulars);
            int transferred = 0;
            foreach (MobileParty donor in leader.Army.Parties
                .Where(x => x?.IsActive == true && x.IsLordParty &&
                    !x.IsDisbanding &&
                    x.CurrentSettlement == settlement &&
                    IsSameKingdom(x, settlement))
                .OrderByDescending(x => GarrisonTransferPolicyModel
                    .GetAllowedCaptureContributionCount(x, int.MaxValue))
                .ToList())
            {
                if (remaining <= 0) break;
                int surplus = GarrisonTransferPolicyModel
                    .GetAllowedCaptureContributionCount(donor, remaining);
                if (surplus <= 0) continue;
                int moved = _captureTransfers.ExecuteCaptureContribution(
                    donor, settlement, Math.Min(remaining, surplus))
                    .TransferredCount;
                transferred += moved;
                remaining -= moved;
            }
            return transferred;
        }

        private static bool CanRetryCaptureDonation(MobileParty leader,
            Settlement settlement) => IsCaptureStillValid(leader, settlement) &&
            settlement.Town?.GarrisonParty != null &&
            !settlement.IsUnderSiege;

        private static bool IsCaptureStillValid(MobileParty leader,
            Settlement settlement) => leader?.IsActive == true &&
            leader.Army?.LeaderParty == leader &&
            settlement?.IsFortification == true &&
            IsSameKingdom(leader, settlement);

        private void CompletePendingCaptureIfEstablished(Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (garrison == null) return;
            int target = settlement.IsTown
                ? InitialTownGarrison : InitialCastleGarrison;
            int targetFood = CalculateInitialTargetFood(settlement, garrison);
            if (garrison.MemberRoster.TotalRegulars >= target &&
                garrison.ItemRoster.TotalFood >= targetFood)
            {
                _pendingCaptures.Remove(settlement);
                _lastCaptureDiagnosticStates.Remove(settlement);
            }
        }

        private void LogCaptureDiagnostic(MobileParty leader,
            Settlement settlement, string phase, int troops, int food)
        {
            if (!RecruitmentLogFilter.ShouldLog(leader)) return;
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            List<MobileParty> armyParties = leader?.Army?.Parties?
                .Where(x => x?.IsActive == true && x.IsLordParty &&
                    !x.IsDisbanding)
                .ToList() ?? new List<MobileParty>();
            int inside = armyParties.Count(x =>
                x.CurrentSettlement == settlement);
            int contributors = armyParties.Count(x =>
                x.CurrentSettlement == settlement &&
                IsSameKingdom(x, settlement) &&
                GarrisonTransferPolicyModel
                    .GetAllowedCaptureContributionCount(x, int.MaxValue) > 0);
            int available = armyParties
                .Where(x => x.CurrentSettlement == settlement &&
                    IsSameKingdom(x, settlement))
                .Sum(x => GarrisonTransferPolicyModel
                    .GetAllowedCaptureContributionCount(x, int.MaxValue));
            int capacity = GarrisonRecruitmentCapacity
                .GetTransferFreeSlots(garrison);
            string reason = ResolveCaptureDiagnosticReason(
                leader, settlement, garrison, inside, available, capacity,
                troops, food);
            string state = string.Join("|", phase, reason,
                garrison?.MemberRoster.TotalRegulars ?? -1,
                inside, contributors, available, capacity, troops, food);
            if (_lastCaptureDiagnosticStates.TryGetValue(
                    settlement, out string previous) && previous == state)
                return;
            _lastCaptureDiagnosticStates[settlement] = state;

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_capture_garrison_diagnostic");
            message.SetTextVariable("SETTLEMENT_NAME",
                settlement?.Name ?? GameTexts.FindText(
                    "str_modifiedarmy_common_unknown"));
            message.SetTextVariable("PARTY_NAME",
                PartyLogFormatter.GetDisplayName(leader));
            message.SetTextVariable("CLAN_NAME",
                PartyLogFormatter.GetClanDisplayName(leader?.ActualClan));
            message.SetTextVariable("PHASE", phase);
            message.SetTextVariable("REASON", reason);
            message.SetTextVariable("GARRISON",
                garrison?.MemberRoster.TotalRegulars ?? -1);
            message.SetTextVariable("ARMY_PARTIES", armyParties.Count);
            message.SetTextVariable("INSIDE", inside);
            message.SetTextVariable("CONTRIBUTORS", contributors);
            message.SetTextVariable("AVAILABLE", available);
            message.SetTextVariable("CAPACITY", capacity);
            message.SetTextVariable("TROOPS", Math.Max(0, troops));
            message.SetTextVariable("FOOD", Math.Max(0, food));
            ModLogger.Info(message.ToString());
        }

        private static string ResolveCaptureDiagnosticReason(
            MobileParty leader, Settlement settlement, MobileParty garrison,
            int inside, int available, int capacity, int troops, int food)
        {
            if (leader?.IsActive != true) return "LeaderInactive";
            if (leader.Army?.LeaderParty != leader)
                return "CapturingArmyNoLongerExists";
            if (!IsSameKingdom(leader, settlement))
                return "SettlementOwnershipChanged";
            if (garrison == null) return "GarrisonNotCreatedYet";
            if (settlement.IsUnderSiege) return "SiegeStateNotClearedYet";
            if (troops > 0 || food > 0) return "ContributionCommitted";
            if (inside <= 0) return "ArmyPartiesNotInsideSettlement";
            if (available <= 0) return "NoTroopsAboveThirtyMemberFloor";
            if (capacity <= 0) return "GarrisonHasNoTransferCapacity";
            return "TransferProducedNoCommit";
        }

        private static void LogCaptureContribution(MobileParty leader,
            Settlement settlement, int troops, int food)
        {
            if ((troops <= 0 && food <= 0) ||
                !RecruitmentLogFilter.ShouldLog(leader)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_capture_garrison_contribution");
            message.SetTextVariable("PARTY_NAME",
                PartyLogFormatter.GetDisplayName(leader));
            message.SetTextVariable("CLAN_NAME",
                PartyLogFormatter.GetClanDisplayName(leader.ActualClan));
            message.SetTextVariable("SETTLEMENT_NAME",
                settlement?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("TROOPS", Math.Max(0, troops));
            message.SetTextVariable("FOOD", Math.Max(0, food));
            message.SetTextVariable("GARRISON",
                settlement?.Town?.GarrisonParty?.MemberRoster.TotalRegulars ?? 0);
            message.SetTextVariable("GRANARY",
                settlement?.Town?.GarrisonParty?.ItemRoster.TotalFood ?? 0);
            ModLogger.Notice(message.ToString());
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
            Settlement settlement,
            bool allowMainParty = false)
        {
            return party != null && party.IsActive && party.IsLordParty &&
                (allowMainParty || !party.IsMainParty) &&
                settlement?.IsFortification == true &&
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
