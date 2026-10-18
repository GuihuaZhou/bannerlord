using ModifiedArmy.common;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Garrison.Models;
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
            else if (context?.IsArmyGathering == true)
            {
                scenario = ForceScenario.ArmyGathering;
                ratio = Math.Max(
                    config.ArmyGatheringTargetRatio,
                    atWar ? GetWarTargetRatio(disposition) : 0f);
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

            ForceManagementConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig().AiRecruitment.ForceManagement;
            int ceiling;
            if (context.Operation == ForceOperation.NewRecruitment)
            {
                ceiling = Math.Min(context.HardLimit, context.TargetCount);
            }
            else if (context.Operation == ForceOperation.FieldToGarrison ||
                context.Operation == ForceOperation.GarrisonToField ||
                context.Operation == ForceOperation.Swap)
            {
                ceiling = Math.Min(
                    context.HardLimit,
                    (int)Math.Floor(
                        context.HardLimit *
                            config.ExistingTroopTransferRatio));
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
            int current = garrison?.Party.NumberOfRegularMembers ?? 0;
            int hardLimit = garrison?.Party.PartySizeLimit ?? 0;
            int minimum = GarrisonTransferPolicyModel
                .IsEnemyBorderFortification(settlement)
                    ? Math.Max(current, (int)Math.Ceiling(hardLimit * 0.5f))
                    : (int)Math.Ceiling(hardLimit * 0.4f);
            return new GarrisonDefenseSnapshot
            {
                Settlement = settlement,
                CurrentDefenseValue = current,
                RequiredDefenseValue = minimum,
                CurrentRegularCount = current,
                HardLimit = hardLimit,
                EscortReservedCount = GarrisonRecruitmentCapacity
                    .GetActiveSupplyEscortCount(settlement),
                HasVerifiedActualFoodConsumption = false
            };
        }

        public int GetSafeTransferOutCount(
            GarrisonDefenseSnapshot snapshot,
            TroopRoster roster)
        {
            if (snapshot == null || roster == null ||
                GarrisonTransferPolicyModel.IsEnemyBorderFortification(
                    snapshot.Settlement))
            {
                return 0;
            }

            return Math.Max(
                0,
                snapshot.CurrentRegularCount -
                    (int)Math.Ceiling(snapshot.RequiredDefenseValue));
        }
    }

    public sealed class ExternalReinforcementNeedStubModel
        : IExternalReinforcementNeedModel
    {
        public ReinforcementNeedResult Evaluate(GarrisonDefenseSnapshot snapshot)
            => ReinforcementNeedResult.NoRequest(
                "ThresholdPolicyNotImplemented");
    }

    public sealed class ReinforcementRouteStubModel
        : IReinforcementRouteModel
    {
        public RouteEvaluation Evaluate(
            MobileParty carrier,
            Settlement source,
            Settlement destination) => new RouteEvaluation
            {
                IsSafe = false,
                ReasonCode = "RouteUnsafeOrUnknown"
            };
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
