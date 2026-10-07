using System;
using System.Linq;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedPolitics.HeroOffices.Models
{
    /// <summary>
    /// Contains deterministic office limits, eligibility checks and candidate scores.
    /// </summary>
    public static class OfficeRules
    {
        public static bool IsLocal(OfficeType officeType)
        {
            return officeType == OfficeType.TaxOfficer
                   || officeType == OfficeType.AgricultureOfficer
                   || officeType == OfficeType.MilitaryOfficer
                   || officeType == OfficeType.SecurityOfficer;
        }

        public static int GetOfficeLimit(Kingdom kingdom, OfficeType officeType)
        {
            if (!OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config)
                || !config.IsEnabled(officeType))
                return 0;

            if (!IsLocal(officeType))
                return 1;

            int settlementCount = officeType == OfficeType.MilitaryOfficer
                ? Town.AllCastles.Count(town => town?.OwnerClan?.Kingdom == kingdom)
                : Town.AllTowns.Count(town => town?.OwnerClan?.Kingdom == kingdom);

            if (settlementCount <= 0)
                return 0;

            int dynamicLimit = (int)Math.Ceiling(settlementCount / (double)config.SettlementsPerSlot);
            return Math.Min(config.LocalMaxLimit, Math.Max(config.LocalBaseLimit, dynamicLimit));
        }

        public static bool IsEligible(Hero hero, Kingdom kingdom, OfficeType officeType)
        {
            if (hero == null || kingdom == null || kingdom.IsEliminated
                || !hero.IsAlive || !hero.IsActive || hero.IsPrisoner
                || hero.Clan == null || hero.Clan.Kingdom != kingdom
                || hero.Clan.IsEliminated || hero.Clan.IsClanTypeMercenary)
                return false;

            if (!OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config)
                || !config.IsEnabled(officeType))
                return false;

            if (IsLocal(officeType))
                return hero.GovernorOf?.Settlement?.OwnerClan?.Kingdom == kingdom;

            if (officeType == OfficeType.Marshal)
                return hero != kingdom.Leader && hero.CanLeadParty();

            return true;
        }

        public static float GetCandidateScore(Hero hero, OfficeType officeType)
        {
            if (hero == null)
                return 0f;

            switch (officeType)
            {
                case OfficeType.Marshal:
                    float influence = Math.Min(hero.Clan?.Influence ?? 0f, 900f) * 0.5f;
                    float commanderBonus = hero.GetTraitLevel(DefaultTraits.Commander) > 0 ? 300f : 0f;
                    return hero.GetSkillValue(DefaultSkills.Tactics) * 3f
                           + hero.GetSkillValue(DefaultSkills.Leadership) * 2f
                           + hero.GetSkillValue(DefaultSkills.Steward)
                           + influence
                           + commanderBonus;
                case OfficeType.TaxOfficer:
                    return hero.GetSkillValue(DefaultSkills.Steward) + hero.GetSkillValue(DefaultSkills.Trade);
                case OfficeType.AgricultureOfficer:
                    return hero.GetSkillValue(DefaultSkills.Steward);
                case OfficeType.MilitaryOfficer:
                    return hero.GetSkillValue(DefaultSkills.Leadership) + hero.GetSkillValue(DefaultSkills.Steward);
                case OfficeType.SecurityOfficer:
                    return hero.GetSkillValue(DefaultSkills.Steward) + hero.GetSkillValue(DefaultSkills.Charm);
                default:
                    // Chief minister and court steward AI scoring is intentionally deferred.
                    return 0f;
            }
        }
    }
}
