using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Supply.Services
{
    /// <summary>Restores saved supply component ownership and mission routes.</summary>
    public sealed class SupplyRecoveryService
    {
        public void RestoreLoadedMissions(
            Action<MobileParty, SupplyPartyComponent> advanceAid,
            Action<MobileParty, Settlement> restoreRoute)
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = party?.PartyComponent as
                    SupplyPartyComponent;
                if (component == null) continue;
                component.SynchronizeOwnership();
                party.Ai.SetDoNotMakeNewDecisions(false);
                if (party.MapEvent != null ||
                    TransportThreatModel.IsNativeAvoidanceActive(party))
                    continue;
                if (component.IsAidMission)
                {
                    advanceAid(party, component);
                    continue;
                }
                Settlement destination = component.MissionState ==
                        SupplyPartyMissionState.TravelingToSource
                    ? component.SourceSettlement
                    : component.HomeSettlement;
                if (destination != null)
                    restoreRoute(party, destination);
            }
        }

        public void CompleteReturn(
            MobileParty party,
            SupplyPartyComponent component,
            SupplyCargoService cargo,
            Action<Settlement, MobileParty, int, int, int, int, int>
                logCapacity)
        {
            MobileParty garrison = component?.HomeSettlement?.Town
                ?.GarrisonParty;
            if (party == null || garrison == null) return;
            int returnedTroops = party.MemberRoster.TotalManCount;
            int returnedFood = party.ItemRoster.TotalFood;
            int returnedPackAnimals = party.ItemRoster.NumberOfPackAnimals;
            int returnedMounts = party.ItemRoster.NumberOfMounts;
            int garrisonBefore = garrison.Party.NumberOfAllMembers;
            int reservedBefore = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(component.HomeSettlement);
            int partySizeLimit = garrison.Party.PartySizeLimit;
            int missionState = component.MissionStateValue;

            component.CompleteMission();
            cargo.RestoreTroops(party.MemberRoster, garrison.MemberRoster);
            cargo.TransferAllItems(party.ItemRoster, garrison.ItemRoster);
            logCapacity?.Invoke(component.HomeSettlement, party,
                garrisonBefore, returnedTroops, reservedBefore,
                partySizeLimit, missionState);

            if (component.HomeSettlement.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_returned");
                message.SetTextVariable(
                    "HOME_NAME", component.HomeSettlement.Name);
                message.SetTextVariable("ESCORT", returnedTroops);
                message.SetTextVariable("FOOD", returnedFood);
                message.SetTextVariable("PACK_ANIMALS", returnedPackAnimals);
                message.SetTextVariable("MOUNTS", returnedMounts);
                ModLogger.Notice(message.ToString());
            }
            DestroyPartyAction.Apply(null, party);
        }

        public void HandleDestroyedMission(
            SupplyPartyComponent component,
            int cooldownDays,
            Action<Settlement, float> setCooldown)
        {
            if (component == null || component.MissionState ==
                    SupplyPartyMissionState.Completed)
                return;
            setCooldown(component.HomeSettlement, cooldownDays);
            if (component.HomeSettlement?.OwnerClan != Clan.PlayerClan)
                return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_supply_party_destroyed");
            message.SetTextVariable(
                "HOME_NAME", component.HomeSettlement.Name);
            message.SetTextVariable("COOLDOWN_DAYS", cooldownDays);
            ModLogger.Notice(message.ToString());
        }
    }
}
