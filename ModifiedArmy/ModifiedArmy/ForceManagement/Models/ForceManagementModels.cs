using ModifiedArmy.common;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Models
{
    public sealed class PartyForceTargetModel : IPartyForceTargetModel
    {
        private readonly IWarDispositionProvider _warDisposition;

        public PartyForceTargetModel(IWarDispositionProvider warDisposition)
        {
            _warDisposition = warDisposition;
        }

        public PartyForceTargetResult Evaluate(PartyForceContext context)
        {
            MobileParty party = context?.Party;
            int limit = Math.Max(0, party?.Party.PartySizeLimit ?? 0);
            int current = Math.Max(0, party?.Party.NumberOfAllMembers ?? 0);
            ForceManagementConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig().AiRecruitment.ForceManagement;
            Clan clan = party?.ActualClan;
            bool atWar = IsAtFormalWar(clan);
            float disposition = 0f;
            bool bridgeWar;
            _warDisposition?.TryGet(clan, out disposition, out bridgeWar);
            disposition = Math.Max(-100f, Math.Min(100f, disposition));
            bool aggressive = atWar &&
                disposition > config.AggressiveWarDispositionThreshold;
            ForceScenario scenario;
            float ratio;

            if (context?.IsOwnFiefDefense == true)
            {
                scenario = ForceScenario.OwnFiefDefense;
                ratio = config.OwnFiefDefenseTargetRatio;
            }
            else if (atWar)
            {
                scenario = ForceScenario.WarRoutine;
                ratio = GetWarTargetRatio(disposition);
            }
            else
            {
                scenario = ForceScenario.Peace;
                ratio = config.PeaceTargetRatio;
            }

            int target = (int)Math.Ceiling(limit * ratio);
            if (scenario == ForceScenario.Peace)
            {
                target = Math.Max(config.PeaceMinimumCount, target);
            }
            target = Math.Min(limit, Math.Max(0, target));
            int gap = Math.Max(0, target - current);
            bool canVisit = gap > 0 && party != null && party.IsActive &&
                party.MapEvent == null && !party.Party.IsStarving &&
                scenario != ForceScenario.OwnFiefDefense &&
                party.Army == null;

            return new PartyForceTargetResult
            {
                Scenario = scenario,
                IsAggressiveWarClan = aggressive,
                WarDisposition = disposition,
                PartyLimit = limit,
                CurrentCount = current,
                TargetCount = target,
                GapCount = gap,
                CanStartRecruitmentVisit = canVisit,
                ReasonCode = gap > 0 ? "BelowTarget" : "TargetReached"
            };
        }

        public static float GetWarTargetRatio(float disposition)
        {
            float value = Math.Max(-100f, Math.Min(100f, disposition));
            if (value <= 0f)
            {
                return 0.35f + 0.0015f * (value + 100f);
            }
            if (value <= 20f)
            {
                return 0.50f;
            }
            if (value <= 60f)
            {
                return 0.60f + 0.005f * (value - 20f);
            }
            return 0.80f + 0.00375f * (value - 60f);
        }

        private static bool IsAtFormalWar(Clan clan)
        {
            IFaction faction = clan?.MapFaction;
            if (faction == null)
            {
                return false;
            }

            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom != faction && !kingdom.IsEliminated &&
                    FactionManager.IsAtWarAgainstFaction(faction, kingdom))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public sealed class RecruitmentAdmissionModel
        : IRecruitmentAdmissionModel
    {
        public RecruitmentApproval Evaluate(RecruitmentCapacityContext context)
        {
            if (context == null || context.RequestedCount <= 0)
            {
                return new RecruitmentApproval
                {
                    ReasonCode = "InvalidRequest"
                };
            }

            int ceiling;
            if (context.Operation == ForceOperation.NewRecruitment ||
                context.Operation == ForceOperation.FieldToGarrison ||
                context.Operation == ForceOperation.GarrisonToField)
            {
                ceiling = Math.Min(context.HardLimit, context.TargetCount);
            }
            else
            {
                ceiling = context.HardLimit;
            }
            int approved = Math.Min(
                context.RequestedCount,
                Math.Max(0, ceiling - context.EffectiveCurrentCount));
            return new RecruitmentApproval
            {
                ApprovedCount = approved,
                ReasonCode = approved > 0 ? "Approved" : "CapacityReached"
            };
        }
    }

    public sealed class GarrisonDefenseRequirementModel
        : IGarrisonDefenseRequirementModel
    {
        public GarrisonDefenseSnapshot Evaluate(Settlement settlement)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            int hardLimit = garrison?.Party.PartySizeLimit ?? 0;
            bool enemyBorder = GarrisonTransferPolicyModel
                .IsEnemyBorderFortification(settlement);
            int minimum = Math.Min(
                hardLimit,
                GarrisonRecruitmentCapacity.GetLowerTarget(
                    settlement,
                    hardLimit));
            int escortReserved = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(settlement);
            int effectiveCurrent = GarrisonRecruitmentCapacity
                .GetEffectiveMemberCount(garrison, escortReserved);
            ForceManagementConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig().AiRecruitment.ForceManagement;
            int forecastDays = config.ReinforcementForecastDays;
            int predictedDaily = enemyBorder || settlement?.Town == null
                ? 0
                : GarrisonRecruitFromPrisonersBehavior
                    .GetPredictedDailyRecruitmentCount(settlement.Town);
            GarrisonLogisticsStatus logistics = GarrisonLogisticsModel
                .Calculate(settlement?.Town);
            return new GarrisonDefenseSnapshot
            {
                Settlement = settlement,
                ResidentCount = Math.Max(
                    0,
                    garrison?.Party.NumberOfAllMembers ?? 0),
                EffectiveGarrisonCount = effectiveCurrent,
                MinimumCount = minimum,
                HardLimit = hardLimit,
                ActiveSupplyEscortCount = escortReserved,
                PredictedSevenDayRecruitment = Math.Max(
                    0,
                    predictedDaily * forecastDays),
                SimulatedFoodDays = logistics.FoodSupplyDays,
                IsEnemyBorder = enemyBorder,
                HasVerifiedActualFoodConsumption = false
            };
        }

        public int GetSafeTransferOutCount(
            GarrisonDefenseSnapshot snapshot,
            TroopRoster roster)
        {
            if (snapshot == null || roster == null)
            {
                return 0;
            }

            int establishmentSurplus = Math.Max(
                0,
                snapshot.EffectiveGarrisonCount - snapshot.MinimumCount);
            return Math.Min(
                establishmentSurplus,
                Math.Max(0, roster.TotalRegulars));
        }

    }

    public sealed class ExternalReinforcementNeedModel
        : IExternalReinforcementNeedModel
    {
        public ReinforcementNeedResult Evaluate(GarrisonDefenseSnapshot snapshot)
        {
            if (snapshot?.Settlement?.Town == null)
            {
                return ReinforcementNeedResult.NoRequest("InvalidSettlement");
            }

            int shortage = Math.Max(
                0,
                snapshot.MinimumCount - snapshot.EffectiveGarrisonCount);
            if (shortage <= 0)
            {
                return ReinforcementNeedResult.NoRequest("MinimumCovered");
            }

            if (snapshot.IsEnemyBorder)
            {
                return Request(
                    shortage,
                    "BorderBelowMinimum");
            }

            int forecastShortage = Math.Max(
                0,
                shortage - snapshot.PredictedSevenDayRecruitment);
            return forecastShortage > 0
                ? Request(
                    forecastShortage,
                    "InteriorBelowMinimumAfterForecast")
                : ReinforcementNeedResult.NoRequest(
                    "InteriorForecastCoversMinimum");
        }

        private static ReinforcementNeedResult Request(
            int requestedTroops,
            string reason)
        {
            return new ReinforcementNeedResult
            {
                IsRequested = true,
                RequestedTroops = Math.Max(0, requestedTroops),
                ReasonCode = reason
            };
        }
    }

    public sealed class ReinforcementRouteModel
        : IReinforcementRouteModel
    {
        public RouteEvaluation Evaluate(
            MobileParty carrier,
            Settlement source,
            Settlement destination)
        {
            if (source == null || destination == null ||
                source == destination || source.MapFaction == null ||
                destination.MapFaction == null ||
                source.MapFaction != destination.MapFaction)
                return Unsafe("InvalidRoute");
            float distance = Campaign.Current.Models.MapDistanceModel
                .GetDistance(source, destination, false, false,
                    carrier?.NavigationCapability ??
                        MobileParty.NavigationType.Default);
            if (distance <= 0f || float.IsNaN(distance) ||
                float.IsInfinity(distance) || distance >= Campaign.MapDiagonal)
                return Unsafe("RouteBlocked");
            if (TransportThreatModel.HasSuperiorEnemy(carrier,
                    carrier?.Position ?? source.GatePosition,
                    source.MapFaction))
                return Unsafe("EnemyThreat");
            if (carrier == null &&
                TransportThreatModel.HasSuperiorEnemy(null,
                    destination.GatePosition, destination.MapFaction))
                return Unsafe("EnemyThreat");
            return new RouteEvaluation
            {
                IsSafe = true,
                ReasonCode = "Safe"
            };

            RouteEvaluation Unsafe(string reason) => new RouteEvaluation
            {
                IsSafe = false,
                ReasonCode = reason
            };
        }
    }

    public sealed class BasicTroopValueModel : ITroopValueModel
    {
        public float GetFieldMarginalValue(
            PartyForceSnapshot field,
            CharacterObject troop,
            int count) => GetValue(troop, count);

        public float GetGarrisonMarginalValue(
            GarrisonDefenseSnapshot garrison,
            CharacterObject troop,
            int count) => GetValue(troop, count);

        private static float GetValue(CharacterObject troop, int count) =>
            troop == null ? 0f : Math.Max(0, count) * Math.Max(1, troop.Tier);
    }

    public sealed class DisabledPartyGarrisonExchangeModel
        : IPartyGarrisonExchangeModel
    {
        public ExchangePlan BuildPlan(
            PartyForceSnapshot field,
            GarrisonDefenseSnapshot garrison) =>
            ExchangePlan.NoExchange("AutomaticSwapDisabled");
    }

    /// <summary>
    /// Phase-one planner boundary. It deliberately owns no live tasks until
    /// the daily planning behavior is introduced in the later atomic switch.
    /// </summary>
    public sealed class DisabledAiForcePlanningModel
        : IAiForcePlanningModel
    {
        /// <summary>Returns a visible non-executing result.</summary>
        public PartyForcePlan BuildDailyPlan(
            PartyForcePlanningContext context) =>
            PartyForcePlan.NoPlan("UnifiedForcePlanningDisabled");
    }

    /// <summary>
    /// Phase-one exchange boundary. Existing garrison and fief paths continue
    /// to use their compatibility services until the phase-five switch.
    /// </summary>
    public sealed class DisabledPartyExchangeModel : IPartyExchangeModel
    {
        /// <summary>Rejects every new unified exchange request.</summary>
        public PartyExchangePlan BuildPlan(PartyExchangeContext context) =>
            PartyExchangePlan.Rejected("UnifiedPartyExchangeDisabled");
    }

    /// <summary>
    /// Placeholder for the war-only rest policy defined in phase four.
    /// </summary>
    public sealed class DisabledWarRestPolicyModel : IWarRestPolicyModel
    {
        /// <summary>Refuses rest without inventing unapproved thresholds.</summary>
        public WarRestDecision Evaluate(WarRestContext context) =>
            WarRestDecision.Denied("WarRestPolicyDisabled");
    }

    public sealed class DisabledDemobilizationPolicy
        : IDemobilizationPolicy
    {
        public bool CanDismiss(MobileParty party, int count) => false;
    }

    public sealed class DisabledArmyMissionProtectionModel
        : IArmyMissionProtectionModel
    {
        public bool IsProtected(MobileParty party) => false;

        public bool MayStartReinforcement(MobileParty party)
        {
            return party != null && party.IsActive && party.Army == null &&
                MilitaryConfigManager.Instance.GetActiveConfig()?
                    .AiRecruitment.ForceManagement
                    .EnableAutomaticReinforcement == true;
        }
    }
}
