using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedFamily.Persistence
{
    [SaveableRootClass(2)] public sealed class FamilyCooldownRecord
    {
        [SaveableProperty(1)] public Hero HeroA { get; set; }
        [SaveableProperty(2)] public Hero HeroB { get; set; }
        [SaveableProperty(3)] public CampaignTime Until { get; set; }
    }
    [SaveableRootClass(3)] public sealed class AdoptiveKinshipRecord
    {
        [SaveableProperty(1)] public Hero Adopter { get; set; }
        [SaveableProperty(2)] public Hero Adoptee { get; set; }
        [SaveableProperty(3)] public CampaignTime Date { get; set; }
        [SaveableProperty(4)] public Clan SourceClan { get; set; }
    }
}
