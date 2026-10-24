// Adoption-specific eligibility model; it intentionally does not reuse marriage restrictions.
using ModifiedFamily.Core;
using TaleWorlds.CampaignSystem;
using ModifiedFamily.Marriage.Models;

namespace ModifiedFamily.Models
{
    public sealed class AdoptionModel
    {
        public FamilyResult CanAdopt(Hero adopter, Hero adoptee, Clan destination)
        {
            if (adopter == null || adoptee == null || destination == null || adopter == adoptee || adopter.Clan != destination) return FamilyResult.Deny(Proposals.FamilyReasonCode.InvalidProposal);
            if (adopter.IsDead || adoptee.IsDead || destination.IsEliminated) return FamilyResult.Deny(Proposals.FamilyReasonCode.Dead);
            if (MarriageEligibilityModel.AreBloodRelated(adopter, adoptee)) return FamilyResult.Deny(Proposals.FamilyReasonCode.Related);
            return FamilyResult.Allow();
        }
    }
}
