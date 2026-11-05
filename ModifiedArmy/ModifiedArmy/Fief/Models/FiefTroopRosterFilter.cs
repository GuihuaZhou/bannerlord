using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// Authoritative roster query for feudal troop entries. The returned
    /// snapshot preserves both total and wounded counts.
    /// </summary>
    internal static class FiefTroopRosterFilter
    {
        internal static IReadOnlyList<TroopRosterElement> GetEntries(
            TroopRoster roster, FiefPartyTemplate template)
        {
            if (roster == null || template == null)
                return Array.Empty<TroopRosterElement>();
            return roster.GetTroopRoster().Where(entry =>
                entry.Character != null && entry.Number > 0 &&
                template.IsEnableTroop(entry.Character)).ToList();
        }

        internal static bool TryGetCounts(TroopRoster roster,
            CharacterObject troop, out int count, out int wounded)
        {
            count = 0;
            wounded = 0;
            if (roster == null || troop == null) return false;
            foreach (TroopRosterElement entry in roster.GetTroopRoster())
            {
                if (entry.Character != troop) continue;
                count = entry.Number;
                wounded = entry.WoundedNumber;
                return true;
            }
            return false;
        }
    }
}
