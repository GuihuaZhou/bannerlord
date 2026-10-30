using ModifiedArmy.common;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Orders.Models
{
    public sealed class GarrisonStockPlan
    {
        public int StockOnHand { get; internal set; }
        public int ResidentFloor { get; internal set; }
        public int Target { get; internal set; }
        public int ReorderPoint { get; internal set; }
        public int HardFreeSlots { get; internal set; }
        public float EstimatedUnitCommitment { get; internal set; }
        public string ReasonCode { get; internal set; }
    }

    /// <summary>Pure (s,S) calculation over already captured campaign values.</summary>
    public static class GarrisonStockPlanningModel
    {
        public static GarrisonStockPlan Evaluate(
            Town town,
            ClanRecruitmentBudgetSnapshot clanBudget,
            GarrisonRecruitmentOrdersConfig config,
            float estimatedUnitPurchaseCost)
        {
            MobileParty garrison = town?.GarrisonParty;
            int hardLimit = Math.Max(0, garrison?.Party.PartySizeLimit ?? 0);
            int stock = Math.Max(0, garrison?.Party.NumberOfAllMembers ?? 0);
            int reserved = GarrisonRecruitmentCapacity
                .GetActiveSupplyEscortCount(town?.Settlement);
            GarrisonWageLimitModel.CalculateWagePaymentLimit(
                town, out int strategicCeiling, out _,
                out float estimatedWage, out _, out _);
            float safeCapacity = Math.Max(0f, clanBudget?.SafeCapacity ?? 0f);
            RecruitmentBudget recruitmentBudget = RecruitmentBudget.Create(garrison);
            float wageHeadroom = Math.Max(
                0f,
                recruitmentBudget.UsableWageLimit -
                    recruitmentBudget.CurrentLongTermWage);
            int wageSlots = estimatedWage <= 0f
                ? hardLimit
                : (int)Math.Floor(wageHeadroom / estimatedWage);
            int wageSafeGoal = Math.Min(
                hardLimit, stock + Math.Max(0, wageSlots));
            GarrisonStockTargets targets = GarrisonStockPlanningMath.Calculate(
                hardLimit, stock, reserved, config.ResidentFloor,
                strategicCeiling, safeCapacity, config.WealthSaturationGold,
                config.ReorderFraction, wageSafeGoal);

            return new GarrisonStockPlan
            {
                StockOnHand = stock,
                ResidentFloor = targets.ResidentFloor,
                Target = targets.Target,
                ReorderPoint = targets.ReorderPoint,
                HardFreeSlots = targets.HardFreeSlots,
                EstimatedUnitCommitment = Math.Max(0f, estimatedWage) *
                    ClanRecruitmentBudgetManager.ForecastDays +
                    Math.Max(0f, estimatedUnitPurchaseCost),
                ReasonCode = targets.Target <= stock ? "TargetCovered" :
                    safeCapacity <= 0f ? "NoSafeCapacity" : "Eligible"
            };
        }
    }
}
