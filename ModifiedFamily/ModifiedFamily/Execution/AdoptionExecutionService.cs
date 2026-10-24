using ModifiedFamily.Core;
using ModifiedFamily.Kinship;
using ModifiedFamily.Models;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using ModifiedFamily.Diagnostics;

namespace ModifiedFamily.Execution
{
    public sealed class AdoptionExecutionService
    {
        private readonly AdoptionModel _model;
        private readonly AdoptiveKinshipService _kinship;
        public AdoptionExecutionService(AdoptionModel model, AdoptiveKinshipService kinship) { _model = model; _kinship = kinship; }
        public FamilyResult TryExecute(FamilyProposal p)
        {
            var valid = _model.CanAdopt(p.AdopterHero, p.AdopteeHero, p.DestinationClan); if (!valid.IsAllowed) return valid;
            Hero hero = p.AdopteeHero; Clan source = hero.Clan;
            if (hero.PartyBelongedTo != null || hero.GovernorOf != null) { FamilyDiagnostics.DebugLog("Adoption execution deferred: id=" + p.ProposalId + ", hero=" + hero.StringId); return FamilyResult.Allow(null, FamilyReasonCode.TemporaryInactive); }
            if (hero.CompanionOf != null)
            {
                if (hero.CompanionOf != p.DestinationClan) return FamilyResult.Deny(FamilyReasonCode.ClanChanged);
                hero.CompanionOf = null; hero.Clan = p.DestinationClan;
            }
            else hero.Clan = p.DestinationClan;
            if (hero.CompanionOf != null || hero.Clan != p.DestinationClan) return FamilyResult.Deny(FamilyReasonCode.ExecutionVerificationFailed);
            _kinship.Record(p.AdopterHero, hero, source); p.Status = FamilyProposalStatus.Completed; p.ExecutionStep = FamilyExecutionStep.Completed;
            FamilyDiagnostics.Notice("Adoption completed: id=" + p.ProposalId + ", adopter=" + p.AdopterHero.StringId + ", adoptee=" + hero.StringId + ", clan=" + p.DestinationClan.StringId);
            return FamilyResult.Allow(p.ProposalId);
        }
    }
}
