// Initial AI lord-party qualification contracts. Runtime-only previews rank
// sites and prove the minimum is feasible; they never reserve live resources.
using System;
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
        public int MinimumPartyMembers { get; set; }
        public int InitialMemberCount { get; set; }
        public int RequiredNewTroops => Math.Max(
            0, MinimumPartyMembers - InitialMemberCount);
        public int ApprovedFiefCount { get; set; }
        public int ApprovedGarrisonCount { get; set; }
        public int ApprovedTotal => ApprovedFiefCount + ApprovedGarrisonCount;
        public IReadOnlyList<PlannedVillageWithdrawal> FiefByVillage { get; set; }
        public TroopExchangeManifest GarrisonManifest { get; set; }
        public string RejectReason { get; set; }
        public bool Approved => string.IsNullOrEmpty(RejectReason) &&
            ApprovedTotal >= RequiredNewTroops;
    }

    public enum InitialMobilizationCommitStatus
    {
        Rejected,
        Committed
    }

    public sealed class InitialMobilizationCommitResult
    {
        public InitialMobilizationCommitStatus Status { get; set; }
        public string ReasonCode { get; set; }
        public int CommittedFiefCount { get; set; }
        public int CommittedGarrisonCount { get; set; }
        public int CommittedMercenaryCount { get; set; }
    }
}
