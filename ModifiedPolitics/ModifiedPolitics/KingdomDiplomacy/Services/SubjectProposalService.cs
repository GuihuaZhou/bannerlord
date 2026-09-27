using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Stable entry point for diplomatic subject proposals. Structural
    /// validation is kept separate from clan voting so the UI can keep an
    /// action available even when the other kingdom is likely to reject it.
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
                || overlord.RulingClan != Clan.PlayerClan)
            {
                reason = new TextObject(
                    "{=ModifiedPolitics_SubjectDemandRequiresRuler}Only the ruler of a kingdom can make this demand.");
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

        public static bool DemandSubjectRelation(
            Kingdom overlord,
            Kingdom subject,
            SubjectType type)
        {
            TextObject reason;
            if (!CanDemandSubjectRelation(
                overlord,
                subject,
                type,
                out reason))
            {
                return false;
            }

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
                || subject.RulingClan != Clan.PlayerClan)
            {
                reason = new TextObject(
                    "{=ModifiedPolitics_SubjectOfferRequiresRuler}Only the ruler of a kingdom can offer submission.");
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

        public static bool OfferSubmission(
            Kingdom subject,
            Kingdom overlord,
            SubjectType type)
        {
            TextObject reason;
            if (!CanOfferSubmission(
                subject,
                overlord,
                type,
                out reason))
            {
                return false;
            }

            SubjectProposalEvaluation subjectEvaluation =
                SubjectProposalEvaluationService.EvaluateSubmissionIntent(
                    overlord,
                    subject,
                    type);
            SubjectProposalEvaluationService.Log(subjectEvaluation);
            if (!subjectEvaluation.WouldAccept)
            {
                LogSubmissionCouncilRejected(subject, type);
                return false;
            }

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

        private static void LogSubmissionCouncilRejected(
            Kingdom subject,
            SubjectType type)
        {
            TextObject message = new TextObject(
                "{=MP_SubjectCouncilRejected}[Subject diplomacy] {KINGDOM}'s clans rejected becoming a {SUBJECT_TYPE}.");
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
