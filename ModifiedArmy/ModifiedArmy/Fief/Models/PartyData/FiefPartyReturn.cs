using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models.Fief
{
    public partial class FiefPartyData
    {
        public int ReturnTroopsToSettlement(MobileParty sourceParty,
            Settlement interactionSettlement = null,
            Settlement villageFilter = null)
        {
            if (sourceParty == null ||
                sourceParty.ActualClan == null ||
                sourceParty.ActualClan != _settlement?.OwnerClan)
                return 0;

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null) return 0;
            manager.AdvanceServiceDays(sourceParty);
            manager.ReconcilePartyFiefTroops(sourceParty);

            List<FiefServiceBatch> batches = OrderedVillages()
                .SelectMany(v => v.ServiceBatches)
                .Where(d => d?.ServingParty == sourceParty && !d.IsEmpty())
                .Where(d => villageFilter == null ||
                    d.SourceVillage == villageFilter)
                .OrderBy(d => d.BatchId, StringComparer.Ordinal)
                .ToList();
            if (batches.Count == 0) return 0;
            int returnedCount = 0;
            int restoredHearth = 0;
            foreach (FiefServiceBatch batch in batches)
            {
                FiefReturnResult result = manager.TryReturnFiefTroops(
                    sourceParty,
                    interactionSettlement ?? _settlement,
                    _settlement,
                    batch.BatchId,
                    batch.GetTotalCount());
                returnedCount += result.ActualReturnedCount;
                restoredHearth += result.ActualRestoredHearth;
            }
            if (returnedCount <= 0) return 0;

            TextObject message = GameTexts.FindText("str_modifiedarmy_fief_return_result");
            message.SetTextVariable("PARTY_NAME", PartyLogFormatter.GetDisplayName(sourceParty));
            message.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
            message.SetTextVariable("RETURNED_COUNT", returnedCount);
            message.SetTextVariable("PROSPERITY_COST", 0);
            message.SetTextVariable("HEARTH_COST", restoredHearth);
            SyncManpowerCounters();
            message.SetTextVariable("TROOP_COUNT", _totalTroopCount);
            message.SetTextVariable("OCCUPIED_HEARTH",
                GetTotalOccupiedSupplyHearth());
            message.SetTextVariable("FEUDAL_BUDGET_HEARTH",
                GetTotalFeudalBudgetHearth());
            if (sourceParty.ActualClan == Clan.PlayerClan) ModLogger.Notice(message.ToString());
            else ModLogger.Debug(message.ToString());
            return returnedCount;
        }

        /// <summary>
        /// Commits one partial return as a transaction across the live party
        /// roster, the original service batch, tracked Hearth mobilization and
        /// the source fief's cooling batch. Any later failure rolls back every
        /// mutation made by this method; reconciliation performed beforehand
        /// represents confirmed game state and is not rolled back.
        /// </summary>
        internal FiefReturnResult TryCommitFiefReturn(
            FiefPartyManager manager,
            VillageFiefData village,
            MobileParty sourceParty,
            FiefServiceBatch batch,
            int maximumCount,
            int trackedMobilization,
            IReadOnlyDictionary<CharacterObject, int> requestedTroops = null)
        {
            FiefReturnResult result = new FiefReturnResult
            {
                SourceFief = _settlement,
                BatchId = batch?.BatchId,
                RequestedCount = Math.Max(0, maximumCount),
                ReasonCode = "NoEligibleBatch"
            };
            if (manager == null || sourceParty == null || batch == null ||
                maximumCount <= 0 || trackedMobilization <= 0 ||
                batch.ServingParty != sourceParty || batch.IsEmpty() ||
                village == null || village.RecoveryRequired ||
                !village.ServiceBatches.Contains(batch))
            {
                return result;
            }

            int approved = Math.Min(
                maximumCount,
                Math.Min(batch.GetTotalCount(), trackedMobilization));
            Dictionary<CharacterObject, int> returned = new();
            int remaining = approved;
            IEnumerable<KeyValuePair<CharacterObject, int>> eligible =
                batch.Troops.Where(item => item.Key != null &&
                    item.Value > 0 && (requestedTroops == null ||
                    requestedTroops.TryGetValue(item.Key, out int wanted) &&
                    wanted > 0));
            foreach (var pair in eligible
                .OrderBy(item => item.Key.StringId))
            {
                int available = sourceParty.MemberRoster
                    .GetElementNumber(pair.Key);
                int requested = requestedTroops != null &&
                    requestedTroops.TryGetValue(pair.Key, out int wanted)
                        ? wanted : remaining;
                int count = Math.Min(
                    remaining,
                    Math.Min(requested,
                        Math.Min(pair.Value, Math.Max(0, available))));
                if (count <= 0) continue;
                returned[pair.Key] = count;
                remaining -= count;
                if (remaining <= 0) break;
            }
            int returningCount = returned.Values.Sum();
            if (returningCount <= 0)
            {
                return result;
            }

            bool stateStillValid = sourceParty.IsActive &&
                sourceParty.MapEvent == null &&
                sourceParty.ActualClan != null &&
                sourceParty.ActualClan == _settlement?.OwnerClan &&
                batch.ServingParty == sourceParty &&
                village.ServiceBatches.Contains(batch) &&
                manager.GetTrackedMobilizationCount(village.VillageSettlement) >=
                    returningCount &&
                returned.All(pair =>
                    batch.Troops.TryGetValue(
                        pair.Key,
                        out int registered) &&
                    registered >= pair.Value &&
                    sourceParty.MemberRoster.GetElementNumber(pair.Key) >=
                        pair.Value);
            if (!stateStillValid)
            {
                result.ReasonCode = "StateChangedBeforeCommit";
                return result;
            }

            Dictionary<CharacterObject, int> batchBefore = batch.Troops
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Dictionary<CharacterObject, (int Count, int Wounded)> rosterBefore =
                returned.Keys.ToDictionary(troop => troop,
                    troop => GetRosterCounts(sourceParty.MemberRoster, troop));
            float hearthBefore = village.VillageSettlement.Village.Hearth;
            int mobilizedBefore = village.MobilizedHearth;
            string operationId = Guid.NewGuid().ToString("N");
            Dictionary<CharacterObject, int> woundedRemoved = new();
            Dictionary<CharacterObject, int> rosterRemoved = new();
            Dictionary<Settlement, int> releasedByVillage = null;
            FiefReturnBatch cooling = null;
            Dictionary<CharacterObject, int> coolingBefore = null;
            bool createdCooling = false;
            bool batchRemoved = false;
            try
            {
                foreach (var pair in returned)
                {
                    TroopRosterElement element = sourceParty.MemberRoster
                        .GetTroopRoster()
                        .First(item => item.Character == pair.Key);
                    int healthy = Math.Max(
                        0,
                        element.Number - element.WoundedNumber);
                    int wounded = Math.Max(0, pair.Value - healthy);
                    woundedRemoved[pair.Key] = wounded;
                    sourceParty.MemberRoster.AddToCounts(
                        pair.Key,
                        -pair.Value,
                        false,
                        -wounded,
                        0,
                        true,
                        -1);
                    rosterRemoved[pair.Key] = pair.Value;
                }

                Dictionary<CharacterObject, int> batchAfter = batchBefore
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                foreach (var pair in returned)
                {
                    int value = batchAfter[pair.Key] - pair.Value;
                    if (value > 0) batchAfter[pair.Key] = value;
                    else batchAfter.Remove(pair.Key);
                }
                batch.ReplaceTroops(batchAfter);
                if (batch.IsEmpty())
                {
                    village.ServiceBatches.Remove(batch);
                    manager.UnregisterServingDetachment(_settlement,
                        village.VillageSettlement, batch);
                    batchRemoved = true;
                }

                if (!manager.TryReleaseMobilizationExact(
                    village.VillageSettlement,
                    returningCount,
                    out releasedByVillage))
                {
                    throw new InvalidOperationException(
                        "MobilizationChangedBeforeCommit");
                }

                int cooldown = MilitaryConfigManager.Instance
                    .RequireActiveConfig().GetDoctrine(_settlement)
                    .Service.ReturnCooldownWeeks;
                cooling = village.ReturnBatches.FirstOrDefault(item =>
                    item != null && item.WaitWeeks == cooldown);
                if (cooling == null)
                {
                    cooling = new FiefReturnBatch(cooldown);
                    village.ReturnBatches.Add(cooling);
                    createdCooling = true;
                }
                else
                {
                    coolingBefore = cooling.Troops.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value);
                }
                cooling.AddTroops(returned);
                SyncManpowerCounters();

                result.Succeeded = true;
                result.ReasonCode = returningCount < maximumCount
                    ? "PartiallyCompleted"
                    : "Completed";
                result.ActualReturnedCount = returningCount;
                result.ActualRestoredHearth = returningCount;
                result.ReturnedTroops = new Dictionary<CharacterObject, int>(
                    returned);
                return result;
            }
            catch (Exception exception)
            {
                bool restored = true;
                try
                {
                    if (cooling != null)
                    {
                        if (createdCooling)
                            village.ReturnBatches.Remove(cooling);
                        else
                            cooling.ReplaceTroops(coolingBefore);
                    }
                }
                catch { restored = false; }
                try
                {
                    if (releasedByVillage != null)
                        manager.RollbackMobilizationRelease(
                            village.VillageSettlement,
                            releasedByVillage);
                }
                catch { restored = false; }
                try { batch.ReplaceTroops(batchBefore); }
                catch { restored = false; }
                try
                {
                    if (batchRemoved &&
                        !village.ServiceBatches.Contains(batch))
                    {
                        village.ServiceBatches.Add(batch);
                        manager.RegisterServingDetachment(_settlement,
                            village.VillageSettlement, batch);
                    }
                }
                catch { restored = false; }
                foreach (var pair in rosterBefore)
                {
                    try { RestoreRosterEntry(sourceParty.MemberRoster,
                        pair.Key, pair.Value); }
                    catch { restored = false; }
                }
                restored = restored && batchBefore.Count == batch.Troops.Count &&
                    batchBefore.All(pair => batch.Troops.TryGetValue(
                        pair.Key, out int count) && count == pair.Value) &&
                    rosterBefore.All(pair => GetRosterCounts(
                        sourceParty.MemberRoster, pair.Key) == pair.Value) &&
                    village.MobilizedHearth == mobilizedBefore &&
                    Math.Abs(village.VillageSettlement.Village.Hearth -
                        hearthBefore) <= 0.01f &&
                    village.ServiceBatches.Contains(batch) &&
                    (cooling == null || createdCooling
                        ? cooling == null ||
                            !village.ReturnBatches.Contains(cooling)
                        : coolingBefore.Count == cooling.Troops.Count &&
                            coolingBefore.All(pair => cooling.Troops
                                .TryGetValue(pair.Key, out int count) &&
                                count == pair.Value));
                if (!restored)
                    village.RequireRecovery(
                        "ReturnRollbackUnverified", operationId);
                SyncManpowerCounters();
                result.ReasonCode = restored
                    ? "RolledBackVerified:" + exception.GetType().Name
                    : "RecoveryRequired:" + exception.GetType().Name;
                return result;
            }
        }
    }
}
