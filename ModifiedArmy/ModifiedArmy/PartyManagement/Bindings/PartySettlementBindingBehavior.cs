using ModifiedArmy.PartyManagement.Bindings.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using ModifiedArmy.Models.Fief;

namespace ModifiedArmy.PartyManagement.Bindings
{
    public sealed class FiefSourceAccessDecision
    {
        public bool Allowed { get; private set; }
        public Settlement SourceFief { get; private set; }
        public string BoundPartyId { get; private set; }
        public string ReservationOwner { get; private set; }
        public string ReasonCode { get; private set; }

        internal static FiefSourceAccessDecision Allow(Settlement source,
            string owner, string reason) => new FiefSourceAccessDecision
            {
                Allowed = true, SourceFief = source, BoundPartyId = owner,
                ReasonCode = reason
            };

        internal static FiefSourceAccessDecision Reject(Settlement source,
            string reason, string owner, string reservationOwner) =>
            new FiefSourceAccessDecision
            {
                Allowed = false, SourceFief = source, BoundPartyId = owner,
                ReservationOwner = reservationOwner, ReasonCode = reason
            };
    }

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
                    ResolveSettlement(record.PrimarySettlementId)
                }
                .Where(item => IsLegalBase(party, item))
                .Distinct()
                .ToList();
        }

        public bool IsBoundSettlement(MobileParty party, Settlement settlement) =>
            EvaluateFiefSourceAccess(settlement, party).Allowed;

        public FiefSourceAccessDecision EvaluateFiefSourceAccess(
            Settlement source, MobileParty requestingParty)
        {
            Settlement sourceFief = NormalizeSourceFief(source);
            if (sourceFief == null || requestingParty?.StringId == null)
                return FiefSourceAccessDecision.Reject(sourceFief,
                    "SourceBindingInvalid", null, null);
            IReadOnlyList<MobileParty> servingParties = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetServingPartiesForFief(sourceFief) ??
                Array.Empty<MobileParty>();
            if (servingParties.Count > 1 ||
                (servingParties.Count == 1 &&
                 servingParties[0] != requestingParty))
                return FiefSourceAccessDecision.Reject(sourceFief,
                    "SourceHasConflictingServingParties",
                    _settlementOwners.TryGetValue(sourceFief.StringId,
                        out string conflictOwner) ? conflictOwner : null,
                    null);
            if (_reservations.TryGetValue(sourceFief.StringId,
                    out PendingPartyCreation reservation))
                return FiefSourceAccessDecision.Reject(sourceFief,
                    "SourceReservedForOtherCreation", null,
                    reservation.LeaderId);
            // The player party is intentionally excluded from persistent AI-party
            // bindings. It may use an otherwise unclaimed source that belongs to
            // its clan; ownership and current-settlement checks are repeated by
            // the operation scope before any roster or Hearth mutation.
            if (requestingParty == MobileParty.MainParty &&
                requestingParty.ActualClan != null &&
                sourceFief.OwnerClan == requestingParty.ActualClan &&
                !_settlementOwners.ContainsKey(sourceFief.StringId))
                return FiefSourceAccessDecision.Allow(sourceFief,
                    requestingParty.StringId,
                    "UnboundClanSourceAvailableToMainParty");
            if (!_settlementOwners.TryGetValue(sourceFief.StringId,
                    out string owner))
                return FiefSourceAccessDecision.Reject(sourceFief,
                    "SourceBindingInvalid", null, null);
            return owner == requestingParty.StringId
                ? FiefSourceAccessDecision.Allow(sourceFief, owner,
                    "SourceBoundToSameParty")
                : FiefSourceAccessDecision.Reject(sourceFief,
                    "SourceBoundToOtherParty", owner, null);
        }

        public bool IsLegacyUnbound(MobileParty party) =>
            party?.StringId != null &&
            _records.TryGetValue(party.StringId, out var record) &&
            record.LegacyUnbound;

        public bool CanReserveForCreation(
            Hero leader, Clan clan, Settlement primary) =>
            leader != null && clan != null && primary != null &&
            leader.Clan == clan && IsLegalBase(clan, primary) &&
            IsAvailable(primary, null) &&
            GetServingParties(primary).Count == 0;

        public bool TryBindSecondary(MobileParty party, Settlement settlement)
        {
            PartyBindingDiagnostics.LogBindingRejected(party, settlement,
                "BindSecondary", "SecondaryBindingsDisabled");
            return false;
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
                !IsAvailable(primary, null) ||
                GetServingParties(primary).Count > 0)
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
            if (_settlementOwners.TryGetValue(primary.StringId,
                    out string existingOwner) &&
                existingOwner != createdParty.StringId)
            {
                PartyBindingDiagnostics.LogBindingRejected(createdParty,
                    primary, "CommitCreation", "SourceBoundToOtherParty");
                return false;
            }
            IReadOnlyList<MobileParty> servingParties = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetServingPartiesForFief(primary) ??
                Array.Empty<MobileParty>();
            if (servingParties.Any(party => party != createdParty))
            {
                PartyBindingDiagnostics.LogBindingRejected(createdParty,
                    primary, "CommitCreation",
                    "SourceHasConflictingServingParties");
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
            FiefPartyManager fiefManager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (fiefManager?.GetServingDetachments(party).Count > 0)
            {
                PartyBindingDiagnostics.LogBindingRejected(party,
                    ResolveSettlement(record.PrimarySettlementId),
                    "ReleaseBindings", "UnsettledFiefServiceBatches");
                return;
            }
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
                if (!string.IsNullOrEmpty(record.SecondarySettlementId))
                    PartyBindingDiagnostics.LogBindingRejected(party,
                        ResolveSettlement(record.SecondarySettlementId),
                        "LegacySecondaryQuarantine",
                        "SecondaryBindingsDisabled");
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

        private static IReadOnlyList<MobileParty> GetServingParties(
            Settlement sourceFief) => Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetServingPartiesForFief(sourceFief) ??
                Array.Empty<MobileParty>();

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

        private static Settlement NormalizeSourceFief(Settlement source) =>
            source?.IsVillage == true ? source.Village?.Bound :
            source?.IsFortification == true ? source : null;

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
