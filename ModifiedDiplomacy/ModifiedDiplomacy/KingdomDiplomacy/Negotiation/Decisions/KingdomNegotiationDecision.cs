using System.Collections.Generic;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedDiplomacy.KingdomDiplomacy.Negotiation.Decisions
{
    /// <summary>
    /// Runs one side of a compound diplomatic proposal through Bannerlord's
    /// native kingdom voting system. Approval by the proposing council creates
    /// a second decision for the receiving council; no terms execute here.
    /// </summary>
    public sealed class KingdomNegotiationDecision : StartAllianceDecision
    {
        [SaveableField(201)]
        private readonly List<KingdomNegotiationTermRecord> _terms;

        [SaveableField(202)]
        private readonly bool _isReceivingCouncil;

        private KingdomNegotiationEvaluation _cachedEvaluation;

        public Kingdom OtherKingdom => KingdomToStartAllianceWith;
        public bool IsReceivingCouncil => _isReceivingCouncil;
        public IReadOnlyList<KingdomNegotiationTermRecord> Terms => _terms;

        public KingdomNegotiationDecision(
            Clan proposerClan,
            Kingdom otherKingdom,
            IEnumerable<KingdomNegotiationTermRecord> terms,
            bool isReceivingCouncil)
            : base(proposerClan, otherKingdom)
        {
            _terms = (terms ?? Enumerable.Empty<KingdomNegotiationTermRecord>())
                .ToList();
            _isReceivingCouncil = isReceivingCouncil;
        }

        public KingdomNegotiationDraft CreateDraft()
        {
            Kingdom proposingKingdom = _isReceivingCouncil
                ? OtherKingdom
                : Kingdom;
            Kingdom receivingKingdom = _isReceivingCouncil
                ? Kingdom
                : OtherKingdom;
            return new KingdomNegotiationDraft(
                proposingKingdom,
                receivingKingdom,
                _terms.Select(x => x.ToDraftTerm()));
        }

        public override bool IsAllowed()
        {
            return CanStillVote(out _);
        }

        public override bool CanMakeDecision(
            out TextObject reason,
            bool includeReason = false)
        {
            return CanStillVote(out reason);
        }

        protected override bool ShouldBeCancelledInternal()
        {
            return !CanStillVote(out _);
        }

        public override int GetProposalInfluenceCost()
        {
            if (_isReceivingCouncil)
            {
                return 0;
            }

            // A larger package requires more political capital, while keeping
            // the cost in the same range as native diplomatic proposals.
            return 150 + System.Math.Min(150, (_terms.Count - 1) * 25);
        }

        public override TextObject GetGeneralTitle()
        {
            return BuildText(_isReceivingCouncil
                ? "{=MP_NegotiationResponseDecisionTitle}Respond to {KINGDOM}'s diplomatic proposal"
                : "{=MP_NegotiationProposalDecisionTitle}Send a diplomatic proposal to {KINGDOM}");
        }

        public override TextObject GetSupportTitle()
        {
            return BuildText(_isReceivingCouncil
                ? "{=MP_NegotiationResponseVoteTitle}Vote on accepting {KINGDOM}'s compound proposal."
                : "{=MP_NegotiationProposalVoteTitle}Vote on sending the compound proposal to {KINGDOM}.");
        }

        public override TextObject GetChooseTitle()
        {
            return GetSupportTitle();
        }

        public override TextObject GetSupportDescription()
        {
            return BuildText(_isReceivingCouncil
                ? "{=MP_NegotiationResponseVoteDescription}The council must accept or reject the entire proposal from {KINGDOM}. Individual terms cannot be separated."
                : "{=MP_NegotiationProposalVoteDescription}The council must decide whether to send the entire proposal to {KINGDOM}. Individual terms cannot be separated.");
        }

        public override TextObject GetChooseDescription()
        {
            return GetSupportDescription();
        }

        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            yield return new KingdomNegotiationDecisionOutcome(
                true,
                Kingdom,
                OtherKingdom,
                _isReceivingCouncil);
            yield return new KingdomNegotiationDecisionOutcome(
                false,
                Kingdom,
                OtherKingdom,
                _isReceivingCouncil);
        }

        public override void DetermineSponsors(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            foreach (DecisionOutcome outcome in possibleOutcomes)
            {
                KingdomNegotiationDecisionOutcome negotiationOutcome =
                    outcome as KingdomNegotiationDecisionOutcome;
                if (negotiationOutcome?.IsApproved == true)
                {
                    outcome.SetSponsor(ProposerClan);
                }
                else
                {
                    AssignDefaultSponsor(outcome);
                }
            }
        }

        public override float DetermineSupport(
            Clan clan,
            DecisionOutcome possibleOutcome)
        {
            KingdomNegotiationClanEvaluation clanEvaluation = GetEvaluation()
                .ClanEvaluations.FirstOrDefault(x => x.Clan == clan);
            float score = clanEvaluation?.RawScore ?? -200f;
            return (possibleOutcome as KingdomNegotiationDecisionOutcome)
                ?.IsApproved == true
                    ? score
                    : -score;
        }

        public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
        {
            bool approved = (chosenOutcome
                as KingdomNegotiationDecisionOutcome)?.IsApproved == true;
            // Re-log the exact evaluation used by this council. Conditions may
            // have changed since the negotiation screen was closed or since
            // the proposing kingdom completed its vote.
            KingdomNegotiationEvaluationService.Log(GetEvaluation());
            KingdomNegotiationProposalService.ResolveCouncilVote(
                this,
                approved);
        }

        public override TextObject GetChosenOutcomeText(
            DecisionOutcome chosenOutcome,
            SupportStatus supportStatus,
            bool isShortVersion = false)
        {
            bool approved = (chosenOutcome
                as KingdomNegotiationDecisionOutcome)?.IsApproved == true;
            return BuildText(approved
                ? "{=MP_NegotiationDecisionApproved}{KINGDOM}'s council approved the compound proposal."
                : "{=MP_NegotiationDecisionRejected}{KINGDOM}'s council rejected the compound proposal.");
        }

        public override DecisionOutcome GetQueriedDecisionOutcome(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            return possibleOutcomes.FirstOrDefault(x =>
                (x as KingdomNegotiationDecisionOutcome)?.IsApproved == true);
        }

        public override TextObject GetSecondaryEffects()
        {
            return TextObject.GetEmpty();
        }

        public TextObject GetPanelDescription()
        {
            TextObject description = new TextObject(
                "{=MP_NegotiationDecisionTerms}{INTRO}{newline}{newline}Complete terms:{newline}{TERMS}");
            description.SetTextVariable("INTRO", GetSupportDescription());
            description.SetTextVariable(
                "TERMS",
                string.Join("\n", _terms.Select(FormatTerm)));
            return description;
        }

        private string FormatTerm(
            KingdomNegotiationTermRecord term)
        {
            TextObject text;
            switch (term.Type)
            {
                case KingdomNegotiationTermType.Gold:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermGold}• {KINGDOM} pays {AMOUNT} denars.");
                    text.SetTextVariable("AMOUNT", term.Amount);
                    break;
                case KingdomNegotiationTermType.Settlement:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermSettlement}• {KINGDOM} cedes {ASSET}.");
                    text.SetTextVariable(
                        "ASSET",
                        term.Settlement?.Name ?? TextObject.GetEmpty());
                    break;
                case KingdomNegotiationTermType.PrisonerHero:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermPrisoner}• {KINGDOM} releases {ASSET}.");
                    text.SetTextVariable(
                        "ASSET",
                        term.Hero?.Name ?? TextObject.GetEmpty());
                    break;
                case KingdomNegotiationTermType.Peace:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermPeace}• The kingdoms make peace.");
                    break;
                case KingdomNegotiationTermType.DeclareWar:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermDeclareWar}• {KINGDOM} declares war on {ASSET}.");
                    text.SetTextVariable(
                        "ASSET",
                        term.ProviderKingdom == Kingdom
                            ? OtherKingdom?.Name ?? TextObject.GetEmpty()
                            : Kingdom?.Name ?? TextObject.GetEmpty());
                    break;
                case KingdomNegotiationTermType.TradeAgreement:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermTrade}• The kingdoms sign a trade agreement.");
                    break;
                case KingdomNegotiationTermType.Alliance:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermAlliance}• The kingdoms form an alliance.");
                    break;
                case KingdomNegotiationTermType.EndTradeAgreement:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermEndTrade}• The kingdoms terminate their trade agreement.");
                    break;
                case KingdomNegotiationTermType.EndAlliance:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermEndAlliance}• The kingdoms terminate their alliance.");
                    break;
                case KingdomNegotiationTermType.EndSubjectRelation:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermEndSubject}• The kingdoms terminate their subject agreement.");
                    break;
                case KingdomNegotiationTermType.JoinWar:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermJoinWar}• {KINGDOM} joins the war against {ASSET}.");
                    text.SetTextVariable(
                        "ASSET",
                        term.Kingdom?.Name ?? TextObject.GetEmpty());
                    break;
                case KingdomNegotiationTermType.TargetBecomesVassal:
                case KingdomNegotiationTermType.PlayerBecomesVassal:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermVassal}• {KINGDOM} becomes a vassal state.");
                    break;
                default:
                    text = new TextObject(
                        "{=MP_NegotiationDecisionTermPuppet}• {KINGDOM} becomes a puppet state.");
                    break;
            }

            text.SetTextVariable(
                "KINGDOM",
                term.ProviderKingdom?.Name ?? TextObject.GetEmpty());
            return text.ToString();
        }

        public override void ApplySecondaryEffects(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes,
            DecisionOutcome chosenOutcome)
        {
        }

        private KingdomNegotiationEvaluation GetEvaluation()
        {
            return _cachedEvaluation ?? (_cachedEvaluation =
                KingdomNegotiationEvaluationService.Evaluate(
                    CreateDraft(),
                    Kingdom));
        }

        private bool CanStillVote(out TextObject reason)
        {
            if (OtherKingdom == null
                || OtherKingdom.IsEliminated
                || Kingdom == null
                || Kingdom.IsEliminated)
            {
                reason = new TextObject(
                    "{=MP_NegotiationDecisionPartiesInvalid}One of the negotiating kingdoms no longer exists.");
                return false;
            }

            return KingdomNegotiationDraftValidator.TryValidate(
                CreateDraft(),
                out reason);
        }

        private TextObject BuildText(string source)
        {
            TextObject text = new TextObject(source);
            text.SetTextVariable("KINGDOM", OtherKingdom?.Name
                ?? TextObject.GetEmpty());
            return text;
        }

        public sealed class KingdomNegotiationDecisionOutcome
            : StartAllianceDecision.StartAllianceDecisionOutcome
        {
            [SaveableField(201)]
            private readonly bool _isReceivingCouncil;

            public bool IsApproved => ShouldAllianceBeStarted;

            public KingdomNegotiationDecisionOutcome(
                bool approved,
                Kingdom kingdom,
                Kingdom otherKingdom,
                bool isReceivingCouncil)
                : base(approved, kingdom, otherKingdom)
            {
                _isReceivingCouncil = isReceivingCouncil;
            }

            public override TextObject GetDecisionTitle()
            {
                return new TextObject(IsApproved
                    ? "{=MP_NegotiationDecisionSupport}Support"
                    : "{=MP_NegotiationDecisionOppose}Oppose");
            }

            public override TextObject GetDecisionDescription()
            {
                return new TextObject(IsApproved
                    ? "{=MP_NegotiationDecisionSupportDescription}Support the complete diplomatic proposal."
                    : "{=MP_NegotiationDecisionOpposeDescription}Reject the complete diplomatic proposal.");
            }

            public override string GetDecisionLink()
            {
                return null;
            }

            public override ImageIdentifier GetDecisionImageIdentifier()
            {
                return null;
            }
        }
    }
}
