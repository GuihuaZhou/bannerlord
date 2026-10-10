using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Models
{
    /// <summary>
    /// Matches ModifiedArmy's stable geographic border definition without adding a module dependency.
    /// </summary>
    public static class BorderSettlementModel
    {
        private const int NeighborCount = 5;

        public static bool IsBorderSettlement(Kingdom kingdom, Settlement settlement)
        {
            IFaction ownerFaction = settlement?.MapFaction;
            if (kingdom == null || ownerFaction == null || ownerFaction != kingdom)
                return false;

            return Town.AllFiefs
                .Where(candidate => candidate?.Settlement != null && candidate.Settlement != settlement)
                .OrderBy(candidate => candidate.Settlement.GetPosition2D.DistanceSquared(settlement.GetPosition2D))
                .Take(NeighborCount)
                .Any(candidate => candidate.Settlement.MapFaction != null
                                  && candidate.Settlement.MapFaction != ownerFaction);
        }
    }
}
