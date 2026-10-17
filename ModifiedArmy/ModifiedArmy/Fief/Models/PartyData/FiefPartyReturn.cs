using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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

            Dictionary<CharacterObject, int> returned = new();
            foreach (CharacterObject troop in batches.SelectMany(b => b.Troops.Keys)
                .Distinct().OrderBy(t => t.StringId))
            {
                int available = sourceParty.MemberRoster.GetElementNumber(troop);
                int registered = batches.Sum(b =>
                    b.Troops.TryGetValue(troop, out int n) ? n : 0);
                int target = Math.Min(available, registered);
                if (target <= 0) continue;

                int remaining = target;
                int remainingWeight = registered;
                foreach (FiefServiceBatch batch in batches)
                {
                    if (!batch.Troops.TryGetValue(troop, out int weight) || weight <= 0) continue;
                    int share = remainingWeight <= 0 ? 0 :
                        Math.Min(weight, (int)Math.Floor((double)remaining * weight / remainingWeight));
                    if (share > 0)
                    {
                        batch.Troops[troop] -= share;
                        remaining -= share;
                    }
                    remainingWeight -= weight;
                }
                foreach (FiefServiceBatch batch in batches)
                {
                    if (remaining <= 0) break;
                    if (!batch.Troops.TryGetValue(troop, out int count) || count <= 0) continue;
                    batch.Troops[troop]--;
                    remaining--;
                }
                foreach (FiefServiceBatch batch in batches)
                    if (batch.Troops.TryGetValue(troop, out int count) && count <= 0)
                        batch.Troops.Remove(troop);

                sourceParty.MemberRoster.RemoveTroop(troop, target);
                returned[troop] = target;
            }

            int returnedCount = returned.Values.Sum();
            if (returnedCount <= 0) return 0;
            foreach (FiefServiceBatch empty in batches.Where(b => b.IsEmpty()).ToList())
            {
                ServiceBatches.Remove(empty);
                manager.UnregisterServingDetachment(_settlement, empty);
            }

            int restoredHearth = manager.ReleaseFiefService(
                _settlement,
                returnedCount,
                true);
            int cooldown = MilitaryConfigManager.Instance.RequireActiveConfig()
                .GetDoctrine(_settlement).Service.ReturnCooldownWeeks;
            FiefReturnBatch cooling = ReturnBatches
                .FirstOrDefault(d => d != null && d.WaitWeeks == cooldown);
            if (cooling == null)
            {
                cooling = new FiefReturnBatch(cooldown);
                ReturnBatches.Add(cooling);
            }
            cooling.AddTroops(returned);
            SyncManpowerCounters();

            TextObject message = GameTexts.FindText("str_modifiedarmy_fief_return_result");
            message.SetTextVariable("PARTY_NAME", PartyLogFormatter.GetDisplayName(sourceParty));
            message.SetTextVariable("SETTLEMENT_NAME", _settlement.Name);
            message.SetTextVariable("RETURNED_COUNT", returnedCount);
            message.SetTextVariable("PROSPERITY_COST", 0);
            message.SetTextVariable("HEARTH_COST", restoredHearth);
            message.SetTextVariable("RETINUE_COUNT", _soldierTypeCounts[SoldierType.Retinue]);
            message.SetTextVariable("MAX_RETINUE", _soldierTypeMaxCounts[SoldierType.Retinue]);
            message.SetTextVariable("SERGEANT_COUNT", _soldierTypeCounts[SoldierType.Sergeant]);
            message.SetTextVariable("MAX_SERGEANT", _soldierTypeMaxCounts[SoldierType.Sergeant]);
            message.SetTextVariable("MARINE_COUNT", _soldierTypeCounts[SoldierType.Marine]);
            message.SetTextVariable("MAX_MARINE", _soldierTypeMaxCounts[SoldierType.Marine]);
            message.SetTextVariable("SLAVE_COUNT", _soldierTypeCounts[SoldierType.Slave]);
            message.SetTextVariable("MAX_SLAVE", _soldierTypeMaxCounts[SoldierType.Slave]);
            message.SetTextVariable("MILITIA_COUNT", _soldierTypeCounts[SoldierType.Militia]);
            message.SetTextVariable("MAX_MILITIA", _soldierTypeMaxCounts[SoldierType.Militia]);
            message.SetTextVariable("TROOP_COUNT", _totalTroopCount);
            message.SetTextVariable("TOTAL_LIMIT", _totalLimit);
            if (sourceParty.ActualClan == Clan.PlayerClan) ModLogger.Notice(message.ToString());
            else ModLogger.Debug(message.ToString());
            return returnedCount;
        }
    }
}
