using Helpers;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Behaviors;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Services
{
    public sealed class GarrisonDonationTransferState
    {
        internal MobileParty Source { get; set; }
        internal MobileParty Target { get; set; }
        internal Dictionary<CharacterObject, int> SourceBefore { get; set; }
        internal Dictionary<CharacterObject, int> TargetBefore { get; set; }
    }

    public static class GarrisonDonationAdapter
    {
        public static bool ShouldRunNativeEntry(
            MobileParty party, Settlement settlement)
        {
            if (Campaign.Current?.GameStarted != true) return false;
            if (ManagedPartyPolicy.IsModManagedAiParty(party)) return false;
            if (party != null && party.IsLordParty && !party.IsDisbanding &&
                party.LeaderHero != null &&
                settlement?.IsFortification == true &&
                DiplomacyHelper.IsSameFactionAndNotEliminated(
                    party.MapFaction, settlement.MapFaction))
            {
                bool owns = party.Owner?.Clan == settlement.OwnerClan;
                bool captures = Campaign.Current.GetCampaignBehavior<
                    GarrisonCaptureSupplyBehavior>()?
                    .CanDonateTroopsAfterCapture(party, settlement) == true;
                if (!owns && !captures) return false;
            }
            return true;
        }

        public static bool PrepareDonation(MobileParty party,
            ref int requested, out GarrisonDonationTransferState state)
        {
            if (ManagedPartyPolicy.IsModManagedAiParty(party))
            {
                state = null;
                requested = 0;
                return false;
            }
            MobileParty garrison = party?.CurrentSettlement?.Town?.GarrisonParty;
            state = new GarrisonDonationTransferState
            {
                Source = party,
                Target = garrison,
                SourceBefore = Snapshot(party),
                TargetBefore = Snapshot(garrison)
            };
            Settlement settlement = party?.CurrentSettlement;
            if (garrison == null || settlement == null) return true;
            Campaign.Current?.GetCampaignBehavior<
                GarrisonCaptureSupplyBehavior>()?
                .TryConsumeTroopDonationPermit(party, settlement);
            if (party.ActualClan != settlement.OwnerClan)
            {
                requested = Math.Min(
                    GarrisonTransferPolicyModel.GetAllowedDonationCount(
                        party, requested),
                    GarrisonRecruitmentCapacity.GetTransferFreeSlots(garrison));
                return true;
            }
            ForceManagementFacade.Current.TroopTransfers
                .TransferFieldToGarrison(party, settlement, requested);
            requested = 0;
            state = null;
            return false;
        }

        public static void ReconcileDonation(
            GarrisonDonationTransferState state)
        {
            if (state?.Source == null || state.Target == null) return;
            Dictionary<CharacterObject, int> sourceAfter = Snapshot(state.Source);
            Dictionary<CharacterObject, int> targetAfter = Snapshot(state.Target);
            var transferred = new Dictionary<CharacterObject, int>();
            foreach (CharacterObject troop in state.SourceBefore.Keys
                .Union(state.TargetBefore.Keys))
            {
                int sourceLoss = GetCount(state.SourceBefore, troop) -
                    GetCount(sourceAfter, troop);
                int targetGain = GetCount(targetAfter, troop) -
                    GetCount(state.TargetBefore, troop);
                int moved = Math.Min(Math.Max(0, sourceLoss),
                    Math.Max(0, targetGain));
                if (moved > 0) transferred[troop] = moved;
            }
            Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?
                .TransferFiefService(state.Source, state.Target, transferred);
        }

        public static bool ExecuteWithdrawal(MobileParty party,
            Settlement settlement, int requested, out int before)
        {
            before = settlement?.Town?.GarrisonParty?.Party
                .NumberOfRegularMembers ?? 0;
            if (ManagedPartyPolicy.IsModManagedAiParty(party)) return false;
            GarrisonTransferPolicyModel.TransferApprovedTroopsFromGarrison(
                party, settlement, requested);
            return false;
        }

        public static void LogWithdrawal(MobileParty party,
            Settlement settlement, int before)
        {
            if (party == null || party.IsMainParty ||
                settlement?.Town?.GarrisonParty == null ||
                Clan.PlayerClan?.Kingdom == null ||
                party.MapFaction != Clan.PlayerClan.Kingdom) return;
            int remaining = settlement.Town.GarrisonParty.Party
                .NumberOfRegularMembers;
            int withdrawn = Math.Max(0, before - remaining);
            if (withdrawn <= 0) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_garrison_withdrawal");
            message.SetTextVariable("PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
            message.SetTextVariable("COUNT", withdrawn);
            ModLogger.Notice(message.ToString());
        }

        private static Dictionary<CharacterObject, int> Snapshot(
            MobileParty party) => party?.MemberRoster?.GetTroopRoster()
                .Where(item => item.Character != null &&
                    !item.Character.IsHero && item.Number > 0)
                .ToDictionary(item => item.Character, item => item.Number) ??
                new Dictionary<CharacterObject, int>();

        private static int GetCount(
            IReadOnlyDictionary<CharacterObject, int> roster,
            CharacterObject troop) => roster != null &&
                roster.TryGetValue(troop, out int count) ? count : 0;
    }
}
