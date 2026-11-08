using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;

namespace ModifiedArmy.Population.Patches
{
    /// <summary>
    /// The native recruitment screen writes the player roster directly and
    /// does not call RecruitmentCampaignBehavior.ApplyInternal. Reserve each
    /// local source before that write and compensate on mismatch/exception.
    /// </summary>
    [HarmonyPatch(typeof(RecruitmentVM), "OnDone")]
    internal static class RecruitmentVmPopulationPatch
    {
        private sealed class Reservation
        {
            public Settlement Settlement;
            public CharacterObject Troop;
            public int Count;
            public string OperationId;
        }

        private sealed class PatchState
        {
            public readonly List<Reservation> Reservations =
                new List<Reservation>();
            public readonly Dictionary<CharacterObject, int> RosterBefore =
                new Dictionary<CharacterObject, int>();
            public readonly Dictionary<CharacterObject, int> ExpectedAdded =
                new Dictionary<CharacterObject, int>();
        }

        private static bool Prefix(RecruitmentVM __instance,
            out PatchState __state)
        {
            __state = new PatchState();
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (__instance?.TroopsInCart == null ||
                __instance.TroopsInCart.Count == 0) return true;
            // ModifiedArmy remains usable without the optional Politics module.
            // When Politics is present its campaign behavior registers this
            // service before recruitment can be opened.
            if (population == null) return true;
            var requests = __instance.TroopsInCart
                .Where(item => item?.Character != null &&
                    item.Owner?.OwnerHero?.CurrentSettlement != null)
                .GroupBy(item => new
                {
                    Settlement = item.Owner.OwnerHero.CurrentSettlement,
                    Troop = item.Character
                })
                .OrderBy(group => group.Key.Settlement.StringId,
                    StringComparer.Ordinal)
                .ThenBy(group => group.Key.Troop.StringId,
                    StringComparer.Ordinal);
            foreach (var request in requests)
            {
                Reservation reservation = new Reservation
                {
                    Settlement = request.Key.Settlement,
                    Troop = request.Key.Troop,
                    Count = request.Count(),
                    OperationId = "recruitment-vm:" +
                        Guid.NewGuid().ToString("N")
                };
                if (!__state.RosterBefore.ContainsKey(reservation.Troop))
                    __state.RosterBefore.Add(reservation.Troop,
                        MobileParty.MainParty.MemberRoster.GetElementNumber(
                            reservation.Troop));
                __state.ExpectedAdded[reservation.Troop] =
                    (__state.ExpectedAdded.TryGetValue(reservation.Troop,
                        out int expected) ? expected : 0) + reservation.Count;
                PopulationTransactionResult result;
                try
                {
                    if (reservation.Settlement.IsFortification)
                    {
                        int cost = MilitaryConfigManager.Instance
                            .RequireActiveConfig()
                            .GetDoctrine(reservation.Settlement)
                            .GetProfessionalBurdenCost(reservation.Troop);
                        result = population.TryRecruitProfessional(
                            reservation.Settlement, reservation.Count, cost,
                            reservation.OperationId);
                    }
                    else if (reservation.Settlement.IsVillage)
                        result = population.TryConsume(reservation.Settlement,
                            reservation.Count, reservation.OperationId);
                    else result = null;
                }
                catch (Exception exception)
                {
                    ModLogger.FileError(string.Format(
                        "Recruitment screen population reservation failed: " +
                        "settlement={0}, settlementId={1}, troop={2}, count={3}, " +
                        "operationId={4}, error={5}.", reservation.Settlement.Name,
                        reservation.Settlement.StringId, reservation.Troop.Name,
                        reservation.Count, reservation.OperationId,
                        exception.Message));
                    result = null;
                }
                if (result?.IsCommitted != true)
                {
                    CompensateAll(__state.Reservations,
                        "RecruitmentScreenReservationRejected");
                    ModLogger.FileWarn(string.Format(
                        "Recruitment screen blocked before mutation: " +
                        "settlement={0}, settlementId={1}, troop={2}, count={3}, " +
                        "reason={4}.", reservation.Settlement.Name,
                        reservation.Settlement.StringId, reservation.Troop.Name,
                        reservation.Count, result?.ReasonCode ??
                            "PopulationReservationFailed"));
                    return false;
                }
                __state.Reservations.Add(reservation);
            }
            return true;
        }

        private static void Postfix(PatchState __state)
        {
            if (__state == null) return;
            var mismatchedTroops = new HashSet<CharacterObject>();
            foreach (KeyValuePair<CharacterObject, int> item in
                __state.ExpectedAdded)
            {
                int added = MobileParty.MainParty.MemberRoster.GetElementNumber(
                    item.Key) - __state.RosterBefore[item.Key];
                if (added != item.Value) mismatchedTroops.Add(item.Key);
            }
            foreach (Reservation item in __state.Reservations
                .Where(item => mismatchedTroops.Contains(item.Troop)))
                Compensate(item, "RecruitmentScreenRosterDeltaMismatch");
        }

        private static Exception Finalizer(Exception __exception,
            PatchState __state)
        {
            if (__exception != null)
                CompensateAll(__state?.Reservations,
                    "RecruitmentScreenNativeException");
            return __exception;
        }

        private static void CompensateAll(IEnumerable<Reservation> reservations,
            string reason)
        {
            foreach (Reservation item in (reservations ??
                Enumerable.Empty<Reservation>()).Reverse())
                Compensate(item, reason);
        }

        private static void Compensate(Reservation item, string reason)
        {
            PopulationTransactionResult result =
                SettlementPopulationServiceRegistry.Current?.TryCompensate(
                    item.Settlement, item.OperationId);
            ModLogger.FileError(string.Format(
                "Recruitment screen population compensated: settlement={0}, " +
                "settlementId={1}, troop={2}, count={3}, operationId={4}, " +
                "reason={5}, status={6}, compensationReason={7}.",
                item.Settlement.Name, item.Settlement.StringId,
                item.Troop.Name, item.Count, item.OperationId, reason,
                result?.Status, result?.ReasonCode ?? "none"));
        }
    }
}
