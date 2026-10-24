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
        public int ReturnTroopsToSettlement(MobileParty sourceParty)
        {
            if (sourceParty == null || _fiefParty == null ||
                sourceParty.ActualClan == null ||
                sourceParty.ActualClan != _settlement?.OwnerClan)
                return 0;

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null) return 0;
            manager.AdvanceServiceDays(sourceParty);
            manager.ReconcilePartyFiefTroops(sourceParty);

            List<FiefServiceBatch> batches = ServiceBatches
                .Where(d => d?.ServingParty == sourceParty && !d.IsEmpty())
                .OrderBy(d => d.BatchId, StringComparer.Ordinal)
                .ToList();
            if (batches.Count == 0) return 0;
            int returnedCount = 0;
            int restoredHearth = 0;
            foreach (FiefServiceBatch batch in batches)
            {
                FiefReturnResult result = manager.TryReturnFiefTroops(
                    sourceParty,
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
            message.SetTextVariable("TOTAL_LIMIT", _totalLimit);
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
            MobileParty sourceParty,
            FiefServiceBatch batch,
            int maximumCount,
            int trackedMobilization)
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
                !ServiceBatches.Contains(batch))
            {
                return result;
            }

            int approved = Math.Min(
                maximumCount,
                Math.Min(batch.GetTotalCount(), trackedMobilization));
            Dictionary<CharacterObject, int> returned = new();
            int remaining = approved;
            foreach (var pair in batch.Troops
                .Where(item => item.Key != null && item.Value > 0)
                .OrderBy(item => item.Key.StringId))
            {
                int available = sourceParty.MemberRoster
                    .GetElementNumber(pair.Key);
                int count = Math.Min(
                    remaining,
                    Math.Min(pair.Value, Math.Max(0, available)));
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
                ServiceBatches.Contains(batch) &&
                manager.GetTrackedMobilizationCount(_settlement) >=
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
                    ServiceBatches.Remove(batch);
                    manager.UnregisterServingDetachment(_settlement, batch);
                    batchRemoved = true;
                }

                if (!manager.TryReleaseMobilizationExact(
                    _settlement,
                    returningCount,
                    out releasedByVillage))
                {
                    throw new InvalidOperationException(
                        "MobilizationChangedBeforeCommit");
                }

                int cooldown = MilitaryConfigManager.Instance
                    .RequireActiveConfig().GetDoctrine(_settlement)
                    .Service.ReturnCooldownWeeks;
                cooling = ReturnBatches.FirstOrDefault(item =>
                    item != null && item.WaitWeeks == cooldown);
                if (cooling == null)
                {
                    cooling = new FiefReturnBatch(cooldown);
                    ReturnBatches.Add(cooling);
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
                if (cooling != null)
                {
                    if (createdCooling)
                        ReturnBatches.Remove(cooling);
                    else
                        cooling.ReplaceTroops(coolingBefore);
                }
                if (releasedByVillage != null)
                    manager.RollbackMobilizationRelease(
                        _settlement,
                        releasedByVillage);
                batch.ReplaceTroops(batchBefore);
                if (batchRemoved && !ServiceBatches.Contains(batch))
                {
                    ServiceBatches.Add(batch);
                    manager.RegisterServingDetachment(_settlement, batch);
                }
                foreach (var pair in rosterRemoved)
                {
                    sourceParty.MemberRoster.AddToCounts(
                        pair.Key,
                        pair.Value,
                        false,
                        woundedRemoved.TryGetValue(
                            pair.Key,
                            out int wounded)
                                ? wounded
                                : 0,
                        0,
                        true,
                        -1);
                }
                SyncManpowerCounters();
                result.ReasonCode = "RolledBack:" +
                    exception.GetType().Name;
                return result;
            }
        }
    }
}
