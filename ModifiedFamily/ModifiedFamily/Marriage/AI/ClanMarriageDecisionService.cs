// Decentralized clan AI: each clan selects and evaluates proposals for its own benefit.
using System.Linq;
using ModifiedFamily.Core;
using ModifiedFamily.Models;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Localization;
using ModifiedFamily.Marriage.Models;

namespace ModifiedFamily.Marriage.AI
{
    public sealed class ClanMarriageDecisionService
    {
        private readonly FamilyProposalService _service;
        private readonly MarriageProposalModel _proposalModel;
        private readonly ClanHeroValueModel _values = new ClanHeroValueModel();
        public ClanMarriageDecisionService(FamilyProposalService service, MarriageProposalModel proposalModel) { _service = service; _proposalModel = proposalModel; }

        public void ConsiderClan(Clan clan)
        {
            if (!MarriageEligibilityModel.CanUseClan(clan).IsAllowed || (!MarriagePolicyModel.EnableAiToPlayer && clan == Clan.PlayerClan) || _service.GetOutgoing(clan).Count > 0) return;
            var own = clan.Heroes.Where(h => MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed).OrderByDescending(h => h.Age).ToList();
            if (own.Count == 0) return;
            var candidates = Clan.All.Where(c => c != clan && MarriageEligibilityModel.CanUseClan(c).IsAllowed && (MarriagePolicyModel.EnableAiToPlayer || c != Clan.PlayerClan))
                .SelectMany(c => c.Heroes).Where(h => MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed && h.IsFemale != own[0].IsFemale)
                .Select(h => new { Hero = h, Tier = MatchTier(own[0], h), Score = _values.MarriageScore(own[0], h, clan, own[0].IsFemale ? h.Clan : clan, 0) })
                .Where(x => x.Tier < 99).OrderBy(x => x.Tier).ThenByDescending(x => x.Score).ToList();
            if (candidates.Count == 0 || candidates[0].Score < 0f) return;
            Hero other = candidates[0].Hero;
            var direction = MarriageDirection.FemaleJoinsMaleClan;
            FamilyDiagnostics.DebugLog(FamilyLogText.AiCandidate(clan, own[0], other, candidates[0].Tier, candidates[0].Score));
            _service.TryCreateMarriageProposal(new MarriageProposalDraft { ProposerClan = clan, ReceiverClan = other.Clan, HeroA = own[0], HeroB = other, Direction = direction, GiftAmount = 0 });
        }

        public void Respond(FamilyProposal p)
        {
            var valid = _proposalModel.CanRespond(p);
            if (!valid.IsAllowed) { _service.TryReject(p.ProposalId, p.ReceiverClan, valid.ReasonCode); return; }
            Hero own = p.HeroA.Clan == p.ReceiverClan ? p.HeroA : p.HeroB;
            Hero other = own == p.HeroA ? p.HeroB : p.HeroA;
            int giftDelta = p.GoldReceiverClan == p.ReceiverClan ? p.GiftAmount : -p.GiftAmount;
            float score = _values.MarriageScore(own, other, p.ReceiverClan, p.DestinationClan, giftDelta);
            FamilyDiagnostics.DebugLog(FamilyLogText.AiResponse(p, score));
            if (score >= 0f) _service.TryAccept(p.ProposalId, p.ReceiverClan); else _service.TryReject(p.ProposalId, p.ReceiverClan, FamilyReasonCode.InvalidProposal);
        }

        private static int MatchTier(Hero a, Hero b)
        {
            bool sameCulture = a.Culture == b.Culture, sameKingdom = a.Clan.Kingdom != null && a.Clan.Kingdom == b.Clan.Kingdom;
            if (sameKingdom && sameCulture) return 1;
            if (!sameKingdom && sameCulture) return 2;
            if (sameKingdom && MarriagePolicyModel.AllowSameKingdomDifferentCulture) return 3;
            return 99;
        }
    }
}
