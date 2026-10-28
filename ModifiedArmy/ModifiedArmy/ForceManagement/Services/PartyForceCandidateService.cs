using HarmonyLib;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Diagnostics;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyManagement.Finance.Models;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Builds read-only recruitment candidates across every source and
    /// settlement. It does not reserve money, Hearth, offers, or soldiers.
    /// </summary>
    public sealed class PartyForceCandidateService
    {
        /// <summary>Captures all pure inputs required by the daily planner.</summary>
        public PartyForcePlanningContext BuildContext(
            MobileParty party,
            PartyForceTask currentTask,
            bool randomGateOpen)
        {
            MilitaryConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig();
            PartyForceTargetResult target = ForceManagementFacade.Current
                .GetPartyTarget(party);
            bool atWar = ForceManagementFacade.IsAtFormalWar(party?.ActualClan);
            PartyForceSourceWeights weights = GetSourceWeights(
                party,
                config,
                atWar);
            return new PartyForcePlanningContext
            {
                Party = party,
                CurrentTask = currentTask,
                ForceTarget = target,
                IsAtFormalWar = atWar,
                WarRest = ForceManagementFacade.Current.WarRest.Evaluate(
                    ForceManagementFacade.Current.WarRestPlanning.BuildContext(
                        party,
                        target,
                        IsProtected(party))),
                Finance = GetFinance(party),
                SourceWeights = weights,
                Candidates = EnumerateCandidates(
                    party,
                    target,
                    weights,
                    config),
                RecruitmentNeedScore = CalculateNeedScore(
                    party,
                    target,
                    config,
                    atWar),
                RecruitmentNeedThreshold = config.AiNeedThreshold,
                IsDailyRandomGateOpen = randomGateOpen,
                IsRandomGateOpen = randomGateOpen &&
                    !ForceManagementFacade.Current.WarRestPlanning
                        .ShouldSuppressRecruitment(party),
                IsProtectedByHigherPriorityOrder = IsProtected(party),
                Today = CurrentDay()
            };
        }

        /// <summary>Enumerates all feasible source/settlement combinations.</summary>
        public IReadOnlyList<PartyForceCandidate> EnumerateCandidates(
            MobileParty party,
            PartyForceTargetResult target,
            PartyForceSourceWeights weights,
            MilitaryConfig config)
        {
            var result = new List<PartyForceCandidate>();
            if (party?.ActualClan == null || target?.GapCount <= 0)
                return result;
            FiefPartyManager fiefs = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            foreach (Settlement settlement in Settlement.All
                .Where(item => item != null)
                .OrderBy(item => item.StringId, StringComparer.Ordinal))
            {
                if (settlement.IsUnderSiege) continue;
                if (!TryGetTravelDistance(party, settlement, out float distance))
                    continue;
                if (settlement.OwnerClan == party.ActualClan &&
                    settlement.IsFortification)
                {
                    AddFiefCandidate(
                        result, party, target, weights, config,
                        fiefs, settlement, distance);
                    AddProfessionalCandidate(
                        result, party, target, weights, config,
                        settlement, distance);
                }
                if (settlement.IsTown && IsMercenaryDestinationAllowed(
                        party,
                        settlement,
                        distance,
                        config))
                {
                    AddMercenaryCandidate(
                        result, party, target, weights, config,
                        settlement, distance);
                }
            }
            return result;
        }

        /// <summary>Adds one exact fief preview when its minimum batch is viable.</summary>
        private static void AddFiefCandidate(
            ICollection<PartyForceCandidate> result,
            MobileParty party,
            PartyForceTargetResult target,
            PartyForceSourceWeights weights,
            MilitaryConfig config,
            FiefPartyManager manager,
            Settlement settlement,
            float distance)
        {
            if (manager == null) return;
            int approved = manager.PreviewFiefRecruitmentApprovals(
                settlement,
                party,
                target.GapCount).Values.Sum();
            if (approved <= 0) return;
            float culture = party.LeaderHero?.Culture == settlement.Culture
                ? config.AiCultMatchFiefBonus
                : 1f;
            result.Add(CreateCandidate(
                settlement, PartyForceSourceKind.Fief, approved,
                target.GapCount, weights.Fief, culture, distance, 0f));
        }

        /// <summary>Adds a safe garrison-transfer candidate after model admission.</summary>
        private static void AddProfessionalCandidate(
            ICollection<PartyForceCandidate> result,
            MobileParty party,
            PartyForceTargetResult target,
            PartyForceSourceWeights weights,
            MilitaryConfig config,
            Settlement settlement,
            float distance)
        {
            MobileParty garrison = settlement.Town?.GarrisonParty;
            int safe = GarrisonTransferPolicyModel
                .GetTransferableGarrisonTroopCount(settlement);
            GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                .GarrisonDefense.Evaluate(settlement);
            if (garrison == null)
            {
                ForceRecruitmentDiagnostics.LogGarrisonCandidate(
                    party, settlement, 0,
                    defense?.ActiveSupplyEscortCount ?? 0,
                    defense?.EffectiveGarrisonCount ?? 0,
                    defense?.MinimumCount ?? 0,
                    safe, 0, 0, 0, target.GapCount);
                return;
            }
            Dictionary<CharacterObject, int> protectedCounts = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetServingTroopCounts(garrison) ?? new Dictionary<CharacterObject, int>();
            List<RecruitmentCandidate> offers = garrison.MemberRoster
                .GetTroopRoster()
                .Where(item => item.Character != null && !item.Character.IsHero)
                .Select(item => new RecruitmentCandidate(
                    item.Character,
                    Math.Max(0, item.Number - GetCount(
                        protectedCounts,
                        item.Character)),
                    RecruitmentSource.Transfer))
                .Where(item => item.AvailableCount > 0)
                .ToList();
            int approved = safe <= 0 ? 0 :
                RecruitmentModelManager.Model.BuildPlan(party, offers)
                    .Evaluations.Sum(item => Math.Max(0, item.RecruitableCount));
            approved = Math.Min(target.GapCount, Math.Min(safe, approved));
            ForceRecruitmentDiagnostics.LogGarrisonCandidate(
                party,
                settlement,
                garrison.MemberRoster.TotalRegulars,
                defense?.ActiveSupplyEscortCount ?? 0,
                defense?.EffectiveGarrisonCount ?? 0,
                defense?.MinimumCount ?? 0,
                safe,
                protectedCounts.Values.Sum(value => Math.Max(0, value)),
                offers.Sum(item => Math.Max(0, item.AvailableCount)),
                approved,
                target.GapCount);
            if (approved <= 0) return;
            float culture = party.LeaderHero?.Culture == settlement.Culture
                ? 1f
                : config.AiCultMismatchGarrisonTransferPenalty;
            result.Add(CreateCandidate(
                settlement, PartyForceSourceKind.Professional, approved,
                target.GapCount, weights.Professional, culture, distance, 0f));
        }

        /// <summary>Adds a mercenary offer after capacity, wage, and cost admission.</summary>
        private static void AddMercenaryCandidate(
            ICollection<PartyForceCandidate> result,
            MobileParty party,
            PartyForceTargetResult target,
            PartyForceSourceWeights weights,
            MilitaryConfig config,
            Settlement settlement,
            float distance)
        {
            if (!TryGetMercenaryOffer(
                    settlement,
                    out CharacterObject troop,
                    out int available))
                return;
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                party,
                new[]
                {
                    new RecruitmentCandidate(
                        troop,
                        Math.Min(available, target.GapCount),
                        RecruitmentSource.Mercenary)
                });
            int approved = plan.Evaluations.Sum(item =>
                Math.Max(0, item.RecruitableCount));
            if (approved <= 0) return;
            float unitCost = Campaign.Current.Models.PartyWageModel
                .GetTroopRecruitmentCost(troop, party.LeaderHero, false)
                .RoundedResultNumber;
            result.Add(CreateCandidate(
                settlement, PartyForceSourceKind.Mercenary, approved,
                target.GapCount, weights.Mercenary, 1f, distance,
                Math.Max(0f, unitCost * approved)));
        }

        /// <summary>Creates a candidate using fixed and explainable score terms.</summary>
        private static PartyForceCandidate CreateCandidate(
            Settlement settlement,
            PartyForceSourceKind source,
            int available,
            int gap,
            float preference,
            float culture,
            float distance,
            float economicImpact)
        {
            int covered = Math.Min(Math.Max(0, gap), Math.Max(0, available));
            float coverage = gap <= 0 ? 0f : (float)covered / gap;
            float travelCost = Math.Max(0f, distance) * 0.05f;
            float economicPenalty = economicImpact <= 0f
                ? 0f
                : Math.Min(25f, economicImpact / 1000f);
            return new PartyForceCandidate
            {
                Settlement = settlement,
                SourceKind = source,
                AvailableCount = Math.Max(0, available),
                GapCovered = covered,
                PreferenceScore = Math.Max(0f, preference),
                CultureMatchFactor = Math.Max(0f, culture),
                EffectScore = coverage,
                Distance = Math.Max(0f, distance),
                TravelCost = travelCost,
                RouteRisk = 0f,
                EconomicImpact = Math.Max(0f, economicImpact),
                IsSafe = true,
                ReasonCode = "Feasible",
                Score = preference * culture * 100f + coverage * 80f -
                    travelCost - economicPenalty
            };
        }

        /// <summary>Calculates cultural and situational source weights.</summary>
        private static PartyForceSourceWeights GetSourceWeights(
            MobileParty party,
            MilitaryConfig config,
            bool atWar)
        {
            var basis = config.GetAiCulturalPreference(
                party?.LeaderHero?.Culture);
            var situation = config.GetAiSituationalModifier(
                atWar ? "war" : "peace");
            float fief = basis.Fief * situation.Fief;
            float professional = basis.GarrisonTransfer *
                situation.GarrisonTransfer;
            float mercenary = basis.Mercenary * situation.Mercenary;
            ApplyConditionalModifier(
                config,
                "siege",
                party?.ActualClan?.Settlements.Any(item =>
                    item?.IsUnderSiege == true) == true,
                ref fief,
                ref professional,
                ref mercenary);
            ApplyConditionalModifier(
                config,
                "broke",
                IsBroke(party, config),
                ref fief,
                ref professional,
                ref mercenary);
            ApplyConditionalModifier(
                config,
                "critical",
                party != null && party.PartySizeRatio <
                    config.GetAiSituationalThreshold("critical"),
                ref fief,
                ref professional,
                ref mercenary);
            float total = fief + professional + mercenary;
            if (total <= 0.0001f)
                return new PartyForceSourceWeights
                    { Fief = 1f / 3f, Professional = 1f / 3f, Mercenary = 1f / 3f };
            return new PartyForceSourceWeights
            {
                Fief = fief / total,
                Professional = professional / total,
                Mercenary = mercenary / total
            };
        }

        /// <summary>Applies one configured multiplier without changing XML.</summary>
        private static void ApplyConditionalModifier(
            MilitaryConfig config,
            string id,
            bool apply,
            ref float fief,
            ref float professional,
            ref float mercenary)
        {
            if (!apply) return;
            var modifier = config.GetAiSituationalModifier(id);
            fief *= modifier.Fief;
            professional *= modifier.GarrisonTransfer;
            mercenary *= modifier.Mercenary;
        }

        /// <summary>Calculates the existing configurable recruitment urgency.</summary>
        private static float CalculateNeedScore(
            MobileParty party,
            PartyForceTargetResult target,
            MilitaryConfig config,
            bool atWar)
        {
            if (party == null || target == null || target.GapCount <= 0)
                return 0f;
            float gap = target.TargetCount <= 0 ? 0f : Math.Min(
                1f,
                (float)target.GapCount / target.TargetCount);
            bool underSiege = party.ActualClan?.Settlements.Any(item =>
                item?.IsUnderSiege == true) == true;
            float urgency = underSiege
                ? config.AiNeedSiegeUrgency
                : atWar
                    ? config.AiNeedWarUrgency
                    : config.AiNeedPeaceUrgency;
            float funds = RecruitmentBudget.GetOperatingFunds(party);
            float economy = party.TotalWage <= 0 ? 1f : Math.Min(
                1f,
                funds / (party.TotalWage * config.AiNeedEconomicWeeks));
            return config.AiNeedManpowerWeight * gap +
                config.AiNeedWarUrgencyWeight * urgency +
                config.AiNeedEconomicWeight * economy;
        }

        /// <summary>Captures the clan finances without committing a budget.</summary>
        private static ClanFinanceSnapshot GetFinance(MobileParty party) =>
            new ClanFinanceSnapshot
            {
                Clan = party?.ActualClan,
                OperatingFunds = RecruitmentBudget.GetOperatingFunds(party),
                CurrentDailyWage = party == null ? 0f : party.TotalWage,
                AvailableRecruitmentBudget = party == null
                    ? 0f
                    : AiRecruitmentFinancialModel
                        .GetAvailableAdditionalDailyWage(party)
            };

        /// <summary>Protects parties controlled by higher-priority game systems.</summary>
        private static bool IsProtected(MobileParty party) => party == null ||
            party == MobileParty.MainParty || !party.IsLordParty ||
            party.IsDisbanding || party.Army != null || party.MapEvent != null ||
            party.TargetParty != null || party.ShortTermTargetParty != null ||
            party.Party.IsStarving || party.CurrentSettlement?.SiegeEvent != null ||
            TransportThreatModel.IsNativeAvoidanceActive(party);

        /// <summary>
        /// Uses Bannerlord's navigation-aware distance model and treats an
        /// invalid/infinite result as unreachable. No parallel pathfinder is
        /// introduced by the force planner.
        /// </summary>
        private static bool TryGetTravelDistance(
            MobileParty party,
            Settlement settlement,
            out float distance)
        {
            distance = float.MaxValue;
            if (party == null || settlement == null || Campaign.Current == null)
                return false;
            try
            {
                distance = Campaign.Current.Models.MapDistanceModel.GetDistance(
                    party,
                    settlement,
                    false,
                    party.NavigationCapability,
                    out float _);
                return !float.IsNaN(distance) &&
                    !float.IsInfinity(distance) && distance < float.MaxValue;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Checks whether a mercenary town is friendly and in range.</summary>
        private static bool IsMercenaryDestinationAllowed(
            MobileParty party,
            Settlement settlement,
            float distance,
            MilitaryConfig config)
        {
            IFaction owner = settlement.OwnerClan?.MapFaction;
            if (party.MapFaction != null && owner != null &&
                FactionManager.IsAtWarAgainstFaction(party.MapFaction, owner))
                return false;
            return owner == party.MapFaction ||
                distance <= config.AiMercenarySearchRadius;
        }

        /// <summary>Reads the native mercenary offer without consuming it.</summary>
        private static bool TryGetMercenaryOffer(
            Settlement settlement,
            out CharacterObject troop,
            out int available)
        {
            troop = null;
            available = 0;
            RecruitmentCampaignBehavior behavior = Campaign.Current?
                .GetCampaignBehavior<RecruitmentCampaignBehavior>();
            if (behavior == null || settlement?.Town == null) return false;
            try
            {
                object data = Traverse.Create(behavior)
                    .Method("GetMercenaryData", settlement.Town).GetValue();
                if (data == null || !Traverse.Create(data)
                    .Method("HasAvailableMercenary", Occupation.Mercenary)
                    .GetValue<bool>())
                    return false;
                troop = Traverse.Create(data).Property("TroopType")
                    .GetValue<CharacterObject>();
                available = Traverse.Create(data).Property("Number")
                    .GetValue<int>();
                return troop != null && available > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Reports whether the clan cannot comfortably fund wages.</summary>
        private static bool IsBroke(MobileParty party, MilitaryConfig config) =>
            party != null && party.TotalWage > 0 &&
            RecruitmentBudget.GetOperatingFunds(party) < party.TotalWage *
                config.GetAiSituationalThreshold("broke");

        /// <summary>Reads a protected count safely.</summary>
        private static int GetCount(
            IReadOnlyDictionary<CharacterObject, int> counts,
            CharacterObject troop) => counts != null && troop != null &&
            counts.TryGetValue(troop, out int value) ? Math.Max(0, value) : 0;

        /// <summary>Returns the current campaign day for stable task versions.</summary>
        private static int CurrentDay() => Campaign.Current == null
            ? 0
            : (int)Math.Floor(CampaignTime.Now.ToDays);
    }
}
