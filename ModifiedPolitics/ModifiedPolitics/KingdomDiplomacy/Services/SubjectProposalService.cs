using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Decisions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Stable entry point for diplomatic subject proposals. Structural
    /// validation, the player's kingdom decision, and the foreign response
    /// remain separate stages so each side can make its own decision.
    /// </summary>
    public static class SubjectProposalService
    {
        public static bool CanDemandSubjectRelation(
            Kingdom overlord,
            Kingdom subject,
            SubjectType type,
            out TextObject reason)
        {
            reason = TextObject.GetEmpty();
            if (overlord == null
                || subject == null
                || type == SubjectType.None
                || Clan.PlayerClan?.Kingdom != overlord
                || Clan.PlayerClan.IsUnderMercenaryService)
            {
                reason = new TextObject(
                    "{=ModifiedPolitics_SubjectDemandRequiresRuler}Only a full member clan of the kingdom can propose this demand.");
                return false;
            }

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager == null
                || !manager.CanEstablishSubjectRelation(
                    overlord,
                    subject,
                    type))
            {
                reason = new TextObject(
                    "{=ModifiedPolitics_SubjectDemandUnavailable}A subject relation cannot be established with this kingdom.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the foreign response after the player's own council has
        /// approved sending a subject demand.
        /// </summary>
        public static bool ResolveApprovedDemand(
            Kingdom overlord,
            Kingdom subject,
            SubjectType type)
        {
            SubjectProposalEvaluation evaluation =
                SubjectProposalEvaluationService.EvaluateDemand(
                    overlord,
                    subject,
                    type);
            SubjectProposalEvaluationService.Log(evaluation);
            if (!evaluation.WouldAccept)
            {
                LogDemandRejected(subject, type);
                return false;
            }

            return SubjectRelationAction.TryEstablish(
                overlord,
                subject,
                type);
        }

        public static bool CanOfferSubmission(
            Kingdom subject,
            Kingdom overlord,
            SubjectType type,
            out TextObject reason)
        {
            reason = TextObject.GetEmpty();
            if (subject == null
                || overlord == null
                || type == SubjectType.None
                || Clan.PlayerClan?.Kingdom != subject
                || Clan.PlayerClan.IsUnderMercenaryService)
            {
                reason = new TextObject(
                    "{=ModifiedPolitics_SubjectOfferRequiresRuler}Only a full member clan of the kingdom can propose submission.");
                return false;
            }

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager == null
                || !manager.CanEstablishSubjectRelation(
                    overlord,
                    subject,
                    type))
            {
                reason = new TextObject(
                    "{=ModifiedPolitics_SubjectOfferUnavailable}Your kingdom cannot become a subject of this kingdom.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Resolves the prospective overlord's response after the player's
        /// own council has approved offering submission.
        /// </summary>
        public static bool ResolveApprovedSubmission(
            Kingdom subject,
            Kingdom overlord,
            SubjectType type)
        {
            SubjectProposalEvaluation admissionEvaluation =
                SubjectProposalEvaluationService.EvaluateSubmissionOffer(
                    overlord,
                    subject,
                    type);
            SubjectProposalEvaluationService.Log(admissionEvaluation);
            if (!admissionEvaluation.WouldAccept)
            {
                LogSubmissionRejected(overlord, type);
                return false;
            }

            return SubjectRelationAction.TryEstablish(
                overlord,
                subject,
                type);
        }

        public static SubjectProposalKingdomDecision CreateDecision(
            Kingdom targetKingdom,
            SubjectType type,
            bool isSubmissionOffer)
        {
            return new SubjectProposalKingdomDecision(
                Clan.PlayerClan,
                targetKingdom,
                type,
                isSubmissionOffer);
        }

        private static void LogDemandRejected(
            Kingdom subject,
            SubjectType type)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectDemandRejected}[Subject diplomacy] {KINGDOM} rejected the demand to become a {SUBJECT_TYPE}.");
            message.SetTextVariable("KINGDOM", subject.Name);
            message.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(type));
            ModLogger.Notice(message.ToString());
        }

        private static void LogSubmissionRejected(
            Kingdom overlord,
            SubjectType type)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectOfferRejected}[Subject diplomacy] {KINGDOM} rejected the offer to accept your kingdom as a {SUBJECT_TYPE}.");
            message.SetTextVariable("KINGDOM", overlord.Name);
            message.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(type));
            ModLogger.Notice(message.ToString());
        }
    }
}
