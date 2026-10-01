using System.Collections.Generic;
using System.Linq;
using ModifiedDiplomacy.KingdomDiplomacy.Finance;
using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.KingdomDiplomacy.Actions
{
    /// <summary>
    /// Single mutation entry point for establishing and releasing subjects.
    /// Future war settlements and diplomatic agreements must reuse this action.
    /// </summary>
    public static class SubjectRelationAction
    {
        public static bool TryEstablish(
            Kingdom overlord,
            Kingdom subject,
            SubjectType type,
            int dailyTribute = 0)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager == null
                || !manager.CanEstablishSubjectRelation(
                    overlord,
                    subject,
                    type))
            {
                return false;
            }

            // Preserve wars unique to the prospective subject. Former enemies
            // decide whether to accept peace or extend the war to the overlord.
            List<Kingdom> formerEnemies = Kingdom.All
                .Where(other => other != null
                    && !other.IsEliminated
                    && other != overlord
                    && other != subject
                    && FactionManager.IsAtWarAgainstFaction(subject, other)
                    && !FactionManager.IsAtWarAgainstFaction(overlord, other))
                .ToList();

            // The two kingdoms must first leave their former war state. This is
            // done before saving the relation so the peace event cannot be
            // mistaken for a puppet synchronization event.
            if (FactionManager.IsAtWarAgainstFaction(overlord, subject))
            {
                MakePeaceAction.Apply(overlord, subject);
            }

            if (!manager.TryEstablishSubjectRelation(
                overlord,
                subject,
                type,
                dailyTribute))
            {
                return false;
            }

            if (type == SubjectType.Puppet)
            {
                PuppetDiplomacySynchronizer.JoinCurrentOverlordWars(subject);
            }
            else if (type == SubjectType.Vassal)
            {
                VassalWarObligationSynchronizer
                    .JoinCurrentOverlordWars(subject);
            }

            SubjectFormerWarResolutionService.Resolve(
                overlord,
                subject,
                formerEnemies);

            SubjectAllianceRestrictionService
                .RemoveExistingAlliances(subject);
            SubjectTradeRestrictionService
                .RemoveInvalidTradeAgreements(subject);
            SubjectTributeCalculator.InvalidateAssessment();

            TextObject message = new TextObject(
                "{=ModifiedPolitics_SubjectEstablished}[Kingdom diplomacy] {SUBJECT} became a {SUBJECT_TYPE} of {OVERLORD}.");
            message.SetTextVariable("SUBJECT", subject.Name);
            message.SetTextVariable("OVERLORD", overlord.Name);
            message.SetTextVariable(
                "SUBJECT_TYPE",
                GetSubjectTypeText(type));
            ModLogger.Notice(message.ToString());
            return true;
        }

        public static bool TryRelease(Kingdom subject)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectRelationData relation = manager?.GetSubjectRelation(subject);
            if (relation == null || !manager.RemoveSubjectRelation(subject))
            {
                return false;
            }

            SubjectTributeCalculator.InvalidateAssessment();

            TextObject message = new TextObject(
                "{=ModifiedPolitics_SubjectReleased}[Kingdom diplomacy] {SUBJECT} is no longer a subject of {OVERLORD}.");
            message.SetTextVariable("SUBJECT", subject.Name);
            message.SetTextVariable("OVERLORD", relation.OverlordKingdom.Name);
            ModLogger.Notice(message.ToString());
            return true;
        }

        private static TextObject GetSubjectTypeText(SubjectType type)
        {
            return type == SubjectType.Puppet
                ? new TextObject("{=ModifiedPolitics_SubjectTypePuppet}puppet")
                : new TextObject("{=ModifiedPolitics_SubjectTypeVassal}vassal");
        }
    }
}
