// Decentralized clan AI: searches bounded candidates and independently negotiates each proposal.
using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedFamily.Core;
using ModifiedFamily.Diagnostics;
using ModifiedFamily.Localization;
using ModifiedFamily.Marriage.Models;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Core;
using ModifiedFamily.Marriage.Configuration;

namespace ModifiedFamily.Marriage.AI
{
    public sealed class ClanMarriageDecisionService
    {
        private readonly FamilyProposalService _service;
        private readonly MarriageProposalModel _proposalModel;
        private readonly ClanHeroValueModel _values = new ClanHeroValueModel();

        public ClanMarriageDecisionService(FamilyProposalService service, MarriageProposalModel proposalModel)
        { _service = service; _proposalModel = proposalModel; }

        public void ConsiderClan(Clan clan, int today, int phase)
        {
            MarriageAiConfig config = MarriageAiConfig.Current;
            FamilyProposal previous = _service
                .GetLatestOriginalMarriageProposal(clan);
            int previousDay = previous == null ? int.MinValue :
                (int)Math.Floor(previous.CreatedAt.ToDays);
            if (previousDay < 0 || previousDay > today)
                previousDay = int.MinValue;
            int cooldownRemaining = previous == null ? 0 : Math.Max(0,
                config.ClanProposalCooldownDays - (today - previousDay));
            string reason = clan == Clan.PlayerClan ? "PlayerClan" :
                !MarriageEligibilityModel.CanUseClan(clan).IsAllowed
                    ? "ClanIneligible" :
                _service.GetOutgoing(clan).Any(proposal =>
                    proposal.ProposalType == FamilyProposalType.Marriage)
                    ? "ActiveOutgoingProposal" :
                cooldownRemaining > 0 ? "ClanProposalCooldown" : null;
            if (reason != null)
            {
                TraceProposalCheck(clan, today, phase, previousDay,
                    cooldownRemaining, config.BaseProposalChance, -1f,
                    false, reason, null);
                return;
            }
            float proposalRoll = StableProposalRoll(clan.StringId, today);
            bool enterSearch = proposalRoll < config.BaseProposalChance;
            TraceProposalCheck(clan, today, phase, previousDay,
                cooldownRemaining, config.BaseProposalChance, proposalRoll,
                enterSearch, enterSearch ? "ChancePassed" : "ChanceFailed",
                null);
            if (!enterSearch) return;
            List<Hero> ownHeroes = clan.Heroes.Where(h => MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed).Take(3).ToList();
            if (ownHeroes.Count == 0)
            {
                TraceProposalCheck(clan, today, phase, previousDay, 0,
                    config.BaseProposalChance, proposalRoll, true,
                    "NoEligibleOwnHero", null);
                return;
            }
            List<Clan> targetClans = Clan.All.Where(c => c != clan && MarriageEligibilityModel.CanUseClan(c).IsAllowed && (MarriagePolicyModel.EnableAiToPlayer || c != Clan.PlayerClan))
                .OrderBy(c => ClanTier(clan, c)).ThenByDescending(c => clan.GetRelationWithClan(c)).Take(8).ToList();
            var candidates = new List<OfferCandidate>();
            foreach (Hero own in ownHeroes)
            foreach (Clan targetClan in targetClans)
            foreach (Hero other in targetClan.Heroes.Where(h => MarriageEligibilityModel.CanNegotiateHero(h).IsAllowed && h.IsFemale != own.IsFemale).Take(3))
            foreach (MarriageDirection direction in new[] { MarriageDirection.FemaleJoinsMaleClan, MarriageDirection.MaleJoinsFemaleClan })
            {
                AddCandidate(candidates, clan, targetClan, own, other, direction, null, 0);
                foreach (int gift in config.GiftSteps)
                {
                    if (CanAiOfferGift(clan, gift, config)) AddCandidate(candidates, clan, targetClan, own, other, direction, clan, gift);
                    if (CanAiOfferGift(targetClan, gift, config)) AddCandidate(candidates, clan, targetClan, own, other, direction, targetClan, gift);
                }
            }
            foreach (OfferCandidate candidate in candidates.OrderBy(x => x.Tier)
                .ThenByDescending(x => x.ProposerScore)
                .ThenByDescending(x => x.ReceiverAcceptanceChance)
                .ThenBy(x => x.Draft.GiftAmount))
            {
                FamilyResult created = _service.TryCreateMarriageProposal(candidate.Draft);
                if (!created.IsAllowed) continue;
                FamilyDiagnostics.DebugLog(FamilyLogText.AiCandidate(clan, candidate.Draft.HeroA, candidate.Draft.HeroB, candidate.Tier, candidate.ProposerScore));
                FamilyProposal proposal = _service.GetProposal(created.ProposalId);
                TraceProposalCheck(clan, today, phase, previousDay, 0,
                    config.BaseProposalChance, proposalRoll, true,
                    "ProposalCreated", proposal);
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(proposal, _values.Evaluate(proposal, clan), "Proposer"));
                FamilyDiagnostics.DebugLog(FamilyLogText.MarriageEvaluation(proposal, _values.Evaluate(proposal, candidate.Draft.ReceiverClan), "PredictedReceiver"));
                return;
            }
            TraceProposalCheck(clan, today, phase, previousDay, 0,
                config.BaseProposalChance, proposalRoll, true,
                "NoViableCandidate", null);
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
            float chance = MarriageScoreTuning.CalculateAcceptanceChance(
                score.Total);
            if (chance <= 0f && proposal.CounterCount == 0)
            {
                TraceAcceptance(proposal, score, chance, -1f,
                    "TryCounter", false, "ZeroAcceptanceChance");
                if (TryCounter(proposal)) return;
            }
            else if (chance > 0f)
            {
                float roll = MBRandom.RandomFloat;
                bool accepted = roll < chance;
                FamilyResult action = accepted
                    ? _service.TryAccept(proposal.ProposalId,
                        proposal.ReceiverClan)
                    : _service.TryReject(proposal.ProposalId,
                        proposal.ReceiverClan,
                        FamilyReasonCode.LowClanBenefit);
                TraceAcceptance(proposal, score, chance, roll,
                    accepted ? "Accept" : "Reject", action.IsAllowed,
                    action.ReasonCode.ToString());
                TraceActionResult(accepted ? "AI_ACCEPT_RESULT" :
                    "AI_REJECT_RESULT", proposal, action);
                if (action.IsAllowed)
                    FamilyDiagnostics.DebugLog(FamilyLogText.MarriageDecision(
                        proposal, accepted ? "Accepted" : "Rejected",
                        accepted ? FamilyReasonCode.None :
                            FamilyReasonCode.LowClanBenefit));
                return;
            }
            FamilyReasonCode reason = FamilyReasonCode.LowClanBenefit;
            if (proposal.CounterCount != 0)
                TraceDecision(proposal, score, "Reject", reason);
            FamilyResult rejected = _service.TryReject(proposal.ProposalId, proposal.ReceiverClan, reason);
            TraceActionResult("AI_REJECT_RESULT", proposal, rejected);
            if (rejected.IsAllowed)
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
            float receiverChance = MarriageScoreTuning
                .CalculateAcceptanceChance(receiverScore);
            if (proposerScore < MarriageScoreTuning.AcceptanceThreshold ||
                receiver != Clan.PlayerClan && receiverChance <= 0f) return;
            candidates.Add(new OfferCandidate { Draft = draft,
                Tier = MatchTier(own, other), ProposerScore = proposerScore,
                ReceiverScore = receiverScore,
                ReceiverAcceptanceChance = receiverChance });
        }

        private bool TryCounter(FamilyProposal original)
        {
            Clan responder = original.ReceiverClan;
            Clan otherClan = original.ProposerClan;
            MarriageDirection currentDirection = DirectionOf(original);
            var terms = new List<CounterTerms> { new CounterTerms { Direction = Opposite(currentDirection) } };
            MarriageAiConfig config = MarriageAiConfig.Current;
            foreach (MarriageDirection direction in new[] { currentDirection, Opposite(currentDirection) })
            foreach (int gift in config.GiftSteps)
            {
                if (CanAiOfferGift(otherClan, gift, config))
                    terms.Add(new CounterTerms { Direction = direction, GoldPayer = otherClan, GiftAmount = gift });
                if (CanAiOfferGift(responder, gift, config))
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

        private static bool CanAiOfferGift(Clan payer, int gift,
            MarriageAiConfig config)
        {
            int gold = payer?.Leader?.Gold ?? -1;
            return gift >= 0 && gold >= gift && gift <=
                (int)Math.Floor(gold * config.AiMaxGiftGoldRatio);
        }

        private static float StableProposalRoll(string clanId, int day)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in clanId ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619;
                }
                return MBRandom.RandomFloatWithSeed(hash, (uint)day);
            }
        }

        private static void TraceProposalCheck(Clan clan, int today,
            int phase, int previousDay, int cooldownRemaining,
            float configuredChance, float randomValue, bool enteredSearch,
            string result, FamilyProposal proposal)
        {
            MarriageDiagnosticTrace.Write("AI_PROPOSAL_CHECK",
                MarriageDiagnosticTrace.F("day", today),
                MarriageDiagnosticTrace.F("clanName",
                    MarriageDiagnosticTrace.Name(clan)),
                MarriageDiagnosticTrace.F("clanId",
                    MarriageDiagnosticTrace.Id(clan)),
                MarriageDiagnosticTrace.F("phase", phase),
                MarriageDiagnosticTrace.F("lastOriginalProposalDay",
                    previousDay == int.MinValue ? "none" :
                        previousDay.ToString()),
                MarriageDiagnosticTrace.F("cooldownRemaining",
                    cooldownRemaining),
                MarriageDiagnosticTrace.F("configuredChance",
                    configuredChance),
                MarriageDiagnosticTrace.F("randomValue",
                    randomValue < 0f ? "not_drawn" : randomValue.ToString(
                        "0.0000", System.Globalization.CultureInfo.InvariantCulture)),
                MarriageDiagnosticTrace.F("enteredSearch", enteredSearch),
                MarriageDiagnosticTrace.F("result", result),
                MarriageDiagnosticTrace.F("proposalId",
                    proposal?.ProposalId ?? "none"),
                MarriageDiagnosticTrace.F("chainId",
                    proposal?.ChainId ?? "none"),
                MarriageDiagnosticTrace.F("gift", proposal?.GiftAmount ?? 0));
        }

        private static void TraceAcceptance(FamilyProposal proposal,
            MarriageScoreBreakdown score, float chance, float randomValue,
            string decision, bool serviceSucceeded, string serviceReason)
        {
            MarriageDiagnosticTrace.Write("AI_MARRIAGE_ACCEPTANCE",
                MarriageDiagnosticTrace.F("proposalId", proposal?.ProposalId),
                MarriageDiagnosticTrace.F("chainId", proposal?.ChainId),
                MarriageDiagnosticTrace.F("receiverClanName",
                    MarriageDiagnosticTrace.Name(proposal?.ReceiverClan)),
                MarriageDiagnosticTrace.F("receiverClanId",
                    MarriageDiagnosticTrace.Id(proposal?.ReceiverClan)),
                MarriageDiagnosticTrace.F("score", score?.Total ?? 0f),
                MarriageDiagnosticTrace.F("maxPositiveScore",
                    MarriageScoreTuning.MaxPositiveScore),
                MarriageDiagnosticTrace.F("acceptanceChance", chance),
                MarriageDiagnosticTrace.F("randomValue",
                    randomValue < 0f ? "not_drawn" : randomValue.ToString(
                        "0.0000", System.Globalization.CultureInfo.InvariantCulture)),
                MarriageDiagnosticTrace.F("counterCount",
                    proposal?.CounterCount ?? 0),
                MarriageDiagnosticTrace.F("decision", decision),
                MarriageDiagnosticTrace.F("serviceSucceeded",
                    serviceSucceeded),
                MarriageDiagnosticTrace.F("serviceReason", serviceReason));
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
        private sealed class OfferCandidate { public MarriageProposalDraft Draft; public int Tier; public float ProposerScore; public float ReceiverScore; public float ReceiverAcceptanceChance; }
        private sealed class CounterTerms { public MarriageDirection Direction; public Clan GoldPayer; public int GiftAmount; }
    }
}
