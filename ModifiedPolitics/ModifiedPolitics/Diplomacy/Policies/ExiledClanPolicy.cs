using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.Diplomacy.Policies
{
    /// <summary>
    /// Centralizes the definition of an exiled noble clan used by the
    /// survival and diplomatic-realignment systems.
    /// </summary>
    public static class ExiledClanPolicy
    {
        /// <summary>
        /// Returns whether the clan is a normal noble clan with at least one
        /// living adult member. Rebel, minor, bandit, mercenary and player
        /// clans deliberately retain their native behavior.
        /// </summary>
        public static bool IsProtectedNobleClan(Clan clan)
        {
            return clan != null &&
                   clan != Clan.PlayerClan &&
                   !clan.IsEliminated &&
                   !clan.IsRebelClan &&
                   !clan.IsBanditFaction &&
                   !clan.IsMinorFaction &&
                   !clan.IsClanTypeMercenary &&
                   clan.Heroes.Any(hero => hero != null &&
                       hero.IsAlive && !hero.IsChild);
        }

        /// <summary>
        /// Returns whether the clan is currently landless and independent,
        /// and may therefore use the special realignment rules.
        /// </summary>
        public static bool IsEligibleExiledClan(Clan clan)
        {
            return IsProtectedNobleClan(clan) &&
                   clan.Kingdom == null &&
                   clan.Settlements.Count == 0 &&
                   clan.Leader != null &&
                   clan.Leader.IsAlive &&
                   !clan.Leader.IsChild;
        }
    }
}
