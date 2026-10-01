using ModifiedDiplomacy.KingdomDiplomacy.Finance;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.Actions
{
    /// <summary>
    /// Ends a subject relation through rebellion and guarantees a war against
    /// the former overlord. It is shared by voluntary independence and detected
    /// puppet defiance so those paths cannot leave different diplomatic states.
    /// </summary>
    public static class SubjectIndependenceAction
    {
        public static bool TryApply(
            Kingdom subject,
            SubjectIndependenceReason reason)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectRelationData relation = manager?.GetSubjectRelation(subject);
            Kingdom overlord = relation?.OverlordKingdom;
            if (subject == null
                || overlord == null
                || subject.IsEliminated
                || overlord.IsEliminated
                || !manager.RemoveSubjectRelation(subject))
            {
                return false;
            }

            SubjectTributeCalculator.InvalidateAssessment();

            // The relation must be removed before declaring war. Otherwise the
            // resulting native war event would be interpreted as another act
            // of subject defiance and process independence twice.
            if (!FactionManager.IsAtWarAgainstFaction(subject, overlord))
            {
                DeclareWarAction.ApplyByDefault(subject, overlord);
            }

            TextObject message = new TextObject(
                "{=ModifiedPolitics_SubjectIndependence}[Kingdom diplomacy] {SUBJECT} rebelled against {OVERLORD}. Reason: {REASON}.");
            message.SetTextVariable("SUBJECT", subject.Name);
            message.SetTextVariable("OVERLORD", overlord.Name);
            message.SetTextVariable("REASON", GetReasonText(reason));
            ModLogger.Notice(message.ToString());
            return true;
        }

        private static TextObject GetReasonText(
            SubjectIndependenceReason reason)
        {
            switch (reason)
            {
                case SubjectIndependenceReason.PuppetDefiance:
                    return new TextObject(
                        "{=ModifiedPolitics_IndependenceReasonPuppetDefiance}the puppet defied its overlord's foreign policy");
                case SubjectIndependenceReason.WarWithOverlord:
                    return new TextObject(
                        "{=ModifiedPolitics_IndependenceReasonWarWithOverlord}the subject entered war with its overlord");
                case SubjectIndependenceReason.OverlordAggression:
                    return new TextObject(
                        "{=ModifiedPolitics_IndependenceReasonOverlordAggression}the overlord attacked its subject");
                default:
                    return new TextObject(
                        "{=ModifiedPolitics_IndependenceReasonVoluntary}the subject declared independence");
            }
        }
    }
}
