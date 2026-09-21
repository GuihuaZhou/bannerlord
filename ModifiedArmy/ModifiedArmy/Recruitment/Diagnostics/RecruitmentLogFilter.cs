using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Recruitment.Diagnostics
{
    /// <summary>
    /// Keeps recruitment diagnostics relevant to the player's current
    /// political context instead of reporting every kingdom on the map.
    /// </summary>
    public static class RecruitmentLogFilter
    {
        public static bool ShouldLog(MobileParty party)
        {
            Clan clan = party?.ActualClan
                ?? party?.LeaderHero?.Clan
                ?? party?.CurrentSettlement?.OwnerClan;
            return ShouldLog(clan);
        }

        public static bool ShouldLog(Clan clan)
        {
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;
            return playerKingdom != null &&
                clan?.Kingdom == playerKingdom;
        }
    }
}
