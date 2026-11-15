// Defines the read-only clan wealth bands used by party finance. Hero gold is
// authoritative; no state is cached or persisted, so save compatibility and
// the native/mod financial mutation owners remain unchanged.
using System;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.PartyManagement.Finance.Models
{
    /// <summary>Stable three-band interpretation of the existing wage thresholds.</summary>
    public enum ClanFiscalState
    {
        Critical,
        Tight,
        Normal
    }

    /// <summary>
    /// Exposes the wage calculator's existing wealth boundaries without
    /// creating a second financial ledger or introducing persistence.
    /// </summary>
    public static class ClanFiscalStateModel
    {
        public const int PoorClanGold = 30000;
        public const int RichClanGold = 90000;

        /// <summary>
        /// Reads the leader's current gold. Failure is explicit because a
        /// missing leader must not be interpreted as a healthy clan.
        /// </summary>
        public static bool TryGetState(Clan clan, out ClanFiscalState state)
        {
            state = ClanFiscalState.Normal;
            if (clan?.Leader == null)
                return false;

            int gold = Math.Max(0, clan.Leader.Gold);
            state = gold <= PoorClanGold
                ? ClanFiscalState.Critical
                : gold <= RichClanGold
                    ? ClanFiscalState.Tight
                    : ClanFiscalState.Normal;
            return true;
        }
    }
}
