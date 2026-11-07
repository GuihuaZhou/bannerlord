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
            (int Count, int Wounded) source = GetCounts(garrison.MemberRoster,
                sourceTroop);
            int protectedCount = manager.GetServingTroopCounts(garrison)
                .TryGetValue(sourceTroop, out int serving) ? serving : 0;
            int healthy = Math.Max(0, source.Count -
                source.Wounded - protectedCount);
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
                (int Count, int Wounded) after = GetCounts(
                    garrison.MemberRoster,
                    sourceTroop);
                if (after.Count != source.Count - actual ||
                    after.Wounded != source.Wounded)
                    throw new InvalidOperationException(
                        "ProfessionalSourceCommitMismatch");
            }
            catch
            {
                bool restoredSource = TryRestore(garrison.MemberRoster,
                    sourceTroop, source.Count, source.Wounded);
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
            (int Count, int Wounded) source = GetCounts(sourceRoster,
                sourceTroop);
            if (source.Count - source.Wounded < requested)
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

            var hearthCommits = new List<string>();
            var targetSnapshots = targets.ToDictionary(pair => pair.Key,
                pair => GetCounts(garrison.MemberRoster, pair.Key));
            try
            {
                sourceRoster.AddToCounts(sourceTroop, -requested,
                    false, 0, 0, true, -1);
                (int Count, int Wounded) sourceAfter = GetCounts(sourceRoster,
                    sourceTroop);
                if (sourceAfter.Count != source.Count - requested ||
                    sourceAfter.Wounded != source.Wounded)
                    throw new InvalidOperationException(
                        "FeudalSourceCommitMismatch");

                foreach (KeyValuePair<CharacterObject, int> target in targets)
                {
                    if (!manager.TryConsumeProfessionalHearth(
                        fief, target.Key, target.Value,
                        out string hearthOperationId))
                        throw new InvalidOperationException(
                            "InsufficientProfessionalHearth");
                    hearthCommits.Add(hearthOperationId);
                    garrison.MemberRoster.AddToCounts(target.Key,
                        target.Value, false, 0, 0, true, -1);
                    (int Count, int Wounded) after = GetCounts(
                        garrison.MemberRoster,
                        target.Key);
                    (int Count, int Wounded) before =
                        targetSnapshots[target.Key];
                    if (after.Count != before.Count + target.Value ||
                        after.Wounded != before.Wounded)
                        throw new InvalidOperationException(
                            "ProfessionalTargetCommitMismatch");
                }
            }
            catch (Exception exception)
            {
                bool restored = TryRestore(sourceRoster, sourceTroop,
                    source.Count, source.Wounded);
                foreach (KeyValuePair<CharacterObject,
                    (int Count, int Wounded)>
                    target in targetSnapshots)
                    restored &= TryRestore(garrison.MemberRoster, target.Key,
                        target.Value.Count, target.Value.Wounded);
                foreach (string hearthOperationId in hearthCommits)
                    restored &= manager.RollbackProfessionalHearth(
                        fief,
                        hearthOperationId);
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
                (int Count, int Wounded) current = GetCounts(roster,
                    item.Troop);
                if (roster == null || current.Count < item.Count ||
                    !TryRestore(roster, item.Troop,
                        current.Count - item.Count, current.Wounded))
                {
                    village?.RequireRecovery("ConversionRollbackFailed");
                    restored = false;
                }
            }
            data.RefreshManpowerCounters();
            return restored;
        }

        private static (int Count, int Wounded) GetCounts(
            TroopRoster roster, CharacterObject troop)
        {
            FiefTroopRosterFilter.TryGetCounts(roster, troop,
                out int count, out int wounded);
            return (count, wounded);
        }

        private static bool TryRestore(TroopRoster roster,
            CharacterObject troop, int count, int wounded)
        {
            if (roster == null || troop == null) return false;
            try
            {
                (int Count, int Wounded) current = GetCounts(roster, troop);
                roster.AddToCounts(troop, count - current.Count, false,
                    wounded - current.Wounded, 0, true, -1);
                current = GetCounts(roster, troop);
                return current.Count == count &&
                    current.Wounded == wounded;
            }
            catch
            {
                return false;
            }
        }
    }
}
