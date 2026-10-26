using System;

namespace ModifiedArmy.Garrison.Supply.Models
{
    public sealed class SupplyProvisionInput
    {
        public float RouteDistance { get; set; }
        public float TravelDaysToSource { get; set; }
        public float EstimatedReturnDays { get; set; }
        public float DailyFoodDemand { get; set; }
        public int MinimumProvisionDays { get; set; }
        public int SafetyDays { get; set; }
        public int ExistingFood { get; set; }
        public int AvailableGranaryFood { get; set; }
        public int GranaryReserveFood { get; set; }
        public int CargoCapacity { get; set; }
    }

    public sealed class SupplyProvisionPlan
    {
        public float RouteDistance { get; set; }
        public float TravelDaysToSource { get; set; }
        public float EstimatedReturnDays { get; set; }
        public float DailyFoodDemand { get; set; }
        public int SafetyDays { get; set; }
        public int RequiredDays { get; set; }
        public int RequiredFood { get; set; }
        public int AvailableGranaryFood { get; set; }
        public int GranaryReserveFood { get; set; }
        public int PlannedLoad { get; set; }
        public bool CanDepart { get; set; }
        public string Reason { get; set; }

        public SupplyProvisionPlan WithCommitResult(int loaded, int finalFood)
        {
            bool enough = finalFood >= RequiredFood && DailyFoodDemand > 0f;
            string reason = Reason;
            if (!enough && reason == "Ready")
                reason = "InsufficientGranaryFood";
            return new SupplyProvisionPlan
            {
                RouteDistance = RouteDistance,
                TravelDaysToSource = TravelDaysToSource,
                EstimatedReturnDays = EstimatedReturnDays,
                DailyFoodDemand = DailyFoodDemand,
                SafetyDays = SafetyDays,
                RequiredDays = RequiredDays,
                RequiredFood = RequiredFood,
                AvailableGranaryFood = AvailableGranaryFood,
                GranaryReserveFood = GranaryReserveFood,
                PlannedLoad = Math.Max(0, loaded),
                CanDepart = enough,
                Reason = reason
            };
        }
    }

    public static class SupplyProvisionModel
    {
        public static SupplyProvisionPlan Plan(SupplyProvisionInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            int requiredDays = Math.Max(input.MinimumProvisionDays,
                (int)Math.Ceiling(input.TravelDaysToSource +
                    input.EstimatedReturnDays + input.SafetyDays));
            int requiredFood = (int)Math.Ceiling(
                input.DailyFoodDemand * requiredDays);
            int needed = Math.Max(0, requiredFood - input.ExistingFood);
            int plannedLoad = Math.Min(needed, Math.Min(
                Math.Max(0, input.CargoCapacity),
                Math.Max(0, input.AvailableGranaryFood)));
            string reason = input.DailyFoodDemand <= 0f
                ? "InvalidFoodDemand"
                : input.CargoCapacity < needed
                    ? "InsufficientCargoCapacity"
                    : plannedLoad < needed
                        ? "InsufficientGranaryFood"
                        : "Ready";
            return new SupplyProvisionPlan
            {
                RouteDistance = input.RouteDistance,
                TravelDaysToSource = input.TravelDaysToSource,
                EstimatedReturnDays = input.EstimatedReturnDays,
                DailyFoodDemand = input.DailyFoodDemand,
                SafetyDays = input.SafetyDays,
                RequiredDays = requiredDays,
                RequiredFood = requiredFood,
                AvailableGranaryFood = input.AvailableGranaryFood,
                GranaryReserveFood = input.GranaryReserveFood,
                PlannedLoad = plannedLoad,
                CanDepart = reason == "Ready",
                Reason = reason
            };
        }
    }
}
