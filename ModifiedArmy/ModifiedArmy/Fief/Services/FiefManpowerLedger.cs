using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// Operates the save-owned mobilization dictionaries held by
    /// <see cref="FiefPartyManager"/>. This service never owns a parallel ledger.
    /// </summary>
    internal sealed class FiefManpowerLedger
    {
        private readonly Dictionary<Settlement, int> _mobilizedHearth;
        private readonly Dictionary<Settlement, Settlement> _owners;
        private readonly Dictionary<Settlement, int> _legacy;

        public FiefManpowerLedger(
            Dictionary<Settlement, int> mobilizedHearth,
            Dictionary<Settlement, Settlement> owners,
            Dictionary<Settlement, int> legacy)
        {
            _mobilizedHearth = mobilizedHearth ?? throw new ArgumentNullException(nameof(mobilizedHearth));
            _owners = owners ?? throw new ArgumentNullException(nameof(owners));
            _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
        }

        public int GetMobilizedHearth(Village village) => village?.Settlement != null &&
            _mobilizedHearth.TryGetValue(village.Settlement, out int value)
                ? Math.Max(0, value)
                : 0;

        public bool TryMobilize(Settlement fief, int count, int hearthPerTroop)
        {
            if (fief == null || count <= 0 || hearthPerTroop <= 0) return false;
            List<Village> villages = fief.BoundVillages
                .Where(v => v?.Settlement != null)
                .OrderBy(v => v.Settlement.StringId).ToList();
            int required = checked(count * hearthPerTroop);
            if (villages.Sum(v => Math.Max(0, (int)Math.Floor(v.Hearth) - 100)) < required)
                return false;
            int remaining = required;
            while (remaining > 0)
            {
                bool changed = false;
                foreach (Village village in villages)
                {
                    if (remaining <= 0) break;
                    if (village.Hearth <= 100f) continue;
                    village.Hearth -= 1f;
                    Settlement key = village.Settlement;
                    _mobilizedHearth[key] = GetMobilizedHearth(village) + 1;
                    _owners[key] = fief;
                    remaining--;
                    changed = true;
                }
                if (!changed) return false;
            }
            return true;
        }

        public int GetTrackedCount(Settlement fief) => fief == null ? 0 : _owners
            .Where(pair => pair.Value == fief)
            .Sum(pair => _mobilizedHearth.TryGetValue(pair.Key, out int count)
                ? Math.Max(0, count) : 0);

        public int Release(Settlement fief, int count, bool restoreHearth)
        {
            if (fief == null || count <= 0) return 0;
            List<Settlement> villages = OwnedVillages(fief);
            int released = 0;
            while (released < count)
            {
                bool changed = false;
                foreach (Settlement village in villages)
                {
                    if (released >= count) break;
                    if (!_mobilizedHearth.TryGetValue(village, out int value) || value <= 0) continue;
                    _mobilizedHearth[village] = value - 1;
                    if (restoreHearth && village.Village != null) village.Village.Hearth += 1f;
                    released++;
                    changed = true;
                }
                if (!changed) break;
            }
            RemoveEmpty(villages);
            return released;
        }

        public int ReleaseService(Settlement fief, int count, bool restoreHearth)
        {
            if (fief == null || count <= 0) return 0;
            int legacy = _legacy.TryGetValue(fief, out int value) ? Math.Max(0, value) : 0;
            int legacyReleased = Math.Min(count, legacy);
            if (legacyReleased > 0)
            {
                int remaining = legacy - legacyReleased;
                if (remaining > 0) _legacy[fief] = remaining;
                else _legacy.Remove(fief);
            }
            return Release(fief, count - legacyReleased, restoreHearth);
        }

        public bool TryReleaseExact(Settlement fief, int count,
            out Dictionary<Settlement, int> releasedByVillage)
        {
            releasedByVillage = new Dictionary<Settlement, int>();
            if (fief == null || count <= 0 || GetTrackedCount(fief) < count) return false;
            List<Settlement> villages = OwnedVillages(fief);
            int released = 0;
            while (released < count)
            {
                foreach (Settlement village in villages)
                {
                    if (released >= count) break;
                    if (!_mobilizedHearth.TryGetValue(village, out int current) || current <= 0) continue;
                    _mobilizedHearth[village] = current - 1;
                    if (village.Village != null) village.Village.Hearth += 1f;
                    releasedByVillage[village] = releasedByVillage.TryGetValue(village, out int old)
                        ? old + 1 : 1;
                    released++;
                }
            }
            RemoveEmpty(villages);
            return true;
        }

        public void RollbackRelease(Settlement fief,
            IReadOnlyDictionary<Settlement, int> releasedByVillage)
        {
            if (fief == null || releasedByVillage == null) return;
            foreach (var pair in releasedByVillage)
            {
                if (pair.Key == null || pair.Value <= 0) continue;
                _mobilizedHearth[pair.Key] = _mobilizedHearth.TryGetValue(pair.Key, out int old)
                    ? old + pair.Value : pair.Value;
                _owners[pair.Key] = fief;
                if (pair.Key.Village != null) pair.Key.Village.Hearth -= pair.Value;
            }
        }

        private List<Settlement> OwnedVillages(Settlement fief) => _owners
            .Where(p => p.Value == fief && _mobilizedHearth.TryGetValue(p.Key, out int n) && n > 0)
            .Select(p => p.Key).OrderBy(s => s.StringId).ToList();

        private void RemoveEmpty(IEnumerable<Settlement> villages)
        {
            foreach (Settlement village in villages.Where(v =>
                _mobilizedHearth.TryGetValue(v, out int n) && n <= 0).ToList())
            {
                _mobilizedHearth.Remove(village);
                _owners.Remove(village);
            }
        }
    }
}
