using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedPolitics.Governor.Models
{
    /// <summary>
    /// Keeps fief priority and governor candidate scoring separate from assignment flow.
    /// </summary>
    public static class GovernorAssignmentModel
    {
        public static float GetSettlementPriority(Town town)
        {
            if (town?.Settlement == null)
                return float.MinValue;

            int boundVillageCount = town.Villages?.Count ?? 0;
            // Towns start above castles; square-root scaling prevents prosperity from dominating.
            return (town.IsCastle ? 1f : 3f)
                   + (float)Math.Sqrt(Math.Max(0f, town.Prosperity) / 1000f)
                   + boundVillageCount;
        }

        public static float GetCandidateScore(Hero hero, Town town)
        {
            if (hero == null || town?.Settlement == null)
                return float.MinValue;

            float baseScore = hero.GetSkillValue(DefaultSkills.Steward) * 2f
                              + hero.GetSkillValue(DefaultSkills.Trade) * 1.2f
                              + hero.GetSkillValue(DefaultSkills.Leadership)
                              + hero.GetSkillValue(DefaultSkills.Charm) * 0.9f
                              + hero.GetSkillValue(DefaultSkills.Engineering) * 0.8f
                              + hero.GetSkillValue(DefaultSkills.Tactics) * 0.3f;

            if (hero.GetTraitLevel(DefaultTraits.Honor) > 0)
                // Positive honor is a fixed bonus and does not scale with additional trait levels.
                baseScore += 100f;

            // Matching culture is a soft preference rather than an eligibility requirement.
            float cultureFactor = hero.Culture == town.Culture ? 1.2f : 1f;
            return baseScore * cultureFactor;
        }
    }
}
