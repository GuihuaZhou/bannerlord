// Central marriage policy model used by candidate discovery and compatibility patches.
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Marriage.Models
{
    /// <summary>
    /// Holds marriage-only policy switches and conversation compatibility rules.
    /// Keeping these decisions outside patches makes later UI replacement straightforward.
    /// </summary>
    public static class MarriagePolicyModel
    {
        public const bool AllowSameKingdomDifferentCulture = true;
        // Enabled together with the recoverable M7.1 player inbox and map notification.
        public const bool EnableAiToPlayer = true;
        public const int MaxNewProposalsPerClanTick = 1;

        public static bool CanUseVanillaMarriageBarter(Hero selectedHero)
        {
            // The main hero keeps the vanilla romance route; relatives must use proposals.
            return selectedHero == null || selectedHero == Hero.MainHero;
        }
    }
}
