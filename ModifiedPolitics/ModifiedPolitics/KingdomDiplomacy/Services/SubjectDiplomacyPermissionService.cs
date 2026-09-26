using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Centralizes foreign-policy permissions for subject kingdoms.
    /// Vassals retain normal diplomacy; puppets may only follow their overlord.
    /// </summary>
    public static class SubjectDiplomacyPermissionService
    {
        public static bool HasIndependentDiplomacy(Kingdom kingdom)
        {
            return KingdomDiplomacyManager.Current?.GetSubjectType(kingdom)
                != SubjectType.Puppet;
        }

        public static bool CanStartNormalDiplomacy(
            Kingdom actingKingdom,
            IFaction targetFaction)
        {
            if (actingKingdom == null || targetFaction == null)
            {
                return false;
            }

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager == null)
            {
                return true;
            }

            SubjectType subjectType = manager.GetSubjectType(actingKingdom);
            if (subjectType == SubjectType.Puppet)
            {
                return false;
            }

            // A vassal remains diplomatically independent, except that war
            // against its overlord must use the future independence action.
            return subjectType != SubjectType.Vassal
                || targetFaction != manager.GetOverlord(actingKingdom);
        }
    }
}
