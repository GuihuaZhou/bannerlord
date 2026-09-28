using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Decisions
{
    /// <summary>
    /// Runs voluntary independence through the subject kingdom's council.
    /// Independence is unilateral, so the overlord has no consent stage, but
    /// it still requires the same internal vote as a declaration of war.
    /// </summary>
    public sealed class SubjectIndependenceKingdomDecision
        : DeclareWarDecision
    {
        [SaveableField(201)]
        private readonly SubjectType _subjectType;

        public Kingdom OverlordKingdom => FactionToDeclareWarOn as Kingdom;

        public SubjectIndependenceKingdomDecision(
            Clan proposerClan,
            Kingdom overlordKingdom,
            SubjectType subjectType)
            : base(proposerClan, overlordKingdom)
        {
            _subjectType = subjectType;
        }

        public override bool IsAllowed()
        {
            Kingdom subject = Kingdom;
            Kingdom overlord = OverlordKingdom;
            if (subject == null
                || overlord == null
                || subject.IsEliminated
                || overlord.IsEliminated
                || subject.IsAtWarWith(overlord))
            {
                return false;
            }

            SubjectRelationData relation = KingdomDiplomacyManager.Current
                ?.GetSubjectRelation(subject);
            return relation?.OverlordKingdom == overlord
                && relation.Type == _subjectType;
        }

        protected override bool ShouldBeCancelledInternal()
        {
            return !IsAllowed();
        }

        public override TextObject GetGeneralTitle()
        {
            return BuildText(
                "{=MP_SubjectIndependenceGeneral}Declare independence from {KINGDOM}");
        }

        public override TextObject GetSupportTitle()
        {
            return BuildText(
                "{=MP_SubjectIndependenceSupport}Vote on declaring independence from {KINGDOM}.");
        }

        public override TextObject GetChooseTitle()
        {
            return GetSupportTitle();
        }

        public override TextObject GetSupportDescription()
        {
            return BuildText(
                "{=MP_SubjectIndependenceDescription}The council must decide whether to end the subject relation and enter a war of independence against {KINGDOM}. You may spend influence to support or oppose the proposal.");
        }

        public override TextObject GetChooseDescription()
        {
            return GetSupportDescription();
        }

        public override float DetermineSupport(
            Clan clan,
            DecisionOutcome possibleOutcome)
        {
            float nativeWarSupport = base.DetermineSupport(
                clan,
                possibleOutcome);
            bool supportsIndependence = (possibleOutcome
                as DeclareWarDecision.DeclareWarDecisionOutcome)
                ?.ShouldWarBeDeclared == true;

            // Native war support measures the military and diplomatic risk.
            // Restored sovereignty provides an additional political reason
            // to rebel, especially for a puppet with fewer retained rights.
            float sovereigntyValue = _subjectType == SubjectType.Puppet
                ? 90f
                : 60f;
            return supportsIndependence
                ? nativeWarSupport + sovereigntyValue
                : nativeWarSupport - sovereigntyValue;
        }

        public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
        {
            if ((chosenOutcome
                as DeclareWarDecision.DeclareWarDecisionOutcome)
                ?.ShouldWarBeDeclared == true)
            {
                SubjectIndependenceAction.TryApply(
                    Kingdom,
                    SubjectIndependenceReason.VoluntaryDeclaration);
            }
        }

        public override TextObject GetChosenOutcomeText(
            DecisionOutcome chosenOutcome,
            KingdomDecision.SupportStatus supportStatus,
            bool isShortVersion = false)
        {
            bool declared = (chosenOutcome
                as DeclareWarDecision.DeclareWarDecisionOutcome)
                ?.ShouldWarBeDeclared == true;
            return BuildText(declared
                ? "{=MP_SubjectIndependenceApproved}{SUBJECT} will declare independence from {KINGDOM}."
                : "{=MP_SubjectIndependenceRejected}{SUBJECT} will remain a subject of {KINGDOM}.");
        }

        private TextObject BuildText(string value)
        {
            TextObject text = new TextObject(value);
            text.SetTextVariable("SUBJECT", Kingdom.Name);
            text.SetTextVariable("KINGDOM", OverlordKingdom.Name);
            return text;
        }
    }
}
