using ModifiedArmy.common;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Models.Fief
{
    internal enum FiefPlacementPurpose
    {
        PlayerReturn,
        PeaceDemobilization,
        WarRest,
        PartyDisbandment
    }

    internal sealed class FiefPlacementResult
    {
        public int PlacedCount { get; internal set; }
        public IReadOnlyDictionary<CharacterObject, int> PlacedByTroop {
            get;
            internal set;
        } = new ReadOnlyDictionary<CharacterObject, int>(
            new Dictionary<CharacterObject, int>());
        public string ReasonCode { get; internal set; }
        public bool RecoveryRequired { get; internal set; }
    }

    /// <summary>
    /// Places reconciled surplus feudal troops into existing village cooling
    /// queues. It never changes Hearth, mobilization, or service identities.
    /// </summary>
    internal static class FiefTroopPlacementService
    {
        private sealed class PlacementAmount
        {
            public int Count;
            public int Wounded;
        }

        private sealed class VillagePlan
        {
            public VillageFiefData Village;
            public Dictionary<CharacterObject, PlacementAmount> Troops = new();
            public int PlannedHearth;
        }

        internal static FiefPlacementResult TryPlaceAfterReturn(
            FiefPartyManager manager,
            MobileParty party,
            Settlement interaction,
            FiefPlacementPurpose purpose,
            int maximumCount = int.MaxValue)
        {
            FiefPlacementResult result = new() { ReasonCode = "NotStarted" };
            string operationId = string.Format("fief-placement:{0}:{1}:{2}",
                party?.StringId ?? "missing-party",
                (int)CampaignTime.Now.ToDays,
                Guid.NewGuid().ToString("N"));
            if (manager == null || party?.MemberRoster == null ||
                party.IsActive != true || maximumCount <= 0 ||
                !manager.TryResolveOperationScope(interaction, party, true,
                    out FiefOperationScope scope) ||
                scope.FiefData?.IsRuntimeReady != true ||
                scope.FiefData.GetFiefPartyTemplate() == null)
            {
                return Reject(result, party, interaction, purpose,
                    operationId, "PlacementScopeInvalid");
            }

            FiefPartyTemplate template = scope.FiefData.GetFiefPartyTemplate();
            CulturalMilitaryDoctrine doctrine;
            try
            {
                doctrine = MilitaryConfigManager.Instance
                    .RequireActiveConfig().GetDoctrine(scope.SourceFief);
            }
            catch (Exception exception)
            {
                return Reject(result, party, interaction, purpose,
                    operationId, "PlacementDoctrineUnavailable",
                    exception.Message);
            }
            if (doctrine == null)
                return Reject(result, party, interaction, purpose,
                    operationId, "PlacementDoctrineUnavailable");

            manager.AdvanceServiceDays(party);
            FiefReconciliationResult reconciliation = manager
                .ReconcilePartyFiefTroops(party, true);
            if (reconciliation.BlockedByRecovery ||
                !reconciliation.PlacementSnapshotValid)
            {
                return Reject(result, party, interaction, purpose,
                    operationId, reconciliation.ReasonCode ??
                        "PlacementSnapshotInvalid");
            }
            if (RecruitmentLogFilter.ShouldLog(party))
                ModLogger.FileDebug(string.Format(
                    "Fief placement snapshot accepted: party={0}, " +
                    "partyId={1}, settlement={2}, settlementId={3}, " +
                    "purpose={4}, operation={5}, protected={6}, losses={7}, " +
                    "unassigned={8}, woundedUnassigned={9}.",
                    PartyLogFormatter.GetDisplayName(party), party.StringId,
                    interaction.Name, interaction.StringId, purpose,
                    operationId, reconciliation.MatchedCount,
                    reconciliation.LostCount,
                    reconciliation.UnassignedTroops.Values.Sum(value =>
                        value.Total),
                    reconciliation.UnassignedTroops.Values.Sum(value =>
                        value.Wounded)));

            List<VillagePlan> plans = scope.FiefData
                .OrderedVillages(scope.VillageFilter)
                .Where(village => village?.VillageSettlement != null &&
                    !village.RecoveryRequired &&
                    village.GetReadyRoster() != null)
                .Select(village => new VillagePlan { Village = village })
                .ToList();
            if (plans.Count == 0)
                return Reject(result, party, interaction, purpose,
                    operationId, "NoEligiblePlacementVillage");

            int remainingLimit = Math.Max(0, maximumCount);
            bool woundedFirst = purpose != FiefPlacementPurpose.PlayerReturn;
            foreach (var pair in reconciliation.UnassignedTroops
                .Where(pair => pair.Key != null && pair.Value.Total > 0)
                .OrderBy(pair => pair.Key.StringId, StringComparer.Ordinal))
            {
                CharacterObject troop = pair.Key;
                if (!template.IsEnableTroop(troop) ||
                    !doctrine.TryGetFeudalSupplyCost(troop, out int cost) ||
                    cost <= 0) continue;
                int approved = Math.Min(pair.Value.Total, remainingLimit);
                int approvedWounded = woundedFirst
                    ? Math.Min(pair.Value.Wounded, approved)
                    : Math.Max(0, approved - pair.Value.Healthy);
                for (int i = 0; i < approved; i++)
                {
                    VillagePlan selected = plans
                        .Where(plan => CanPlanOne(plan, template, doctrine,
                            troop, cost))
                        .OrderByDescending(plan => EffectiveRemaining(
                            plan, template, doctrine))
                        .ThenBy(plan => plan.Village.VillageSettlement.StringId,
                            StringComparer.Ordinal)
                        .FirstOrDefault();
                    if (selected == null) break;
                    bool wounded = woundedFirst
                        ? i < approvedWounded
                        : i >= approved - approvedWounded;
                    if (!selected.Troops.TryGetValue(troop,
                        out PlacementAmount amount))
                        selected.Troops[troop] = amount = new PlacementAmount();
                    amount.Count++;
                    if (wounded) amount.Wounded++;
                    selected.PlannedHearth = checked(
                        selected.PlannedHearth + cost);
                    remainingLimit--;
                    if (remainingLimit <= 0) break;
                }
                if (remainingLimit <= 0) break;
            }

            Dictionary<CharacterObject, int> committed = new();
            int committedVillages = 0;
            foreach (VillagePlan plan in plans
                .Where(plan => plan.Troops.Count > 0)
                .OrderBy(plan => plan.Village.VillageSettlement.StringId,
                    StringComparer.Ordinal))
            {
                if (!CommitVillage(manager, party, interaction, scope,
                    template, doctrine, plan, operationId,
                    out string reason, out bool recoveryRequired))
                {
                    result.RecoveryRequired = recoveryRequired;
                    result.ReasonCode = reason;
                    if (RecruitmentLogFilter.ShouldLog(party))
                        ModLogger.FileWarn(string.Format(
                            "Fief placement village commit stopped: " +
                            "village={0}, villageId={1}, party={2}, " +
                            "partyId={3}, operation={4}, requested={5}, " +
                            "reason={6}, recoveryRequired={7}.",
                            plan.Village.VillageSettlement?.Name,
                            plan.Village.VillageSettlement?.StringId,
                            PartyLogFormatter.GetDisplayName(party),
                            party.StringId, operationId,
                            plan.Troops.Values.Sum(value => value.Count),
                            reason, recoveryRequired));
                    break;
                }
                foreach (var pair in plan.Troops)
                    committed[pair.Key] = committed.TryGetValue(pair.Key,
                        out int old) ? old + pair.Value.Count : pair.Value.Count;
                committedVillages++;
            }

            result.PlacedCount = committed.Values.Sum();
            result.PlacedByTroop = new ReadOnlyDictionary<CharacterObject, int>(
                committed);
            if (result.PlacedCount > 0)
            {
                result.ReasonCode = result.RecoveryRequired
                    ? result.ReasonCode : "Completed";
                scope.FiefData.RefreshManpowerCounters();
                LogCommitted(party, interaction, purpose, operationId,
                    result.PlacedCount, committed.Count, committedVillages);
            }
            else if (string.IsNullOrEmpty(result.ReasonCode) ||
                result.ReasonCode == "NotStarted")
            {
                result.ReasonCode = "NoEligibleTroopsOrCapacity";
            }
            return result;
        }

        private static bool CommitVillage(FiefPartyManager manager,
            MobileParty party, Settlement interaction, FiefOperationScope scope,
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine,
            VillagePlan plan, string operationId, out string reason,
            out bool recoveryRequired)
        {
            reason = null;
            recoveryRequired = false;
            if (!manager.TryResolveOperationScope(interaction, party, true,
                    out FiefOperationScope liveScope) ||
                liveScope.SourceFief != scope.SourceFief ||
                !liveScope.FiefData.IsCurrentVillage(
                    plan.Village.VillageSettlement) ||
                plan.Village.RecoveryRequired ||
                plan.Village.GetReadyRoster() == null)
            {
                reason = "PlacementScopeChanged";
                return false;
            }
            FiefReconciliationResult live = manager
                .ReconcilePartyFiefTroops(party, true);
            if (!live.PlacementSnapshotValid || live.BlockedByRecovery)
            {
                reason = live.ReasonCode ?? "PlacementSnapshotChanged";
                return false;
            }
            foreach (var pair in plan.Troops)
            {
                if (!live.UnassignedTroops.TryGetValue(pair.Key,
                        out FiefTroopCount available) ||
                    available.Total < pair.Value.Count ||
                    available.Wounded < pair.Value.Wounded ||
                    available.Healthy < pair.Value.Count -
                        pair.Value.Wounded ||
                    party.MemberRoster.GetTroopCount(pair.Key) <
                        pair.Value.Count ||
                    !plan.Village.CanIncreaseSupply(template, doctrine,
                        pair.Key, pair.Value.Count))
                {
                    reason = "PlacementPrecommitChanged";
                    return false;
                }
            }
            if (EffectiveRemaining(new VillagePlan
                {
                    Village = plan.Village
                }, template, doctrine) < plan.PlannedHearth)
            {
                reason = "PlacementAggregateCapacityChanged";
                return false;
            }

            int cooldown = Math.Max(0, doctrine.Service.ReturnCooldownWeeks);
            FiefReturnBatch cooling = plan.Village.ReturnBatches
                .FirstOrDefault(batch => batch != null &&
                    batch.WaitWeeks == cooldown);
            bool created = cooling == null;
            if (created) cooling = new FiefReturnBatch(cooldown);
            Dictionary<CharacterObject, int> coolingBefore = cooling.Troops
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<CharacterObject, (int Count, int Wounded)> rosterBefore =
                plan.Troops.Keys.ToDictionary(troop => troop,
                    troop => Snapshot(party.MemberRoster, troop));
            try
            {
                foreach (var pair in plan.Troops.OrderBy(item =>
                    item.Key.StringId, StringComparer.Ordinal))
                {
                    party.MemberRoster.AddToCounts(pair.Key,
                        -pair.Value.Count, false, -pair.Value.Wounded,
                        0, true, -1);
                    (int Count, int Wounded) after = Snapshot(
                        party.MemberRoster, pair.Key);
                    if (after.Count != rosterBefore[pair.Key].Count -
                            pair.Value.Count ||
                        after.Wounded != rosterBefore[pair.Key].Wounded -
                            pair.Value.Wounded)
                        throw new InvalidOperationException(
                            "PlacementRosterDeltaMismatch");
                }
                if (created) plan.Village.ReturnBatches.Add(cooling);
                cooling.AddTroops(plan.Troops.ToDictionary(
                    pair => pair.Key, pair => pair.Value.Count));
                foreach (var pair in plan.Troops)
                {
                    int before = coolingBefore.TryGetValue(pair.Key,
                        out int old) ? old : 0;
                    if (!cooling.Troops.TryGetValue(pair.Key, out int after) ||
                        after != before + pair.Value.Count)
                        throw new InvalidOperationException(
                            "PlacementCoolingDeltaMismatch");
                }
                if (plan.Troops.Values.Sum(item => item.Count) !=
                    cooling.GetTotalCount() - coolingBefore.Values.Sum())
                    throw new InvalidOperationException(
                        "PlacementConservationMismatch");
                return true;
            }
            catch (Exception exception)
            {
                bool restored = true;
                try
                {
                    cooling.ReplaceTroops(coolingBefore);
                    if (created) plan.Village.ReturnBatches.Remove(cooling);
                }
                catch { restored = false; }
                foreach (var pair in rosterBefore)
                {
                    try
                    {
                        (int Count, int Wounded) current = Snapshot(
                            party.MemberRoster, pair.Key);
                        party.MemberRoster.AddToCounts(pair.Key,
                            pair.Value.Count - current.Count, false,
                            pair.Value.Wounded - current.Wounded,
                            0, true, -1);
                    }
                    catch { restored = false; }
                }
                restored = restored && DictionaryEquals(
                        cooling.Troops, coolingBefore) &&
                    (!created || !plan.Village.ReturnBatches.Contains(cooling)) &&
                    rosterBefore.All(pair => Snapshot(
                        party.MemberRoster, pair.Key) == pair.Value);
                if (!restored)
                {
                    plan.Village.RequireRecovery(
                        "PlacementRollbackUnverified", operationId);
                    recoveryRequired = true;
                    reason = "PlacementRollbackUnverified";
                    ModLogger.FileError(string.Format(
                        "Fief placement rollback unverified: village={0}, " +
                        "villageId={1}, party={2}, partyId={3}, operation={4}, " +
                        "error={5}.", plan.Village.VillageSettlement?.Name,
                        plan.Village.VillageSettlement?.StringId,
                        PartyLogFormatter.GetDisplayName(party),
                        party?.StringId, operationId, exception.Message));
                }
                else reason = "PlacementRolledBackVerified";
                return false;
            }
        }

        private static bool CanPlanOne(VillagePlan plan,
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine,
            CharacterObject troop, int cost) =>
            plan.Village.CanIncreaseSupply(template, doctrine, troop, 1) &&
            EffectiveRemaining(plan, template, doctrine) >= cost;

        private static int EffectiveRemaining(VillagePlan plan,
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine) =>
            Math.Min(plan.Village.GetFreeSupplyHearth(template, doctrine),
                plan.Village.GetFeudalBudgetRemainingHearth(
                    template, doctrine)) - plan.PlannedHearth;

        private static (int Count, int Wounded) Snapshot(
            TroopRoster roster, CharacterObject troop)
        {
            TroopRosterElement item = roster.GetTroopRoster()
                .FirstOrDefault(entry => entry.Character == troop);
            return (Math.Max(0, item.Number),
                Math.Max(0, item.WoundedNumber));
        }

        private static bool DictionaryEquals(
            IReadOnlyDictionary<CharacterObject, int> left,
            IReadOnlyDictionary<CharacterObject, int> right) =>
            left.Count == right.Count && left.All(pair =>
                right.TryGetValue(pair.Key, out int value) &&
                value == pair.Value);

        private static FiefPlacementResult Reject(FiefPlacementResult result,
            MobileParty party, Settlement interaction,
            FiefPlacementPurpose purpose, string operationId, string reason,
            string detail = null)
        {
            result.ReasonCode = reason;
            if (RecruitmentLogFilter.ShouldLog(party))
                ModLogger.FileDebug(string.Format(
                    "Fief placement rejected: party={0}, partyId={1}, " +
                    "settlement={2}, settlementId={3}, purpose={4}, " +
                    "operation={5}, reason={6}, detail={7}.",
                    PartyLogFormatter.GetDisplayName(party), party?.StringId,
                    interaction?.Name, interaction?.StringId, purpose,
                    operationId, reason, detail ?? "none"));
            return result;
        }

        private static void LogCommitted(MobileParty party,
            Settlement interaction, FiefPlacementPurpose purpose,
            string operationId, int count, int troopKinds, int villages)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            ModLogger.FileInfo(string.Format(
                "Fief placement committed: party={0}, partyId={1}, " +
                "settlement={2}, settlementId={3}, purpose={4}, " +
                "operation={5}, placed={6}, troopKinds={7}, villages={8}.",
                PartyLogFormatter.GetDisplayName(party), party?.StringId,
                interaction?.Name, interaction?.StringId, purpose,
                operationId, count, troopKinds, villages));
        }
    }
}
