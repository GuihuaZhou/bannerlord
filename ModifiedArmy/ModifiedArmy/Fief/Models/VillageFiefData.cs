using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>Save-owned lifecycle state for feudal troops from one village.</summary>
    [SaveableRootClass(12)]
    public sealed class VillageFiefData
    {
        [SaveableProperty(1)] public Settlement VillageSettlement { get; private set; }
        [SaveableProperty(2)] public List<FiefServiceBatch> ServiceBatches { get; private set; }
        [SaveableProperty(3)] public List<FiefReturnBatch> ReturnBatches { get; private set; }
        [SaveableProperty(4)] public int MobilizedHearth { get; private set; }
        [SaveableProperty(5)] public bool RecoveryRequired { get; private set; }

        public VillageFiefData()
        {
            ServiceBatches = new List<FiefServiceBatch>();
            ReturnBatches = new List<FiefReturnBatch>();
        }

        public VillageFiefData(Settlement village) : this() => VillageSettlement = village;

        internal void Repair(Settlement village)
        {
            VillageSettlement = village ?? VillageSettlement;
            ServiceBatches ??= new List<FiefServiceBatch>();
            ReturnBatches ??= new List<FiefReturnBatch>();
            MobilizedHearth = Math.Max(0, MobilizedHearth);
        }

        public TroopRoster GetReadyRoster()
        {
            var component = VillageSettlement?.MilitiaPartyComponent;
            return component?.MobileParty?.IsActive == true
                ? component.MobileParty.MemberRoster
                : null;
        }

        public int GetReadyFiefTroopCount(FiefPartyTemplate template) =>
            FilteredEntries(template).Sum(e => Math.Max(0, e.Number));

        public int GetServingFiefTroopCount() => ServiceBatches?
            .Where(b => b != null).Sum(b => b.GetTotalCount()) ?? 0;

        public int GetCoolingFiefTroopCount() => ReturnBatches?
            .Where(b => b != null).Sum(b => b.GetTotalCount()) ?? 0;

        public int GetTotalFiefTroopCount(FiefPartyTemplate template) =>
            GetReadyFiefTroopCount(template) + GetServingFiefTroopCount() +
            GetCoolingFiefTroopCount();

        public TroopRoster GetReadyFiefTroopRoster(FiefPartyTemplate template)
        {
            TroopRoster result = TroopRoster.CreateDummyTroopRoster();
            foreach (TroopRosterElement element in FilteredEntries(template))
                result.AddToCounts(element.Character, element.Number, false,
                    element.WoundedNumber);
            return result;
        }

        internal bool TryMobilize(int count)
        {
            Village village = VillageSettlement?.Village;
            if (RecoveryRequired || village == null || count <= 0 ||
                Math.Max(0, (int)Math.Floor(village.Hearth) - 100) < count)
                return false;
            village.Hearth -= count;
            MobilizedHearth += count;
            return true;
        }

        internal bool TryReleaseMobilization(int count, bool restoreHearth)
        {
            if (RecoveryRequired || count <= 0 || MobilizedHearth < count)
                return false;
            MobilizedHearth -= count;
            if (restoreHearth && VillageSettlement?.Village != null)
                VillageSettlement.Village.Hearth += count;
            return true;
        }

        internal void RollbackMobilization(int count, bool hearthWasRestored)
        {
            if (count <= 0) return;
            MobilizedHearth += count;
            if (hearthWasRestored && VillageSettlement?.Village != null)
                VillageSettlement.Village.Hearth -= count;
        }

        internal bool RollbackMobilizationReservation(int count)
        {
            if (count <= 0 || MobilizedHearth < count ||
                VillageSettlement?.Village == null) return false;
            MobilizedHearth -= count;
            VillageSettlement.Village.Hearth += count;
            return true;
        }

        internal void RequireRecovery() => RecoveryRequired = true;

        private IEnumerable<TroopRosterElement> FilteredEntries(FiefPartyTemplate template)
        {
            TroopRoster roster = GetReadyRoster();
            return roster == null || template == null
                ? Enumerable.Empty<TroopRosterElement>()
                : roster.GetTroopRoster().Where(e => e.Character != null &&
                    e.Number > 0 && template.IsEnableTroop(e.Character));
        }
    }
}
