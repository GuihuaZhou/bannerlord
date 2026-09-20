using ModifiedArmy.Recruitment.Classification;
using System;
using System.Collections.Generic;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Defines role and quality ratio ranges for one culture and party type.
    /// Minimums affect priority; maximums are hard recruitment limits.
    /// </summary>
    public sealed class ArmyCompositionTemplate
    {
        public ArmyCompositionTemplate(
            IDictionary<CombatRole, RatioRange> combatRoles,
            IDictionary<TroopQuality, RatioRange> qualities)
        {
            CombatRoles = new Dictionary<CombatRole, RatioRange>(combatRoles);
            Qualities = new Dictionary<TroopQuality, RatioRange>(qualities);
            Validate();
        }

        public IReadOnlyDictionary<CombatRole, RatioRange> CombatRoles
        {
            get;
        }

        public IReadOnlyDictionary<TroopQuality, RatioRange> Qualities
        {
            get;
        }

        public RatioRange GetRange(CombatRole role) => CombatRoles[role];

        public RatioRange GetRange(TroopQuality quality) => Qualities[quality];

        private void Validate()
        {
            ValidateDimension(CombatRoles, Enum.GetValues(typeof(CombatRole)).Length);
            ValidateDimension(Qualities, Enum.GetValues(typeof(TroopQuality)).Length);
        }

        private static void ValidateDimension<TKey>(
            IReadOnlyDictionary<TKey, RatioRange> ranges,
            int expectedCount)
        {
            if (ranges.Count != expectedCount)
            {
                throw new ArgumentException("Composition template is incomplete.");
            }

            float minimumTotal = 0f;
            float maximumTotal = 0f;

            foreach (RatioRange range in ranges.Values)
            {
                if (range == null
                    || range.MinimumRatio < 0f
                    || range.MaximumRatio > 1f
                    || range.MinimumRatio > range.MaximumRatio)
                {
                    throw new ArgumentException("Composition ratio is invalid.");
                }

                minimumTotal += range.MinimumRatio;
                maximumTotal += range.MaximumRatio;
            }

            if (minimumTotal > 1.0001f || maximumTotal < 0.9999f)
            {
                throw new ArgumentException("Composition dimension cannot cover a party.");
            }
        }
    }
}
