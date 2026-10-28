using System;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Tool;
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
                count <= 0 || pools == null)
            {
                return false;
            }
            int rosterBefore = garrison.MemberRoster.GetElementNumber(troop);
            if (!pools.TryConsumeProfessionalOffer(
                    settlement, troop, count,
                    out ProfessionalOfferConsumption consumption))
                return false;
            try
            {
                garrison.MemberRoster.AddToCounts(
                    troop, count, false, 0, 0, true, -1);
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    int added = Math.Max(0,
                        garrison.MemberRoster.GetElementNumber(troop) -
                        rosterBefore);
                    if (added > 0)
                        garrison.MemberRoster.AddToCounts(
                            troop, -added, false, 0, 0, true, -1);
                    pools.RollbackProfessionalOffer(consumption);
                }
                catch (Exception rollbackException)
                {
                    ModLogger.Error(
                        "Professional recruitment rollback failed: " +
                        rollbackException);
                }
                ModLogger.Error(
                    "Professional recruitment commit failed: " + exception);
                return false;
            }
        }
    }
}
