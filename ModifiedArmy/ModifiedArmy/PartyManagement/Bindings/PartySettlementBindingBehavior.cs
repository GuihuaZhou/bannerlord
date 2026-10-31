using ModifiedArmy.PartyManagement.Bindings.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Bindings
{
    /// <summary>
    /// Owns persistent lord-party base bindings and the runtime reverse index.
    /// Recovery only repairs identities and never moves troops.
    /// </summary>
    public sealed class PartySettlementBindingBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, PartySettlementBindingRecord> _records =
            new Dictionary<string, PartySettlementBindingRecord>();
        private readonly Dictionary<string, string> _settlementOwners =
            new Dictionary<string, string>();
        private readonly Dictionary<string, PendingPartyCreation> _reservations =
            new Dictionary<string, PendingPartyCreation>();
        private bool _migrationComplete;

        public override void RegisterEvents()
        {
            CampaignEvents.MobilePartyCreated.AddNonSerializedListener(
                this, OnMobilePartyCreated);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(
                this, OnMobilePartyDestroyed);
            CampaignEvents.OnPartyDisbandStartedEvent.AddNonSerializedListener(
                this, OnPartyDisbandStarted);
            CampaignEvents.OnPartyDisbandCanceledEvent.AddNonSerializedListener(
                this, OnPartyDisbandCanceled);
            CampaignEvents.OnPartyDisbandedEvent.AddNonSerializedListener(
                this, OnPartyDisbanded);
            CampaignEvents.OnPartyRemovedEvent.AddNonSerializedListener(
                this, OnPartyRemoved);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(
                this, OnSettlementOwnerChanged);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(
                this, OnClanChangedKingdom);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_party_settlement_bindings_v1",
                ref _records);
            dataStore.SyncData(
                "modifiedarmy_party_settlement_bindings_migrated_v1",
                ref _migrationComplete);
            _records ??= new Dictionary<string, PartySettlementBindingRecord>();
        }

        public IReadOnlyList<Settlement> GetBoundSettlements(MobileParty party)
        {
            if (party?.StringId == null ||
                !_records.TryGetValue(party.StringId, out var record))
                return Array.Empty<Settlement>();
            return new[]
                {
                    ResolveSettlement(record.PrimarySettlementId),
                    ResolveSettlement(record.SecondarySettlementId)
                }
                .Where(item => IsLegalBase(party, item))
                .Distinct()
                .ToList();
        }

        public bool IsBoundSettlement(MobileParty party, Settlement settlement) =>
            settlement != null && GetBoundSettlements(party).Contains(settlement);

        public bool IsLegacyUnbound(MobileParty party) =>
            party?.StringId != null &&
            _records.TryGetValue(party.StringId, out var record) &&
            record.LegacyUnbound;

        public bool CanReserveForCreation(
            Hero leader, Clan clan, Settlement primary) =>
            leader != null && clan != null && primary != null &&
            leader.Clan == clan && IsLegalBase(clan, primary) &&
            IsAvailable(primary, null);

        public bool TryBindSecondary(MobileParty party, Settlement settlement)
        {
            if (!TryGetValidRecord(party, out var record) ||
                string.IsNullOrEmpty(record.PrimarySettlementId) ||
                !IsLegalBase(party, settlement) ||
                record.PrimarySettlementId == settlement.StringId ||
                !IsAvailable(settlement, party.StringId) ||
                !string.IsNullOrEmpty(record.SecondarySettlementId))
            {
                PartyBindingDiagnostics.LogBindingRejected(
                    party,
                    settlement,
                    "BindSecondary",
                    "InvalidRecordSlotOrSettlement");
                return false;
            }
            record.SecondarySettlementId = settlement.StringId;
            record.Version++;
            record.LegacyUnbound = false;
            _settlementOwners[settlement.StringId] = party.StringId;
            return true;
        }

        public bool TryReserveForCreation(
            Hero leader,
            Clan clan,
            Settlement primary,
            out PendingPartyCreation token)
        {
            token = null;
            if (leader == null || clan == null || primary == null ||
                leader.Clan != clan || !IsLegalBase(clan, primary) ||
                !IsAvailable(primary, null))
            {
                PartyBindingDiagnostics.LogReservation(
                    leader,
                    primary,
                    null,
                    "Rejected",
                    "InvalidLeaderClanOrSettlement",
                    true);
                return false;
            }
            token = new PendingPartyCreation(
                Guid.NewGuid().ToString("N"), leader.StringId,
                clan.StringId, primary.StringId,
                (int)CampaignTime.Now.ToDays);
            _reservations[primary.StringId] = token;
            PartyBindingDiagnostics.LogReservation(
                leader, primary, token.TokenId, "Reserved", "Approved");
            return true;
        }

        public bool TryCommitCreation(
            PendingPartyCreation token,
            MobileParty createdParty)
        {
            if (token == null || createdParty?.StringId == null ||
                !_reservations.TryGetValue(token.PrimarySettlementId,
                    out var current) || current.TokenId != token.TokenId ||
                createdParty.ActualClan?.StringId != token.ClanId)
            {
                PartyBindingDiagnostics.LogBindingRejected(
                    createdParty,
                    ResolveSettlement(token?.PrimarySettlementId),
                    "CommitCreation",
                    "InvalidTokenPartyOrClan");
                return false;
            }
            Settlement primary = ResolveSettlement(token.PrimarySettlementId);
            if (!IsLegalBase(createdParty, primary))
            {
                PartyBindingDiagnostics.LogBindingRejected(
                    createdParty,
                    primary,
                    "CommitCreation",
                    "SettlementNoLongerLegal");
                return false;
            }
            _records[createdParty.StringId] = new PartySettlementBindingRecord
            {
                PartyId = createdParty.StringId,
                ClanId = token.ClanId,
                PrimarySettlementId = primary.StringId,
                Version = 1
            };
            _settlementOwners[primary.StringId] = createdParty.StringId;
            _reservations.Remove(primary.StringId);
            PartyBindingDiagnostics.LogCommitted(
                createdParty, primary, 1);
            return true;
        }

        public void CancelCreationReservation(PendingPartyCreation token)
        {
            if (token != null &&
                _reservations.TryGetValue(token.PrimarySettlementId,
                    out var current) && current.TokenId == token.TokenId)
            {
                _reservations.Remove(token.PrimarySettlementId);
                PartyBindingDiagnostics.LogReservation(
                    ResolveHero(token.LeaderId),
                    ResolveSettlement(token.PrimarySettlementId),
                    token.TokenId,
                    "Cancelled",
                    "CallerCancelled");
            }
        }

        public void ReleasePartyBindings(MobileParty party, string reason)
        {
            if (party?.StringId == null ||
                !_records.TryGetValue(party.StringId, out var record))
                return;
            _records.Remove(party.StringId);
            ReleaseIndex(record.PrimarySettlementId, party.StringId);
            ReleaseIndex(record.SecondarySettlementId, party.StringId);
            PartyBindingDiagnostics.LogReleased(party, reason);
        }

        public void RevalidateAndRebuildIndexes()
        {
            _settlementOwners.Clear();
            _reservations.Clear();
            int conflicts = 0;
            foreach (var pair in _records.OrderBy(item => item.Key).ToList())
            {
                MobileParty party = ResolveParty(pair.Key);
                PartySettlementBindingRecord record = pair.Value;
                if (!IsEligibleParty(party) || record == null)
                {
                    _records.Remove(pair.Key);
                    continue;
                }
                record.PartyId = party.StringId;
                record.ClanId = party.ActualClan.StringId;
                ValidateSlot(party, record, true, ref conflicts);
                ValidateSlot(party, record, false, ref conflicts);
                if (record.PrimarySettlementId == null &&
                    record.SecondarySettlementId != null)
                {
                    record.PrimarySettlementId = record.SecondarySettlementId;
                    record.SecondarySettlementId = null;
                }
            }

            foreach (MobileParty party in MobileParty.All
                .Where(IsEligibleParty)
                .OrderBy(item => item.ActualClan.StringId)
                .ThenBy(item => item.StringId))
            {
                if (!_records.TryGetValue(party.StringId, out var record))
                {
                    record = new PartySettlementBindingRecord
                    {
                        PartyId = party.StringId,
                        ClanId = party.ActualClan.StringId,
                        Version = 1
                    };
                    _records[party.StringId] = record;
                }
                if (record.PrimarySettlementId == null)
                {
                    Settlement available = party.ActualClan.Settlements
                        .Where(item => IsLegalBase(party, item) &&
                            IsAvailable(item, party.StringId))
                        .OrderBy(item => item.StringId)
                        .FirstOrDefault();
                    if (available != null)
                    {
                        record.PrimarySettlementId = available.StringId;
                        record.LegacyUnbound = false;
                        record.Version++;
                        _settlementOwners[available.StringId] = party.StringId;
                    }
                    else
                    {
                        record.LegacyUnbound = true;
                    }
                }
            }
            _migrationComplete = true;
            PartyBindingDiagnostics.LogRecovery(
                _records.Count,
                _settlementOwners.Count,
                _records.Values.Count(item => item.LegacyUnbound),
                conflicts);
        }

        private void ValidateSlot(
            MobileParty party,
            PartySettlementBindingRecord record,
            bool primary,
            ref int conflicts)
        {
            string id = primary
                ? record.PrimarySettlementId
                : record.SecondarySettlementId;
            if (string.IsNullOrEmpty(id)) return;
            Settlement settlement = ResolveSettlement(id);
            bool duplicate = !primary && id == record.PrimarySettlementId;
            if (duplicate || !IsLegalBase(party, settlement) ||
                !IsAvailable(settlement, party.StringId))
            {
                if (primary) record.PrimarySettlementId = null;
                else record.SecondarySettlementId = null;
                record.Version++;
                conflicts++;
                return;
            }
            _settlementOwners[id] = party.StringId;
        }

        private bool TryGetValidRecord(
            MobileParty party,
            out PartySettlementBindingRecord record)
        {
            record = null;
            return IsEligibleParty(party) &&
                _records.TryGetValue(party.StringId, out record) &&
                record != null;
        }

        private bool IsAvailable(Settlement settlement, string partyId)
        {
            if (settlement?.StringId == null ||
                _reservations.ContainsKey(settlement.StringId))
                return false;
            return !_settlementOwners.TryGetValue(
                settlement.StringId, out string owner) || owner == partyId;
        }

        private static bool IsEligibleParty(MobileParty party) =>
            party != null && party.IsActive && party.IsLordParty &&
            party != MobileParty.MainParty && party.ActualClan != null &&
            !party.ActualClan.IsMinorFaction;

        private static bool IsLegalBase(MobileParty party, Settlement settlement) =>
            party?.ActualClan != null && IsLegalBase(party.ActualClan, settlement);

        private static bool IsLegalBase(Clan clan, Settlement settlement) =>
            clan != null && settlement != null && settlement.IsFortification &&
            !settlement.IsUnderSiege && settlement.OwnerClan == clan;

        private static MobileParty ResolveParty(string id) =>
            string.IsNullOrEmpty(id) ? null : MobileParty.All.FirstOrDefault(
                item => item?.StringId == id);

        private static Settlement ResolveSettlement(string id) =>
            string.IsNullOrEmpty(id) ? null : Settlement.All.FirstOrDefault(
                item => item?.StringId == id);

        private static Hero ResolveHero(string id) =>
            string.IsNullOrEmpty(id) ? null : Hero.AllAliveHeroes.FirstOrDefault(
                item => item?.StringId == id);

        private void ReleaseIndex(string settlementId, string partyId)
        {
            if (!string.IsNullOrEmpty(settlementId) &&
                _settlementOwners.TryGetValue(settlementId, out string owner) &&
                owner == partyId)
                _settlementOwners.Remove(settlementId);
        }

        private void OnMobilePartyCreated(MobileParty party)
        {
            if (!IsEligibleParty(party) || _records.ContainsKey(party.StringId))
                return;
            _records[party.StringId] = new PartySettlementBindingRecord
            {
                PartyId = party.StringId,
                ClanId = party.ActualClan.StringId,
                Version = 1,
                LegacyUnbound = true
            };
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer) =>
            ReleasePartyBindings(party, "Destroyed");

        private void OnPartyDisbandStarted(MobileParty party)
        {
            if (party?.StringId != null &&
                _records.TryGetValue(party.StringId, out var record))
                record.PendingDisband = true;
        }

        private void OnPartyDisbandCanceled(MobileParty party)
        {
            if (party?.StringId != null &&
                _records.TryGetValue(party.StringId, out var record))
                record.PendingDisband = false;
        }

        private void OnPartyDisbanded(MobileParty party, Settlement settlement) =>
            ReleasePartyBindings(party, "Disbanded");

        private void OnPartyRemoved(PartyBase party)
        {
            if (party?.MobileParty != null)
                ReleasePartyBindings(party.MobileParty, "Removed");
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail) =>
            RevalidateAndRebuildIndexes();

        private void OnClanChangedKingdom(
            Clan clan,
            Kingdom oldKingdom,
            Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail,
            bool showNotification) => RevalidateAndRebuildIndexes();
    }

    public sealed class PendingPartyCreation
    {
        internal PendingPartyCreation(
            string tokenId,
            string leaderId,
            string clanId,
            string primarySettlementId,
            int createdDay)
        {
            TokenId = tokenId;
            LeaderId = leaderId;
            ClanId = clanId;
            PrimarySettlementId = primarySettlementId;
            CreatedDay = createdDay;
        }

        public string TokenId { get; }
        public string LeaderId { get; }
        public string ClanId { get; }
        public string PrimarySettlementId { get; }
        public int CreatedDay { get; }
    }
}
