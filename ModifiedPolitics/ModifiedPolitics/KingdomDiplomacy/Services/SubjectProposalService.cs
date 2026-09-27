using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Stable entry point for diplomatic subject proposals. The current
    /// development rule makes every valid player demand succeed immediately;
    /// later AI acceptance logic can replace that rule without changing UI or
    /// the authoritative SubjectRelationAction.
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

            // Temporary development rule: every valid demand made by the
            // player is accepted. Future AI evaluation belongs at this point.
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

            // Temporary development rule: every valid player offer is
            // accepted. Future AI evaluation belongs at this point.
            return SubjectRelationAction.TryEstablish(
                overlord,
                subject,
                type);
        }
    }
}
