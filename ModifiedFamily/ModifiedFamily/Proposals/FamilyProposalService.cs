using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedFamily.Core;
using ModifiedFamily.Execution;
using ModifiedFamily.Persistence;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Proposals
{
    public sealed class FamilyProposalService
    {
        private readonly IList<FamilyProposal> _proposals;
        private readonly IList<FamilyCooldownRecord> _cooldowns;
        private readonly FamilyProposalValidator _validator;
        private readonly Dictionary<Hero, string> _locks = new Dictionary<Hero, string>();

        public FamilyProposalService(IList<FamilyProposal> proposals, IList<FamilyCooldownRecord> cooldowns, FamilyProposalValidator validator)
        { _proposals = proposals; _cooldowns = cooldowns; _validator = validator; RebuildIndexes(); }

        public void RebuildIndexes()
        {
            _locks.Clear();
            foreach (var p in _proposals.Where(x => x != null && x.IsActive))
            { if (p.HeroA != null) _locks[p.HeroA] = p.ChainId; if (p.HeroB != null) _locks[p.HeroB] = p.ChainId; if (p.AdopteeHero != null) _locks[p.AdopteeHero] = p.ChainId; }
        }

        public FamilyResult TryCreateMarriageProposal(MarriageProposalDraft draft)
        {
            if (draft == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            Clan destination = draft.Direction == MarriageDirection.FemaleJoinsMaleClan ? (draft.HeroA.IsFemale ? draft.HeroB.Clan : draft.HeroA.Clan) : (draft.HeroA.IsFemale ? draft.HeroA.Clan : draft.HeroB.Clan);
            var p = new FamilyProposal { ProposalId = Guid.NewGuid().ToString("N"), ChainId = Guid.NewGuid().ToString("N"), ProposalType = FamilyProposalType.Marriage, Status = FamilyProposalStatus.PendingResponse, ProposerClan = draft.ProposerClan, ReceiverClan = draft.ReceiverClan, HeroA = draft.HeroA, HeroB = draft.HeroB, OriginClanA = draft.HeroA?.Clan, OriginClanB = draft.HeroB?.Clan, DestinationClan = destination, GoldPayerClan = draft.GoldPayerClan, GoldReceiverClan = draft.GiftAmount > 0 ? (draft.GoldPayerClan == draft.ProposerClan ? draft.ReceiverClan : draft.ProposerClan) : null, GiftAmount = draft.GiftAmount, CreatedAt = CampaignTime.Now, ReplyDeadline = CampaignTime.DaysFromNow(FamilyConfig.ReplyDays), ExecutionStep = FamilyExecutionStep.NotStarted };
            var valid = _validator.CanProposeMarriage(p); if (!valid.IsAllowed) return valid;
            if (IsLocked(p.HeroA) || IsLocked(p.HeroB)) return FamilyResult.Deny(FamilyReasonCode.HeroLocked);
            if (OnCooldown(p.HeroA, p.HeroB)) return FamilyResult.Deny(FamilyReasonCode.PairCooldown);
            _proposals.Add(p); _locks[p.HeroA] = p.ChainId; _locks[p.HeroB] = p.ChainId; return FamilyResult.Allow(p.ProposalId, valid.TemporaryBlockers.ToArray());
        }

        public FamilyResult TryCreateAdoptionProposal(AdoptionProposalDraft draft)
        {
            if (draft == null || draft.AdopterHero == null || draft.AdopteeHero == null || draft.DestinationClan == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (IsLocked(draft.AdopteeHero)) return FamilyResult.Deny(FamilyReasonCode.HeroLocked);
            if (draft.AdopterHero.Clan != draft.DestinationClan || draft.AdopteeHero.IsDead || draft.AdopterHero.IsDead) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            var p = new FamilyProposal
            {
                ProposalId = Guid.NewGuid().ToString("N"), ChainId = Guid.NewGuid().ToString("N"), ProposalType = FamilyProposalType.Adoption,
                Status = draft.ReceiverClan == null || draft.ReceiverClan == draft.ProposerClan ? FamilyProposalStatus.AcceptedPendingExecution : FamilyProposalStatus.PendingResponse,
                ProposerClan = draft.ProposerClan, ReceiverClan = draft.ReceiverClan, AdopterHero = draft.AdopterHero, AdopteeHero = draft.AdopteeHero,
                OriginClanA = draft.AdopterHero.Clan, OriginClanB = draft.AdopteeHero.Clan, DestinationClan = draft.DestinationClan,
                CreatedAt = CampaignTime.Now, ReplyDeadline = CampaignTime.DaysFromNow(FamilyConfig.ReplyDays), ExecutionDeadline = CampaignTime.DaysFromNow(FamilyConfig.ExecutionDays)
            };
            _proposals.Add(p); _locks[p.AdopteeHero] = p.ChainId; return FamilyResult.Allow(p.ProposalId);
        }

        public FamilyResult TryAccept(string id, Clan responder)
        {
            var p = FindPending(id); if (p == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (p.ReceiverClan != responder) return FamilyResult.Deny(FamilyReasonCode.WrongResponder);
            if (p.ProposalType == FamilyProposalType.Adoption)
            {
                if (p.AdopterHero == null || p.AdopteeHero == null || p.AdopteeHero.IsDead || p.AdopteeHero.Clan != p.OriginClanB)
                { Invalidate(p, FamilyReasonCode.ClanChanged, false); return FamilyResult.Deny(FamilyReasonCode.ClanChanged); }
                p.Status = FamilyProposalStatus.AcceptedPendingExecution; p.ExecutionDeadline = CampaignTime.DaysFromNow(FamilyConfig.ExecutionDays); return FamilyResult.Allow(id);
            }
            var valid = _validator.CanRespond(p); if (!valid.IsAllowed) { Invalidate(p, valid.ReasonCode, false); return valid; }
            p.Status = FamilyProposalStatus.AcceptedPendingExecution; p.ExecutionDeadline = CampaignTime.DaysFromNow(FamilyConfig.ExecutionDays); return FamilyResult.Allow(id, valid.TemporaryBlockers.ToArray());
        }

        public FamilyResult TryReject(string id, Clan responder, FamilyReasonCode reason)
        {
            var p = FindPending(id); if (p == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (p.ReceiverClan != responder) return FamilyResult.Deny(FamilyReasonCode.WrongResponder);
            p.Status = FamilyProposalStatus.Rejected; p.ReasonCode = reason; AddCooldown(p); ReleaseChain(p.ChainId); return FamilyResult.Allow(id);
        }

        public FamilyResult TryWithdraw(string id, Clan proposer)
        {
            var p = FindPending(id); if (p == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (p.ProposerClan != proposer) return FamilyResult.Deny(FamilyReasonCode.WrongProposer);
            p.Status = FamilyProposalStatus.Withdrawn; AddCooldown(p); ReleaseChain(p.ChainId); return FamilyResult.Allow(id);
        }

        public FamilyResult TryCounter(string id, Clan responder, MarriageDirection direction, Clan goldPayer, int giftAmount)
        {
            var old = FindPending(id); if (old == null || old.ReceiverClan != responder) return FamilyResult.Deny(FamilyReasonCode.WrongResponder);
            if (old.CounterCount >= 1) return FamilyResult.Deny(FamilyReasonCode.CounterLimitReached);
            var draft = new MarriageProposalDraft { ProposerClan = old.ReceiverClan, ReceiverClan = old.ProposerClan, HeroA = old.HeroA, HeroB = old.HeroB, Direction = direction, GoldPayerClan = goldPayer, GiftAmount = giftAmount };
            Clan destination = direction == MarriageDirection.FemaleJoinsMaleClan ? (old.HeroA.IsFemale ? old.HeroB.Clan : old.HeroA.Clan) : (old.HeroA.IsFemale ? old.HeroA.Clan : old.HeroB.Clan);
            var p = new FamilyProposal { ProposalId = Guid.NewGuid().ToString("N"), ChainId = old.ChainId, ParentProposalId = old.ProposalId, CounterCount = 1, ProposalType = FamilyProposalType.Marriage, Status = FamilyProposalStatus.PendingResponse, ProposerClan = draft.ProposerClan, ReceiverClan = draft.ReceiverClan, HeroA = old.HeroA, HeroB = old.HeroB, OriginClanA = old.OriginClanA, OriginClanB = old.OriginClanB, DestinationClan = destination, GoldPayerClan = goldPayer, GoldReceiverClan = giftAmount > 0 ? (goldPayer == old.OriginClanA ? old.OriginClanB : old.OriginClanA) : null, GiftAmount = giftAmount, CreatedAt = CampaignTime.Now, ReplyDeadline = CampaignTime.DaysFromNow(FamilyConfig.ReplyDays) };
            var valid = _validator.CanProposeMarriage(p); if (!valid.IsAllowed) return valid;
            old.Status = FamilyProposalStatus.SupersededByCounter; _proposals.Add(p); _locks[p.HeroA] = p.ChainId; _locks[p.HeroB] = p.ChainId; return FamilyResult.Allow(p.ProposalId);
        }

        public IReadOnlyList<FamilyProposal> GetIncoming(Clan clan) => _proposals.Where(p => p.ReceiverClan == clan && p.IsActive).ToList();
        public IReadOnlyList<FamilyProposal> GetOutgoing(Clan clan) => _proposals.Where(p => p.ProposerClan == clan && p.IsActive).ToList();
        public void ReleaseCompleted(FamilyProposal p) => ReleaseChain(p.ChainId);
        public void Invalidate(FamilyProposal p, FamilyReasonCode reason, bool cooldown) { p.Status = FamilyProposalStatus.Invalidated; p.ReasonCode = reason; if (cooldown) AddCooldown(p); ReleaseChain(p.ChainId); }
        private FamilyProposal FindPending(string id) => _proposals.FirstOrDefault(p => p.ProposalId == id && p.Status == FamilyProposalStatus.PendingResponse);
        private bool IsLocked(Hero h) => h != null && _locks.ContainsKey(h);
        private bool OnCooldown(Hero a, Hero b) => _cooldowns.Any(c => c != null && !c.Until.IsPast && ((c.HeroA == a && c.HeroB == b) || (c.HeroA == b && c.HeroB == a)));
        private void AddCooldown(FamilyProposal p) { if (p.HeroA != null && p.HeroB != null) _cooldowns.Add(new FamilyCooldownRecord { HeroA = p.HeroA, HeroB = p.HeroB, Until = CampaignTime.DaysFromNow(FamilyConfig.PairCooldownDays) }); }
        private void ReleaseChain(string chain) { foreach (var key in _locks.Where(x => x.Value == chain).Select(x => x.Key).ToList()) _locks.Remove(key); }
    }
}
