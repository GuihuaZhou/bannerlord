// Initial AI lord-party mobilization contracts. These runtime-only values hold
// plans and fingerprints; they never replace roster, Hearth, budget, or journals.
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Contracts
{
    public sealed class PlannedVillageWithdrawal
    {
        public Settlement Village { get; set; }
        public CharacterObject Troop { get; set; }
        public int Count { get; set; }
        public int WoundedCount { get; set; }
        public int HearthCost { get; set; }
    }

    public sealed class InitialMobilizationPreview
    {
        public string AttemptId { get; set; }
        public Hero Leader { get; set; }
        public Clan Clan { get; set; }
        public Settlement BaseSettlement { get; set; }
        public int AssumedPartyLimit { get; set; }
        public int TargetCount { get; set; }
        public int PaymentLimit { get; set; }
        public int RequiredRegulars { get; set; }
        public int ApprovedFiefCount { get; set; }
        public int ApprovedGarrisonCount { get; set; }
        public int ApprovedTotal => ApprovedFiefCount + ApprovedGarrisonCount;
        public IReadOnlyList<PlannedVillageWithdrawal> FiefByVillage { get; set; }
        public TroopExchangeManifest GarrisonManifest { get; set; }
        public string SourceFingerprint { get; set; }
        public string RejectReason { get; set; }
        public bool Approved => string.IsNullOrEmpty(RejectReason) &&
            ApprovedTotal >= RequiredRegulars;
    }

    public enum InitialMobilizationCommitStatus
    {
        Rejected,
        RolledBack,
        Committed,
        RecoveryRequired
    }

    public sealed class InitialMobilizationCommitResult
    {
        public InitialMobilizationCommitStatus Status { get; set; }
        public string ReasonCode { get; set; }
        public int CommittedFiefCount { get; set; }
        public int CommittedGarrisonCount { get; set; }
    }
}
