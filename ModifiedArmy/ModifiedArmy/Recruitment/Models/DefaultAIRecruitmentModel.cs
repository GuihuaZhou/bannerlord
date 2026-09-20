using ModifiedArmy.PartyFinance.Models;
using ModifiedArmy.Recruitment.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Builds culture-aware recruitment plans constrained by party capacity,
    /// composition ratios, wage limits and combined thirty-day affordability.
    /// </summary>
    public sealed class DefaultAIRecruitmentModel : AIRecruitmentModel
    {
        private readonly RecruitmentTemplateRepository _templates;

        public DefaultAIRecruitmentModel(
            RecruitmentTemplateRepository templates)
        {
            _templates = templates
                ?? throw new ArgumentNullException(nameof(templates));
        }

        /// <summary>
        /// Sorts all candidates once, evaluates them against a shared simulated
        /// state, and commits only approved quantities to that simulation. The
        /// returned plan does not alter the real party or any recruitment pool.
        /// </summary>
        public override RecruitmentPlan BuildPlan(
            MobileParty party,
            IReadOnlyList<RecruitmentCandidate> candidates)
        {
            RecruitmentPartyType partyType = GetPartyType(party);
            string cultureId = GetCultureId(party, partyType);
            RecruitmentSimulationState state =
                RecruitmentSimulationState.Create(party);
            List<RecruitmentEvaluationResult> results =
                new List<RecruitmentEvaluationResult>();

            if (candidates == null || candidates.Count == 0)
            {
                return new RecruitmentPlan(
                    partyType,
                    cultureId,
                    state,
                    results);
            }

            List<RecruitmentCandidate> orderedCandidates = candidates
                .Where(candidate => candidate != null)
                .OrderByDescending(candidate => GetPriority(
                    party,
                    candidate.Troop,
                    candidate.Source,
                    state))
                .ThenByDescending(candidate => candidate.Troop?.Tier ?? 0)
                .ToList();

            foreach (RecruitmentCandidate candidate in orderedCandidates)
            {
                RecruitmentEvaluationResult evaluation =
                    EvaluateRecruitment(
                        party,
                        candidate.Troop,
                        candidate.AvailableCount,
                        candidate.Source,
                        state);

                results.Add(evaluation);

                if (evaluation.RecruitableCount > 0)
                {
                    state.Commit(
                        evaluation.Troop,
                        evaluation.RecruitableCount,
                        evaluation.UnitDailyWage,
                        evaluation.UnitRecruitmentCost);
                }
            }

            return new RecruitmentPlan(
                partyType,
                cultureId,
                state,
                results);
        }

        /// <summary>
        /// Calculates every independent quantity limit for one candidate and
        /// returns their minimum. Callers can inspect PrimaryLimit to explain
        /// why the requested quantity was reduced or rejected.
        /// </summary>
        public override RecruitmentEvaluationResult EvaluateRecruitment(
            MobileParty party,
            CharacterObject troop,
            int availableCount,
            RecruitmentSource source,
            RecruitmentSimulationState state)
        {
            RecruitmentEvaluationResult result =
                CreateBaseResult(troop, source, availableCount);

            if (party == null || state == null || !party.IsActive)
            {
                result.PrimaryLimit = RecruitmentLimitReason.InvalidParty;
                return result;
            }

            if (troop == null || troop.IsHero || availableCount <= 0)
            {
                result.PrimaryLimit = RecruitmentLimitReason.InvalidTroop;
                return result;
            }

            RecruitmentPartyType partyType = GetPartyType(party);
            ArmyCompositionTemplate template = _templates.GetTemplate(
                GetCultureId(party, partyType),
                partyType);
            CombatRole role =
                RecruitmentTroopClassifier.GetCombatRole(troop);
            TroopQuality quality =
                RecruitmentTroopClassifier.GetQuality(troop);
            float unitWage =
                AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                    party,
                    troop);
            int unitCost = source == RecruitmentSource.Fief
                ? 0
                : Campaign.Current.Models.PartyWageModel
                    .GetTroopRecruitmentCost(
                        troop,
                        party.LeaderHero,
                        false)
                    .RoundedResultNumber;

            result.CombatRole = role;
            result.Quality = quality;
            result.UnitDailyWage = unitWage;
            result.UnitRecruitmentCost = Math.Max(0, unitCost);
            result.Priority = GetPriority(
                party,
                troop,
                source,
                state);
            result.AllowedByPartySize = Math.Max(
                0,
                state.PartySizeLimit - state.ProjectedMemberCount);
            result.AllowedByCombatRole = GetAllowedByMaximum(
                state.PartySizeLimit,
                state.GetCount(role),
                template.GetRange(role));
            result.AllowedByQuality = GetAllowedByMaximum(
                state.PartySizeLimit,
                state.GetCount(quality),
                template.GetRange(quality));
            result.AllowedByWageLimit = GetAllowedByWageLimit(
                state,
                unitWage);
            result.AllowedByMaintenance = GetAllowedByMaintenance(
                state,
                unitWage,
                result.UnitRecruitmentCost);

            result.RecruitableCount = Minimum(
                result.RequestedCount,
                result.AllowedByPartySize,
                result.AllowedByCombatRole,
                result.AllowedByQuality,
                result.AllowedByWageLimit,
                result.AllowedByMaintenance);
            result.PrimaryLimit = FindPrimaryLimit(result);
            result.SustainableDays = GetSustainableDays(
                state,
                result.RecruitableCount,
                unitWage,
                result.UnitRecruitmentCost);
            return result;
        }

        /// <summary>
        /// Gives priority to missing combat roles and quality groups. Tier is
        /// a small tie-breaker, while mercenaries receive a small penalty so
        /// equal ordinary troops are considered first.
        /// </summary>
        private float GetPriority(
            MobileParty party,
            CharacterObject troop,
            RecruitmentSource source,
            RecruitmentSimulationState state)
        {
            if (party == null || troop == null || state == null)
            {
                return float.MinValue;
            }

            RecruitmentPartyType partyType = GetPartyType(party);
            ArmyCompositionTemplate template = _templates.GetTemplate(
                GetCultureId(party, partyType),
                partyType);
            CombatRole role =
                RecruitmentTroopClassifier.GetCombatRole(troop);
            TroopQuality quality =
                RecruitmentTroopClassifier.GetQuality(troop);
            float roleNeed = GetNormalizedShortage(
                state.PartySizeLimit,
                state.GetCount(role),
                template.GetRange(role));
            float qualityNeed = GetNormalizedShortage(
                state.PartySizeLimit,
                state.GetCount(quality),
                template.GetRange(quality));
            float tierPreference = Math.Max(0, troop.Tier) * 0.05f;
            float sourcePenalty = source == RecruitmentSource.Mercenary
                ? 0.10f
                : 0f;

            return roleNeed + qualityNeed + tierPreference - sourcePenalty;
        }

        private static RecruitmentEvaluationResult CreateBaseResult(
            CharacterObject troop,
            RecruitmentSource source,
            int availableCount)
        {
            return new RecruitmentEvaluationResult
            {
                Troop = troop,
                Source = source,
                RequestedCount = Math.Max(0, availableCount)
            };
        }

        /// <summary>
        /// Converts a hard maximum ratio into remaining recruitable headcount.
        /// Counts are based on PartySizeLimit rather than current party size.
        /// </summary>
        private static int GetAllowedByMaximum(
            int sizeLimit,
            int currentCount,
            RatioRange range)
        {
            int maximumCount = (int)Math.Floor(
                sizeLimit * range.MaximumRatio);
            return Math.Max(0, maximumCount - currentCount);
        }

        /// <summary>
        /// Returns a normalized value from zero to one for the unfilled soft
        /// minimum. This affects ordering but never forces recruitment.
        /// </summary>
        private static float GetNormalizedShortage(
            int sizeLimit,
            int currentCount,
            RatioRange range)
        {
            int minimumCount = (int)Math.Floor(
                sizeLimit * range.MinimumRatio);

            if (minimumCount <= 0)
            {
                return 0f;
            }

            return Math.Max(0, minimumCount - currentCount)
                / (float)minimumCount;
        }

        /// <summary>
        /// Limits recruitment so projected wages use at most ninety percent
        /// of the party wage-payment ceiling.
        /// </summary>
        private static int GetAllowedByWageLimit(
            RecruitmentSimulationState state,
            float unitWage)
        {
            if (unitWage <= 0f)
            {
                return int.MaxValue;
            }

            float remaining = state.Budget.UsableWageLimit
                - state.ProjectedDailyWage;
            return Math.Max(0, (int)Math.Floor(remaining / unitWage));
        }

        /// <summary>
        /// Combines immediate recruitment cost with the complete projected
        /// party wage for the configured maintenance period.
        /// </summary>
        private static int GetAllowedByMaintenance(
            RecruitmentSimulationState state,
            float unitWage,
            int unitCost)
        {
            float existingCommitment = state.CommittedRecruitmentCost
                + state.ProjectedDailyWage * state.Budget.MaintenanceDays;
            float remaining = state.Budget.SpendableFunds
                - existingCommitment;
            float unitCommitment = unitCost
                + unitWage * state.Budget.MaintenanceDays;

            if (unitCommitment <= 0f)
            {
                return int.MaxValue;
            }

            return Math.Max(
                0,
                (int)Math.Floor(remaining / unitCommitment));
        }

        /// <summary>
        /// Reports how many days the remaining spendable funds can support the
        /// projected wage after the approved recruitment purchase.
        /// </summary>
        private static int GetSustainableDays(
            RecruitmentSimulationState state,
            int approvedCount,
            float unitWage,
            int unitCost)
        {
            float remainingFunds = state.Budget.SpendableFunds
                - state.CommittedRecruitmentCost
                - approvedCount * unitCost;
            float projectedWage = state.ProjectedDailyWage
                + approvedCount * unitWage;

            if (projectedWage <= 0f)
            {
                return int.MaxValue;
            }

            return Math.Max(
                0,
                (int)Math.Floor(remainingFunds / projectedWage));
        }

        /// <summary>
        /// Identifies the first hard constraint equal to the final approved
        /// count. The fixed order keeps diagnostic logs deterministic on ties.
        /// </summary>
        private static RecruitmentLimitReason FindPrimaryLimit(
            RecruitmentEvaluationResult result)
        {
            if (result.RecruitableCount >= result.RequestedCount)
            {
                return RecruitmentLimitReason.None;
            }

            if (result.RecruitableCount == result.AllowedByPartySize)
            {
                return RecruitmentLimitReason.PartySize;
            }

            if (result.RecruitableCount == result.AllowedByCombatRole)
            {
                return RecruitmentLimitReason.CombatRole;
            }

            if (result.RecruitableCount == result.AllowedByQuality)
            {
                return RecruitmentLimitReason.Quality;
            }

            if (result.RecruitableCount == result.AllowedByWageLimit)
            {
                return RecruitmentLimitReason.WageLimit;
            }

            return RecruitmentLimitReason.MaintenanceFunds;
        }

        private static int Minimum(params int[] values)
        {
            int result = int.MaxValue;

            foreach (int value in values)
            {
                result = Math.Min(result, value);
            }

            return result;
        }

        /// <summary>
        /// Resolves which of the two supported composition templates applies.
        /// </summary>
        private static RecruitmentPartyType GetPartyType(MobileParty party)
        {
            return party?.IsGarrison == true
                ? RecruitmentPartyType.Garrison
                : RecruitmentPartyType.MobileParty;
        }

        /// <summary>
        /// Mobile parties follow clan culture. Garrisons follow their current
        /// owner culture and fall back to local settlement culture after an
        /// ownership transition with incomplete data.
        /// </summary>
        private static string GetCultureId(
            MobileParty party,
            RecruitmentPartyType partyType)
        {
            if (partyType == RecruitmentPartyType.Garrison)
            {
                return party?.CurrentSettlement?.OwnerClan?.Culture?.StringId
                    ?? party?.CurrentSettlement?.Culture?.StringId
                    ?? "default";
            }

            return party?.ActualClan?.Culture?.StringId
                ?? party?.LeaderHero?.Culture?.StringId
                ?? "default";
        }
    }
}
