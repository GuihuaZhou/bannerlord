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
using TaleWorlds.CampaignSystem.Roster;

namespace ModifiedArmy.Garrison.Supply.Services
{
    /// <summary>Restores saved supply component ownership and mission routes.</summary>
    public sealed class SupplyRecoveryService
    {
        public void RestoreLoadedMissions(
            Action<MobileParty, SupplyPartyComponent> advanceAid,
            Action<MobileParty, SupplyPartyComponent> reconcileLocal)
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = party?.PartyComponent as
                    SupplyPartyComponent;
                if (component == null) continue;
                component.RestoreLegacyDispatchClan(party.ActualClan);
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
                reconcileLocal(party, component);
            }
        }

        public void CompleteReturn(
            MobileParty party,
            SupplyPartyComponent component,
            SupplyCargoService cargo,
            Action<Settlement, MobileParty, int, int, int, int, int>
                logCapacity)
        {
            CompleteReturn(party, component, component?.HomeSettlement,
                cargo, logCapacity);
        }

        public void CompleteReturn(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement receiver,
            SupplyCargoService cargo,
            Action<Settlement, MobileParty, int, int, int, int, int>
                logCapacity)
        {
            MobileParty garrison = receiver?.Town?.GarrisonParty;
            if (party == null || garrison == null) return;
            component.RestoreLegacyDispatchClan(party.ActualClan);
            if (receiver.OwnerClan != component.OriginalDispatchClan ||
                party.CurrentSettlement != receiver)
            {
                component.RequireRecovery("InvalidReturnReceiver", receiver);
                return;
            }
            int returnedTroops = party.MemberRoster.TotalManCount;
            int returnedFood = party.ItemRoster.TotalFood;
            int returnedPackAnimals = party.ItemRoster.NumberOfPackAnimals;
            int returnedMounts = party.ItemRoster.NumberOfMounts;
            int garrisonBefore = garrison.Party.NumberOfAllMembers;
            int reservedBefore = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(receiver);
            int partySizeLimit = garrison.Party.PartySizeLimit;
            int missionState = component.MissionStateValue;

            cargo.TransferTroopsLimited(party.MemberRoster,
                garrison.MemberRoster, returnedTroops);
            cargo.TransferAllItems(party.ItemRoster, garrison.ItemRoster);
            if (party.MemberRoster.TotalManCount > 0 ||
                party.ItemRoster.Count > 0)
            {
                component.RequireRecovery("ReturnTransferIncomplete", receiver);
                return;
            }
            component.ClearRecovery();
            component.CompleteMission();
            logCapacity?.Invoke(receiver, party,
                garrisonBefore, returnedTroops, reservedBefore,
                partySizeLimit, missionState);

            if (receiver.OwnerClan == Clan.PlayerClan)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_supply_party_returned");
                message.SetTextVariable(
                    "HOME_NAME", receiver.Name);
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

        public void CompleteAidReturn(
            MobileParty party,
            SupplyPartyComponent component,
            Settlement settlement,
            SupplyCargoService cargo,
            Func<Settlement, SupplyPartyComponent, bool> isValidRecovery,
            Func<TroopRoster, TroopRoster, int, int> transferTroops,
            Action<string, MobileParty, int> logAid)
        {
            if (!isValidRecovery(settlement, component))
            {
                component.BeginRecovery(null);
                return;
            }
            MobileParty garrison = settlement.Town.GarrisonParty;
            int reservedEscort = party.MemberRoster.TotalManCount;
            int moved = transferTroops(party.MemberRoster,
                garrison.MemberRoster,
                GarrisonRecruitmentCapacity.GetHardFreeSlots(garrison) +
                    reservedEscort);
            if (party.MemberRoster.TotalManCount > 0)
            {
                component.BeginRecovery(null);
                logAid?.Invoke("RecoveryPartial", party, moved);
                return;
            }
            cargo.TransferAllItems(party.ItemRoster, garrison.ItemRoster);
            component.CompleteMission();
            logAid?.Invoke("Completed", party, moved);
            DestroyPartyAction.Apply(null, party);
        }
    }
}
