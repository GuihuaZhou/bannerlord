// Opens marriage negotiation from the clan members list.
using System.Linq;
using TaleWorlds.CampaignSystem;
using CampaignClan = TaleWorlds.CampaignSystem.Clan;

namespace ModifiedFamily.UI.Marriage
{
    public static class MarriageScreenService
    {
        public static bool Open()
        {
            CampaignClan playerClan = CampaignClan.PlayerClan;
            if (playerClan == null || !playerClan.Heroes.Any(hero =>
                hero != null && hero.Clan == playerClan &&
                ModifiedFamily.Marriage.Models.MarriageEligibilityModel.CanNegotiateHero(hero).IsAllowed)) return false;
            return FamilyActionScreenHost.Open(
                "ModifiedFamilyMarriageShell",
                "ModifiedFamilyMarriageLayer",
                new MarriageNavigationVM(),
                "ui_familyproposal");
        }
    }
}
