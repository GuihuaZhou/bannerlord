using ModifiedArmy.PartyManagement.Finance.Models;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Builds recruitment plans constrained by party capacity and the shared
    /// source-aware finance policy. Fief mobilization grows projected strength
    /// without creating a purchase or thirty-day recruitment commitment.
    /// </summary>
    public class DefaultAiTroopRecruitmentModel : AiTroopRecruitmentModel
    {
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
            RecruitmentSimulationState state = RecruitmentSimulationState.Create(party);
            int target = GetRecruitmentTarget(party, state, RecruitmentSource.Fief);
            RecruitmentSubject subject = RecruitmentSubject.FromLiveParty(party, target);
            return BuildPlanCore(subject, candidates, state, int.MaxValue,
                partyType, cultureId);
        }

        internal RecruitmentPlan BuildPlanCore(
            RecruitmentSubject subject,
            IReadOnlyList<RecruitmentCandidate> candidates,
            RecruitmentSimulationState state,
            int requestLimit,
            RecruitmentPartyType partyType = RecruitmentPartyType.MobileParty,
            string cultureId = "default")
        {
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
                    subject,
                    candidate.Troop,
                    candidate.Source,
                    state))
                .ThenByDescending(candidate => candidate.Troop?.Tier ?? 0)
                .ToList();

            foreach (RecruitmentCandidate candidate in orderedCandidates)
            {
                if (requestLimit <= state.ProjectedRegularCount -
                    Math.Max(0, subject?.CurrentRegularCount ?? 0)) break;
                int available = Math.Min(candidate.AvailableCount,
                    Math.Max(0, requestLimit - (state.ProjectedRegularCount -
                        Math.Max(0, subject?.CurrentRegularCount ?? 0))));
                RecruitmentEvaluationResult evaluation =
                    EvaluateRecruitmentCore(
                        subject,
                        candidate.Troop,
                        available,
                        candidate.Source,
                        state);
                evaluation.Candidate = candidate;

                results.Add(evaluation);

                if (evaluation.RecruitableCount > 0)
                {
                    state.Commit(
                        evaluation.Troop,
                        evaluation.RecruitableCount,
                        evaluation.UnitDailyWage,
                        evaluation.UnitRecruitmentCost,
                        evaluation.Source);
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
            int target = GetRecruitmentTarget(party, state, source);
            return EvaluateRecruitmentCore(
                RecruitmentSubject.FromLiveParty(party, target),
                troop, availableCount, source, state);
        }

        internal RecruitmentEvaluationResult EvaluateRecruitmentCore(
            RecruitmentSubject subject,
            CharacterObject troop,
            int availableCount,
            RecruitmentSource source,
            RecruitmentSimulationState state)
        {
            RecruitmentEvaluationResult result =
                CreateBaseResult(troop, source, availableCount);

            if (subject == null || state == null ||
                !subject.IsActiveForAdmission)
            {
                result.PrimaryLimit = RecruitmentLimitReason.InvalidParty;
                return result;
            }

            if (troop == null || troop.IsHero || availableCount <= 0)
            {
                result.PrimaryLimit = RecruitmentLimitReason.InvalidTroop;
                return result;
            }

            if (RecruitmentTroopEligibilityPolicy
                .ShouldExcludeBanditForAiRecruitment(subject, troop))
            {
                result.PrimaryLimit = RecruitmentLimitReason.BanditTroopExcluded;
                return result;
            }

            int recruitmentTarget = Math.Min(
                state.PartySizeLimit,
                subject.TargetCount);
            ForceOperation operation = source == RecruitmentSource.Transfer
                ? subject.IsGarrison
                    ? ForceOperation.FieldToGarrison
                    : ForceOperation.GarrisonToField
                : ForceOperation.NewRecruitment;
            RecruitmentApproval capacityApproval =
                ForceManagementFacade.Current.RecruitmentAdmission.Evaluate(
                    new RecruitmentCapacityContext
                    {
                        Operation = operation,
                        RequestedCount = availableCount,
                        HardLimit = state.PartySizeLimit,
                        EffectiveCurrentCount =
                            state.EffectiveProjectedMemberCount,
                        TargetCount = recruitmentTarget
                    });
            if (state.PartySizeLimit <= 0 ||
                capacityApproval.ApprovedCount <= 0)
            {
                result.AllowedByPartySize = 0;
                result.PrimaryLimit = RecruitmentLimitReason.PartySize;
                return result;
            }

            float unitWage =
                AiRecruitmentFinancialModel.EstimateUnitDailyWage(
                    troop,
                    subject.IsAtFormalWar && subject.IsLordParty);
            // Fief troops and transferred soldiers require no purchase.
            // Converted prisoners use the discounted cost defined by the
            // active prisoner recruitment model. Every source still reserves
            // its future wages in the shared clan commitment budget. Fief
            // mobilization alone is exempt from this recruitment-time finance
            // decision; its live wages remain owned by the party wage model.
            int unitCost;

            if (source == RecruitmentSource.Fief ||
                source == RecruitmentSource.Transfer)
            {
                unitCost = 0;
            }
            else if (source == RecruitmentSource.Prisoner &&
                Campaign.Current.Models.PrisonerRecruitmentCalculationModel
                    is global::ModifiedArmy.Models.NewPrisonerRecruitmentCalculationModel
                        model)
            {
                unitCost = model.CalculateGoldCostForRecruitment(
                    troop,
                    1,
                    subject.Leader ?? subject.Clan?.Leader);
            }
            else
            {
                unitCost = Campaign.Current.Models.PartyWageModel
                    .GetTroopRecruitmentCost(
                        troop,
                        subject.Leader ?? subject.Clan?.Leader,
                        false)
                    .RoundedResultNumber;
            }

            result.UnitDailyWage = unitWage;
            result.UnitRecruitmentCost = Math.Max(0, unitCost);
            result.Priority = GetPriority(
                subject,
                troop,
                source,
                state);
            result.AllowedByPartySize = Math.Max(
                0,
                capacityApproval.ApprovedCount);
            bool isFinanceExempt = source == RecruitmentSource.Fief;
            result.AllowedByWageLimit = isFinanceExempt
                ? int.MaxValue
                : GetAllowedByWageLimit(state, unitWage);
            bool isExistingTroopTransfer =
                source == RecruitmentSource.Transfer;
            result.AllowedByRecruitmentCost =
                isExistingTroopTransfer || isFinanceExempt
                ? int.MaxValue
                : GetAllowedByRecruitmentCost(
                    state,
                    result.UnitRecruitmentCost);
            result.AllowedByMaintenance =
                isExistingTroopTransfer || isFinanceExempt
                ? int.MaxValue
                : GetAllowedByMaintenance(
                    state,
                    unitWage,
                    result.UnitRecruitmentCost);

            result.RecruitableCount = Minimum(
                result.RequestedCount,
                result.AllowedByPartySize,
                result.AllowedByWageLimit,
                result.AllowedByRecruitmentCost,
                result.AllowedByMaintenance);
            result.PrimaryLimit = FindPrimaryLimit(result);
            result.SustainableDays =
                isExistingTroopTransfer || isFinanceExempt
                ? int.MaxValue
                : GetSustainableDays(
                    state,
                    result.RecruitableCount,
                    unitWage,
                    result.UnitRecruitmentCost);
            return result;
        }

        /// <summary>
        /// Uses only properties of the offered troop and its source. This is
        /// intentionally independent of the party's current composition, so
        /// two equal-tier troops are not ranked differently by a deleted quota.
        /// </summary>
        private float GetPriority(
            RecruitmentSubject subject,
            CharacterObject troop,
            RecruitmentSource source,
            RecruitmentSimulationState state)
        {
            if (subject == null || troop == null || state == null)
            {
                return float.MinValue;
            }

            float tierPreference = Math.Max(0, troop.Tier);
            float sourcePenalty = source == RecruitmentSource.Mercenary
                ? 0.01f
                : 0f;

            return tierPreference - sourcePenalty;
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
        /// Ensures the character who performs the transaction can pay the
        /// immediate purchase price before long-term affordability is tested.
        /// </summary>
        private static int GetAllowedByRecruitmentCost(
            RecruitmentSimulationState state,
            int unitCost)
        {
            if (unitCost <= 0)
            {
                return int.MaxValue;
            }

            float remaining = state.Budget.AvailablePurchaseFunds
                - state.CommittedRecruitmentCost;
            return Math.Max(0, (int)Math.Floor(remaining / unitCost));
        }

        /// <summary>
        /// Charges only newly recruited soldiers against the clan's shared
        /// daily pool. Existing wages are already included in the clan finance
        /// forecast used to calculate that pool and must not be counted twice.
        /// </summary>
        private static int GetAllowedByMaintenance(
            RecruitmentSimulationState state,
            float unitWage,
            int unitCost)
        {
            float existingCommitment = state.CommittedRecruitmentCost
                + state.CommittedRecruitmentDailyWage *
                    state.Budget.MaintenanceDays;
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
        /// Reports how many forecast days remain affordable for the newly
        /// added wage commitment after recruitment purchases are deducted.
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
            float projectedWage = Math.Max(0f,
                state.CommittedRecruitmentDailyWage +
                approvedCount * unitWage);

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

            if (result.RecruitableCount == result.AllowedByWageLimit)
            {
                return RecruitmentLimitReason.WageLimit;
            }

            if (result.RecruitableCount == result.AllowedByRecruitmentCost)
            {
                return RecruitmentLimitReason.RecruitmentCost;
            }

            return RecruitmentLimitReason.MaintenanceFunds;
        }

        private static int GetRecruitmentTarget(
            MobileParty party,
            RecruitmentSimulationState state,
            RecruitmentSource source)
        {
            if (party?.IsGarrison == true)
            {
                return GarrisonRecruitmentCapacity
                    .GetAutomaticRecruitmentTarget(party);
            }

            if (source == RecruitmentSource.Transfer)
            {
                PartyForceTargetResult transferTarget =
                    ForceManagementFacade.Current.GetPartyTarget(party);
                return Math.Min(
                    state.PartySizeLimit,
                    transferTarget.TargetCount);
            }

            PartyForceTargetResult target = ForceManagementFacade.Current
                .GetPartyTarget(party);
            return Math.Min(state.PartySizeLimit, target.TargetCount);
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
        /// Resolves the diagnostic party category recorded in the plan.
        /// </summary>
        private static RecruitmentPartyType GetPartyType(MobileParty party)
        {
            return party?.IsGarrison == true
                ? RecruitmentPartyType.Garrison
                : RecruitmentPartyType.MobileParty;
        }

        /// <summary>
        /// Garrisons normally have no party leader. Their owning clan leader
        /// is the native payer for automatic recruitment expenses.
        /// </summary>
        private static Hero GetRecruitingHero(MobileParty party)
        {
            return party?.LeaderHero
                ?? party?.CurrentSettlement?.OwnerClan?.Leader;
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

    /// <summary>
    /// Compatibility name for integrations compiled against the old class.
    /// </summary>
    [Obsolete(
        "Use DefaultAiTroopRecruitmentModel; this adapter will be removed during the atomic entry-point switch.")]
    public sealed class DefaultAIRecruitmentModel
        : DefaultAiTroopRecruitmentModel
    {
        /// <summary>Creates the compatibility adapter.</summary>
        public DefaultAIRecruitmentModel()
        {
        }
    }
}
