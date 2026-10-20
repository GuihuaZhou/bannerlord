using ModifiedArmy.common;
using ModifiedArmy.ForceManagement.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Provides the mission lifecycle and Army protection boundary without
    /// enabling automatic dispatch. Persistent transport execution is added
    /// only after route and Army-creation protections have been verified.
    /// </summary>
    public sealed class ReinforcementMissionService
        : IArmyMissionProtectionModel
    {
        private readonly Dictionary<MobileParty, ReinforcementMission>
            _missions = new Dictionary<MobileParty, ReinforcementMission>();

        public bool IsProtected(MobileParty party)
        {
            return false;
        }

        public bool MayStartReinforcement(MobileParty party)
        {
            return false;
        }

        public bool TryRegister(
            ReinforcementMission mission,
            bool controlledTest = false)
        {
            return false;
        }

        public ReinforcementMission GetMission(MobileParty party)
        {
            _missions.TryGetValue(party, out ReinforcementMission mission);
            return mission;
        }

        public IReadOnlyCollection<ReinforcementMission> GetActiveMissions() =>
            _missions.Values.Where(mission => mission?.IsActive == true)
                .ToList();

        public void RefreshSources(ReinforcementMission mission)
        {
            Clan clan = mission?.Carrier?.ActualClan;
            if (clan == null) return;

            if (mission.RequestedTroopCount <= 0)
            {
                mission.TroopSource = null;
            }
            else if (!IsValidSource(mission.TroopSource, mission, clan) ||
                mission.TroopSource.Town?.GarrisonParty == null ||
                ForceManagementFacade.Current.GarrisonDefense
                    .GetSafeTransferOutCount(
                        ForceManagementFacade.Current.GarrisonDefense
                            .Evaluate(mission.TroopSource),
                        mission.TroopSource.Town.GarrisonParty
                            .MemberRoster) <= 0)
            {
                mission.TroopSource = Settlement.All.FirstOrDefault(
                    settlement => IsValidSource(settlement, mission, clan) &&
                        settlement.Town?.GarrisonParty != null &&
                        ForceManagementFacade.Current.GarrisonDefense
                            .GetSafeTransferOutCount(
                                ForceManagementFacade.Current
                                    .GarrisonDefense.Evaluate(settlement),
                                settlement.Town.GarrisonParty
                                    .MemberRoster) > 0);
            }

            if (mission.RequestedFoodCount <= 0)
            {
                mission.FoodSource = null;
            }
            else if (!IsValidSource(mission.FoodSource, mission, clan) ||
                !HasFood(mission.FoodSource))
            {
                mission.FoodSource = Settlement.All.FirstOrDefault(
                    settlement => IsValidSource(settlement, mission, clan) &&
                        HasFood(settlement));
            }
        }

        public int GetInboundTroopCommitment(Settlement destination)
        {
            return _missions.Values
                .Where(mission => mission?.IsActive == true &&
                    mission.Destination == destination)
                .Sum(mission => Math.Max(0,
                    mission.RequestedTroopCount));
        }

        public void Restore(IEnumerable<ReinforcementMission> missions)
        {
            _missions.Clear();
            foreach (ReinforcementMission mission in missions ??
                Enumerable.Empty<ReinforcementMission>())
            {
                if (IsValidMission(mission) &&
                    IsEligibleCarrier(mission.Carrier))
                {
                    _missions[mission.Carrier] = mission;
                }
            }
        }

        public void Complete(MobileParty party)
        {
            if (party != null && _missions.TryGetValue(
                party,
                out ReinforcementMission mission))
            {
                mission.State = ReinforcementMissionState.Completed;
                _missions.Remove(party);
            }
        }

        public void Cancel(
            MobileParty party,
            ReinforcementCancelReason reason)
        {
            if (party != null && _missions.TryGetValue(
                party,
                out ReinforcementMission mission))
            {
                mission.State = ReinforcementMissionState.Cancelled;
                _missions.Remove(party);
            }
        }

        private bool IsEligibleCarrier(MobileParty party)
        {
            return party != null && party.IsActive && party.IsLordParty &&
                party.ActualClan != null && party.Army == null &&
                party.MapEvent == null && !IsProtected(party);
        }

        private static bool IsValidMission(ReinforcementMission mission)
        {
            return mission?.Carrier != null && mission.Destination?.Town != null &&
                mission.IsActive && mission.Carrier.ActualClan != null &&
                mission.Destination.OwnerClan == mission.Carrier.ActualClan;
        }

        private static bool IsValidSource(
            Settlement settlement,
            ReinforcementMission mission,
            Clan clan) =>
            settlement != null && settlement != mission.Destination &&
            settlement.OwnerClan == clan && !settlement.IsUnderSiege;

        private static bool HasFood(Settlement settlement) =>
            settlement?.ItemRoster != null && settlement.ItemRoster.Any(
                item => item.EquipmentElement.Item?.IsFood == true &&
                    item.Amount > 0);
    }
}
