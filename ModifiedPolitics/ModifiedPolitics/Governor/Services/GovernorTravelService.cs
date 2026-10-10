using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Services
{
    /// <summary>
    /// Reads Bannerlord's delayed governor travel records, which are separate from Hero.GovernorOf.
    /// </summary>
    public static class GovernorTravelService
    {
        public static Hero GetIncomingGovernor(Town town)
        {
            return town?.Settlement == null
                ? null
                : Hero.AllAliveHeroes.FirstOrDefault(hero => IsTravelingToGovernorPost(hero, town));
        }

        public static bool IsAssignedOrTraveling(Town town, Hero hero)
        {
            return town != null
                   && hero != null
                   && (town.Governor == hero
                       || hero.GovernorOf == town
                       || IsTravelingToGovernorPost(hero, town));
        }

        private static bool IsTravelingToGovernorPost(Hero hero, Town town)
        {
            if (hero?.IsTraveling != true || town?.Settlement == null)
                return false;

            ITeleportationCampaignBehavior behavior =
                Campaign.Current?.GetCampaignBehavior<ITeleportationCampaignBehavior>();
            if (behavior == null)
                return false;

            bool isGovernor;
            bool isPartyLeader;
            IMapPoint target;
            return behavior.GetTargetOfTeleportingHero(
                       hero,
                       out isGovernor,
                       out isPartyLeader,
                       out target)
                   && isGovernor
                   && target == town.Settlement;
        }
    }
}
