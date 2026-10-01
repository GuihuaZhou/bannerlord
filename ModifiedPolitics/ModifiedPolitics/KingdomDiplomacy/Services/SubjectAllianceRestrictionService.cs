using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Temporary compatibility service retained in ModifiedPolitics while the
    /// subject-relation store is migrated. Both vassals and puppets are
    /// subject kingdoms and therefore cannot form bilateral alliances.
    /// </summary>
    public static class SubjectAllianceRestrictionService
    {
        public static bool CanFormAlliance(Kingdom first, Kingdom second)
        {
            if (first == null || second == null || first == second)
            {
                return false;
            }

            return CanSubjectFormAlliance(first, second)
                && CanSubjectFormAlliance(second, first);
        }

        public static SubjectType GetBlockingSubjectType(
            Kingdom first,
            Kingdom second)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectType firstType = manager?.GetSubjectType(first)
                ?? SubjectType.None;
            SubjectType secondType = manager?.GetSubjectType(second)
                ?? SubjectType.None;

            if (firstType == SubjectType.Puppet
                || secondType == SubjectType.Puppet)
            {
                return SubjectType.Puppet;
            }

            return firstType == SubjectType.Vassal
                    || secondType == SubjectType.Vassal
                ? SubjectType.Vassal
                : SubjectType.None;
        }

        /// <summary>
        /// Removes alliances that became invalid when the subject relation or
        /// the overlord's wars changed. Iterating over a copy is required
        /// because EndAlliance updates AlliedKingdoms.
        /// </summary>
        public static void RemoveExistingAlliances(Kingdom subject)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (subject == null
                || manager?.GetSubjectType(subject) == SubjectType.None)
            {
                return;
            }

            IAllianceCampaignBehavior behavior = Campaign.Current
                ?.GetCampaignBehavior<IAllianceCampaignBehavior>();
            if (behavior == null)
            {
                return;
            }

            foreach (Kingdom ally in subject.AlliedKingdoms.ToList())
            {
                if (!CanFormAlliance(subject, ally))
                {
                    behavior.EndAlliance(subject, ally);
                }
            }
        }

        private static bool CanSubjectFormAlliance(
            Kingdom possibleSubject,
            Kingdom prospectiveAlly)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectType type = manager?.GetSubjectType(possibleSubject)
                ?? SubjectType.None;
            return type == SubjectType.None;
        }
    }
}
