using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Enforces the rule that subject kingdoms cannot be members of native
    /// alliances. Trade agreements and other non-alliance treaties remain
    /// independent and are deliberately not handled here.
    /// </summary>
    public static class SubjectAllianceRestrictionService
    {
        public static bool CanFormAlliance(Kingdom first, Kingdom second)
        {
            if (first == null || second == null || first == second)
            {
                return false;
            }

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            return manager == null
                || (manager.GetSubjectType(first) == SubjectType.None
                    && manager.GetSubjectType(second) == SubjectType.None);
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
        /// Removes alliances that predate the subject relation. Iterating over
        /// a copy is required because EndAlliance updates AlliedKingdoms.
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
                behavior.EndAlliance(subject, ally);
            }
        }
    }
}
