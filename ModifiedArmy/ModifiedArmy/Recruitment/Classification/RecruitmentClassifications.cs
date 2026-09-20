using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Classification
{
    public enum CombatRole
    {
        Infantry,
        Ranged,
        Cavalry,
        HorseArcher
    }

    public enum TroopQuality
    {
        LowTier,
        MiddleTier,
        TopTier
    }

    /// <summary>
    /// Classifies every non-hero troop into exactly one combat role and one
    /// quality group so ratio accounting cannot count a troop twice.
    /// </summary>
    public static class RecruitmentTroopClassifier
    {
        public static CombatRole GetCombatRole(CharacterObject troop)
        {
            if (troop != null && troop.IsMounted && troop.IsRanged)
            {
                return CombatRole.HorseArcher;
            }

            if (troop != null && troop.IsMounted)
            {
                return CombatRole.Cavalry;
            }

            if (troop != null && troop.IsRanged)
            {
                return CombatRole.Ranged;
            }

            return CombatRole.Infantry;
        }

        public static TroopQuality GetQuality(CharacterObject troop)
        {
            int tier = troop?.Tier ?? 0;

            if (tier >= 6)
            {
                return TroopQuality.TopTier;
            }

            return tier >= 4
                ? TroopQuality.MiddleTier
                : TroopQuality.LowTier;
        }
    }
}
