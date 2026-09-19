using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Logistics.Models
{
    /// <summary>
    /// Changes the global Bannerlord food ratio from twenty people per food
    /// unit to ten. All other vanilla consumption calculations remain intact.
    /// Garrison consumption remains disabled because the vanilla
    /// DoesPartyConsumeFood implementation is deliberately not overridden yet.
    /// </summary>
    public class NewMobilePartyFoodConsumptionModel
        : DefaultMobilePartyFoodConsumptionModel
    {
        public const int PeoplePerFoodUnit = 10;

        public override int NumberOfMenOnMapToEatOneFood =>
            PeoplePerFoodUnit;
    }
}
