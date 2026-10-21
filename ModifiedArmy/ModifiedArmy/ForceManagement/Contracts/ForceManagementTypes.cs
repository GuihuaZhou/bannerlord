using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.ForceManagement.Contracts
{
    public enum ForceScenario
    {
        Peace,
        WarRoutine,
        OwnFiefDefense
    }

    public enum ForceOperation
    {
        NewRecruitment,
        FieldToGarrison,
        GarrisonToField,
        Swap,
        FiefReturn,
        Reinforcement
    }

    public enum GarrisonDonationPurpose
    {
        NativeDonation,
        PeaceDemobilization
    }

    public sealed class PeaceDemobilizationContext
    {
        public int PeaceSinceDay { get; set; }
        public int Today { get; set; }
        public int AlreadyDemobilizedToday { get; set; }
        public Settlement VisitedSettlement { get; set; }
        public bool IsRoutingEvaluation { get; set; }
        public bool IsExistingDemobilizationRoute { get; set; }
    }

    public sealed class PeaceDemobilizationDecision
    {
        public bool CanDemobilize { get; set; }
        public int TargetCount { get; set; }
        public int CurrentCount { get; set; }
        public int SurplusCount { get; set; }
        public int ApprovedToday { get; set; }
        public string ReasonCode { get; set; }
    }

    public enum ReinforcementMissionState
    {
        Requested,
        ToSource,
        Loading,
        ToDestination,
        Unloading,
        ReturningOrFinishing,
        Completed,
        Cancelled
    }

    public enum ReinforcementCancelReason
    {
        None,
        InvalidOwner,
        ArmyConflict,
        EnemyThreat,
        InvalidDestination,
        NoCargo,
        RouteBlocked,
        Expired
    }

    public sealed class PartyForceContext
    {
        public MobileParty Party { get; set; }
        public bool IsOwnFiefDefense { get; set; }
    }

    public sealed class PartyForceTargetResult
    {
        public ForceScenario Scenario { get; set; }
        public bool IsAggressiveWarClan { get; set; }
        public float WarDisposition { get; set; }
        public int PartyLimit { get; set; }
        public int CurrentCount { get; set; }
        public int TargetCount { get; set; }
        public int GapCount { get; set; }
        public bool CanStartRecruitmentVisit { get; set; }
        public string ReasonCode { get; set; }
    }

    public sealed class RecruitmentCapacityContext
    {
        public ForceOperation Operation { get; set; }
        public int RequestedCount { get; set; }
        public int HardLimit { get; set; }
        public int EffectiveCurrentCount { get; set; }
        public int TargetCount { get; set; }
    }

    public sealed class RecruitmentApproval
    {
        public int ApprovedCount { get; set; }
        public string ReasonCode { get; set; }
    }

    public sealed class GarrisonDefenseSnapshot
    {
        public Settlement Settlement { get; set; }
        public int ResidentCount { get; set; }
        public int EffectiveGarrisonCount { get; set; }
        public int MinimumCount { get; set; }
        public int HardLimit { get; set; }
        public int ActiveSupplyEscortCount { get; set; }
        public int PredictedSevenDayRecruitment { get; set; }
        public float SimulatedFoodDays { get; set; }
        public bool HasVerifiedActualFoodConsumption { get; set; }
        public bool IsEnemyBorder { get; set; }
    }

    public sealed class ReinforcementNeedResult
    {
        public bool IsRequested { get; set; }
        public string ReasonCode { get; set; }
        public int RequestedTroops { get; set; }

        public static ReinforcementNeedResult NoRequest(string reason) =>
            new ReinforcementNeedResult
            {
                IsRequested = false,
                ReasonCode = reason
            };
    }

    public sealed class PartyForceSnapshot
    {
        public MobileParty Party { get; set; }
        public int CurrentCount { get; set; }
        public int TargetCount { get; set; }
        public int HardLimit { get; set; }
    }

    public sealed class ExchangePlan
    {
        public bool ShouldExecute { get; set; }
        public string ReasonCode { get; set; }
        public MobileParty FieldParty { get; set; }
        public MobileParty GarrisonParty { get; set; }
        public Settlement Settlement { get; set; }
        public Clan Clan { get; set; }
        public string FieldRosterFingerprint { get; set; }
        public string GarrisonRosterFingerprint { get; set; }
        public int ExpectedFieldCount { get; set; }
        public int ExpectedGarrisonCount { get; set; }
        public int ExpectedFieldTarget { get; set; }
        public int ExpectedFieldHardLimit { get; set; }
        public int ExpectedGarrisonMinimum { get; set; }
        public int ExpectedGarrisonUpperTarget { get; set; }
        public int ExpectedGarrisonEscortCount { get; set; }
        public float BenefitScore { get; set; }
        public TroopExchangeManifest FieldToGarrison { get; } =
            new TroopExchangeManifest();
        public TroopExchangeManifest GarrisonToField { get; } =
            new TroopExchangeManifest();

        public static ExchangePlan NoExchange(string reason) =>
            new ExchangePlan { ReasonCode = reason };
    }

    public sealed class TroopExchangeAmount
    {
        public int Count { get; set; }
        public int WoundedCount { get; set; }
    }

    public sealed class TroopExchangeManifest
    {
        public Dictionary<CharacterObject, TroopExchangeAmount> Troops { get; } =
            new Dictionary<CharacterObject, TroopExchangeAmount>();

        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (TroopExchangeAmount amount in Troops.Values)
                {
                    total += System.Math.Max(0, amount?.Count ?? 0);
                }
                return total;
            }
        }
    }

    public sealed class ExchangeValidationResult
    {
        public bool IsValid { get; set; }
        public string ReasonCode { get; set; }

        public static ExchangeValidationResult Valid() =>
            new ExchangeValidationResult
            {
                IsValid = true,
                ReasonCode = "Valid"
            };

        public static ExchangeValidationResult Invalid(string reason) =>
            new ExchangeValidationResult { ReasonCode = reason };
    }

    public sealed class ExchangeExecutionResult
    {
        public bool Executed { get; set; }
        public string ReasonCode { get; set; }
        public int FieldToGarrisonCount { get; set; }
        public int GarrisonToFieldCount { get; set; }
    }

    public sealed class RouteEvaluation
    {
        public bool IsSafe { get; set; }
        public string ReasonCode { get; set; }
    }

    [SaveableRootClass(8)]
    public sealed class ReinforcementMission
    {
        [SaveableProperty(1)]
        public MobileParty Carrier { get; set; }
        [SaveableProperty(2)]
        public Settlement Destination { get; set; }
        [SaveableProperty(3)]
        public int RequestedTroopCount { get; set; }
        [SaveableProperty(4)]
        public int RequestedFoodCount { get; set; }
        [SaveableProperty(5)]
        public ReinforcementMissionState State { get; set; }
        [SaveableProperty(6)]
        public CampaignTime Deadline { get; set; }

        // Runtime-only routing decisions. They are recalculated after loading.
        public Settlement TroopSource { get; set; }
        public Settlement FoodSource { get; set; }

        public bool IsActive =>
            State != ReinforcementMissionState.Completed &&
            State != ReinforcementMissionState.Cancelled;
    }

    public interface IWarDispositionProvider
    {
        bool TryGet(Clan clan, out float disposition, out bool atFormalWar);
    }

    public interface IPartyForceTargetModel
    {
        PartyForceTargetResult Evaluate(PartyForceContext context);
    }

    public interface IRecruitmentAdmissionModel
    {
        RecruitmentApproval Evaluate(RecruitmentCapacityContext context);
    }

    public interface IGarrisonDefenseRequirementModel
    {
        GarrisonDefenseSnapshot Evaluate(Settlement settlement);
        int GetSafeTransferOutCount(
            GarrisonDefenseSnapshot snapshot,
            TroopRoster roster);
    }

    public interface IExternalReinforcementNeedModel
    {
        ReinforcementNeedResult Evaluate(GarrisonDefenseSnapshot snapshot);
    }

    public interface ITroopValueModel
    {
        float GetFieldMarginalValue(
            PartyForceSnapshot field,
            CharacterObject troop,
            int count);
        float GetGarrisonMarginalValue(
            GarrisonDefenseSnapshot garrison,
            CharacterObject troop,
            int count);
    }

    public interface IPartyGarrisonExchangeModel
    {
        ExchangePlan BuildPlan(
            PartyForceSnapshot field,
            GarrisonDefenseSnapshot garrison);
    }

    public interface IReinforcementRouteModel
    {
        RouteEvaluation Evaluate(
            MobileParty carrier,
            Settlement source,
            Settlement destination);
    }

    public interface IArmyMissionProtectionModel
    {
        bool IsProtected(MobileParty party);
        bool MayStartReinforcement(MobileParty party);
    }

    public interface IDemobilizationPolicy
    {
        bool CanDismiss(MobileParty party, int count);
    }

    public interface IPeaceDemobilizationModel
    {
        PeaceDemobilizationDecision Evaluate(
            MobileParty party,
            PeaceDemobilizationContext context);
    }
}
