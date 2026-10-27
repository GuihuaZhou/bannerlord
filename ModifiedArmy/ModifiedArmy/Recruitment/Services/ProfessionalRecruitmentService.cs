using ModifiedArmy.Recruitment.Pools.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Services
{
    /// <summary>Single commit path from a professional offer into a garrison.</summary>
    internal static class ProfessionalRecruitmentService
    {
        public static bool TryCommit(Settlement settlement, MobileParty garrison,
            CharacterObject troop, int count,
            SettlementManpowerProductionBehavior pools)
        {
            if (settlement == null || garrison == null || troop == null ||
                count <= 0 || pools == null ||
                !pools.TryConsumeProfessionalOffer(settlement, troop, count))
            {
                return false;
            }

            garrison.MemberRoster.AddToCounts(
                troop, count, false, 0, 0, true, -1);
            return true;
        }
    }
}
