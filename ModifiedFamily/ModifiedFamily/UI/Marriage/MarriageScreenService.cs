// Opens the minimal marriage shell before richer clan content is introduced.
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.UI.Marriage
{
    public static class MarriageScreenService
    {
        public static bool Open(Hero hero)
        {
            if (hero == null || hero.Clan != TaleWorlds.CampaignSystem.Clan.PlayerClan || !ModifiedFamily.Marriage.Models.MarriageEligibilityModel.CanNegotiateHero(hero).IsAllowed) return false;
            return FamilyActionScreenHost.Open(
                "ModifiedFamilyMarriageShell",
                "ModifiedFamilyMarriageLayer",
                new MarriageNavigationVM(hero),
                "ui_kingdomnegotiation");
        }
    }
}
