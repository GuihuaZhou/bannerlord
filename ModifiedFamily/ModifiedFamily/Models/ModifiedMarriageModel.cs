using ModifiedFamily.Campaign;
using ModifiedFamily.Core;
using ModifiedFamily.Execution;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedFamily.Models
{
    public sealed class ModifiedMarriageModel : DefaultMarriageModel
    {
        public override Clan GetClanAfterMarriage(Hero firstHero, Hero secondHero)
        {
            return MarriageExecutionScope.TryGetDestination(firstHero, secondHero, out Clan destination) ? destination : base.GetClanAfterMarriage(firstHero, secondHero);
        }
        public override bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero)
        {
            if (!base.IsCoupleSuitableForMarriage(firstHero, secondHero)) return false;
            var behavior = TaleWorlds.CampaignSystem.Campaign.Current?.GetCampaignBehavior<FamilyCampaignBehavior>();
            return behavior == null || !FamilyEligibility.AreAdoptiveRelated(firstHero, secondHero, behavior.Adoptions);
        }
    }
}
