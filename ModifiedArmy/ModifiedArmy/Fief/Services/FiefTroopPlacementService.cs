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
using TaleWorlds.Localization;

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
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(scope.SourceFief);
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
                .Where(village => village?.VillageSettlement != null)
                .Select(village => new VillagePlan { Village = village })
                .ToList();
            if (plans.Count == 0)
            {
                LogVillageEligibility(scope, template, doctrine, party,
                    purpose, operationId, reconciliation,
                    "NoEligiblePlacementVillage");
                return Reject(result, party, interaction, purpose,
                    operationId, "NoEligiblePlacementVillage");
            }

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
                    out string reason))
                {
                    result.ReasonCode = reason;
                    if (RecruitmentLogFilter.ShouldLog(party))
                        ModLogger.FileWarn(string.Format(
                            "Fief placement village commit stopped: " +
                            "village={0}, villageId={1}, party={2}, " +
                            "partyId={3}, operation={4}, requested={5}, " +
                            "reason={6}.",
                            plan.Village.VillageSettlement?.Name,
                            plan.Village.VillageSettlement?.StringId,
                            PartyLogFormatter.GetDisplayName(party),
                            party.StringId, operationId,
                            plan.Troops.Values.Sum(value => value.Count),
                            reason));
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
                scope.FiefData.RefreshManpowerCounters();
                if (result.ReasonCode == "NotStarted")
                {
                    result.ReasonCode = "Completed";
                    LogCommitted(party, interaction, purpose, operationId,
                        result.PlacedCount, committed.Count, committedVillages);
                }
            }
            else if (string.IsNullOrEmpty(result.ReasonCode) ||
                result.ReasonCode == "NotStarted")
            {
                result.ReasonCode = "NoEligibleTroopsOrCapacity";
                LogNoPlacementSummary(scope, template, doctrine, plans,
                    party, purpose, operationId, reconciliation);
            }
            return result;
        }

        private static void LogVillageEligibility(FiefOperationScope scope,
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine,
            MobileParty party, FiefPlacementPurpose purpose,
            string operationId, FiefReconciliationResult reconciliation,
            string result)
        {
            if (!PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(
                    scope?.SourceFief))
                return;
            int unassigned = reconciliation?.UnassignedTroops?.Values
                .Sum(value => value.Total) ?? 0;
            ModLogger.FileDebug(string.Format(
                "[PlayerFiefPlacement] operation={0} fief={1} fiefId={2} " +
                "party={3} partyId={4} purpose={5} unassigned={6} " +
                "placed=0 result={7}.", operationId,
                scope.SourceFief?.Name, scope.SourceFief?.StringId,
                PartyLogFormatter.GetDisplayName(party), party?.StringId,
                purpose, unassigned, result));

            foreach (VillageFiefData village in
                (scope.FiefData.Villages?.Values ??
                    Enumerable.Empty<VillageFiefData>())
                    .Where(item => item?.VillageSettlement != null)
                    .OrderBy(item => item.VillageSettlement.StringId))
            {
                Settlement settlement = village.VillageSettlement;
                bool currentBound = scope.FiefData.IsCurrentVillage(settlement);
                bool filtered = scope.VillageFilter != null &&
                    scope.VillageFilter != settlement;
                string rejection = !currentBound ? "VillageNoLongerBound" :
                    filtered ? "FilteredToCurrentVillage" : "Eligible";
                ModLogger.FileDebug(string.Format(
                    "[PlayerFiefPlacementVillage] operation={0} fief={1} " +
                    "fiefId={2} village={3} villageId={4} " +
                    "currentBound={5} filtered={6} recoveryRequired={7} " +
                    "recoveryReason={8} {9} rejection={10}.", operationId,
                    scope.SourceFief?.Name, scope.SourceFief?.StringId,
                    settlement.Name, settlement.StringId, currentBound,
                    filtered, village.RecoveryRequired,
                    village.RecoveryReason ?? "none",
                    GetSupplyDiagnostic(village, template, doctrine),
                    rejection));
            }
        }

        private static string GetSupplyDiagnostic(VillageFiefData village,
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine)
        {
            int effective = village.GetEffectiveSupplyHearth();
            int occupied = village.GetOccupiedSupplyHearth(template,
                doctrine);
            int free = village.GetFreeSupplyHearth(template, doctrine);
            int budget = village.GetFeudalBudgetRemainingHearth(template,
                doctrine);
            return string.Format(
                "effectiveHearth={0} occupiedHearth={1} supplyFree={2} " +
                "feudalBudgetRemaining={3} placementCapacity={4}",
                effective, occupied, free, budget,
                Math.Max(0, Math.Min(free, budget)));
        }

        private static void LogNoPlacementSummary(FiefOperationScope scope,
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine,
            IReadOnlyCollection<VillagePlan> plans, MobileParty party,
            FiefPlacementPurpose purpose, string operationId,
            FiefReconciliationResult reconciliation)
        {
            if (!PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(
                    scope?.SourceFief))
                return;
            int unassigned = 0;
            int templateEligible = 0;
            int costEligible = 0;
            int capacityEligible = 0;
            foreach (var pair in reconciliation.UnassignedTroops.Where(pair =>
                pair.Key != null && pair.Value.Total > 0))
            {
                unassigned += pair.Value.Total;
                bool templateAllowed = template.IsEnableTroop(pair.Key);
                if (!templateAllowed)
                {
                    LogTroopEligibility(operationId, pair.Key,
                        pair.Value.Total, false, false, 0,
                        "TroopNotInTemplate");
                    continue;
                }
                templateEligible += pair.Value.Total;
                if (!doctrine.TryGetFeudalSupplyCost(pair.Key, out int cost) ||
                    cost <= 0)
                {
                    LogTroopEligibility(operationId, pair.Key,
                        pair.Value.Total, true, false, 0,
                        "SupplyCostUnavailable");
                    continue;
                }
                costEligible += pair.Value.Total;
                int capacity = plans.Sum(plan => Math.Max(0,
                    EffectiveRemaining(plan, template, doctrine) / cost));
                int eligible = Math.Min(pair.Value.Total, capacity);
                capacityEligible += eligible;
                LogTroopEligibility(operationId, pair.Key,
                    pair.Value.Total, true, true, eligible,
                    eligible > 0 ? "CapacityAvailable" : "CapacityZero");
            }
            ModLogger.FileDebug(string.Format(
                "[PlayerFiefPlacement] operation={0} fief={1} fiefId={2} " +
                "party={3} partyId={4} purpose={5} unassigned={6} " +
                "templateEligible={7} costEligible={8} capacityEligible={9} " +
                "placed=0 result=NoEligibleTroopsOrCapacity.", operationId,
                scope.SourceFief?.Name, scope.SourceFief?.StringId,
                PartyLogFormatter.GetDisplayName(party), party?.StringId,
                purpose, unassigned, templateEligible, costEligible,
                capacityEligible));
            foreach (VillagePlan plan in plans.OrderBy(item =>
                item.Village.VillageSettlement.StringId))
                ModLogger.FileDebug(string.Format(
                    "[PlayerFiefPlacementVillage] operation={0} village={1} " +
                    "villageId={2} {3} " +
                    "rejection=NoEligibleTroopsOrCapacity.", operationId,
                    plan.Village.VillageSettlement.Name,
                    plan.Village.VillageSettlement.StringId,
                    GetSupplyDiagnostic(plan.Village, template, doctrine)));
        }

        private static void LogTroopEligibility(string operationId,
            CharacterObject troop, int unassigned, bool templateEligible,
            bool costEligible, int capacityEligible, string reason)
        {
            ModLogger.FileDebug(string.Format(
                "[PlayerFiefPlacementTroop] operation={0} troop={1} " +
                "troopId={2} unassigned={3} templateEligible={4} " +
                "costEligible={5} capacityEligible={6} reason={7}.",
                operationId, troop?.Name, troop?.StringId, unassigned,
                templateEligible, costEligible, capacityEligible, reason));
        }

        private static bool CommitVillage(FiefPartyManager manager,
            MobileParty party, Settlement interaction, FiefOperationScope scope,
            FiefPartyTemplate template, CulturalMilitaryDoctrine doctrine,
            VillagePlan plan, string operationId, out string reason)
        {
            reason = null;
            if (!manager.TryResolveOperationScope(interaction, party, true,
                    out FiefOperationScope liveScope) ||
                liveScope.SourceFief != scope.SourceFief ||
                !liveScope.FiefData.IsCurrentVillage(
                    plan.Village.VillageSettlement))
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
                if (pair.Key == null || pair.Value.Count <= 0 ||
                    pair.Value.Wounded < 0 ||
                    pair.Value.Wounded > pair.Value.Count ||
                    !template.IsEnableTroop(pair.Key) ||
                    !doctrine.TryGetFeudalSupplyCost(pair.Key, out int cost) ||
                    cost <= 0 ||
                    !live.UnassignedTroops.TryGetValue(pair.Key,
                        out FiefTroopCount available) ||
                    available.Total < pair.Value.Count ||
                    available.Wounded < pair.Value.Wounded ||
                    available.Healthy < pair.Value.Count -
                        pair.Value.Wounded ||
                    party.MemberRoster.GetTroopCount(pair.Key) <
                        pair.Value.Count ||
                    Snapshot(party.MemberRoster, pair.Key).Wounded <
                        pair.Value.Wounded)
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
            int requested = plan.Troops.Values.Sum(value => value.Count);
            int partyBefore = plan.Troops.Keys.Sum(troop =>
                party.MemberRoster.GetTroopCount(troop));
            int coolingBefore = cooling.GetTotalCount();
            if (created) plan.Village.ReturnBatches.Add(cooling);
            foreach (var pair in plan.Troops.OrderBy(item =>
                item.Key.StringId, StringComparer.Ordinal))
            {
                int accepted = cooling.AddTroops(
                    new Dictionary<CharacterObject, int>
                {
                    [pair.Key] = pair.Value.Count
                });
                if (accepted <= 0) continue;

                party.MemberRoster.AddToCounts(pair.Key,
                    -accepted, false,
                    -Math.Min(pair.Value.Wounded, accepted),
                    0, true, -1);
            }

            int partyAfter = plan.Troops.Keys.Sum(troop =>
                party.MemberRoster.GetTroopCount(troop));
            int coolingAfter = cooling.GetTotalCount();
            int written = Math.Max(0, coolingAfter - coolingBefore);
            bool countsMatch = written == requested &&
                partyBefore - partyAfter == written &&
                coolingAfter - coolingBefore == written;
            if (PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(
                scope.SourceFief, plan.Village.VillageSettlement))
                ModLogger.FileDebug(string.Format(
                    "[PlayerFiefPlacementCommit] operation={0} fief={1} " +
                    "fiefId={2} village={3} villageId={4} party={5} " +
                    "partyId={6} requested={7} written={8} partyBefore={9} " +
                    "partyAfter={10} coolingBefore={11} coolingAfter={12} " +
                    "result={13}.", operationId, scope.SourceFief?.Name,
                    scope.SourceFief?.StringId,
                    plan.Village.VillageSettlement?.Name,
                    plan.Village.VillageSettlement?.StringId,
                    PartyLogFormatter.GetDisplayName(party), party?.StringId,
                    requested, written, partyBefore, partyAfter, coolingBefore,
                    coolingAfter, countsMatch ? "Committed" :
                        "PostconditionMismatch"));
            if (!countsMatch)
            {
                if (PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(
                    scope.SourceFief, plan.Village.VillageSettlement))
                    ModLogger.FileError(string.Format(
                        "Fief placement postcondition mismatch: village={0}, " +
                        "villageId={1}, party={2}, partyId={3}, operation={4}, " +
                        "requested={5}, written={6}, partyDelta={7}, " +
                        "coolingDelta={8}.",
                        plan.Village.VillageSettlement?.Name,
                        plan.Village.VillageSettlement?.StringId,
                        PartyLogFormatter.GetDisplayName(party),
                        party?.StringId, operationId, requested, written,
                        partyBefore - partyAfter,
                        coolingAfter - coolingBefore));
                reason = "PlacementPostconditionMismatch";
                return false;
            }
            return true;
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
            Settlement sourceFief = interaction?.IsVillage == true
                ? interaction.Village?.Bound
                : interaction;
            bool notifyPlayer = purpose == FiefPlacementPurpose.PlayerReturn &&
                PlayerFiefDiagnostics.ShouldTracePlayerOwnedFief(sourceFief);
            if (notifyPlayer)
            {
                TextObject message = new TextObject(
                    "{=ModifiedArmy_FiefPlacementCompleted}" +
                    "{SETTLEMENT_NAME} received {COUNT} returning feudal troops.");
                message.SetTextVariable("SETTLEMENT_NAME", sourceFief.Name);
                message.SetTextVariable("COUNT", count);
                ModLogger.Notice(message.ToString());
                return;
            }
            if (RecruitmentLogFilter.ShouldLog(party))
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
