using ModifiedArmy.common;
using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Orders.Models;
using ModifiedArmy.Recruitment.Orders.Diagnostics;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Orders.Behaviors
{
    /// <summary>Stable clan-first daily scheduler for AI garrison orders.</summary>
    public sealed class ClanGarrisonRecruitmentCoordinator : CampaignBehaviorBase
    {
        private int _lastRunDay = int.MinValue;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnDailyTick()
        {
            GarrisonRecruitmentOrdersConfig config = MilitaryConfigManager.Instance
                .GetActiveConfig()?.AiRecruitment.ForceManagement
                .GarrisonRecruitmentOrders;
            if (config?.Enabled != true ||
                !CampaignRecoveryCoordinator.CanMutateCampaignState()) return;

            int day = (int)CampaignTime.Now.ToDays;
            if (_lastRunDay == day) return;
            _lastRunDay = day;

            GarrisonRecruitmentOrderBehavior orders = Campaign.Current
                .GetCampaignBehavior<GarrisonRecruitmentOrderBehavior>();
            SettlementManpowerProductionBehavior pools = Campaign.Current
                .GetCampaignBehavior<SettlementManpowerProductionBehavior>();
            if (orders == null) return;

            foreach (IGrouping<Clan, Town> group in Town.AllFiefs
                .Where(IsEligibleTown)
                .GroupBy(town => town.OwnerClan)
                .OrderBy(group => group.Key.StringId))
            {
                Clan clan = group.Key;
                foreach (Town town in group)
                {
                    if (town.GarrisonParty == null &&
                        town.GarrisonAutoRecruitmentIsEnabled &&
                        town.Settlement.Party.MapEvent == null &&
                        town.Settlement.Party.SiegeEvent == null)
                    {
                        town.Settlement.AddGarrisonParty();
                    }
                }
                ClanRecruitmentBudgetSnapshot budget =
                    ClanRecruitmentBudgetManager.GetSnapshot(clan);
                float planningLimit = budget.SafeCapacity *
                    config.PlanningCommitmentShare;
                float plannedCommitment = 0f;

                var planned = group.Select(town => new
                    {
                        Town = town,
                        Plan = GarrisonStockPlanningModel.Evaluate(
                            town, budget, config,
                            EstimateUnitPurchaseCost(town, pools)),
                        Order = orders.GetActive(town.Settlement)
                    })
                    .OrderByDescending(x =>
                        x.Plan.StockOnHand < x.Plan.ResidentFloor)
                    .ThenByDescending(x => x.Plan.Target <= 0 ? 0f :
                        (x.Plan.Target - x.Plan.StockOnHand) /
                        (float)x.Plan.Target)
                    .ThenBy(x => x.Town.Settlement.StringId)
                    .ToList();

                foreach (var item in planned)
                {
                    GarrisonRecruitmentOrderRecord order = ReviewOrder(
                        orders, item.Town, item.Plan, config, day,
                        planningLimit, ref plannedCommitment, item.Order);
                    GarrisonRecruitmentOrderDiagnostics.LogPlan(
                        item.Order == null && order != null ? "Created" : "Reviewed",
                        item.Town, order, item.Plan, budget, plannedCommitment,
                        order?.LastReasonCode ?? item.Plan.ReasonCode,
                        item.Order == null && order != null);
                    if (order == null || order.Remaining <= 0)
                    {
                        PrisonerGarrisonRecruitmentService.SelectAndPrepare(
                            item.Town.Settlement, out _, out _, out _);
                        continue;
                    }
                    if (order.State == GarrisonRecruitmentOrderState.RecoveryRequired)
                    {
                        continue;
                    }
                    if (order.LastExecutionDay == day) continue;

                    ExecuteOrder(item.Town, order, orders, pools, day);
                }
            }
        }

        private static GarrisonRecruitmentOrderRecord ReviewOrder(
            GarrisonRecruitmentOrderBehavior orders,
            Town town,
            GarrisonStockPlan plan,
            GarrisonRecruitmentOrdersConfig config,
            int day,
            float planningLimit,
            ref float plannedCommitment,
            GarrisonRecruitmentOrderRecord order)
        {
            Settlement settlement = town.Settlement;
            if (order != null && order.OwnerClanId != settlement.OwnerClan?.StringId)
            {
                orders.Close(settlement, day, "OwnerChanged");
                order = null;
            }

            if (order != null)
            {
                if (order.State == GarrisonRecruitmentOrderState.RecoveryRequired)
                {
                    plannedCommitment += order.Remaining *
                        plan.EstimatedUnitCommitment;
                    return order;
                }
                int remaining = GarrisonStockPlanningMath.ReviewRemaining(
                    order.Remaining, plan.Target, plan.StockOnHand,
                    order.AuthorizedGoalAtLastRevision);
                int affordableExisting = (int)Math.Floor(Math.Max(
                    0f, planningLimit - plannedCommitment) /
                    Math.Max(1f, plan.EstimatedUnitCommitment));
                remaining = Math.Min(remaining, affordableExisting);
                orders.Revise(order, remaining, plan.Target, day,
                    remaining > 0 ? "Reviewed" : "TargetCovered");
                if (order.Remaining <= 0)
                {
                    orders.Close(settlement, day, "CompletedOrCovered");
                    return null;
                }
                plannedCommitment += order.Remaining *
                    plan.EstimatedUnitCommitment;
                return order;
            }

            int desired = Math.Max(0, plan.Target - plan.StockOnHand);
            bool emergency = plan.StockOnHand < plan.ResidentFloor;
            int lastClosedDay = orders.GetLastClosedDay(settlement);
            if (plan.StockOnHand >= plan.ReorderPoint ||
                (!emergency && desired < config.MinimumNewOrderCount) ||
                (!emergency && lastClosedDay != int.MinValue &&
                    day - lastClosedDay <
                    config.OrderCooldownDays)) return null;

            float unitCommitment = Math.Max(1f, plan.EstimatedUnitCommitment);
            int affordable = (int)Math.Floor(Math.Max(
                0f, planningLimit - plannedCommitment) / unitCommitment);
            int authorized = Math.Min(desired, affordable);
            if (authorized <= 0) return null;
            plannedCommitment += authorized * unitCommitment;
            return orders.Create(settlement, authorized, plan.Target, day);
        }

        private static void ExecuteOrder(
            Town town,
            GarrisonRecruitmentOrderRecord order,
            GarrisonRecruitmentOrderBehavior orders,
            SettlementManpowerProductionBehavior pools,
            int day)
        {
            Settlement settlement = town.Settlement;
            pools?.EnsureDailyProduction(settlement);
            if (town.GarrisonParty == null && town.GarrisonAutoRecruitmentIsEnabled &&
                settlement.Party.MapEvent == null && settlement.Party.SiegeEvent == null)
                settlement.AddGarrisonParty();
            MobileParty garrison = town.GarrisonParty;
            if (garrison == null || !town.GarrisonAutoRecruitmentIsEnabled ||
                settlement.Party.MapEvent != null || settlement.Party.SiegeEvent != null)
                return;

            List<RecruitmentCandidate> candidates = pools == null
                ? new List<RecruitmentCandidate>()
                : pools.GetAvailableProfessionalOffers(settlement)
                    .Where(x => x.Key != null && x.Value > 0)
                    .Select(x => new RecruitmentCandidate(
                        x.Key, x.Value, RecruitmentSource.ProfessionalProduction))
                    .ToList();
            candidates.AddRange(PrisonerGarrisonRecruitmentService.SelectAndPrepare(
                settlement, out _, out _, out _));

            int dailyLimit = Math.Min(
                Math.Max(0, Campaign.Current.Models.SettlementGarrisonModel
                    .GetMaximumDailyAutoRecruitmentCount(town)),
                ModifiedArmy.Garrison.Models.GarrisonRecruitmentCapacity
                    .GetHardFreeSlots(garrison));
            GarrisonRecruitmentExecutionResult result =
                new GarrisonRecruitmentExecutionResult();
            if (dailyLimit > 0 && candidates.Count > 0)
            {
                RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                    garrison, candidates,
                    new AiGarrisonOrderRecruitmentContext(order.Remaining));
                result = GarrisonRecruitmentExecutionService.ExecuteUnderOrder(
                    settlement, garrison, plan, dailyLimit, order.Remaining,
                    true, pools, order.OrderId + ":" + day);
            }

            GarrisonRecruitmentOrderDiagnostics.LogExecution(
                town, order, result,
                ClanRecruitmentBudgetManager.GetSnapshot(town.OwnerClan)
                    .RemainingCommitmentBudget);
            orders.ApplyExecution(order, result.ProfessionalRecruited,
                result.PrisonerRecruited, day, result.OperationId,
                result.RecoveryRequired, result.FailureReason);
            pools?.GetOrCreateProductionData(settlement)
                ?.RecordProfessionalRecruitment(day, result.ProfessionalRecruited);
            if (!result.RecoveryRequired && order.Remaining <= 0)
                orders.Close(settlement, day, "Completed");
        }

        private static bool IsEligibleTown(Town town)
        {
            Clan clan = town?.OwnerClan;
            return town?.Settlement?.IsFortification == true && clan != null &&
                clan != Clan.PlayerClan && !clan.IsMinorFaction &&
                town.GarrisonAutoRecruitmentIsEnabled &&
                town.Settlement.Party.MapEvent == null &&
                town.Settlement.Party.SiegeEvent == null;
        }

        private static float EstimateUnitPurchaseCost(
            Town town,
            SettlementManpowerProductionBehavior pools)
        {
            if (town?.Settlement == null || Campaign.Current == null) return 0f;
            IEnumerable<CharacterObject> professional = pools?
                .GetPredictedProfessionalOffers(town.Settlement)
                .Where(x => x.Key != null && x.Value > 0)
                .Select(x => x.Key) ?? Enumerable.Empty<CharacterObject>();
            IEnumerable<CharacterObject> prisoners = town.Settlement.Party
                .PrisonRoster.GetTroopRoster()
                .Where(x => x.Character != null && x.Number > 0 &&
                    PrisonerGarrisonRecruitmentService.IsRegularTroop(x.Character) &&
                    x.Character.Tier >= 3)
                .Select(x => x.Character);
            Hero payer = town.OwnerClan?.Leader;
            return professional.Concat(prisoners).Distinct()
                .Select(troop => (float)Campaign.Current.Models.PartyWageModel
                    .GetTroopRecruitmentCost(troop, payer, false)
                    .RoundedResultNumber)
                .DefaultIfEmpty(0f)
                .Max();
        }
    }
}
