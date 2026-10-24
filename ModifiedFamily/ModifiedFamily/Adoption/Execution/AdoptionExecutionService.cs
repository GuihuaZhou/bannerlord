// Executes companion promotion or clan transfer while preserving biological genealogy.
using ModifiedFamily.Core;
using ModifiedFamily.Kinship;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Localization;
using ModifiedFamily.Adoption.Models;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedFamily.Adoption.Execution
{
    public sealed class AdoptionExecutionService
    {
        private readonly AdoptionModel _model;
        private readonly AdoptiveKinshipService _kinship;
        public AdoptionExecutionService(AdoptionModel model, AdoptiveKinshipService kinship) { _model = model; _kinship = kinship; }
        public FamilyResult TryExecute(FamilyProposal p)
        {
            var valid = _model.CanExecute(p);
            if (!valid.IsAllowed || valid.TemporaryBlockers.Count > 0)
            {
                if (valid.TemporaryBlockers.Count > 0) FamilyDiagnostics.DebugLog(FamilyLogText.AdoptionWaiting(p));
                return valid;
            }
            Hero hero = p.AdopteeHero; Clan source = hero.Clan;
            if (hero.CompanionOf != null)
            {
                if (hero.CompanionOf != p.DestinationClan) return FamilyResult.Deny(FamilyReasonCode.ClanChanged);
                // Clearing CompanionOf first is mandatory because Hero.Clan returns CompanionOf preferentially.
                hero.CompanionOf = null; hero.Clan = p.DestinationClan;
            }
            else
            {
                PrepareNobleForClanTransfer(hero, p.DestinationClan);
                hero.Clan = p.DestinationClan;
            }
            if (hero.CompanionOf != null || hero.Clan != p.DestinationClan) return FamilyResult.Deny(FamilyReasonCode.ExecutionVerificationFailed);
            _kinship.Record(p.AdopterHero, hero, source); p.Status = FamilyProposalStatus.Completed; p.ExecutionStep = FamilyExecutionStep.Completed;
            FamilyDiagnostics.Notice(FamilyLogText.AdoptionCompleted(p));
            return FamilyResult.Allow(p.ProposalId);
        }

        private static void PrepareNobleForClanTransfer(Hero hero, Clan destination)
        {
            // This follows the ordering used by vanilla marriage clan transfer.
            Clan source = hero.Clan;
            if (hero.GovernorOf != null) ChangeGovernorAction.RemoveGovernorOf(hero);
            MobileParty party = hero.PartyBelongedTo;
            if (party == null) return;
            if (source?.Kingdom != destination.Kingdom)
            {
                if (party.Army != null)
                {
                    if (party.Army.LeaderParty == party) DisbandArmyAction.ApplyByUnknownReason(party.Army);
                    else party.Army = null;
                }
                IFaction destinationFaction = destination.Kingdom ?? (IFaction)destination;
                FactionHelper.FinishAllRelatedHostileActionsOfNobleToFaction(hero, destinationFaction);
            }
            bool ledParty = party.LeaderHero == hero;
            party.MemberRoster.RemoveTroop(hero.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
            MakeHeroFugitiveAction.Apply(hero, false);
            if (ledParty && party.IsLordParty) DisbandPartyAction.StartDisband(party);
        }
    }
}
