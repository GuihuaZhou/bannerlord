using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedArmy.common;
using TaleWorlds.CampaignSystem;
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
        [SaveableProperty(6)] public string RecoveryReason { get; private set; }
        [SaveableProperty(7)] public string RecoveryOperationId { get; private set; }

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

        public int GetEffectiveSupplyHearth() => Math.Max(0,
            (int)Math.Floor((VillageSettlement?.Village?.Hearth ?? 0f) +
                MobilizedHearth));

        public int GetOccupiedSupplyHearth(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine)
        {
            if (template == null || doctrine == null)
                throw new InvalidOperationException(
                    "Fief supply calculation requires template and doctrine.");
            int occupied = 0;
            checked
            {
                foreach (TroopRosterElement element in FilteredEntries(template))
                    occupied += Math.Max(0, element.Number) *
                        doctrine.GetFeudalSupplyCost(element.Character);
                foreach (FiefServiceBatch batch in ServiceBatches ??
                    Enumerable.Empty<FiefServiceBatch>())
                foreach (var pair in batch?.Troops ??
                    new Dictionary<CharacterObject, int>())
                    if (pair.Key != null && pair.Value > 0)
                        occupied += pair.Value *
                            doctrine.GetFeudalSupplyCost(pair.Key);
                foreach (FiefReturnBatch batch in ReturnBatches ??
                    Enumerable.Empty<FiefReturnBatch>())
                foreach (var pair in batch?.Troops ??
                    new Dictionary<CharacterObject, int>())
                    if (pair.Key != null && pair.Value > 0)
                        occupied += pair.Value *
                            doctrine.GetFeudalSupplyCost(pair.Key);
            }
            return occupied;
        }

        public int GetFreeSupplyHearth(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine) => checked(
                GetEffectiveSupplyHearth() -
                GetOccupiedSupplyHearth(template, doctrine));

        public int GetSupplyDeficitHearth(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine) => Math.Max(0,
                -GetFreeSupplyHearth(template, doctrine));

        public int GetFeudalBudgetHearth(
            CulturalMilitaryDoctrine doctrine)
        {
            if (doctrine == null)
                throw new InvalidOperationException(
                    "Fief budget calculation requires a doctrine.");
            int allocatable = Math.Max(0, GetEffectiveSupplyHearth() - 100);
            return checked((int)Math.Floor(
                (double)allocatable * doctrine.FiefWeight /
                (doctrine.FiefWeight + doctrine.ProfessionalWeight)));
        }

        public int GetFeudalBudgetRemainingHearth(
            FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine) => checked(
                GetFeudalBudgetHearth(doctrine) -
                GetOccupiedSupplyHearth(template, doctrine));

        public bool CanIncreaseSupply(FiefPartyTemplate template,
            CulturalMilitaryDoctrine doctrine, CharacterObject troop,
            int count)
        {
            if (RecoveryRequired || troop == null || count <= 0) return false;
            int required = checked(
                doctrine.GetFeudalSupplyCost(troop) * count);
            return GetFreeSupplyHearth(template, doctrine) >= required &&
                GetFeudalBudgetRemainingHearth(template, doctrine) >= required;
        }

        internal Dictionary<CharacterObject, int>
            GetRegisteredFeudalTroopCounts(FiefPartyTemplate template)
        {
            var counts = new Dictionary<CharacterObject, int>();
            void Add(CharacterObject troop, int count)
            {
                if (troop == null || count <= 0) return;
                counts[troop] = counts.TryGetValue(troop, out int existing)
                    ? checked(existing + count)
                    : count;
            }

            foreach (TroopRosterElement entry in FilteredEntries(template))
                Add(entry.Character, entry.Number);
            foreach (FiefServiceBatch batch in ServiceBatches ??
                Enumerable.Empty<FiefServiceBatch>())
                if (batch != null)
                    foreach (var pair in batch.Troops)
                        Add(pair.Key, pair.Value);
            foreach (FiefReturnBatch batch in ReturnBatches ??
                Enumerable.Empty<FiefReturnBatch>())
                if (batch != null)
                    foreach (var pair in batch.Troops)
                        Add(pair.Key, pair.Value);
            return counts;
        }

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

        internal void RequireRecovery(string reason = "InvariantMismatch",
            string operationId = null)
        {
            RecoveryRequired = true;
            RecoveryReason = string.IsNullOrEmpty(reason)
                ? "InvariantMismatch" : reason;
            RecoveryOperationId = operationId;
        }

        internal void CompleteRecovery()
        {
            RecoveryRequired = false;
            RecoveryReason = null;
            RecoveryOperationId = null;
        }

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
