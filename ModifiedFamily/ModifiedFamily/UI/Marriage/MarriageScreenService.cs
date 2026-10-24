// Opens the minimal marriage shell before richer clan content is introduced.
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.UI.Marriage
{
    public static class MarriageScreenService
    {
        public static bool Open(Hero hero) => FamilyActionScreenHost.Open("ModifiedFamilyMarriageShell", "ModifiedFamilyMarriageLayer", new FamilyActionScreenVM(hero, true, null));
    }
}
