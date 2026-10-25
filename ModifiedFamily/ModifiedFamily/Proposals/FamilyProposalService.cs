// Sole mutation boundary for proposal state, hero locks, counters, and cooldowns.
using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedFamily.Core;
using ModifiedFamily.Persistence;
using TaleWorlds.CampaignSystem;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Localization;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Adoption.Models;

namespace ModifiedFamily.Proposals
{
    public sealed class FamilyProposalService
    {
        public event Action<FamilyProposal> ProposalCreated;
        public event Action<FamilyProposal> ProposalInvalidated;
        private readonly IList<FamilyProposal> _proposals;
        private readonly IList<FamilyCooldownRecord> _cooldowns;
        private readonly MarriageProposalModel _marriageModel;
        private readonly AdoptionModel _adoptionModel;
        private readonly Dictionary<Hero, string> _locks = new Dictionary<Hero, string>();

        public FamilyProposalService(IList<FamilyProposal> proposals, IList<FamilyCooldownRecord> cooldowns, MarriageProposalModel marriageModel, AdoptionModel adoptionModel)
        { _proposals = proposals; _cooldowns = cooldowns; _marriageModel = marriageModel; _adoptionModel = adoptionModel; RebuildIndexes(); }

        public void RebuildIndexes()
        {
            // Locks are derived runtime state; persisting them separately could create stale save data.
            _locks.Clear();
            foreach (var p in _proposals.Where(x => x != null && x.IsActive))
            { if (p.HeroA != null) _locks[p.HeroA] = p.ChainId; if (p.HeroB != null) _locks[p.HeroB] = p.ChainId; if (p.AdopteeHero != null) _locks[p.AdopteeHero] = p.ChainId; }
        }

        public FamilyResult TryCreateMarriageProposal(MarriageProposalDraft draft)
        {
            if (draft == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            var p = CreateMarriageProposalPreview(draft);
            var valid = ValidateNewMarriageProposal(p); if (!valid.IsAllowed) return valid;
            _proposals.Add(p); _locks[p.HeroA] = p.ChainId; _locks[p.HeroB] = p.ChainId;
            FamilyDiagnostics.Info(FamilyLogText.MarriageProposal(p, false));
            ProposalCreated?.Invoke(p);
            return FamilyResult.Allow(p.ProposalId, valid.TemporaryBlockers.ToArray());
        }

        // Provides the exact non-mutating validation used by UI candidate discovery.
        public FamilyResult CanCreateMarriageProposal(MarriageProposalDraft draft)
        {
            if (draft == null || draft.HeroA == null || draft.HeroB == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            return ValidateNewMarriageProposal(CreateMarriageProposalPreview(draft));
        }

        public FamilyProposal CreateMarriageProposalPreview(MarriageProposalDraft draft)
        {
            Clan destination = draft.Direction == MarriageDirection.FemaleJoinsMaleClan ? (draft.HeroA.IsFemale ? draft.HeroB.Clan : draft.HeroA.Clan) : (draft.HeroA.IsFemale ? draft.HeroA.Clan : draft.HeroB.Clan);
            // The absolute deadline is saved with the proposal and checked by the daily lifecycle tick.
            return new FamilyProposal { ProposalId = Guid.NewGuid().ToString("N"), ChainId = Guid.NewGuid().ToString("N"), ProposalType = FamilyProposalType.Marriage, Status = FamilyProposalStatus.PendingResponse, ProposerClan = draft.ProposerClan, ReceiverClan = draft.ReceiverClan, HeroA = draft.HeroA, HeroB = draft.HeroB, OriginClanA = draft.HeroA?.Clan, OriginClanB = draft.HeroB?.Clan, DestinationClan = destination, GoldPayerClan = draft.GoldPayerClan, GoldReceiverClan = draft.GiftAmount > 0 ? (draft.GoldPayerClan == draft.ProposerClan ? draft.ReceiverClan : draft.ProposerClan) : null, GiftAmount = draft.GiftAmount, CreatedAt = CampaignTime.Now, ReplyDeadline = CampaignTime.DaysFromNow(FamilyConfig.ReplyDays), ExecutionStep = FamilyExecutionStep.NotStarted };
        }

        private FamilyResult ValidateNewMarriageProposal(FamilyProposal proposal)
        {
            FamilyResult valid = _marriageModel.CanProposeMarriage(proposal); if (!valid.IsAllowed) return valid;
            if (proposal.GiftAmount > 0 && (proposal.GoldPayerClan?.Leader == null || proposal.GoldPayerClan.Leader.Gold < proposal.GiftAmount)) return FamilyResult.Deny(FamilyReasonCode.InsufficientFunds);
            if (IsLocked(proposal.HeroA) || IsLocked(proposal.HeroB)) return FamilyResult.Deny(FamilyReasonCode.HeroLocked);
            if (OnCooldown(proposal.HeroA, proposal.HeroB)) return FamilyResult.Deny(FamilyReasonCode.PairCooldown);
            return valid;
        }

        public FamilyResult TryCreateAdoptionProposal(AdoptionProposalDraft draft)
        {
            if (draft == null || draft.AdopterHero == null || draft.AdopteeHero == null || draft.DestinationClan == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (IsLocked(draft.AdopteeHero)) return FamilyResult.Deny(FamilyReasonCode.HeroLocked);
            var valid = _adoptionModel.CanCreate(draft.AdopterHero, draft.AdopteeHero, draft.DestinationClan, draft.ReceiverClan);
            if (!valid.IsAllowed) return valid;
            var p = new FamilyProposal
            {
                ProposalId = Guid.NewGuid().ToString("N"), ChainId = Guid.NewGuid().ToString("N"), ProposalType = FamilyProposalType.Adoption,
                Status = draft.ReceiverClan == null || draft.ReceiverClan == draft.ProposerClan ? FamilyProposalStatus.AcceptedPendingExecution : FamilyProposalStatus.PendingResponse,
                ProposerClan = draft.ProposerClan, ReceiverClan = draft.ReceiverClan, AdopterHero = draft.AdopterHero, AdopteeHero = draft.AdopteeHero,
                OriginClanA = draft.AdopterHero.Clan, OriginClanB = draft.AdopteeHero.Clan, DestinationClan = draft.DestinationClan,
                CreatedAt = CampaignTime.Now, ReplyDeadline = CampaignTime.DaysFromNow(FamilyConfig.ReplyDays), ExecutionDeadline = CampaignTime.DaysFromNow(FamilyConfig.ExecutionDays)
            };
            _proposals.Add(p); _locks[p.AdopteeHero] = p.ChainId;
            FamilyDiagnostics.Info(FamilyLogText.AdoptionProposal(p));
            ProposalCreated?.Invoke(p);
            return FamilyResult.Allow(p.ProposalId);
        }

        public FamilyResult TryAccept(string id, Clan responder)
        {
            var p = FindPending(id); if (p == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (p.ReceiverClan != responder) return FamilyResult.Deny(FamilyReasonCode.WrongResponder);
            if (p.ProposalType == FamilyProposalType.Adoption)
            {
                if (p.AdopterHero == null || p.AdopteeHero == null || p.AdopteeHero.IsDead || p.AdopteeHero.Clan != p.OriginClanB)
                { Invalidate(p, FamilyReasonCode.ClanChanged, false); return FamilyResult.Deny(FamilyReasonCode.ClanChanged); }
                p.Status = FamilyProposalStatus.AcceptedPendingExecution; p.ExecutionDeadline = CampaignTime.DaysFromNow(FamilyConfig.ExecutionDays); FamilyDiagnostics.Notice(FamilyLogText.AdoptionAccepted(p)); return FamilyResult.Allow(id);
            }
            var valid = _marriageModel.CanRespond(p); if (!valid.IsAllowed) { Invalidate(p, valid.ReasonCode, false); return valid; }
            p.Status = FamilyProposalStatus.AcceptedPendingExecution; p.ExecutionDeadline = CampaignTime.DaysFromNow(FamilyConfig.ExecutionDays); FamilyDiagnostics.Notice(FamilyLogText.MarriageAccepted(p)); return FamilyResult.Allow(id, valid.TemporaryBlockers.ToArray());
        }

        public FamilyResult TryReject(string id, Clan responder, FamilyReasonCode reason)
        {
            var p = FindPending(id); if (p == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (p.ReceiverClan != responder) return FamilyResult.Deny(FamilyReasonCode.WrongResponder);
            p.Status = FamilyProposalStatus.Rejected; p.ReasonCode = reason; AddCooldown(p); ReleaseChain(p.ChainId); FamilyDiagnostics.Info(FamilyLogText.MarriageRejected(p)); return FamilyResult.Allow(id);
        }

        public FamilyResult TryExpirePending(string id)
        {
            var p = FindPending(id); if (p == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            p.Status = FamilyProposalStatus.TimedOut; p.ReasonCode = FamilyReasonCode.Expired;
            AddCooldown(p); ReleaseChain(p.ChainId); FamilyDiagnostics.Info(FamilyLogText.ProposalTimedOut(p));
            return FamilyResult.Allow(id);
        }

        public FamilyResult TryWithdraw(string id, Clan proposer)
        {
            var p = FindPending(id); if (p == null) return FamilyResult.Deny(FamilyReasonCode.InvalidProposal);
            if (p.ProposerClan != proposer) return FamilyResult.Deny(FamilyReasonCode.WrongProposer);
            p.Status = FamilyProposalStatus.Withdrawn; AddCooldown(p); ReleaseChain(p.ChainId); FamilyDiagnostics.Info(FamilyLogText.Withdrawn(p)); return FamilyResult.Allow(id);
        }

        public FamilyResult TryCounter(string id, Clan responder, MarriageDirection direction, Clan goldPayer, int giftAmount)
        {
            var old = FindPending(id); if (old == null || old.ReceiverClan != responder) return FamilyResult.Deny(FamilyReasonCode.WrongResponder);
            var current = _marriageModel.CanRespond(old); if (!current.IsAllowed) { TryInvalidateActive(old, current.ReasonCode); return current; }
            if (old.CounterCount >= 1) return FamilyResult.Deny(FamilyReasonCode.CounterLimitReached);
            if (giftAmount < 0) return FamilyResult.Deny(FamilyReasonCode.InvalidGift);
            var draft = new MarriageProposalDraft { ProposerClan = old.ReceiverClan, ReceiverClan = old.ProposerClan, HeroA = old.HeroA, HeroB = old.HeroB, Direction = direction, GoldPayerClan = goldPayer, GiftAmount = giftAmount };
            Clan destination = direction == MarriageDirection.FemaleJoinsMaleClan ? (old.HeroA.IsFemale ? old.HeroB.Clan : old.HeroA.Clan) : (old.HeroA.IsFemale ? old.HeroA.Clan : old.HeroB.Clan);
            var p = new FamilyProposal { ProposalId = Guid.NewGuid().ToString("N"), ChainId = old.ChainId, ParentProposalId = old.ProposalId, CounterCount = 1, ProposalType = FamilyProposalType.Marriage, Status = FamilyProposalStatus.PendingResponse, ProposerClan = draft.ProposerClan, ReceiverClan = draft.ReceiverClan, HeroA = old.HeroA, HeroB = old.HeroB, OriginClanA = old.OriginClanA, OriginClanB = old.OriginClanB, DestinationClan = destination, GoldPayerClan = goldPayer, GoldReceiverClan = giftAmount > 0 ? (goldPayer == old.OriginClanA ? old.OriginClanB : old.OriginClanA) : null, GiftAmount = giftAmount, CreatedAt = CampaignTime.Now, ReplyDeadline = CampaignTime.DaysFromNow(FamilyConfig.ReplyDays) };
            var valid = _marriageModel.CanProposeMarriage(p); if (!valid.IsAllowed) return valid;
            bool sameTerms = p.DestinationClan == old.DestinationClan && p.GoldPayerClan == old.GoldPayerClan && p.GiftAmount == old.GiftAmount;
            if (sameTerms) return FamilyResult.Deny(FamilyReasonCode.UnacceptableTerms);
            if (p.GiftAmount > 0 && (p.GoldPayerClan?.Leader == null || p.GoldPayerClan.Leader.Gold < p.GiftAmount)) return FamilyResult.Deny(FamilyReasonCode.InsufficientFunds);
            // The old record and replacement share one chain, so hero locks transfer atomically.
            old.Status = FamilyProposalStatus.SupersededByCounter; _proposals.Add(p); _locks[p.HeroA] = p.ChainId; _locks[p.HeroB] = p.ChainId; FamilyDiagnostics.Notice(FamilyLogText.MarriageProposal(p, true)); ProposalCreated?.Invoke(p); return FamilyResult.Allow(p.ProposalId);
        }

        public IReadOnlyList<FamilyProposal> GetIncoming(Clan clan) => _proposals.Where(p => p.ReceiverClan == clan && p.IsActive).ToList();
        public IReadOnlyList<FamilyProposal> GetOutgoing(Clan clan) => _proposals.Where(p => p.ProposerClan == clan && p.IsActive).ToList();
        public FamilyProposal GetProposal(string id) => id == null ? null : _proposals.FirstOrDefault(p => p?.ProposalId == id);
        // UI diagnostics use this read-only lookup to explain why a selected hero cannot start another negotiation.
        public FamilyProposal GetActiveProposalFor(Hero hero) => hero == null ? null : _proposals.FirstOrDefault(p => p != null && p.IsActive && (p.HeroA == hero || p.HeroB == hero || p.AdopteeHero == hero));
        public void ReleaseCompleted(FamilyProposal p) => ReleaseChain(p.ChainId);
        public bool TryInvalidateDueToPoliticalChange(FamilyProposal p, FamilyReasonCode reason)
        {
            if (p == null || !MarriagePoliticalValidationModel.IsPoliticalFailure(reason)) return false;
            return TryInvalidateActive(p, reason);
        }
        public bool TryInvalidateActive(FamilyProposal p, FamilyReasonCode reason)
        {
            if (p == null) return false;
            if (p.Status != FamilyProposalStatus.PendingResponse && p.Status != FamilyProposalStatus.AcceptedPendingExecution) return false;
            Invalidate(p, reason, false);
            return true;
        }
        public void Invalidate(FamilyProposal p, FamilyReasonCode reason, bool cooldown)
        {
            if (p == null || p.Status == FamilyProposalStatus.Invalidated || p.Status == FamilyProposalStatus.Completed ||
                p.Status == FamilyProposalStatus.Executing || p.Status == FamilyProposalStatus.PaymentReconcileRequired) return;
            p.Status = FamilyProposalStatus.Invalidated; p.ReasonCode = reason; if (cooldown) AddCooldown(p); ReleaseChain(p.ChainId); FamilyDiagnostics.Warn(FamilyLogText.Invalidated(p));
            FamilyDiagnostics.DebugLog(FamilyLogText.MarriageDecision(p, "Invalidated", reason));
            ProposalInvalidated?.Invoke(p);
        }
        private FamilyProposal FindPending(string id) => _proposals.FirstOrDefault(p => p.ProposalId == id && p.Status == FamilyProposalStatus.PendingResponse);
        private bool IsLocked(Hero h) => h != null && _locks.ContainsKey(h);
        private bool OnCooldown(Hero a, Hero b) => _cooldowns.Any(c => c != null && !c.Until.IsPast && ((c.HeroA == a && c.HeroB == b) || (c.HeroA == b && c.HeroB == a)));
        private void AddCooldown(FamilyProposal p) { if (p.HeroA != null && p.HeroB != null) _cooldowns.Add(new FamilyCooldownRecord { HeroA = p.HeroA, HeroB = p.HeroB, Until = CampaignTime.DaysFromNow(FamilyConfig.PairCooldownDays) }); }
        private void ReleaseChain(string chain) { foreach (var key in _locks.Where(x => x.Value == chain).Select(x => x.Key).ToList()) _locks.Remove(key); }
    }
}
