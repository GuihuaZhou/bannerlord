using TaleWorlds.SaveSystem;

namespace ModifiedArmy.PartyManagement.Bindings
{
    /// <summary>Persistent identity-only binding between one lord party and its bases.</summary>
    public sealed class PartySettlementBindingRecord
    {
        [SaveableProperty(1)]
        public string PartyId { get; set; }

        [SaveableProperty(2)]
        public string ClanId { get; set; }

        [SaveableProperty(3)]
        public string PrimarySettlementId { get; set; }

        [SaveableProperty(4)]
        public string SecondarySettlementId { get; set; }

        [SaveableProperty(5)]
        public int Version { get; set; }

        [SaveableProperty(6)]
        public bool LegacyUnbound { get; set; }

        [SaveableProperty(7)]
        public bool PendingDisband { get; set; }
    }
}
