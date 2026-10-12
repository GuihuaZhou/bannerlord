using System;
using System.Linq;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.HeroOffices.Models
{
    /// <summary>
    /// Provides the single daily influence value used by campaign simulation and UI explanations.
    /// </summary>
    public static class OfficeInfluenceModel
    {
        public static float GetDailyInfluence(Clan clan)
        {
            Kingdom kingdom = clan?.Kingdom;
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            if (clan == null || kingdom == null || kingdom.IsEliminated || behavior == null)
                return 0f;

            float influence = 0f;
            foreach (OfficeType officeType in Enum.GetValues(typeof(OfficeType)))
            {
                int holderCount = behavior.GetAssignments(kingdom, officeType)
                    .Count(assignment => assignment?.Hero?.Clan == clan);
                influence += holderCount * GetDailyInfluencePerHolder(officeType);
            }

            return influence;
        }

        public static float GetDailyInfluencePerHolder(OfficeType officeType)
        {
            return OfficeRules.IsLocal(officeType) ? 1f : 5f;
        }
    }
}
