using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Decisions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapNotificationTypes;
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
            if (subject == Clan.PlayerClan?.Kingdom
                && overlord != subject)
            {
                return SendProposalToPlayer(
                    overlord,
                    type,
                    true);
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
            if (overlord == Clan.PlayerClan?.Kingdom
                && subject != overlord)
            {
                return SendProposalToPlayer(
                    subject,
                    type,
                    false);
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

        /// <summary>
        /// Delivers a foreign council's approved proposal to the player's
        /// kingdom. The notification opens a second, player-side council vote.
        /// </summary>
        private static bool SendProposalToPlayer(
            Kingdom foreignKingdom,
            SubjectType type,
            bool playerBecomesSubject)
        {
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            if (playerKingdom == null
                || Clan.PlayerClan.IsUnderMercenaryService
                || foreignKingdom == null
                || playerKingdom.UnresolvedDecisions
                    .OfType<SubjectResponseKingdomDecision>()
                    .Any(x => x.ForeignKingdom == foreignKingdom))
            {
                return false;
            }

            SubjectResponseKingdomDecision decision =
                new SubjectResponseKingdomDecision(
                    Clan.PlayerClan,
                    foreignKingdom,
                    type,
                    playerBecomesSubject);
            playerKingdom.AddDecision(decision, true);

            TextObject notice = new TextObject(playerBecomesSubject
                ? "{=MP_SubjectIncomingDemandNotice}{KINGDOM} has demanded that your kingdom become its {SUBJECT_TYPE}. Click to convene the council."
                : "{=MP_SubjectIncomingOfferNotice}{KINGDOM} has offered to become your {SUBJECT_TYPE}. Click to convene the council.");
            notice.SetTextVariable("KINGDOM", foreignKingdom.Name);
            notice.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(type));
            Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(
                new KingdomDecisionMapNotification(
                    playerKingdom,
                    decision,
                    notice));

            // The map notice already provides the visible player prompt.
            // Keep the duplicate diagnostic in the file-oriented Info level.
            ModLogger.Info(notice.ToString());
            return true;
        }

        public static void LogPlayerResponse(
            Kingdom foreignKingdom,
            SubjectType type,
            bool playerBecomesSubject,
            bool accepted)
        {
            if (foreignKingdom == null)
            {
                return;
            }

            TextObject message = new TextObject(
                "{=MP_SubjectPlayerResponseLog}[Subject diplomacy] The player's council {RESULT} {KINGDOM}'s {DIRECTION} proposal for {SUBJECT_TYPE} status.");
            message.SetTextVariable(
                "RESULT",
                accepted
                    ? new TextObject("{=MP_SubjectAccepted}accepted")
                    : new TextObject("{=MP_SubjectRejected}rejected"));
            message.SetTextVariable("KINGDOM", foreignKingdom.Name);
            message.SetTextVariable(
                "DIRECTION",
                playerBecomesSubject
                    ? new TextObject("{=MP_SubjectDemandDirection}demand")
                    : new TextObject("{=MP_SubjectOfferDirection}submission offer"));
            message.SetTextVariable(
                "SUBJECT_TYPE",
                SubjectProposalEvaluationService.TypeText(type));
            ModLogger.Notice(message.ToString());
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
