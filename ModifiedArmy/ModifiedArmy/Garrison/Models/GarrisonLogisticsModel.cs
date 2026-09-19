using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Immutable snapshot of a fortification's military food supply.
    /// </summary>
    public sealed class GarrisonLogisticsStatus
    {
        public int CurrentFood { get; internal set; }

        public float DailyConsumption { get; internal set; }

        public float FoodSupplyDays { get; internal set; }

        public int TargetFood { get; internal set; }

        public int FoodDeficit { get; internal set; }

        public bool HasGarrison { get; internal set; }
    }

    /// <summary>
    /// Provides the shared logistics calculations used by the garrison UI,
    /// local requisition, market purchasing, and future supply parties.
    /// </summary>
    public static class GarrisonLogisticsModel
    {
        public const int EmergencySupplyDays = 15;

        public const int ReplenishmentThresholdDays = 30;

        public const int TargetSupplyDays = 45;

        /// <summary>
        /// Calculates military food status from the garrison party's real item
        /// roster. Prisoners count as half a regular member for food demand.
        /// </summary>
        public static GarrisonLogisticsStatus Calculate(Town town)
        {
            MobileParty garrison = town?.GarrisonParty;

            if (garrison == null)
            {
                return new GarrisonLogisticsStatus();
            }

            float dailyConsumption = CalculateDailyConsumption(garrison);

            int currentFood = garrison.ItemRoster?.TotalFood ?? 0;
            int targetFood = dailyConsumption > 0f
                ? (int)Math.Ceiling(
                    dailyConsumption * TargetSupplyDays)
                : 0;

            float supplyDays = dailyConsumption > 0f
                ? currentFood / dailyConsumption
                : 0f;

            return new GarrisonLogisticsStatus
            {
                CurrentFood = currentFood,
                DailyConsumption = dailyConsumption,
                FoodSupplyDays = supplyDays,
                TargetFood = targetFood,
                FoodDeficit = Math.Max(0, targetFood - currentFood),
                HasGarrison = true
            };
        }

        /// <summary>
        /// Returns whether the settlement should begin replenishing its
        /// military granary under the 30-day threshold rule.
        /// </summary>
        public static bool NeedsReplenishment(
            GarrisonLogisticsStatus status)
        {
            return status?.HasGarrison == true
                && status.DailyConsumption > 0f
                && status.FoodSupplyDays < ReplenishmentThresholdDays;
        }

        /// <summary>
        /// Uses the active Bannerlord party food model so perks, terrain, siege
        /// effects, and the global people-per-food ratio remain authoritative.
        /// Calling the calculation methods does not enable or consume food for
        /// garrisons; actual consumption remains disabled in this batch.
        /// </summary>
        private static float CalculateDailyConsumption(
            MobileParty garrison)
        {
            var foodModel = Campaign.Current?
                .Models?
                .MobilePartyFoodConsumptionModel;

            if (foodModel == null
                || garrison.Party.NumberOfAllMembers <= 0)
            {
                return 0f;
            }

            var baseConsumption =
                foodModel.CalculateDailyBaseFoodConsumptionf(
                    garrison,
                    false);

            var finalConsumption =
                foodModel.CalculateDailyFoodConsumptionf(
                    garrison,
                    baseConsumption);

            return Math.Abs(finalConsumption.ResultNumber);
        }
    }
}
