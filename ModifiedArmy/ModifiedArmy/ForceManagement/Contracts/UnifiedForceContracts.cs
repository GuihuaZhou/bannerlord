using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Contracts
{
    /// <summary>Strategic purpose owned by the unified force planner.</summary>
    public enum PartyForceIntent
    {
        Maintain,
        Recruit,
        WarRest,
        Suspended
    }

    /// <summary>Preferred source category without mixing legacy enums.</summary>
    public enum PartyForceSourceKind
    {
        None,
        Fief,
        Professional,
        Mercenary
    }

    /// <summary>Lifecycle state of one persisted force-planning task.</summary>
    public enum PartyForceTaskStatus
    {
        Planned,
        Traveling,
        Visiting,
        Executing,
        Completed,
        Cancelled,
        Suspended
    }

    /// <summary>Supported movement of existing soldiers between resources.</summary>
    public enum PartyExchangeOperation
    {
        None,
        FieldToGarrison,
        GarrisonToField,
        FiefMobilization,
        FiefReturn,
        BidirectionalSwap
    }

    /// <summary>Identity of a live troop resource; never a copied roster.</summary>
    public enum PartyTroopSourceKind
    {
        None,
        Mobile,
        Garrison,
        Fief
    }

    /// <summary>Business reason for an exchange request.</summary>
    public enum PartyExchangePurpose
    {
        NaturalVisit,
        Reinforcement,
        NewlyCapturedSettlement,
        PeaceDemobilization,
        WarRest,
        PartyDisbandment
    }

    /// <summary>Executor selected by a pure exchange plan.</summary>
    public enum PartyExchangeExecutorKind
    {
        None,
        MobileRosterTransfer,
        FiefMobilization,
        FiefReturn,
        Composite
    }

    /// <summary>Read-only financial inputs captured for daily planning.</summary>
    public sealed class ClanFinanceSnapshot
    {
        public Clan Clan { get; set; }
        public float OperatingFunds { get; set; }
        public float CurrentDailyWage { get; set; }
        public float AvailableRecruitmentBudget { get; set; }
    }

    /// <summary>Culture preference weights preserved without normalization loss.</summary>
    public sealed class PartyForceSourceWeights
    {
        public float Fief { get; set; }
        public float Professional { get; set; }
        public float Mercenary { get; set; }
    }

    /// <summary>One evaluated destination and source combination.</summary>
    public sealed class PartyForceCandidate
    {
        public Settlement Settlement { get; set; }
        public PartyForceSourceKind SourceKind { get; set; }
        public int AvailableCount { get; set; }
        public float PreferenceScore { get; set; }
        public float EffectScore { get; set; }
        public float Distance { get; set; }
        public int GapCovered { get; set; }
        public float CultureMatchFactor { get; set; }
        public float TravelCost { get; set; }
        public float RouteRisk { get; set; }
        public float EconomicImpact { get; set; }
        public float Score { get; set; }
        public bool IsSafe { get; set; }
        public string ReasonCode { get; set; }
    }

    /// <summary>
    /// Runtime task selected by the planner. Roster snapshots are deliberately
    /// excluded so a later persistence layer can rebuild volatile decisions.
    /// </summary>
    public sealed class PartyForceTask
    {
        public MobileParty Party { get; set; }
        public string OwnerKey { get; set; }
        public int Version { get; set; }
        public int CreatedDay { get; set; }
        public PartyForceIntent Intent { get; set; }
        public PartyForceSourceKind SourceKind { get; set; }
        public string SourceBatchId { get; set; }
        public Settlement TargetSettlement { get; set; }
        public string ReasonCode { get; set; }
        public string PartyId { get; set; }
        public string ClanId { get; set; }
        public int LastValidatedDay { get; set; }
        public PartyForceTaskStatus Status { get; set; }
        public int Progress { get; set; }
        public bool OwnsMovementOrder { get; set; }
    }

    /// <summary>All read-only inputs used to build a daily strategic plan.</summary>
    public sealed class PartyForcePlanningContext
    {
        public MobileParty Party { get; set; }
        public PartyForceTask CurrentTask { get; set; }
        public PartyForceTargetResult ForceTarget { get; set; }
        public bool IsAtFormalWar { get; set; }
        public WarRestDecision WarRest { get; set; }
        public ClanFinanceSnapshot Finance { get; set; }
        public PartyForceSourceWeights SourceWeights { get; set; }
        public IReadOnlyList<PartyForceCandidate> Candidates { get; set; } =
            Array.Empty<PartyForceCandidate>();
        public float RecruitmentNeedScore { get; set; }
        public float RecruitmentNeedThreshold { get; set; }
        public bool IsRandomGateOpen { get; set; }
        public bool IsProtectedByHigherPriorityOrder { get; set; }
        public int Today { get; set; }
    }

    /// <summary>Pure daily output; applying movement remains a Behavior duty.</summary>
    public sealed class PartyForcePlan
    {
        public PartyForceTask Task { get; set; }
        public IReadOnlyList<PartyForceCandidate> RankedCandidates { get; set; } =
            Array.Empty<PartyForceCandidate>();
        public bool ShouldReplaceCurrentTask { get; set; }
        public string ReasonCode { get; set; }

        /// <summary>Creates an explicit non-executing planning result.</summary>
        public static PartyForcePlan NoPlan(string reason) =>
            new PartyForcePlan
            {
                Task = new PartyForceTask
                {
                    Intent = PartyForceIntent.Suspended,
                    ReasonCode = reason
                },
                ReasonCode = reason
            };
    }

    /// <summary>Read-only inputs reserved for the later war-rest policy.</summary>
    public sealed class WarRestContext
    {
        public MobileParty Party { get; set; }
        public PartyForceTargetResult ForceTarget { get; set; }
        public bool IsAtFormalWar { get; set; }
        public bool IsOwnerValid { get; set; }
        public bool IsMilitaryPriority { get; set; }
        public ClanFinanceSnapshot Finance { get; set; }
        public WarRestPolicySettings Settings { get; set; }
        public IReadOnlyList<WarRestFiefBatchCandidate> FiefBatches
            { get; set; } = Array.Empty<WarRestFiefBatchCandidate>();
        public IReadOnlyList<PartyForceCandidate> DestinationOptions
            { get; set; } = Array.Empty<PartyForceCandidate>();
        public int CompletedToday { get; set; }
        public int Today { get; set; }
    }

    /// <summary>Explicit settings; automatic war rest remains disabled by default.</summary>
    public sealed class WarRestPolicySettings
    {
        public bool Enabled { get; set; }
        public int MaximumDailyCount { get; set; } = 30;
        public float MinimumUrgencyScore { get; set; } = 0.75f;
        public float DispositionWeight { get; set; } = 0.30f;
        public float FinanceWeight { get; set; } = 0.30f;
        public float ServiceWeight { get; set; } = 0.40f;
    }

    /// <summary>One real service batch and its only legal return destination.</summary>
    public sealed class WarRestFiefBatchCandidate
    {
        public Settlement SourceFief { get; set; }
        public string BatchId { get; set; }
        public int AvailableCount { get; set; }
        public int ServiceDays { get; set; }
        public int FreeWageDays { get; set; }
        public IReadOnlyDictionary<CharacterObject, int> Troops { get; set; } =
            new Dictionary<CharacterObject, int>();
        public float Distance { get; set; }
        public bool IsSafe { get; set; }
        public string ReasonCode { get; set; }
    }

    /// <summary>Pure approval returned by the war-rest policy.</summary>
    public sealed class WarRestDecision
    {
        public bool IsApproved { get; set; }
        public bool CanRest => IsApproved;
        public float UrgencyScore { get; set; }
        public int MilitarySafeFloor { get; set; }
        public int ApprovedReturnCount { get; set; }
        public int ApprovedRegularDonationCount { get; set; }
        public IReadOnlyList<FiefBatchTransferApproval> CandidateFiefBatches
            { get; set; } = Array.Empty<FiefBatchTransferApproval>();
        public IReadOnlyList<PartyForceCandidate> DestinationOptions
            { get; set; } = Array.Empty<PartyForceCandidate>();
        public string ReasonCode { get; set; }

        /// <summary>Creates an explicit refusal without side effects.</summary>
        public static WarRestDecision Denied(string reason) =>
            new WarRestDecision { ReasonCode = reason };
    }

    /// <summary>
    /// Describes a live source through authoritative campaign objects. Roster
    /// is an accessor reference and must never be serialized as a copied truth.
    /// </summary>
    public sealed class PartyTroopSource
    {
        public PartyTroopSourceKind Kind { get; set; }
        public MobileParty Party { get; set; }
        public Settlement Settlement { get; set; }
        public string BatchId { get; set; }
        public TroopRoster Roster { get; set; }
    }

    /// <summary>Immutable-by-convention constraint snapshot for validation.</summary>
    public sealed class PartyRosterSnapshot
    {
        public string Fingerprint { get; set; }
        public int RegularCount { get; set; }
        public int WoundedCount { get; set; }
        public int HardLimit { get; set; }
        public float DailyWage { get; set; }
    }

    /// <summary>All read-only inputs for planning an existing-troop exchange.</summary>
    public sealed class PartyExchangeContext
    {
        public string OperationId { get; set; }
        public MobileParty Initiator { get; set; }
        public PartyExchangeOperation Operation { get; set; }
        public PartyForceIntent Intent { get; set; }
        public PartyExchangePurpose Purpose { get; set; }
        public PartyTroopSource Source { get; set; }
        public PartyTroopSource Destination { get; set; }
        public Settlement TargetSettlement { get; set; }
        public PartyRosterSnapshot SourceSnapshot { get; set; }
        public PartyRosterSnapshot DestinationSnapshot { get; set; }
        public int RequestedCount { get; set; }
        public int Today { get; set; }
    }

    /// <summary>Approved amount tied to one authoritative fief batch.</summary>
    public sealed class FiefBatchTransferApproval
    {
        public Settlement SourceFief { get; set; }
        public string BatchId { get; set; }
        public int ApprovedCount { get; set; }
        public TroopExchangeManifest Troops { get; } =
            new TroopExchangeManifest();
    }

    /// <summary>
    /// Pure exchange plan with manifests and validation fingerprints. Empty
    /// plans always carry a reason code and never authorize execution.
    /// </summary>
    public sealed class PartyExchangePlan
    {
        /// <summary>The party whose approved business operation created this plan.</summary>
        public MobileParty Initiator { get; set; }
        public string OperationId { get; set; }
        public bool ShouldExecute { get; set; }
        public string ReasonCode { get; set; }
        public PartyExchangeOperation Operation { get; set; }
        public PartyExchangePurpose Purpose { get; set; }
        public PartyExchangeExecutorKind Executor { get; set; }
        public int RequestedCount { get; set; }
        public PartyTroopSource Source { get; set; }
        public PartyTroopSource Destination { get; set; }
        public Settlement TargetSettlement { get; set; }
        public TroopExchangeManifest SourceToDestination { get; } =
            new TroopExchangeManifest();
        public TroopExchangeManifest DestinationToSource { get; } =
            new TroopExchangeManifest();
        public IReadOnlyList<FiefBatchTransferApproval> FiefBatches { get; set; } =
            Array.Empty<FiefBatchTransferApproval>();
        public PartyRosterSnapshot ExpectedSource { get; set; }
        public PartyRosterSnapshot ExpectedDestination { get; set; }
        public int ExpectedSourceFinalCount { get; set; }
        public int ExpectedDestinationFinalCount { get; set; }
        public float ExpectedSourceFinalWage { get; set; }
        public float ExpectedDestinationFinalWage { get; set; }

        /// <summary>Creates an explicit rejected plan.</summary>
        public static PartyExchangePlan Rejected(string reason) =>
            new PartyExchangePlan
            {
                ReasonCode = reason,
                Executor = PartyExchangeExecutorKind.None
            };
    }

    /// <summary>Result of one guarded unified exchange execution.</summary>
    public sealed class PartyExchangeExecutionResult
    {
        public bool Executed { get; set; }
        public string ReasonCode { get; set; }
        public int TransferredCount { get; set; }
    }

    public interface IAiForcePlanningModel
    {
        PartyForcePlan BuildDailyPlan(PartyForcePlanningContext context);
    }

    public interface IPartyExchangeModel
    {
        PartyExchangePlan BuildPlan(PartyExchangeContext context);
    }

    public interface IWarRestPolicyModel
    {
        WarRestDecision Evaluate(WarRestContext context);
    }
}
