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

namespace ModifiedArmy.Recruitment.Services
{
    internal sealed class RescuedPrisonerSnapshot
    {
        internal readonly Dictionary<MobileParty,
            Dictionary<CharacterObject, (int Total, int Wounded)>> Parties =
                new Dictionary<MobileParty,
                    Dictionary<CharacterObject, (int Total, int Wounded)>>();
    }

    internal static class RescuedPrisonerFilterService
    {
        internal static RescuedPrisonerSnapshot Capture(
            MBReadOnlyList<MapEventParty> winners)
        {
            var snapshot = new RescuedPrisonerSnapshot();
            if (winners == null) return snapshot;
            foreach (MapEventParty entry in winners)
            {
                MobileParty party = entry?.Party?.MobileParty;
                if (party == null || !entry.IsNpcParty ||
                    !RecruitmentTroopEligibilityPolicy
                        .IsRegularAiLordParty(party) ||
                    !ReferenceEquals(entry.RosterToReceiveLootMembers,
                        party.MemberRoster) ||
                    snapshot.Parties.ContainsKey(party)) continue;
                snapshot.Parties[party] = SnapshotBandits(party.MemberRoster);
            }
            return snapshot;
        }

        internal static void RemoveNewBandits(RescuedPrisonerSnapshot snapshot)
        {
            if (snapshot?.Parties == null) return;
            foreach (var partySnapshot in snapshot.Parties)
            {
                MobileParty party = partySnapshot.Key;
                TroopRoster roster = party?.MemberRoster;
                if (party == null || !party.IsActive || roster == null) continue;
                int removed = 0;
                var details = new List<string>();
                foreach (TroopRosterElement element in
                    roster.GetTroopRoster().ToList())
                {
                    CharacterObject troop = element.Character;
                    if (!RecruitmentTroopEligibilityPolicy.IsBanditTroop(troop))
                        continue;
                    partySnapshot.Value.TryGetValue(troop, out var before);
                    int added = Math.Max(0, element.Number - before.Total);
                    int healthy = element.Number - element.WoundedNumber;
                    if (added <= 0 || healthy < added) continue;
                    roster.AddToCounts(troop, -added, false, 0, 0, true, -1);
                    (int finalTotal, int finalWounded) = GetCounts(roster, troop);
                    if (finalTotal != before.Total ||
                        finalWounded != before.Wounded)
                    {
                        if (RecruitmentLogFilter.ShouldLog(party))
                            ModLogger.Info(
                                $"[RescuedBanditFilterMismatch] Party={party.StringId}, " +
                                $"troop={troop.StringId}, expectedTotal={before.Total}, " +
                                $"actualTotal={finalTotal}, expectedWounded={before.Wounded}, " +
                                $"actualWounded={finalWounded}");
                        continue;
                    }
                    removed += added;
                    details.Add($"{troop.StringId}x{added}");
                }
                if (removed > 0 && RecruitmentLogFilter.ShouldLog(party))
                    ModLogger.Info(
                        $"[RescuedBanditExcluded] Party={party.StringId}, " +
                        $"rejectedTotal={removed}, troops={string.Join(",", details)}, " +
                        "action=released");
            }
        }

        private static Dictionary<CharacterObject, (int Total, int Wounded)>
            SnapshotBandits(TroopRoster roster)
        {
            var result = new Dictionary<CharacterObject,
                (int Total, int Wounded)>();
            foreach (TroopRosterElement element in roster.GetTroopRoster())
                if (RecruitmentTroopEligibilityPolicy
                    .IsBanditTroop(element.Character))
                    result[element.Character] =
                        (element.Number, element.WoundedNumber);
            return result;
        }

        private static (int Total, int Wounded) GetCounts(
            TroopRoster roster, CharacterObject troop)
        {
            foreach (TroopRosterElement element in roster.GetTroopRoster())
                if (element.Character == troop)
                    return (element.Number, element.WoundedNumber);
            return (0, 0);
        }
    }
}
