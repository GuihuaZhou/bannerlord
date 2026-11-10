using System.Linq;
using ModifiedArmy.Army.Contracts;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.HeroOffices.Integration
{
    internal sealed class MarshalArmyAuthorityProvider : IArmyAuthorityProvider
    {
        public bool HasAppointedMarshal(Kingdom kingdom) =>
            kingdom != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(item => item?.Hero != null) == true;

        public bool IsAppointedMarshal(Hero hero, Kingdom kingdom) =>
            hero != null && kingdom != null && HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.Marshal)
                .Any(item => item?.Hero == hero) == true;

        public bool IsEligibleMarshal(Hero hero, Kingdom kingdom) =>
            IsAppointedMarshal(hero, kingdom) &&
            OfficeRules.IsEligible(hero, kingdom, OfficeType.Marshal);
    }
}
