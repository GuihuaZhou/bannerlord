using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Logistics.Models
{
    /// <summary>
    /// Changes the global Bannerlord food ratio from twenty people per food
    /// unit to ten. Garrison parties also consume food from their real item
    /// roster, while all other vanilla party eligibility rules remain intact.
    /// </summary>
    public class NewMobilePartyFoodConsumptionModel
        : DefaultMobilePartyFoodConsumptionModel
    {
        public const int PeoplePerFoodUnit = 10;

        public override int NumberOfMenOnMapToEatOneFood =>
            PeoplePerFoodUnit;

        /// <summary>
        /// Enables vanilla food consumption for active garrison parties. The
        /// base method remains authoritative for every other party type.
        /// </summary>
        public override bool DoesPartyConsumeFood(
            TaleWorlds.CampaignSystem.Party.MobileParty mobileParty)
        {
            if (mobileParty?.IsGarrison == true)
            {
                return mobileParty.IsActive;
            }

            return base.DoesPartyConsumeFood(mobileParty);
        }
    }
}
