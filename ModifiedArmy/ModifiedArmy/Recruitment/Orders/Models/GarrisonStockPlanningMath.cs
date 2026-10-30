using System;

namespace ModifiedArmy.Recruitment.Orders.Models
{
    public sealed class GarrisonStockTargets
    {
        public int ResidentFloor { get; internal set; }
        public int Target { get; internal set; }
        public int ReorderPoint { get; internal set; }
        public int HardFreeSlots { get; internal set; }
    }

    /// <summary>Deterministic numeric core for boundary testing.</summary>
    public static class GarrisonStockPlanningMath
    {
        public static GarrisonStockTargets Calculate(
            int hardLimit,
            int stockOnHand,
            int reservedReturningMembers,
            int configuredResidentFloor,
            int strategicCeiling,
            float safeCapacity,
            float wealthSaturationGold,
            float reorderFraction,
            int wageSafeGoal)
        {
            hardLimit = Math.Max(0, hardLimit);
            stockOnHand = Math.Max(0, stockOnHand);
            int residentFloor = Math.Min(
                Math.Max(0, configuredResidentFloor), hardLimit);
            safeCapacity = Math.Max(0f, safeCapacity);
            wealthSaturationGold = Math.Max(0f, wealthSaturationGold);
            float denominator = safeCapacity + wealthSaturationGold;
            float wealthFactor = denominator <= 0f
                ? 0f : safeCapacity / denominator;
            int grossGoal = residentFloor + (int)Math.Floor(
                Math.Max(0, strategicCeiling - residentFloor) * wealthFactor);
            int capacityGoal = Math.Max(
                0, hardLimit - Math.Max(0, reservedReturningMembers));
            int target = Math.Max(0, Math.Min(
                grossGoal,
                Math.Min(capacityGoal, Math.Max(0, wageSafeGoal))));
            int reorderPoint = residentFloor + (int)Math.Floor(
                Math.Max(0, target - residentFloor) *
                Math.Max(0f, Math.Min(1f, reorderFraction)));
            return new GarrisonStockTargets
            {
                ResidentFloor = residentFloor,
                Target = target,
                ReorderPoint = reorderPoint,
                HardFreeSlots = Math.Max(0, capacityGoal - stockOnHand)
            };
        }

        public static int ReviewRemaining(
            int previousRemaining,
            int currentTarget,
            int currentStock,
            int previousAuthorizedGoal)
        {
            int availableNeed = Math.Max(0, currentTarget - currentStock);
            int remaining = Math.Min(
                Math.Max(0, previousRemaining), availableNeed);
            if (currentTarget > previousAuthorizedGoal)
                remaining = Math.Min(
                    availableNeed,
                    remaining + currentTarget - previousAuthorizedGoal);
            return remaining;
        }
    }
}
