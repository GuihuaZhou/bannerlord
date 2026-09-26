using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Centralizes war and peace permissions for subject kingdoms. Alliance
    /// restrictions are handled separately; trade diplomacy stays independent.
    /// </summary>
    public static class SubjectDiplomacyPermissionService
    {
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
