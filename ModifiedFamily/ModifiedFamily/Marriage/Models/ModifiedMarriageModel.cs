// Extends the vanilla marriage model only where approved clan destination and kinship require it.
using ModifiedFamily.Campaign;
using ModifiedFamily.Marriage.Execution;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedFamily.Marriage.Models
{
    public sealed class ModifiedMarriageModel : DefaultMarriageModel
    {
        public override Clan GetClanAfterMarriage(Hero firstHero, Hero secondHero)
        {
            // Ordinary vanilla marriages retain the base game's destination rules.
            return MarriageExecutionScope.TryGetDestination(firstHero, secondHero, out Clan destination) ? destination : base.GetClanAfterMarriage(firstHero, secondHero);
        }
        public override bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero)
        {
            // Legal adoptive kinship augments, rather than replaces, vanilla suitability checks.
            if (!base.IsCoupleSuitableForMarriage(firstHero, secondHero)) return false;
            var behavior = TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>();
            return behavior == null || !MarriageEligibilityModel.AreAdoptiveRelated(firstHero, secondHero, behavior.Adoptions);
        }
    }
}
