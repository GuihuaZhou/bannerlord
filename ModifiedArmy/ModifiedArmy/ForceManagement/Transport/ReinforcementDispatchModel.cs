using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Transport
{
    public sealed class ReinforcementDispatchPlan
    {
        public Settlement Source { get; set; }
        public Settlement Destination { get; set; }
        public Clan Clan { get; set; }
        public int TroopCount { get; set; }
        public int FoodCount { get; set; }
        public string ReasonCode { get; set; }
        public bool IsApproved => TroopCount >= ReinforcementDispatchModel
            .MinimumPartySize;
    }

    public sealed class ReinforcementDispatchModel
    {
        public const int MaximumPartySize = 50;
        public const int MinimumPartySize = 10;

        public ReinforcementDispatchPlan BuildControlledPlan(
            Settlement source,
            Settlement destination,
            int requestedTroops,
            int requestedFood = 0)
        {
            ReinforcementDispatchPlan plan = new ReinforcementDispatchPlan
            {
                Source = source,
                Destination = destination,
                Clan = destination?.OwnerClan,
                FoodCount = Math.Max(0, requestedFood)
            };
            if (!IsSameClanRoute(source, destination))
            {
                plan.ReasonCode = "InvalidSameClanRoute";
                return plan;
            }
            ReinforcementNeedResult need = GetNeed(destination);
            if (!need.IsRequested)
            {
                plan.ReasonCode = need.ReasonCode;
                return plan;
            }
            int inbound = GetInboundTroopCommitment(
                destination, destination.OwnerClan);
            int remainingNeed = Math.Max(0,
                Math.Min(need.RequestedTroops,
                    GarrisonRecruitmentCapacity.GetTransferFreeSlots(
                        destination.Town.GarrisonParty)) - inbound);
            int safe = GetSafeOrdinaryHealthyCount(source);
            plan.TroopCount = Math.Min(MaximumPartySize,
                Math.Min(Math.Max(0, requestedTroops),
                    Math.Min(remainingNeed, safe)));
            plan.ReasonCode = plan.IsApproved
                ? "Approved" : "InsufficientSafeTroops";
            return plan;
        }

        public ReinforcementDispatchPlan FindBestSource(
            Settlement destination,
            int requestedTroops,
            int requestedFood = 0)
        {
            if (destination?.OwnerClan == null)
                return new ReinforcementDispatchPlan
                    { ReasonCode = "InvalidDestination" };
            return Settlement.All
                .Where(source => IsSameClanRoute(source, destination))
                .Select(source => BuildControlledPlan(source, destination,
                    requestedTroops, requestedFood))
                .Where(plan => plan.IsApproved)
                .OrderByDescending(plan => plan.TroopCount)
                .ThenBy(plan => plan.Source.GetPosition2D.DistanceSquared(
                    destination.GetPosition2D))
                .FirstOrDefault() ?? new ReinforcementDispatchPlan
                    { Destination = destination,
                      Clan = destination.OwnerClan,
                      ReasonCode = "NoEligibleSource" };
        }

        public int GetSafeOrdinaryHealthyCount(Settlement source)
        {
            MobileParty garrison = source?.Town?.GarrisonParty;
            if (garrison == null) return 0;
            GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                .GarrisonDefense.Evaluate(source);
            int safe = Math.Max(0,
                defense.ResidentCount - defense.MinimumCount);
            Dictionary<CharacterObject, int> protectedCounts =
                Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                    .GetServingTroopCounts(garrison) ??
                    new Dictionary<CharacterObject, int>();
            int eligible = garrison.MemberRoster.GetTroopRoster()
                .Where(x => x.Character != null && !x.Character.IsHero)
                .Sum(x => Math.Max(0, x.Number - x.WoundedNumber -
                    (protectedCounts.TryGetValue(x.Character, out int count)
                        ? count : 0)));
            return Math.Min(safe, eligible);
        }

        public ReinforcementNeedResult GetNeed(Settlement destination) =>
            ForceManagementFacade.Current.ReinforcementNeed.Evaluate(
                ForceManagementFacade.Current.GarrisonDefense
                    .Evaluate(destination));

        public int GetInboundTroopCommitment(
            Settlement destination,
            Clan dispatchClan)
        {
            if (destination == null || dispatchClan == null) return 0;
            return MobileParty.All.Where(party => party?.IsActive == true &&
                    party.PartyComponent is ReinforcementPartyComponent c &&
                    c.DispatchClan == dispatchClan &&
                    c.Destination == destination &&
                    (c.State == ReinforcementTransportState
                        .TravelingToDestination ||
                     c.State == ReinforcementTransportState.Unloading))
                .Sum(party => Math.Max(0,
                    party.MemberRoster.TotalManCount));
        }

        public Settlement FindRecoverySettlement(
            MobileParty party,
            ReinforcementPartyComponent component)
        {
            if (party == null || component?.DispatchClan == null) return null;
            bool needsTroopCapacity = party.MemberRoster.TotalManCount > 0;
            return Settlement.All.Where(settlement =>
                    settlement?.OwnerClan == component.DispatchClan &&
                    settlement.Town?.GarrisonParty != null &&
                    !settlement.IsUnderSiege &&
                    (!needsTroopCapacity ||
                     GarrisonRecruitmentCapacity.GetHardFreeSlots(
                         settlement.Town.GarrisonParty) > 0))
                .OrderBy(settlement => party.Position.DistanceSquared(
                    settlement.GatePosition))
                .FirstOrDefault();
        }

        private static bool IsSameClanRoute(
            Settlement source,
            Settlement destination) =>
            source?.Town?.GarrisonParty != null &&
            destination?.Town?.GarrisonParty != null &&
            source != destination && source.OwnerClan != null &&
            source.OwnerClan == destination.OwnerClan &&
            !source.IsUnderSiege && !destination.IsUnderSiege;
    }
}
