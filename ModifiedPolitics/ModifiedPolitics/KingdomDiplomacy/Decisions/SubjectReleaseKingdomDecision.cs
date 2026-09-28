using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Decisions
{
    /// <summary>
    /// Lets the overlord's council decide whether an existing subject should
    /// be released. The subject has no veto, but valuable subjects naturally
    /// attract stronger opposition from the overlord's clans.
    /// </summary>
    public sealed class SubjectReleaseKingdomDecision
        : StartAllianceDecision
    {
        [SaveableField(201)]
        private readonly SubjectType _subjectType;

        private SubjectProposalEvaluation _cachedEvaluation;

        public Kingdom SubjectKingdom => KingdomToStartAllianceWith;

        public SubjectReleaseKingdomDecision(
            Clan proposerClan,
            Kingdom subjectKingdom,
            SubjectType subjectType)
            : base(proposerClan, subjectKingdom)
        {
            _subjectType = subjectType;
        }

        public override bool IsAllowed()
        {
            if (Kingdom == null
                || SubjectKingdom == null
                || SubjectKingdom.IsEliminated)
            {
                return false;
            }

            SubjectRelationData relation = KingdomDiplomacyManager.Current
                ?.GetSubjectRelation(SubjectKingdom);
            return relation?.OverlordKingdom == Kingdom
                && relation.Type == _subjectType;
        }

        public override bool CanMakeDecision(
            out TextObject reason,
            bool includeReason = false)
        {
            if (!IsAllowed())
            {
                reason = new TextObject(
                    "{=MP_SubjectReleaseUnavailable}This subject relation can no longer be released.");
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
            return Campaign.Current.Models.DiplomacyModel
                .GetInfluenceCostOfProposingPeace(ProposerClan);
        }

        public override TextObject GetGeneralTitle()
        {
            return BuildText(
                "{=MP_SubjectReleaseGeneral}Release {KINGDOM} from {SUBJECT_TYPE} status");
        }

        public override TextObject GetSupportTitle()
        {
            return BuildText(
                "{=MP_SubjectReleaseSupport}Vote on releasing {KINGDOM} from {SUBJECT_TYPE} status.");
        }

        public override TextObject GetChooseTitle()
        {
            return GetSupportTitle();
        }

        public override TextObject GetSupportDescription()
        {
            return BuildText(
                "{=MP_SubjectReleaseDescription}The council must decide whether to release {KINGDOM}. You may spend influence to support or oppose the proposal.");
        }

        public override TextObject GetChooseDescription()
        {
            return GetSupportDescription();
        }

        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            yield return new SubjectReleaseDecisionOutcome(
                true,
                Kingdom,
                SubjectKingdom,
                _subjectType);
            yield return new SubjectReleaseDecisionOutcome(
                false,
                Kingdom,
                SubjectKingdom,
                _subjectType);
        }

        public override void DetermineSponsors(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            foreach (DecisionOutcome outcome in possibleOutcomes)
            {
                SubjectReleaseDecisionOutcome releaseOutcome =
                    outcome as SubjectReleaseDecisionOutcome;
                if (releaseOutcome?.ShouldAllianceBeStarted == true)
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
            float valueOfKeepingSubject = clanEvaluation?.RawScore ?? 0f;
            bool supportsRelease = (possibleOutcome
                as SubjectReleaseDecisionOutcome)
                ?.ShouldAllianceBeStarted == true;
            return supportsRelease
                ? -valueOfKeepingSubject
                : valueOfKeepingSubject;
        }

        public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
        {
            if ((chosenOutcome as SubjectReleaseDecisionOutcome)
                ?.ShouldAllianceBeStarted == true)
            {
                SubjectRelationAction.TryRelease(SubjectKingdom);
            }
        }

        public override TextObject GetChosenOutcomeText(
            DecisionOutcome chosenOutcome,
            SupportStatus supportStatus,
            bool isShortVersion = false)
        {
            bool released = (chosenOutcome as SubjectReleaseDecisionOutcome)
                ?.ShouldAllianceBeStarted == true;
            return BuildText(released
                ? "{=MP_SubjectReleaseApproved}{KINGDOM} will be released from {SUBJECT_TYPE} status."
                : "{=MP_SubjectReleaseRejected}{KINGDOM} will remain a {SUBJECT_TYPE}.");
        }

        public override DecisionOutcome GetQueriedDecisionOutcome(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            return possibleOutcomes.FirstOrDefault(x =>
                (x as SubjectReleaseDecisionOutcome)
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
                "{=MP_SubjectReleasePanel}Decide whether to release {KINGDOM} from {SUBJECT_TYPE} status.");
        }

        private SubjectProposalEvaluation GetEvaluation()
        {
            if (_cachedEvaluation == null)
            {
                _cachedEvaluation = SubjectProposalEvaluationService
                    .EvaluateSubmissionOffer(
                        Kingdom,
                        SubjectKingdom,
                        _subjectType);
            }

            return _cachedEvaluation;
        }

        private TextObject BuildText(string value)
        {
            TextObject text = new TextObject(value);
            text.SetTextVariable("KINGDOM", SubjectKingdom.Name);
            text.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(_subjectType));
            return text;
        }

        public sealed class SubjectReleaseDecisionOutcome
            : StartAllianceDecision.StartAllianceDecisionOutcome
        {
            [SaveableField(201)]
            private readonly SubjectType _subjectType;

            public SubjectReleaseDecisionOutcome(
                bool release,
                Kingdom kingdom,
                Kingdom subjectKingdom,
                SubjectType subjectType)
                : base(release, kingdom, subjectKingdom)
            {
                _subjectType = subjectType;
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
                    ? "{=MP_SubjectReleaseSupportDescription}Support ending the {SUBJECT_TYPE} relation."
                    : "{=MP_SubjectReleaseOpposeDescription}Oppose ending the {SUBJECT_TYPE} relation.");
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
