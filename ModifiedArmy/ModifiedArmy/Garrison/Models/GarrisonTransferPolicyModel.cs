using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Centralizes policy calculations used by native party-garrison exchange
    /// patches. Harmony integration should only pass inputs to this model.
    /// </summary>
    public static class GarrisonTransferPolicyModel
    {
        public const float InteriorIdealGarrisonRatio = 0.40f;

        public static int GetExchangeIdealGarrisonSize(
            Settlement settlement,
            int nativeIdealSize)
        {
            if (nativeIdealSize <= 0)
            {
                return 0;
            }

            return IsEnemyBorderFortification(settlement)
                ? nativeIdealSize
                : Math.Max(
                    0,
                    MBRandom.RoundRandomized(
                        nativeIdealSize * InteriorIdealGarrisonRatio));
        }

        /// <summary>
        /// A frontline border requires a direct fortification neighbor that
        /// currently belongs to an enemy faction.
        /// </summary>
        public static bool IsEnemyBorderFortification(Settlement settlement)
        {
            IFaction ownerFaction = settlement?.MapFaction;
            if (settlement?.IsFortification != true ||
                settlement.Town == null || ownerFaction == null ||
                !ownerFaction.IsKingdomFaction)
            {
                return false;
            }

            foreach (Settlement neighbor in settlement.Town
                .GetNeighborFortifications(MobileParty.NavigationType.All))
            {
                IFaction neighborFaction = neighbor?.MapFaction;
                if (neighbor?.IsFortification == true &&
                    neighborFaction != null &&
                    FactionManager.IsAtWarAgainstFaction(
                        ownerFaction,
                        neighborFaction))
                {
                    return true;
                }
            }

            return false;
        }

        public static int GetAllowedDonationCount(
            MobileParty party,
            int requestedCount)
        {
            if (party == null || requestedCount <= 0)
            {
                return 0;
            }

            int sizeLimit = party.Party.PartySizeLimit;
            int wageLimit = MathF.Round(
                party.PaymentLimit / Campaign.Current.AverageWage);
            float food = party.Army == null
                ? party.Food
                : party.Army.Parties.Sum(item => item.Food);
            int foodLimit = MathF.Round(
                Campaign.Current.Models.MobilePartyFoodConsumptionModel
                    .NumberOfMenOnMapToEatOneFood * food / 2f);
            int reasonableTarget = Math.Max(
                30,
                Math.Min(sizeLimit, Math.Min(wageLimit, foodLimit)));
            int operationalSurplus = Math.Max(
                0,
                party.Party.NumberOfRegularMembers - reasonableTarget);
            int forceTargetSurplus = ForceManagementFacade.Current
                .GetSafeFieldSurplus(party);
            return Math.Min(
                requestedCount,
                Math.Min(operationalSurplus, forceTargetSurplus));
        }

        /// <summary>
        /// Transfers only the troop types approved by the unified recruitment
        /// model, including field wage and long-term maintenance constraints.
        /// </summary>
        public static int TransferApprovedTroopsFromGarrison(
            MobileParty receivingParty,
            Settlement settlement,
            int requestedCount)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            if (receivingParty == null || garrison == null ||
                requestedCount <= 0)
            {
                return 0;
            }

            GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                .GarrisonDefense.Evaluate(settlement);
            int safeTransferCount = ForceManagementFacade.Current
                .GarrisonDefense.GetSafeTransferOutCount(
                    defense,
                    garrison.MemberRoster);
            requestedCount = Math.Min(requestedCount, safeTransferCount);
            if (requestedCount <= 0)
            {
                return 0;
            }

            List<RecruitmentCandidate> candidates = garrison.MemberRoster
                .GetTroopRoster()
                .Where(element => element.Character != null &&
                    !element.Character.IsHero && element.Number > 0)
                .Select(element => new RecruitmentCandidate(
                    element.Character,
                    element.Number,
                    RecruitmentSource.Transfer))
                .ToList();
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                receivingParty,
                candidates);
            int remainingRequest = requestedCount;
            int transferred = 0;

            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
            {
                CharacterObject troop = evaluation.Troop;
                int available = troop == null
                    ? 0
                    : garrison.MemberRoster.GetTroopCount(troop);
                int count = Math.Min(
                    remainingRequest,
                    Math.Min(evaluation.RecruitableCount, available));
                if (count <= 0)
                {
                    continue;
                }

                int woundedAvailable = garrison.MemberRoster
                    .GetTroopRoster()
                    .Where(element => element.Character == troop)
                    .Select(element => element.WoundedNumber)
                    .FirstOrDefault();
                int healthyAvailable = Math.Max(
                    0,
                    available - woundedAvailable);
                int wounded = Math.Max(0, count - healthyAvailable);

                receivingParty.MemberRoster.AddToCounts(
                    troop, count, false, wounded, 0, true, -1);
                garrison.MemberRoster.AddToCounts(
                    troop, -count, false, -wounded, 0, true, -1);
                ClanRecruitmentBudgetManager.CommitRecruitment(
                    receivingParty,
                    count,
                    0f,
                    evaluation.UnitDailyWage);
                remainingRequest -= count;
                transferred += count;
                if (remainingRequest <= 0)
                {
                    break;
                }
            }

            return transferred;
        }
    }
}
