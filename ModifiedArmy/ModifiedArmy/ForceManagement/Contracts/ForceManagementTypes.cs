using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Contracts
{
    public enum ForceScenario
    {
        Peace,
        WarRoutine,
        ArmyGathering,
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

    public enum ReinforcementPriority
    {
        None,
        Routine,
        Urgent,
        Emergency
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
        public bool IsArmyGathering { get; set; }
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
        public float CurrentDefenseValue { get; set; }
        public float RequiredDefenseValue { get; set; }
        public int CurrentRegularCount { get; set; }
        public int HardLimit { get; set; }
        public int EscortReservedCount { get; set; }
        public float LocalSevenDayRecoveryValue { get; set; }
        public float SimulatedFoodDays { get; set; }
        public bool HasVerifiedActualFoodConsumption { get; set; }
    }

    public sealed class ReinforcementNeedResult
    {
        public ReinforcementPriority Priority { get; set; }
        public string ReasonCode { get; set; }

        public static ReinforcementNeedResult NoRequest(string reason) =>
            new ReinforcementNeedResult
            {
                Priority = ReinforcementPriority.None,
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

        public static ExchangePlan NoExchange(string reason) =>
            new ExchangePlan { ReasonCode = reason };
    }

    public sealed class RouteEvaluation
    {
        public bool IsSafe { get; set; }
        public string ReasonCode { get; set; }
    }

    public sealed class TroopCargoManifest
    {
        public Dictionary<CharacterObject, int> OwnTroops { get; } =
            new Dictionary<CharacterObject, int>();
        public Dictionary<CharacterObject, int> GarrisonTroops { get; } =
            new Dictionary<CharacterObject, int>();
    }

    public sealed class FoodCargoManifest
    {
        public Dictionary<ItemObject, int> PlannedItems { get; } =
            new Dictionary<ItemObject, int>();
        public int LoadedFood { get; set; }
        public int DeliveredFood { get; set; }
    }

    public sealed class ReinforcementMission
    {
        public MobileParty Carrier { get; set; }
        public Settlement Source { get; set; }
        public Settlement Destination { get; set; }
        public ReinforcementMissionState State { get; set; }
        public ReinforcementCancelReason CancelReason { get; set; }
        public TroopCargoManifest Troops { get; } =
            new TroopCargoManifest();
        public FoodCargoManifest Food { get; } =
            new FoodCargoManifest();

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
}
