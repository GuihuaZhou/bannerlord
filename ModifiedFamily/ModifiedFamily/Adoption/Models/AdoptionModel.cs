// Adoption-specific eligibility model; it intentionally does not reuse marriage restrictions.
using ModifiedFamily.Core;
using TaleWorlds.CampaignSystem;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Proposals;

namespace ModifiedFamily.Adoption.Models
{
    /// <summary>
    /// Defines adoption-specific eligibility without inheriting marriage culture rules.
    /// </summary>
    public sealed class AdoptionModel
    {
        public FamilyResult CanCreate(Hero adopter, Hero adoptee, Clan destination, Clan receiver)
        {
            if (adopter == null || adoptee == null || destination == null || adopter == adoptee || adopter.Clan != destination) return FamilyResult.Deny(Proposals.FamilyReasonCode.InvalidProposal);
            if (adopter.IsDead || adoptee.IsDead || destination.IsEliminated) return FamilyResult.Deny(Proposals.FamilyReasonCode.Dead);
            if (MarriageEligibilityModel.AreBloodRelated(adopter, adoptee)) return FamilyResult.Deny(Proposals.FamilyReasonCode.Related);
            if (adoptee == Hero.MainHero || adoptee.Clan?.Leader == adoptee) return FamilyResult.Deny(Proposals.FamilyReasonCode.LeaderCannotLeave);
            if (adoptee.CompanionOf == destination) return FamilyResult.Allow();
            if (adoptee.Clan == null || receiver != adoptee.Clan || adoptee.Clan == destination || adoptee.Clan.IsEliminated) return FamilyResult.Deny(Proposals.FamilyReasonCode.InvalidClan);
            return FamilyResult.Allow();
        }

        public FamilyResult CanExecute(FamilyProposal proposal)
        {
            var valid = CanCreate(proposal.AdopterHero, proposal.AdopteeHero, proposal.DestinationClan, proposal.ReceiverClan);
            if (!valid.IsAllowed) return valid;
            Hero hero = proposal.AdopteeHero;
            if (hero.PartyBelongedTo?.MapEvent != null) return FamilyResult.Allow(null, Proposals.FamilyReasonCode.InBattle);
            if (hero.IsPrisoner) return FamilyResult.Allow(null, Proposals.FamilyReasonCode.Prisoner);
            if (hero.IsFugitive) return FamilyResult.Allow(null, Proposals.FamilyReasonCode.Fugitive);
            return FamilyResult.Allow();
        }
    }
}
