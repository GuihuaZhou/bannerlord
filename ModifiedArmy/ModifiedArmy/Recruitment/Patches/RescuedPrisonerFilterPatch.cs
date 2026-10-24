using HarmonyLib;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Removes only bandit troops newly added to regular AI lord parties by the
    /// vanilla rescued-prisoner flow. The original method always runs normally;
    /// this patch does not alter defeated soldiers placed in a prisoner roster.
    /// </summary>
    [HarmonyPatch(typeof(MapEvent), "LootDefeatedPartyPrisoners")]
    internal static class RescuedPrisonerFilterPatch
    {
        internal sealed class Snapshot
        {
            // State belongs to one patched method invocation. Keeping it in
            // __state avoids cross-battle leakage and requires no save data.
            internal readonly Dictionary<MobileParty,
                Dictionary<CharacterObject, (int Total, int Wounded)>> Parties = new();
        }

        /// <summary>
        /// Captures the pre-call bandit roster for eligible winners. The receiving
        /// roster must be the party's real MemberRoster; temporary player-facing
        /// loot rosters and alternate settlement rosters are skipped conservatively.
        /// </summary>
        private static void Prefix(
            MBReadOnlyList<MapEventParty> winnerParties,
            out Snapshot __state)
        {
            __state = new Snapshot();
            if (winnerParties == null) return;
            foreach (MapEventParty entry in winnerParties)
            {
                MobileParty party = entry?.Party?.MobileParty;
                // One party may appear through more than one map-event entry. A
                // single baseline per party prevents applying the delta twice.
                if (party == null || !entry.IsNpcParty ||
                    !RecruitmentTroopEligibilityPolicy.IsRegularAiLordParty(party) ||
                    !ReferenceEquals(entry.RosterToReceiveLootMembers,
                        party.MemberRoster) || __state.Parties.ContainsKey(party))
                    continue;
                __state.Parties[party] = SnapshotBandits(party.MemberRoster);
            }
        }

        /// <summary>
        /// Compares the post-call roster against the captured baseline and removes
        /// only a positive net increase of healthy bandits. Existing bandits and
        /// historical wounded troops remain untouched.
        /// </summary>
        private static void Postfix(Snapshot __state)
        {
            if (__state?.Parties == null) return;
            foreach (var partySnapshot in __state.Parties)
            {
                MobileParty party = partySnapshot.Key;
                TroopRoster roster = party?.MemberRoster;
                if (party == null || !party.IsActive || roster == null) continue;
                int removed = 0;
                List<string> details = new();
                foreach (TroopRosterElement element in roster.GetTroopRoster().ToList())
                {
                    CharacterObject troop = element.Character;
                    if (!RecruitmentTroopEligibilityPolicy.IsBanditTroop(troop))
                        continue;
                    partySnapshot.Value.TryGetValue(troop, out var before);
                    int added = Math.Max(0, element.Number - before.Total);
                    int healthy = element.Number - element.WoundedNumber;
                    // Vanilla adds rescued troops as healthy members. If the
                    // healthy count cannot cover the observed delta, another patch
                    // or an unexpected roster transition may be involved, so skip
                    // the troop rather than risk deleting historical wounded units.
                    if (added <= 0 || healthy < added) continue;
                    roster.AddToCounts(troop, -added, false, 0, 0, true, -1);
                    (int finalTotal, int finalWounded) = GetCounts(roster, troop);
                    // The false wounded flag should remove healthy troops in the
                    // target game version. Verify both counters so a future API
                    // semantic change is visible instead of silently corrupting
                    // the wounded portion of the roster.
                    if (finalTotal != before.Total ||
                        finalWounded != before.Wounded)
                    {
                        if (RecruitmentLogFilter.ShouldLog(party))
                            ModLogger.Info($"[RescuedBanditFilterMismatch] Party={party.StringId}, " +
                                $"troop={troop.StringId}, expectedTotal={before.Total}, " +
                                $"actualTotal={finalTotal}, expectedWounded={before.Wounded}, " +
                                $"actualWounded={finalWounded}");
                        continue;
                    }
                    removed += added;
                    details.Add($"{troop.StringId}×{added}");
                }
                if (removed > 0 && RecruitmentLogFilter.ShouldLog(party))
                    ModLogger.Info($"[RescuedBanditExcluded] Party={party.StringId}, " +
                        $"rejectedTotal={removed}, troops={string.Join(",", details)}, action=released");
            }
        }

        /// <summary>
        /// Builds the per-party baseline used to distinguish pre-existing bandits
        /// from troops added during the patched vanilla method.
        /// </summary>
        private static Dictionary<CharacterObject, (int Total, int Wounded)>
            SnapshotBandits(TroopRoster roster)
        {
            Dictionary<CharacterObject, (int, int)> result = new();
            foreach (TroopRosterElement element in roster.GetTroopRoster())
                if (RecruitmentTroopEligibilityPolicy.IsBanditTroop(element.Character))
                    result[element.Character] = (element.Number, element.WoundedNumber);
            return result;
        }

        /// <summary>
        /// Reads the current total and wounded counts for one troop type. A missing
        /// roster element is equivalent to zero total and zero wounded members.
        /// </summary>
        private static (int Total, int Wounded) GetCounts(
            TroopRoster roster,
            CharacterObject troop)
        {
            foreach (TroopRosterElement element in roster.GetTroopRoster())
                if (element.Character == troop)
                    return (element.Number, element.WoundedNumber);
            return (0, 0);
        }
    }
}
