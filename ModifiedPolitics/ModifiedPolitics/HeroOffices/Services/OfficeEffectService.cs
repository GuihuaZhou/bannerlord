using System.Linq;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.HeroOffices.Services
{
    /// <summary>
    /// Single read-only API used by campaign models and ModifiedArmy integration points.
    /// </summary>
    public static class OfficeEffectService
    {
        public static bool HasOffice(Hero hero, OfficeType officeType)
        {
            OfficeAssignment assignment = HeroOfficeBehavior.Current?.GetAssignment(hero);
            return assignment != null && assignment.OfficeType == officeType;
        }

        public static bool HasOffice(Town town, OfficeType officeType)
        {
            Hero governor = town?.Governor;
            OfficeAssignment assignment = HeroOfficeBehavior.Current?.GetAssignment(governor);
            return assignment != null
                   && assignment.OfficeType == officeType
                   && assignment.Kingdom == town.OwnerClan?.Kingdom;
        }

        public static float GetLocalOfficeStrength(Kingdom kingdom)
        {
            bool hasChiefMinister = HeroOfficeBehavior.Current?
                .GetAssignments(kingdom, OfficeType.ChiefMinister)
                .Any() == true;
            return hasChiefMinister ? 1.1f : 1f;
        }

        public static float GetTaxFactor(Town town)
        {
            return HasOffice(town, OfficeType.TaxOfficer)
                ? 0.20f * GetLocalOfficeStrength(town.OwnerClan?.Kingdom)
                : 0f;
        }

        public static float GetFoodProductionFactor(Town town)
        {
            return HasOffice(town, OfficeType.AgricultureOfficer)
                ? 0.20f * GetLocalOfficeStrength(town.OwnerClan?.Kingdom)
                : 0f;
        }

        public static float GetRegularRecruitmentFactor(Settlement settlement)
        {
            Town town = settlement?.Town;
            return HasOffice(town, OfficeType.MilitaryOfficer)
                ? 0.25f * GetLocalOfficeStrength(town.OwnerClan?.Kingdom)
                : 0f;
        }

        public static float GetGarrisonRecruitmentFactor(Settlement settlement)
        {
            Town town = settlement?.Town;
            return HasOffice(town, OfficeType.MilitaryOfficer)
                ? 0.50f * GetLocalOfficeStrength(town.OwnerClan?.Kingdom)
                : 0f;
        }

        public static float GetDailyLoyalty(Town town)
        {
            return HasOffice(town, OfficeType.SecurityOfficer)
                ? 2f * GetLocalOfficeStrength(town.OwnerClan?.Kingdom)
                : 0f;
        }
    }
}
