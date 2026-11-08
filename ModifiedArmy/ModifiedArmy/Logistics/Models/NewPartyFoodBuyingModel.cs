using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Logistics.Models
{
    /// <summary>
    /// Keeps the native food-purchase transaction and changes only the target
    /// reserve used when parties buy food from towns and villages.
    /// </summary>
    public sealed class NewPartyFoodBuyingModel
        : DefaultPartyFoodBuyingModel
    {
        public const float TargetFoodDays = 40f;

        public override float MinimumDaysFoodToLastWhileBuyingFoodFromTown =>
            TargetFoodDays;

        public override float MinimumDaysFoodToLastWhileBuyingFoodFromVillage =>
            TargetFoodDays;
    }
}
