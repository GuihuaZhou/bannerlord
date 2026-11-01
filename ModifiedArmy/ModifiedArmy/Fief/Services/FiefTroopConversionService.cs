using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Models.Fief
{
    public enum FiefConversionDirection
    {
        ProfessionalToFeudal = 1,
        FeudalToProfessional = 2
    }

    internal sealed class FiefTroopProductionResult
    {
        public Settlement Village { get; }
        public CharacterObject Troop { get; }
        public int Count { get; private set; }

        public FiefTroopProductionResult(Settlement village,
            CharacterObject troop, int count)
        {
            Village = village;
            Troop = troop;
            Count = Math.Max(0, count);
        }

        public void Add(int count) => Count = checked(Count +
            Math.Max(0, count));
    }

    internal static class FiefTroopConversionService
    {
        public static bool TryConvert(FiefPartyManager manager,
            Settlement fief, Settlement sourceVillage,
            CharacterObject sourceTroop, int count,
            FiefConversionDirection direction, out int converted,
            out string reason)
        {
            converted = 0;
            reason = "InvalidRequest";
            if (manager == null || fief?.IsFortification != true ||
                sourceTroop == null || count <= 0)
                return false;
            return direction == FiefConversionDirection.ProfessionalToFeudal
                ? TryProfessionalToFeudal(manager, fief, sourceTroop,
                    count, out converted, out reason)
                : direction == FiefConversionDirection.FeudalToProfessional
                    && TryFeudalToProfessional(manager, fief,
                        sourceVillage, sourceTroop, count,
                        out converted, out reason);
        }

        private static bool TryProfessionalToFeudal(
            FiefPartyManager manager, Settlement fief,
            CharacterObject sourceTroop, int requested,
            out int converted, out string reason)
        {
            converted = 0;
            reason = "InvalidProfessionalSource";
            FiefPartyData data = manager.GetFiefData(fief);
            MobileParty garrison = fief.Town?.GarrisonParty;
            if (data == null || garrison == null ||
                !TroopProductionTemplateRepository.Instance
                    .GetEligibility(fief, sourceTroop).IsProfessional)
                return false;
            manager.ReconcilePartyFiefTroops(garrison);
            if (manager.IsServicePartyBlocked(garrison))
            {
                reason = "RecoveryRequired";
                return false;
            }
            TroopRosterElement source = Find(garrison.MemberRoster,
                sourceTroop);
            int protectedCount = manager.GetServingTroopCounts(garrison)
                .TryGetValue(sourceTroop, out int serving) ? serving : 0;
            int healthy = Math.Max(0, source.Number -
                source.WoundedNumber - protectedCount);
            if (healthy < requested)
            {
                reason = "InsufficientHealthySource";
                return false;
            }

            IReadOnlyList<FiefTroopProductionResult> produced =
                data.ProduceDailyFiefTroopsDetailed(requested);
            int actual = produced.Sum(item => item.Count);
            if (actual <= 0)
            {
                reason = "InsufficientFeudalHearth";
                return false;
            }
            try
            {
                garrison.MemberRoster.AddToCounts(sourceTroop, -actual,
                    false, 0, 0, true, -1);
                TroopRosterElement after = Find(garrison.MemberRoster,
                    sourceTroop);
                if (after.Number != source.Number - actual ||
                    after.WoundedNumber != source.WoundedNumber)
                    throw new InvalidOperationException(
                        "ProfessionalSourceCommitMismatch");
            }
            catch
            {
                bool restoredSource = TryRestore(garrison.MemberRoster,
                    sourceTroop, source.Number, source.WoundedNumber);
                bool restoredTargets = RollbackProduced(data, produced);
                reason = restoredSource && restoredTargets
                    ? "RolledBack" : "RecoveryRequired";
                return false;
            }
            data.RefreshManpowerCounters();
            converted = actual;
            reason = actual == requested ? "Completed" : "PartiallyCompleted";
            return true;
        }

        private static bool TryFeudalToProfessional(
            FiefPartyManager manager, Settlement fief,
            Settlement sourceVillage, CharacterObject sourceTroop,
            int requested, out int converted, out string reason)
        {
            converted = 0;
            reason = "InvalidFeudalSource";
            FiefPartyData data = manager.GetFiefData(fief);
            VillageFiefData village = data?.GetVillageData(sourceVillage);
            TroopRoster sourceRoster = village?.GetReadyRoster();
            MobileParty garrison = fief.Town?.GarrisonParty;
            SettlementManpowerProductionBehavior pools = Campaign.Current?
                .GetCampaignBehavior<SettlementManpowerProductionBehavior>();
            if (data == null || village == null || sourceRoster == null ||
                garrison == null || pools == null || village.RecoveryRequired ||
                !data.IsCurrentVillage(sourceVillage) ||
                data.GetFiefPartyTemplate()?.IsEnableTroop(sourceTroop) != true)
                return false;
            TroopRosterElement source = Find(sourceRoster, sourceTroop);
            if (source.Number - source.WoundedNumber < requested)
            {
                reason = "InsufficientHealthySource";
                return false;
            }
            int freeSlots = Math.Min(
                GarrisonRecruitmentCapacity.GetHardFreeSlots(garrison),
                GarrisonRecruitmentCapacity.GetSoftRecruitmentFreeSlots(
                    garrison));
            if (freeSlots < requested)
            {
                reason = "InsufficientGarrisonCapacity";
                return false;
            }
            IReadOnlyDictionary<CharacterObject, int> targets =
                pools.AllocateProfessionalConversionTroops(
                    fief, requested, false);
            if (targets.Values.Sum() != requested)
            {
                reason = "NoEligibleProfessionalTemplate";
                return false;
            }

            var hearthCommits = new List<Dictionary<Settlement, int>>();
            var targetSnapshots = targets.ToDictionary(pair => pair.Key,
                pair => Find(garrison.MemberRoster, pair.Key));
            try
            {
                sourceRoster.AddToCounts(sourceTroop, -requested,
                    false, 0, 0, true, -1);
                TroopRosterElement sourceAfter = Find(sourceRoster,
                    sourceTroop);
                if (sourceAfter.Number != source.Number - requested ||
                    sourceAfter.WoundedNumber != source.WoundedNumber)
                    throw new InvalidOperationException(
                        "FeudalSourceCommitMismatch");

                foreach (KeyValuePair<CharacterObject, int> target in targets)
                {
                    if (!manager.TryConsumeProfessionalHearth(
                        fief, target.Key, target.Value,
                        out Dictionary<Settlement, int> hearth))
                        throw new InvalidOperationException(
                            "InsufficientProfessionalHearth");
                    hearthCommits.Add(hearth);
                    garrison.MemberRoster.AddToCounts(target.Key,
                        target.Value, false, 0, 0, true, -1);
                    TroopRosterElement after = Find(garrison.MemberRoster,
                        target.Key);
                    TroopRosterElement before = targetSnapshots[target.Key];
                    if (after.Number != before.Number + target.Value ||
                        after.WoundedNumber != before.WoundedNumber)
                        throw new InvalidOperationException(
                            "ProfessionalTargetCommitMismatch");
                }
            }
            catch (Exception exception)
            {
                bool restored = TryRestore(sourceRoster, sourceTroop,
                    source.Number, source.WoundedNumber);
                foreach (KeyValuePair<CharacterObject, TroopRosterElement>
                    target in targetSnapshots)
                    restored &= TryRestore(garrison.MemberRoster, target.Key,
                        target.Value.Number, target.Value.WoundedNumber);
                foreach (Dictionary<Settlement, int> hearth in hearthCommits)
                    manager.RollbackProfessionalHearth(hearth);
                if (!restored)
                    village.RequireRecovery("ConversionRollbackFailed");
                reason = !restored ? "RecoveryRequired" :
                    exception.Message;
                return false;
            }

            pools.AllocateProfessionalConversionTroops(
                fief, requested, true);
            data.RefreshManpowerCounters();
            converted = requested;
            reason = "Completed";
            return true;
        }

        private static bool RollbackProduced(FiefPartyData data,
            IReadOnlyList<FiefTroopProductionResult> produced)
        {
            bool restored = true;
            foreach (FiefTroopProductionResult item in produced)
            {
                VillageFiefData village = data.GetVillageData(item.Village);
                TroopRoster roster = village?.GetReadyRoster();
                TroopRosterElement current = Find(roster, item.Troop);
                if (roster == null || current.Number < item.Count ||
                    !TryRestore(roster, item.Troop,
                        current.Number - item.Count, current.WoundedNumber))
                {
                    village?.RequireRecovery("ConversionRollbackFailed");
                    restored = false;
                }
            }
            data.RefreshManpowerCounters();
            return restored;
        }

        private static TroopRosterElement Find(TroopRoster roster,
            CharacterObject troop) => roster?.GetTroopRoster()
            .FirstOrDefault(item => item.Character == troop) ?? default;

        private static bool TryRestore(TroopRoster roster,
            CharacterObject troop, int count, int wounded)
        {
            if (roster == null || troop == null) return false;
            try
            {
                TroopRosterElement current = Find(roster, troop);
                roster.AddToCounts(troop, count - current.Number, false,
                    wounded - current.WoundedNumber, 0, true, -1);
                current = Find(roster, troop);
                return current.Number == count &&
                    current.WoundedNumber == wounded;
            }
            catch
            {
                return false;
            }
        }
    }
}
