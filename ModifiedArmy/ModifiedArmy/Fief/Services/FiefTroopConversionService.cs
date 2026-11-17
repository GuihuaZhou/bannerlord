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
            catch (Exception exception)
            {
                converted = actual;
                reason = "PartialStatePreserved:" +
                    exception.GetType().Name;
                data.RefreshManpowerCounters();
                return converted > 0;
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
                garrison == null || pools == null ||
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
                    converted += target.Value;
                }
            }
            catch (Exception exception)
            {
                if (converted > 0)
                    pools.AllocateProfessionalConversionTroops(
                        fief, converted, true);
                data.RefreshManpowerCounters();
                reason = "PartialStatePreserved:" + exception.GetType().Name;
                return converted > 0;
            }

            pools.AllocateProfessionalConversionTroops(
                fief, requested, true);
            data.RefreshManpowerCounters();
            converted = requested;
            reason = "Completed";
            return true;
        }

        private static (int Count, int Wounded) GetCounts(
            TroopRoster roster, CharacterObject troop)
        {
            FiefTroopRosterFilter.TryGetCounts(roster, troop,
                out int count, out int wounded);
            return (count, wounded);
        }

    }
}
