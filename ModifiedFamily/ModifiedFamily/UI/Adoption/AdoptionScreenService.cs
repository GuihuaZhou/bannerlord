// Opens the adoption confirmation movie for the selected companion.
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.UI.Adoption
{
    public static class AdoptionScreenService
    {
        public static bool Open(Hero hero) => FamilyActionScreenHost.Open("ModifiedFamilyAdoptionScreen", "ModifiedFamilyAdoptionLayer", new FamilyActionScreenVM(hero, false, FamilyPlayerInteractionService.ShowAdoptionActionsFor));
    }
}
