// Decentralized clan AI: searches bounded candidates and independently negotiates each proposal.
using System.Collections.Generic;
using System.Linq;
using ModifiedFamily.Core;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Localization;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Marriage.AI
{
    public sealed class ClanMarriageDecisionService
    {
        private readonly FamilyProposalService _service;
        private readonly MarriageProposalModel _proposalModel;
        private readonly ClanHeroValueModel _values = new ClanHeroValueModel();
        private static readonly int[] GiftSteps = { 5000, 10000, 20000 };

        public ClanMarriageDecisionService(FamilyProposalService service, MarriageProposalModel proposalModel)
        { _service = service; _proposalModel = proposalModel; }

        public void ConsiderClan(Clan clan)
        {
            if (clan == Clan.PlayerClan || !MarriageEligibilityModel.CanUseClan(clan).IsAllowed || _service.GetOutgoing(clan).Count > 0) return;
            List<Hero> ownHeroes = clan.Heroes.Where(h => MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed).Take(3).ToList();
            if (ownHeroes.Count == 0) return;
            List<Clan> targetClans = Clan.All.Where(c => c != clan && MarriageEligibilityModel.CanUseClan(c).IsAllowed && (MarriagePolicyModel.EnableAiToPlayer || c != Clan.PlayerClan))
                .OrderBy(c => ClanTier(clan, c)).ThenByDescending(c => clan.GetRelationWithClan(c)).Take(8).ToList();
            var candidates = new List<OfferCandidate>();
            foreach (Hero own in ownHeroes)
            foreach (Clan targetClan in targetClans)
            foreach (Hero other in targetClan.Heroes.Where(h => MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed && h.IsFemale != own.IsFemale).Take(3))
            foreach (MarriageDirection direction in new[] { MarriageDirection.FemaleJoinsMaleClan, MarriageDirection.MaleJoinsFemaleClan })
            {
                AddCandidate(candidates, clan, targetClan, own, other, direction, null, 0);
                foreach (int gift in GiftSteps)
                {
                    if (clan.Leader != null && clan.Leader.Gold >= gift) AddCandidate(candidates, clan, targetClan, own, other, direction, clan, gift);
                    if (targetClan.Leader != null && targetClan.Leader.Gold >= gift) AddCandidate(candidates, clan, targetClan, own, other, direction, targetClan, gift);
                }
            }
            foreach (OfferCandidate candidate in candidates.OrderBy(x => x.Tier).ThenByDescending(x => x.ProposerScore).ThenByDescending(x => x.ReceiverScore))
            {
                FamilyResult created = _service.TryCreateMarriageProposal(candidate.Draft);
                if (!created.IsAllowed) continue;
                FamilyDiagnostics.DebugLog(FamilyLogText.AiCandidate(clan, candidate.Draft.HeroA, candidate.Draft.HeroB, candidate.Tier, candidate.ProposerScore));
                FamilyProposal proposal = _service.GetProposal(created.ProposalId);
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(proposal, _values.Evaluate(proposal, clan), "Proposer"));
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(proposal, _values.Evaluate(proposal, candidate.Draft.ReceiverClan), "PredictedReceiver"));
                return;
            }
        }

        public void Respond(FamilyProposal proposal)
        {
            if (proposal == null || proposal.ReceiverClan == Clan.PlayerClan)
            {
                FamilyDiagnostics.Warn("Prevented AI from responding to a marriage proposal on behalf of the player clan; the proposal remains pending for player input.");
                return;
            }
            FamilyResult valid = _proposalModel.CanRespond(proposal);
            if (!valid.IsAllowed)
            {
                TraceDecision(proposal, null, "Invalidated", valid.ReasonCode);
                _service.TryInvalidateActive(proposal, valid.ReasonCode);
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageDecision(proposal, "Invalidated", valid.ReasonCode));
                return;
            }
            MarriageScoreBreakdown score = _values.Evaluate(proposal, proposal.ReceiverClan);
            FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(proposal, score, "Receiver"));
            if (score.Total >= MarriageScoreTuning.AcceptanceThreshold)
            {
                TraceDecision(proposal, score, "Accept", FamilyReasonCode.None);
                FamilyResult result = _service.TryAccept(proposal.ProposalId, proposal.ReceiverClan);
                TraceActionResult("AI_ACCEPT_RESULT", proposal, result);
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageDecision(proposal, "Accepted", FamilyReasonCode.None));
                return;
            }
            if (proposal.CounterCount == 0)
            {
                TraceDecision(proposal, score, "TryCounter", FamilyReasonCode.LowClanBenefit);
                if (TryCounter(proposal)) return;
            }
            FamilyReasonCode reason = FamilyReasonCode.LowClanBenefit;
            if (proposal.CounterCount != 0)
                TraceDecision(proposal, score, "Reject", reason);
            FamilyResult rejected = _service.TryReject(proposal.ProposalId, proposal.ReceiverClan, reason);
            TraceActionResult("AI_REJECT_RESULT", proposal, rejected);
            FamilyDiagnostics.DebugLog(FamilyLogText.MarriageDecision(proposal, "Rejected", reason));
        }

        public bool ShouldContinueAcceptedProposal(FamilyProposal proposal)
        {
            if (proposal == null || proposal.Status != FamilyProposalStatus.AcceptedPendingExecution) return true;
            foreach (Clan clan in new[] { proposal.ProposerClan, proposal.ReceiverClan }.Where(c => c != null && c != Clan.PlayerClan).Distinct())
            {
                MarriageScoreBreakdown score = _values.Evaluate(proposal, clan);
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(proposal, score, "PoliticalReassessment"));
                if (score.Total < MarriageScoreTuning.CommitmentThreshold) return false;
            }
            return true;
        }

        private void AddCandidate(List<OfferCandidate> candidates, Clan proposer, Clan receiver, Hero own, Hero other, MarriageDirection direction, Clan payer, int gift)
        {
            var draft = new MarriageProposalDraft { ProposerClan = proposer, ReceiverClan = receiver, HeroA = own, HeroB = other, Direction = direction, GoldPayerClan = payer, GiftAmount = gift };
            FamilyResult allowed = _service.CanCreateMarriageProposal(draft);
            if (!allowed.IsAllowed) return;
            FamilyProposal preview = _service.CreateMarriageProposalPreview(draft);
            float proposerScore = _values.Evaluate(preview, proposer).Total;
            float receiverScore = _values.Evaluate(preview, receiver).Total;
            if (proposerScore < MarriageScoreTuning.AcceptanceThreshold || receiverScore < MarriageScoreTuning.CandidateReceiverFloor) return;
            candidates.Add(new OfferCandidate { Draft = draft, Tier = MatchTier(own, other), ProposerScore = proposerScore, ReceiverScore = receiverScore });
        }

        private bool TryCounter(FamilyProposal original)
        {
            Clan responder = original.ReceiverClan;
            Clan otherClan = original.ProposerClan;
            MarriageDirection currentDirection = DirectionOf(original);
            var terms = new List<CounterTerms> { new CounterTerms { Direction = Opposite(currentDirection) } };
            foreach (MarriageDirection direction in new[] { currentDirection, Opposite(currentDirection) })
            foreach (int gift in GiftSteps)
            {
                if (otherClan?.Leader != null && otherClan.Leader.Gold >= gift)
                    terms.Add(new CounterTerms { Direction = direction, GoldPayer = otherClan, GiftAmount = gift });
                if (responder?.Leader != null && responder.Leader.Gold >= gift)
                    terms.Add(new CounterTerms { Direction = direction, GoldPayer = responder, GiftAmount = gift });
            }

            var ranked = terms.Select(t => new { Term = t, Preview = CounterPreview(original, t) })
                .Where(x => _proposalModel.CanProposeMarriage(x.Preview).IsAllowed)
                .Select(x => new { x.Term, x.Preview, Own = _values.Evaluate(x.Preview, responder), Other = _values.Evaluate(x.Preview, otherClan) })
                .Where(x => x.Own.Total >= MarriageScoreTuning.AcceptanceThreshold && x.Other.Total >= MarriageScoreTuning.CounterOtherClanFloor)
                .OrderByDescending(x => x.Own.Total + x.Other.Total).ToList();
            FamilyResult lastResult = null;
            foreach (var candidate in ranked)
            {
                FamilyResult result = _service.TryCounter(original.ProposalId, responder, candidate.Term.Direction, candidate.Term.GoldPayer, candidate.Term.GiftAmount);
                if (!result.IsAllowed) { lastResult = result; continue; }
                FamilyProposal counter = _service.GetProposal(result.ProposalId);
                if (MarriageDiagnosticTrace.IsPlayerRelated(original))
                    MarriageDiagnosticTrace.Write("AI_COUNTER_RESULT",
                    MarriageDiagnosticTrace.F("sourceProposalId", original.ProposalId),
                    MarriageDiagnosticTrace.F("newProposalId", result.ProposalId),
                    MarriageDiagnosticTrace.F("chainId", original.ChainId),
                    MarriageDiagnosticTrace.F("directionsCompared", ranked.Select(x => x.Term.Direction).Distinct().Count()),
                    MarriageDiagnosticTrace.F("gift", candidate.Term.GiftAmount),
                    MarriageDiagnosticTrace.F("ownScore", candidate.Own.Total),
                    MarriageDiagnosticTrace.F("otherScore", candidate.Other.Total),
                    MarriageDiagnosticTrace.F("acceptanceThreshold", MarriageScoreTuning.AcceptanceThreshold),
                    MarriageDiagnosticTrace.F("otherClanFloor", MarriageScoreTuning.CounterOtherClanFloor),
                    MarriageDiagnosticTrace.F("reason", "Created"),
                    MarriageDiagnosticTrace.F("newStatus", counter?.Status.ToString() ?? "missing"));
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(counter, _values.Evaluate(counter, responder), "CounterProposer"));
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(counter, _values.Evaluate(counter, otherClan), "PredictedCounterReceiver"));
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageDecision(original, "Countered", FamilyReasonCode.LowClanBenefit));
                return true;
            }
            if (MarriageDiagnosticTrace.IsPlayerRelated(original))
                MarriageDiagnosticTrace.Write("AI_COUNTER_RESULT",
                MarriageDiagnosticTrace.F("sourceProposalId", original.ProposalId),
                MarriageDiagnosticTrace.F("newProposalId", "none"),
                MarriageDiagnosticTrace.F("chainId", original.ChainId),
                MarriageDiagnosticTrace.F("directionsCompared", ranked.Select(x => x.Term.Direction).Distinct().Count()),
                MarriageDiagnosticTrace.F("candidateCount", ranked.Count),
                MarriageDiagnosticTrace.F("acceptanceThreshold", MarriageScoreTuning.AcceptanceThreshold),
                MarriageDiagnosticTrace.F("otherClanFloor", MarriageScoreTuning.CounterOtherClanFloor),
                MarriageDiagnosticTrace.F("reason", lastResult?.ReasonCode.ToString() ?? "NoViableCounterProposal"),
                MarriageDiagnosticTrace.F("newStatus", "none"));
            return false;
        }

        private static void TraceDecision(FamilyProposal proposal,
            MarriageScoreBreakdown score, string plannedDecision,
            FamilyReasonCode reason)
        {
            if (!MarriageDiagnosticTrace.IsPlayerRelated(proposal)) return;
            MarriageDiagnosticTrace.Write("AI_DECISION",
                MarriageDiagnosticTrace.F("proposalId", proposal?.ProposalId),
                MarriageDiagnosticTrace.F("chainId", proposal?.ChainId),
                MarriageDiagnosticTrace.F("parentProposalId", proposal?.ParentProposalId ?? "none"),
                MarriageDiagnosticTrace.F("counterCount", proposal?.CounterCount ?? 0),
                MarriageDiagnosticTrace.F("proposerClanName", MarriageDiagnosticTrace.Name(proposal?.ProposerClan)),
                MarriageDiagnosticTrace.F("proposerClanId", MarriageDiagnosticTrace.Id(proposal?.ProposerClan)),
                MarriageDiagnosticTrace.F("receiverClanName", MarriageDiagnosticTrace.Name(proposal?.ReceiverClan)),
                MarriageDiagnosticTrace.F("receiverClanId", MarriageDiagnosticTrace.Id(proposal?.ReceiverClan)),
                MarriageDiagnosticTrace.F("heroAName", MarriageDiagnosticTrace.Name(proposal?.HeroA)),
                MarriageDiagnosticTrace.F("heroAId", MarriageDiagnosticTrace.Id(proposal?.HeroA)),
                MarriageDiagnosticTrace.F("heroBName", MarriageDiagnosticTrace.Name(proposal?.HeroB)),
                MarriageDiagnosticTrace.F("heroBId", MarriageDiagnosticTrace.Id(proposal?.HeroB)),
                MarriageDiagnosticTrace.F("destinationClanId", MarriageDiagnosticTrace.Id(proposal?.DestinationClan)),
                MarriageDiagnosticTrace.F("direction", MarriageDiagnosticTrace.Direction(proposal)),
                MarriageDiagnosticTrace.F("goldPayerClanId", MarriageDiagnosticTrace.Id(proposal?.GoldPayerClan)),
                MarriageDiagnosticTrace.F("giftAmount", proposal?.GiftAmount ?? 0),
                MarriageDiagnosticTrace.F("ownAdultCount", score?.AdultHeroCount ?? 0),
                MarriageDiagnosticTrace.F("receivesHero", proposal?.DestinationClan == proposal?.ReceiverClan),
                MarriageDiagnosticTrace.F("scoreTotal", score?.Total ?? 0f),
                MarriageDiagnosticTrace.F("acceptanceThreshold", MarriageScoreTuning.AcceptanceThreshold),
                MarriageDiagnosticTrace.F("scoreMinusThreshold", (score?.Total ?? 0f) - MarriageScoreTuning.AcceptanceThreshold),
                MarriageDiagnosticTrace.F("baseHeroValue", score?.BaseHeroValue ?? 0f),
                MarriageDiagnosticTrace.F("inflow", score?.InflowValue ?? 0f),
                MarriageDiagnosticTrace.F("outflow", score?.OutflowCost ?? 0f),
                MarriageDiagnosticTrace.F("succession", score?.SuccessionPenalty ?? 0f),
                MarriageDiagnosticTrace.F("alliance", score?.AllianceBenefit ?? 0f),
                MarriageDiagnosticTrace.F("relationRaw", score?.RelationRaw ?? 0),
                MarriageDiagnosticTrace.F("relationValue", score?.RelationValue ?? 0f),
                MarriageDiagnosticTrace.F("cultureValue", score?.CultureValue ?? 0f),
                MarriageDiagnosticTrace.F("kingdomValue", score?.KingdomValue ?? 0f),
                MarriageDiagnosticTrace.F("giftValue", score?.GiftValue ?? 0f),
                MarriageDiagnosticTrace.F("plannedDecision", plannedDecision),
                MarriageDiagnosticTrace.F("reason", reason),
                MarriageDiagnosticTrace.F("statusBefore", proposal?.Status.ToString() ?? "none"));
        }

        private static void TraceActionResult(string eventName,
            FamilyProposal proposal, FamilyResult result)
        {
            if (!MarriageDiagnosticTrace.IsPlayerRelated(proposal)) return;
            MarriageDiagnosticTrace.Write(eventName,
                MarriageDiagnosticTrace.F("proposalId", proposal?.ProposalId),
                MarriageDiagnosticTrace.F("chainId", proposal?.ChainId),
                MarriageDiagnosticTrace.F("operationAllowed", result?.IsAllowed == true),
                MarriageDiagnosticTrace.F("reason", result?.ReasonCode.ToString() ?? "none"),
                MarriageDiagnosticTrace.F("statusAfter", proposal?.Status.ToString() ?? "none"));
        }

        private FamilyProposal CounterPreview(FamilyProposal old, CounterTerms terms) => _service.CreateMarriageProposalPreview(new MarriageProposalDraft
        {
            ProposerClan = old.ReceiverClan, ReceiverClan = old.ProposerClan, HeroA = old.HeroA, HeroB = old.HeroB,
            Direction = terms.Direction, GoldPayerClan = terms.GoldPayer, GiftAmount = terms.GiftAmount
        });

        private static MarriageDirection DirectionOf(FamilyProposal p)
        {
            Hero female = p.HeroA.IsFemale ? p.HeroA : p.HeroB;
            return p.DestinationClan == female.Clan ? MarriageDirection.MaleJoinsFemaleClan : MarriageDirection.FemaleJoinsMaleClan;
        }
        private static MarriageDirection Opposite(MarriageDirection direction) => direction == MarriageDirection.FemaleJoinsMaleClan ? MarriageDirection.MaleJoinsFemaleClan : MarriageDirection.FemaleJoinsMaleClan;
        private static int ClanTier(Clan a, Clan b)
        {
            bool sameCulture = a?.Culture == b?.Culture, sameKingdom = a?.Kingdom != null && a.Kingdom == b?.Kingdom;
            if (sameKingdom && sameCulture) return 1;
            if (!sameKingdom && sameCulture) return 2;
            if (sameKingdom && MarriagePolicyModel.AllowSameKingdomDifferentCulture) return 3;
            return 99;
        }
        private static int MatchTier(Hero a, Hero b) => ClanTier(a?.Clan, b?.Clan);
        private sealed class OfferCandidate { public MarriageProposalDraft Draft; public int Tier; public float ProposerScore; public float ReceiverScore; }
        private sealed class CounterTerms { public MarriageDirection Direction; public Clan GoldPayer; public int GiftAmount; }
    }
}
