using System.Collections.Generic;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Actions;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedDiplomacy.KingdomDiplomacy.Decisions
{
    /// <summary>
    /// Represents the player's kingdom response after a foreign AI kingdom
    /// has already approved sending a subject proposal through its council.
    /// </summary>
    public sealed class SubjectResponseKingdomDecision
        : StartAllianceDecision
    {
        [SaveableField(201)]
        private readonly SubjectType _subjectType;

        [SaveableField(202)]
        private readonly bool _playerBecomesSubject;

        private SubjectProposalEvaluation _cachedEvaluation;

        public Kingdom ForeignKingdom => KingdomToStartAllianceWith;
        public Kingdom Overlord => _playerBecomesSubject
            ? ForeignKingdom
            : Kingdom;
        public Kingdom Subject => _playerBecomesSubject
            ? Kingdom
            : ForeignKingdom;

        public SubjectResponseKingdomDecision(
            Clan proposerClan,
            Kingdom foreignKingdom,
            SubjectType subjectType,
            bool playerBecomesSubject)
            : base(proposerClan, foreignKingdom)
        {
            _subjectType = subjectType;
            _playerBecomesSubject = playerBecomesSubject;
        }

        public override bool IsAllowed()
        {
            return ForeignKingdom != null
                && !ForeignKingdom.IsEliminated
                && KingdomDiplomacyManager.Current
                    ?.CanEstablishSubjectRelation(
                        Overlord,
                        Subject,
                        _subjectType) == true;
        }

        public override bool CanMakeDecision(
            out TextObject reason,
            bool includeReason = false)
        {
            if (!IsAllowed())
            {
                reason = new TextObject(
                    "{=MP_SubjectResponseUnavailable}This subject proposal is no longer available.");
                return false;
            }

            reason = TextObject.GetEmpty();
            return true;
        }

        protected override bool ShouldBeCancelledInternal()
        {
            return !IsAllowed();
        }

        public override int GetProposalInfluenceCost()
        {
            // The foreign kingdom has sent this proposal. Responding to an
            // incoming diplomatic offer does not charge the player influence.
            return 0;
        }

        public override TextObject GetGeneralTitle()
        {
            return BuildText(
                "{=MP_SubjectResponseGeneralDemand}Respond to {KINGDOM}'s subject demand",
                "{=MP_SubjectResponseGeneralOffer}Respond to {KINGDOM}'s submission offer");
        }

        public override TextObject GetSupportTitle()
        {
            return BuildText(
                "{=MP_SubjectResponseSupportDemand}Vote on becoming a {SUBJECT_TYPE} of {KINGDOM}.",
                "{=MP_SubjectResponseSupportOffer}Vote on accepting {KINGDOM} as a {SUBJECT_TYPE}.");
        }

        public override TextObject GetChooseTitle()
        {
            return GetSupportTitle();
        }

        public override TextObject GetSupportDescription()
        {
            return BuildText(
                "{=MP_SubjectResponseDescriptionDemand}{KINGDOM} has demanded that your realm become its {SUBJECT_TYPE}. Your council must decide whether to accept.",
                "{=MP_SubjectResponseDescriptionOffer}{KINGDOM} has offered to become your {SUBJECT_TYPE}. Your council must decide whether to accept.");
        }

        public override TextObject GetChooseDescription()
        {
            return GetSupportDescription();
        }

        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            yield return new SubjectResponseDecisionOutcome(
                true,
                Kingdom,
                ForeignKingdom,
                _subjectType,
                _playerBecomesSubject);
            yield return new SubjectResponseDecisionOutcome(
                false,
                Kingdom,
                ForeignKingdom,
                _subjectType,
                _playerBecomesSubject);
        }

        public override void DetermineSponsors(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            foreach (DecisionOutcome outcome in possibleOutcomes)
            {
                SubjectResponseDecisionOutcome response =
                    outcome as SubjectResponseDecisionOutcome;
                if (response?.ShouldAllianceBeStarted == true)
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
            SubjectClanSupportEvaluation clanEvaluation = GetEvaluation()
                .ClanEvaluations.FirstOrDefault(x => x.Clan == clan);
            float score = clanEvaluation?.RawScore ?? -200f;
            return (possibleOutcome as SubjectResponseDecisionOutcome)
                ?.ShouldAllianceBeStarted == true
                    ? score
                    : -score;
        }

        public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
        {
            bool accepted = (chosenOutcome
                as SubjectResponseDecisionOutcome)
                ?.ShouldAllianceBeStarted == true;
            SubjectProposalService.LogPlayerResponse(
                ForeignKingdom,
                _subjectType,
                _playerBecomesSubject,
                accepted);
            if (accepted)
            {
                SubjectRelationAction.TryEstablish(
                    Overlord,
                    Subject,
                    _subjectType);
            }
        }

        public override TextObject GetChosenOutcomeText(
            DecisionOutcome chosenOutcome,
            SupportStatus supportStatus,
            bool isShortVersion = false)
        {
            bool accepted = (chosenOutcome
                as SubjectResponseDecisionOutcome)
                ?.ShouldAllianceBeStarted == true;
            return BuildText(
                accepted
                    ? "{=MP_SubjectResponseAcceptedDemand}Your council accepted {KINGDOM}'s demand."
                    : "{=MP_SubjectResponseRejectedDemand}Your council rejected {KINGDOM}'s demand.",
                accepted
                    ? "{=MP_SubjectResponseAcceptedOffer}Your council accepted {KINGDOM}'s submission offer."
                    : "{=MP_SubjectResponseRejectedOffer}Your council rejected {KINGDOM}'s submission offer.");
        }

        public override DecisionOutcome GetQueriedDecisionOutcome(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            return possibleOutcomes.FirstOrDefault(x =>
                (x as SubjectResponseDecisionOutcome)
                    ?.ShouldAllianceBeStarted == true);
        }

        public override TextObject GetSecondaryEffects()
        {
            return TextObject.GetEmpty();
        }

        public override void ApplySecondaryEffects(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes,
            DecisionOutcome chosenOutcome)
        {
        }

        public TextObject GetPanelDescription()
        {
            return GetSupportDescription();
        }

        private SubjectProposalEvaluation GetEvaluation()
        {
            if (_cachedEvaluation == null)
            {
                _cachedEvaluation = _playerBecomesSubject
                    ? SubjectProposalEvaluationService
                        .EvaluateSubmissionIntent(
                            Overlord,
                            Subject,
                            _subjectType)
                    : SubjectProposalEvaluationService
                        .EvaluateSubmissionOffer(
                            Overlord,
                            Subject,
                            _subjectType);
            }

            return _cachedEvaluation;
        }

        private TextObject BuildText(
            string demandText,
            string offerText)
        {
            TextObject text = new TextObject(
                _playerBecomesSubject ? demandText : offerText);
            text.SetTextVariable("KINGDOM", ForeignKingdom.Name);
            text.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(_subjectType));
            return text;
        }

        public sealed class SubjectResponseDecisionOutcome
            : StartAllianceDecision.StartAllianceDecisionOutcome
        {
            [SaveableField(201)]
            private readonly SubjectType _subjectType;

            [SaveableField(202)]
            private readonly bool _playerBecomesSubject;

            public SubjectResponseDecisionOutcome(
                bool accepted,
                Kingdom kingdom,
                Kingdom foreignKingdom,
                SubjectType subjectType,
                bool playerBecomesSubject)
                : base(accepted, kingdom, foreignKingdom)
            {
                _subjectType = subjectType;
                _playerBecomesSubject = playerBecomesSubject;
            }

            public override TextObject GetDecisionTitle()
            {
                return new TextObject(ShouldAllianceBeStarted
                    ? "{=MP_SubjectDecisionSupport}Support"
                    : "{=MP_SubjectDecisionOppose}Oppose");
            }

            public override TextObject GetDecisionDescription()
            {
                TextObject text = new TextObject(ShouldAllianceBeStarted
                    ? "{=MP_SubjectResponseSupportDescription}Support accepting the proposed {SUBJECT_TYPE} relation."
                    : "{=MP_SubjectResponseOpposeDescription}Oppose accepting the proposed {SUBJECT_TYPE} relation.");
                text.SetTextVariable(
                    "SUBJECT_TYPE",
                    SubjectProposalEvaluationService.TypeText(_subjectType));
                return text;
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
