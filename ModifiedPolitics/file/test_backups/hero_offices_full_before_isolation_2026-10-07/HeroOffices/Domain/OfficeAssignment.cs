using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Domain
{
    /// <summary>
    /// Persisted office membership. A local office follows the hero's current governed town.
    /// </summary>
    public sealed class OfficeAssignment
    {
        public OfficeAssignment()
        {
        }

        public OfficeAssignment(Kingdom kingdom, Hero hero, OfficeType officeType)
        {
            Kingdom = kingdom;
            Hero = hero;
            OfficeType = officeType;
            AppointedAt = CampaignTime.Now;
        }

        [SaveableProperty(1)]
        public Kingdom Kingdom { get; private set; }

        [SaveableProperty(2)]
        public Hero Hero { get; private set; }

        [SaveableProperty(3)]
        public OfficeType OfficeType { get; private set; }

        [SaveableProperty(4)]
        public CampaignTime AppointedAt { get; private set; }
    }
}
