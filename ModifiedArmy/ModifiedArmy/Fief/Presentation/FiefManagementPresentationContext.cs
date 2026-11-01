using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief.Presentation
{
    internal sealed class FiefManagementPresentationContext
    {
        private readonly FiefPartyManager _manager;
        private readonly MobileParty _party;
        private string _fingerprint;

        private FiefManagementPresentationContext(Settlement interaction,
            FiefPartyManager manager, MobileParty party, bool canModify)
        {
            Interaction = interaction;
            _manager = manager;
            _party = party;
            CanModify = canModify;
            State = FiefManagementState.Ready;
            Village = interaction.IsVillage ? interaction : null;
            RefreshFingerprint();
        }

        internal static FiefManagementPresentationContext Pending { get; private set; }
        internal Settlement Interaction { get; }
        internal Settlement Village { get; private set; }
        internal FiefManagementState State { get; private set; }
        internal bool CanModify { get; }

        internal static FiefManagementPresentationContext Begin(
            Settlement interaction, FiefPartyManager manager,
            MobileParty party, bool canModify)
        {
            Pending = new FiefManagementPresentationContext(
                interaction, manager, party, canModify);
            return Pending;
        }

        internal static FiefManagementPresentationContext Claim()
        {
            FiefManagementPresentationContext result = Pending;
            Pending = null;
            return result;
        }

        internal IEnumerable<Settlement> GetVillages()
        {
            if (Interaction.IsVillage)
                return new[] { Interaction };
            return _manager.GetFiefData(Interaction)?.OrderedVillages()
                .Select(data => data.VillageSettlement)
                .Where(village => village != null)
                ?? Enumerable.Empty<Settlement>();
        }

        internal void SelectVillage(Settlement village)
        {
            Village = Interaction.IsVillage ? Interaction : village;
            RefreshFingerprint();
        }

        internal void SelectState(FiefManagementState state)
        {
            State = state;
            RefreshFingerprint();
        }

        internal TroopRoster GetFullRoster()
        {
            return _manager.GetFiefManagementRoster(
                Interaction, Village, State, _party,
                CanModify && State == FiefManagementState.Serving);
        }

        internal TroopRoster GetSelectableRoster()
        {
            if (!CanModify || State == FiefManagementState.Cooling)
                return TroopRoster.CreateDummyTroopRoster();
            return _manager.GetFiefManagementRoster(
                Interaction, Village, State, _party, true);
        }

        internal bool CanSelect(CharacterObject troop)
        {
            return troop != null && GetSelectableRoster()
                .GetElementNumber(troop) > 0;
        }

        internal int GetMaximumSelection()
        {
            int maximum = GetSelectableRoster().TotalManCount;
            if (State == FiefManagementState.Ready)
            {
                maximum = Math.Min(maximum, Math.Max(0,
                    _party.Party.PartySizeLimit -
                    _party.Party.NumberOfAllMembers));
            }
            return maximum;
        }

        internal int GetCount(FiefManagementState state,
            Settlement village = null)
        {
            return _manager.GetFiefManagementRoster(
                Interaction, village ?? Village, state, _party)
                .TotalManCount;
        }

        internal int GetSourceCount(FiefManagementState state,
            Settlement village)
        {
            return _manager.GetFiefManagementRoster(
                Interaction, village, state, _party).TotalManCount;
        }

        internal void Commit(TroopRoster selected)
        {
            if (selected == null || selected.TotalManCount <= 0 ||
                State == FiefManagementState.Cooling ||
                Interaction != Settlement.CurrentSettlement)
                return;
            if (!string.Equals(_fingerprint,
                _manager.GetFiefManagementFingerprint(
                    Interaction, Village, State, _party),
                StringComparison.Ordinal))
            {
                ModLogger.Warn(
                    "Fief management rejected: source state changed before commit.");
                return;
            }
            if (State == FiefManagementState.Ready)
                _manager.ManualRecruitFromFief(
                    Interaction, selected, _party, Village);
            else if (State == FiefManagementState.Serving)
                _manager.ReturnSelectedFiefTroops(
                    Interaction, Village, _party, selected);
        }

        private void RefreshFingerprint()
        {
            _fingerprint = _manager.GetFiefManagementFingerprint(
                Interaction, Village, State, _party);
        }
    }
}
