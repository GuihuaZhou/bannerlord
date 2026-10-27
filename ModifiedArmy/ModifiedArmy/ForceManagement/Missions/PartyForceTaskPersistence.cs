using ModifiedArmy.ForceManagement.Contracts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Missions
{
    /// <summary>Maps runtime tasks to the unchanged primitive save format.</summary>
    public sealed class PartyForceTaskPersistence
    {
        public void Capture(
            IEnumerable<PartyForceTask> tasks,
            Dictionary<string, int> records)
        {
            records.Clear();
            foreach (PartyForceTask task in tasks ??
                Enumerable.Empty<PartyForceTask>())
            {
                string partyId = task?.Party?.StringId ?? task?.PartyId;
                string targetId = task?.TargetSettlement?.StringId;
                if (string.IsNullOrEmpty(partyId) ||
                    string.IsNullOrEmpty(targetId) ||
                    task.Status == PartyForceTaskStatus.Completed ||
                    task.Status == PartyForceTaskStatus.Cancelled)
                    continue;
                string record = string.Join("\u001f", new[]
                {
                    partyId, targetId, task.SourceBatchId ?? string.Empty,
                    ((int)task.Intent).ToString(CultureInfo.InvariantCulture),
                    ((int)task.SourceKind).ToString(CultureInfo.InvariantCulture),
                    task.Version.ToString(CultureInfo.InvariantCulture),
                    task.CreatedDay.ToString(CultureInfo.InvariantCulture),
                    ((int)task.Status).ToString(CultureInfo.InvariantCulture),
                    task.Progress.ToString(CultureInfo.InvariantCulture)
                });
                records[record] = 1;
            }
        }

        public List<PartyForceTask> Restore(
            IDictionary<string, int> records)
        {
            var tasks = new List<PartyForceTask>();
            foreach (string record in records?.Keys ??
                Enumerable.Empty<string>())
            {
                if (!TryParse(record, out string partyId,
                        out string targetId, out string batchId,
                        out PartyForceIntent intent,
                        out PartyForceSourceKind sourceKind, out int version,
                        out int createdDay, out PartyForceTaskStatus status,
                        out int progress))
                    continue;
                MobileParty party = MobileParty.All.FirstOrDefault(item =>
                    item?.StringId == partyId);
                Settlement target = Settlement.All.FirstOrDefault(item =>
                    item?.StringId == targetId);
                if (party == null || target == null) continue;
                tasks.Add(new PartyForceTask
                {
                    Party = party,
                    PartyId = partyId,
                    ClanId = party.ActualClan?.StringId,
                    OwnerKey = "ModifiedArmy.AiForcePlanning",
                    TargetSettlement = target,
                    SourceBatchId = batchId,
                    Intent = intent,
                    SourceKind = sourceKind,
                    Version = version,
                    CreatedDay = createdDay,
                    LastValidatedDay = createdDay,
                    Status = status,
                    Progress = progress,
                    OwnsMovementOrder = false
                });
            }
            return tasks;
        }

        private static bool TryParse(
            string record, out string partyId, out string targetId,
            out string batchId, out PartyForceIntent intent,
            out PartyForceSourceKind sourceKind, out int version,
            out int createdDay, out PartyForceTaskStatus status,
            out int progress)
        {
            string[] parts = (record ?? string.Empty).Split('\u001f');
            partyId = parts.Length > 0 ? parts[0] : null;
            targetId = parts.Length > 1 ? parts[1] : null;
            batchId = parts.Length > 2 ? parts[2] : null;
            intent = PartyForceIntent.Suspended;
            sourceKind = PartyForceSourceKind.None;
            status = PartyForceTaskStatus.Suspended;
            version = createdDay = progress = 0;
            if (parts.Length != 9 || string.IsNullOrEmpty(partyId) ||
                string.IsNullOrEmpty(targetId) ||
                !int.TryParse(parts[3], out int intentValue) ||
                !int.TryParse(parts[4], out int sourceValue) ||
                !int.TryParse(parts[5], out version) ||
                !int.TryParse(parts[6], out createdDay) ||
                !int.TryParse(parts[7], out int statusValue) ||
                !int.TryParse(parts[8], out progress) ||
                !Enum.IsDefined(typeof(PartyForceIntent), intentValue) ||
                !Enum.IsDefined(typeof(PartyForceSourceKind), sourceValue) ||
                !Enum.IsDefined(typeof(PartyForceTaskStatus), statusValue))
                return false;
            intent = (PartyForceIntent)intentValue;
            sourceKind = (PartyForceSourceKind)sourceValue;
            status = (PartyForceTaskStatus)statusValue;
            return true;
        }
    }
}
