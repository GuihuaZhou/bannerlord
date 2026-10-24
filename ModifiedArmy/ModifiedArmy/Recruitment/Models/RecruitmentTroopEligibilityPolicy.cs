using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Provides the shared troop eligibility rules used by AI recruitment paths.
    /// This policy is intentionally side-effect free: it never changes rosters,
    /// settlements, recruitment pools, or party resources.
    /// </summary>
    public static class RecruitmentTroopEligibilityPolicy
    {
        /// <summary>
        /// Treats both occupation-based bandits and troops belonging to a bandit
        /// culture as bandits. String IDs and localized names are not reliable
        /// classification inputs and therefore are deliberately ignored.
        /// </summary>
        public static bool IsBanditTroop(CharacterObject troop) =>
            troop != null && (troop.Occupation == Occupation.Bandit ||
                troop.Culture?.IsBandit == true);

        /// <summary>
        /// Identifies an active AI-controlled lord party. The player's main party
        /// must remain outside automatic filtering because its recruitment choices
        /// are explicitly controlled by the player.
        /// </summary>
        public static bool IsRegularAiLordParty(MobileParty party) =>
            party != null && party.IsActive && !party.IsMainParty &&
            party.IsLordParty;

        /// <summary>
        /// Returns whether an automatic recruitment operation should reject the
        /// supplied bandit troop. Regular lord parties are covered directly. A
        /// garrison is covered only when its settlement enables automatic garrison
        /// recruitment and is not owned by the player's clan.
        /// </summary>
        public static bool ShouldExcludeBanditForAiRecruitment(
            MobileParty party,
            CharacterObject troop)
        {
            if (party == null || party.IsMainParty || !IsBanditTroop(troop))
                return false;
            if (party.IsLordParty) return true;
            if (!party.IsGarrison) return false;

            // CurrentSettlement is authoritative while the party is physically
            // inside a settlement; HomeSettlement is the stable fallback used by
            // garrisons during transient campaign-state updates.
            Settlement home = party.CurrentSettlement ?? party.HomeSettlement;
            return home != null && home.OwnerClan != Clan.PlayerClan &&
                home.Town?.GarrisonAutoRecruitmentIsEnabled == true;
        }
    }
}
