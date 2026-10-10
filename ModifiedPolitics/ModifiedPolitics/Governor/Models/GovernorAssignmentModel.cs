using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedPolitics.Governor.Models
{
    /// <summary>
    /// Calculates governor profiles, ability thresholds and political ordering bands.
    /// </summary>
    public static class GovernorAssignmentModel
    {
        public static GovernorSettlementContext BuildContext(Kingdom kingdom, Town town)
        {
            bool border = BorderSettlementModel.IsBorderSettlement(kingdom, town?.Settlement);
            GovernorProfile profile = town?.IsTown == true && !border
                ? GovernorProfile.Civil
                : GovernorProfile.Frontier;
            float prosperity = Math.Max(0f, town?.Prosperity ?? 0f);
            float required = (town?.IsTown == true ? 450f : 350f)
                             + Math.Min(250f, prosperity / 40f)
                             + (border ? 100f : 0f);
            return new GovernorSettlementContext(town, border, profile, required);
        }

        public static float GetProfileGovernorAbility(Hero hero, GovernorProfile profile)
        {
            if (hero == null)
                return float.MinValue;

            if (profile == GovernorProfile.Civil)
            {
                return hero.GetSkillValue(DefaultSkills.Steward) * 2f
                       + hero.GetSkillValue(DefaultSkills.Trade) * 1.5f
                       + hero.GetSkillValue(DefaultSkills.Charm)
                       + hero.GetSkillValue(DefaultSkills.Leadership) * 0.5f
                       + hero.GetSkillValue(DefaultSkills.Engineering) * 0.5f;
            }

            return hero.GetSkillValue(DefaultSkills.Steward) * 2f
                   + hero.GetSkillValue(DefaultSkills.Leadership) * 1.5f
                   + hero.GetSkillValue(DefaultSkills.Engineering) * 1.2f
                   + hero.GetSkillValue(DefaultSkills.Tactics)
                   + hero.GetSkillValue(DefaultSkills.Charm) * 0.5f
                   + hero.GetSkillValue(DefaultSkills.Trade) * 0.5f;
        }

        public static float GetEffectiveGovernorAbility(Hero hero, GovernorSettlementContext context)
        {
            if (hero == null || context?.Town == null)
                return float.MinValue;

            float cultureFactor = hero.Culture == context.Town.Culture ? 1.2f : 1f;
            return GetProfileGovernorAbility(hero, context.Profile) * cultureFactor;
        }

        public static bool MeetsAbilityRequirement(Hero hero, GovernorSettlementContext context)
        {
            return GetEffectiveGovernorAbility(hero, context) >= context.RequiredAbility;
        }

        public static int GetInfluenceBand(float influence)
        {
            if (influence < 100f) return 0;
            if (influence < 300f) return 1;
            if (influence < 600f) return 2;
            if (influence < 1000f) return 3;
            return 4;
        }

        public static int GetFiefWeight(Clan clan)
        {
            return clan?.Fiefs?.Sum(town => town.IsTown ? 3 : 1) ?? 0;
        }
    }
}
