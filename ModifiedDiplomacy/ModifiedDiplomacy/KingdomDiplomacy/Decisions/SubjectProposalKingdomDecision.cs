using System.Collections.Generic;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedDiplomacy.KingdomDiplomacy.Decisions
{
    /// <summary>
    /// Runs the player's side of a subject proposal through Bannerlord's
    /// normal kingdom voting screen. A demand asks the player's council
    /// whether another realm should be admitted. An offer asks whether the
    /// player's own realm should surrender part of its sovereignty.
    /// </summary>
    public sealed class SubjectProposalKingdomDecision
        : StartAllianceDecision
    {
        [SaveableField(201)]
        private readonly SubjectType _subjectType;

        [SaveableField(202)]
        private readonly bool _isSubmissionOffer;

        private SubjectProposalEvaluation _cachedEvaluation;

        public SubjectType SubjectType => _subjectType;
        public bool IsSubmissionOffer => _isSubmissionOffer;
        public Kingdom TargetKingdom => KingdomToStartAllianceWith;
        public Kingdom Overlord => _isSubmissionOffer
            ? TargetKingdom
            : Kingdom;
        public Kingdom Subject => _isSubmissionOffer
            ? Kingdom
            : TargetKingdom;

        public SubjectProposalKingdomDecision(
            Clan proposerClan,
            Kingdom targetKingdom,
            SubjectType subjectType,
            bool isSubmissionOffer)
            : base(proposerClan, targetKingdom)
        {
            _subjectType = subjectType;
            _isSubmissionOffer = isSubmissionOffer;
        }

        public override bool IsAllowed()
        {
            return CanStillPropose();
        }

        public override bool CanMakeDecision(
            out TextObject reason,
            bool includeReason = false)
        {
            if (!CanStillPropose())
            {
                reason = new TextObject(
                    "{=ModifiedPolitics_SubjectDemandUnavailable}A subject relation cannot be established with this kingdom.");
                return false;
            }

            reason = TextObject.GetEmpty();
            return true;
        }

        protected override bool ShouldBeCancelledInternal()
        {
            return !CanStillPropose();
        }

        public override int GetProposalInfluenceCost()
        {
            // Demands use the native war-proposal cost because they exert
            // pressure on another realm. Voluntary submission uses the native
            // peace-proposal cost because it seeks a negotiated settlement.
            return _isSubmissionOffer
                ? Campaign.Current.Models.DiplomacyModel
                    .GetInfluenceCostOfProposingPeace(ProposerClan)
                : Campaign.Current.Models.DiplomacyModel
                    .GetInfluenceCostOfProposingWar(ProposerClan);
        }

        public override TextObject GetGeneralTitle()
        {
            return BuildText(
                "{=MP_SubjectDecisionGeneralDemand}Demand {KINGDOM} to become a {SUBJECT_TYPE}",
                "{=MP_SubjectDecisionGeneralOffer}Offer to become a {SUBJECT_TYPE} of {KINGDOM}");
        }

        public override TextObject GetSupportTitle()
        {
            return BuildText(
                "{=MP_SubjectDecisionSupportDemand}Vote on demanding that {KINGDOM} become a {SUBJECT_TYPE}.",
                "{=MP_SubjectDecisionSupportOffer}Vote on becoming a {SUBJECT_TYPE} of {KINGDOM}.");
        }

        public override TextObject GetChooseTitle()
        {
            return GetSupportTitle();
        }

        public override TextObject GetSupportDescription()
        {
            return BuildText(
                "{=MP_SubjectDecisionDescriptionDemand}The council must decide whether to demand that {KINGDOM} become a {SUBJECT_TYPE}. You may spend influence to support or oppose the proposal.",
                "{=MP_SubjectDecisionDescriptionOffer}The council must decide whether to offer submission as a {SUBJECT_TYPE} of {KINGDOM}. You may spend influence to support or oppose the proposal.");
        }

        public override TextObject GetChooseDescription()
        {
            return GetSupportDescription();
        }

        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            yield return new SubjectProposalDecisionOutcome(
                true,
                Kingdom,
                TargetKingdom,
                _subjectType,
                _isSubmissionOffer);
            yield return new SubjectProposalDecisionOutcome(
                false,
                Kingdom,
                TargetKingdom,
                _subjectType,
                _isSubmissionOffer);
        }

        public override void DetermineSponsors(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            foreach (DecisionOutcome outcome in possibleOutcomes)
            {
                SubjectProposalDecisionOutcome subjectOutcome =
                    outcome as SubjectProposalDecisionOutcome;
                if (subjectOutcome?.ShouldAllianceBeStarted == true)
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
            SubjectProposalDecisionOutcome outcome =
                possibleOutcome as SubjectProposalDecisionOutcome;
            SubjectClanSupportEvaluation clanEvaluation = GetEvaluation()
                .ClanEvaluations.FirstOrDefault(x => x.Clan == clan);
            float score = clanEvaluation?.RawScore ?? -200f;
            return outcome?.ShouldAllianceBeStarted == true
                ? score
                : -score;
        }

        public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
        {
            SubjectProposalDecisionOutcome outcome =
                chosenOutcome as SubjectProposalDecisionOutcome;
            if (outcome?.ShouldAllianceBeStarted != true)
            {
                LogCouncilResult(false);
                return;
            }

            LogCouncilResult(true);

            if (_isSubmissionOffer)
            {
                SubjectProposalService.ResolveApprovedSubmission(
                    Subject,
                    Overlord,
                    _subjectType);
            }
            else
            {
                SubjectProposalService.ResolveApprovedDemand(
                    Overlord,
                    Subject,
                    _subjectType);
            }
        }

        private void LogCouncilResult(bool approved)
        {
            TextObject message = BuildText(
                approved
                    ? "{=MP_SubjectDemandVoteApprovedLog}[Subject diplomacy] {KINGDOM}'s council approved the demand for {SUBJECT_TYPE} status."
                    : "{=MP_SubjectDemandVoteRejectedLog}[Subject diplomacy] {KINGDOM}'s council rejected the demand for {SUBJECT_TYPE} status.",
                approved
                    ? "{=MP_SubjectOfferVoteApprovedLog}[Subject diplomacy] {KINGDOM}'s council approved offering {SUBJECT_TYPE} status."
                    : "{=MP_SubjectOfferVoteRejectedLog}[Subject diplomacy] {KINGDOM}'s council rejected offering {SUBJECT_TYPE} status.");
            message.SetTextVariable("KINGDOM", Kingdom.Name);
            ModLogger.Notice(message.ToString());
        }

        public override TextObject GetChosenOutcomeText(
            DecisionOutcome chosenOutcome,
            SupportStatus supportStatus,
            bool isShortVersion = false)
        {
            SubjectProposalDecisionOutcome outcome =
                chosenOutcome as SubjectProposalDecisionOutcome;
            TextObject text = BuildText(
                outcome?.ShouldAllianceBeStarted == true
                    ? "{=MP_SubjectDecisionApprovedDemand}{KINGDOM}'s council approved sending the {SUBJECT_TYPE} demand."
                    : "{=MP_SubjectDecisionRejectedDemand}{KINGDOM}'s council rejected sending the {SUBJECT_TYPE} demand.",
                outcome?.ShouldAllianceBeStarted == true
                    ? "{=MP_SubjectDecisionApprovedOffer}{KINGDOM}'s council approved the offer to become a {SUBJECT_TYPE}."
                    : "{=MP_SubjectDecisionRejectedOffer}{KINGDOM}'s council rejected the offer to become a {SUBJECT_TYPE}.");
            text.SetTextVariable("KINGDOM", Kingdom.Name);
            return text;
        }

        public override DecisionOutcome GetQueriedDecisionOutcome(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            return possibleOutcomes.FirstOrDefault(x =>
                (x as SubjectProposalDecisionOutcome)
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
            return BuildText(
                "{=MP_SubjectDecisionPanelDemand}Decide whether to demand that {KINGDOM} become a {SUBJECT_TYPE}.",
                "{=MP_SubjectDecisionPanelOffer}Decide whether to offer becoming a {SUBJECT_TYPE} of {KINGDOM}.");
        }

        /// <summary>
        /// Records the same clan scores that seed the native voting window.
        /// This makes influence-driven changes in the final vote diagnosable.
        /// </summary>
        public void LogInitialCouncilEvaluation()
        {
            SubjectProposalEvaluationService.Log(GetEvaluation());
        }

        private bool CanStillPropose()
        {
            return TargetKingdom != null
                && !TargetKingdom.IsEliminated
                && KingdomDiplomacyManager.Current
                    ?.CanEstablishSubjectRelation(
                        Overlord,
                        Subject,
                        _subjectType) == true;
        }

        private SubjectProposalEvaluation GetEvaluation()
        {
            if (_cachedEvaluation == null)
            {
                _cachedEvaluation = _isSubmissionOffer
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
                _isSubmissionOffer ? offerText : demandText);
            text.SetTextVariable("KINGDOM", TargetKingdom.Name);
            text.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(_subjectType));
            return text;
        }

        public sealed class SubjectProposalDecisionOutcome
            : StartAllianceDecision.StartAllianceDecisionOutcome
        {
            [SaveableField(201)]
            private readonly SubjectType _subjectType;

            [SaveableField(202)]
            private readonly bool _isSubmissionOffer;

            public SubjectProposalDecisionOutcome(
                bool approved,
                Kingdom kingdom,
                Kingdom targetKingdom,
                SubjectType subjectType,
                bool isSubmissionOffer)
                : base(approved, kingdom, targetKingdom)
            {
                _subjectType = subjectType;
                _isSubmissionOffer = isSubmissionOffer;
            }

            public override TextObject GetDecisionTitle()
            {
                TextObject text = new TextObject(
                    ShouldAllianceBeStarted
                        ? "{=MP_SubjectDecisionSupport}Support"
                        : "{=MP_SubjectDecisionOppose}Oppose");
                return text;
            }

            public override TextObject GetDecisionDescription()
            {
                TextObject text = new TextObject(
                    ShouldAllianceBeStarted
                        ? "{=MP_SubjectDecisionSupportDescription}Support establishing the proposed {SUBJECT_TYPE} relation."
                        : "{=MP_SubjectDecisionOpposeDescription}Oppose establishing the proposed {SUBJECT_TYPE} relation.");
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
