using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Revalidates and executes unified one-way exchange plans. This service
    /// has no event subscriptions; phase five decides when it becomes live.
    /// </summary>
    public sealed class PartyExchangeService
    {
        private readonly HashSet<string> _completedOperationIds =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Revalidates a plan without changing any campaign state.</summary>
        public PartyExchangeExecutionResult DryRun(PartyExchangePlan plan)
        {
            string reason = Validate(plan);
            return Result(false, reason, 0);
        }

        /// <summary>Captures the live constraints used to detect stale plans.</summary>
        public PartyRosterSnapshot CaptureSnapshot(PartyTroopSource source)
        {
            TroopRoster roster = source?.Party?.MemberRoster ?? source?.Roster;
            if (roster == null) return null;
            return new PartyRosterSnapshot
            {
                Fingerprint = source.Party != null
                    ? TroopTransferService.GetRosterFingerprint(source.Party)
                    : GetFingerprint(roster),
                RegularCount = Math.Max(0, roster.TotalRegulars),
                WoundedCount = roster.GetTroopRoster()
                    .Where(item => item.Character != null &&
                        !item.Character.IsHero)
                    .Sum(item => Math.Max(0, item.WoundedNumber)),
                HardLimit = Math.Max(0,
                    source.Party?.Party?.PartySizeLimit ?? int.MaxValue),
                DailyWage = source.Party == null
                    ? 0f
                    : ModifiedArmy.PartyFinance.Models
                        .AiRecruitmentFinancialModel
                        .GetLongTermDailyWage(source.Party)
            };
        }

        /// <summary>Executes an approved plan once, with stale-state checks.</summary>
        public PartyExchangeExecutionResult Execute(PartyExchangePlan plan)
        {
            string validation = Validate(plan);
            if (validation != "Valid")
            {
                PartyExchangeExecutionResult rejected = Result(
                    false, validation, 0);
                Log(plan, rejected);
                return rejected;
            }

            PartyExchangeExecutionResult result;
            switch (plan.Executor)
            {
                case PartyExchangeExecutorKind.MobileRosterTransfer:
                    result = ExecuteRosterTransfer(plan);
                    break;
                case PartyExchangeExecutorKind.FiefMobilization:
                    result = ExecuteFiefMobilization(plan);
                    break;
                case PartyExchangeExecutorKind.FiefReturn:
                    result = ExecuteFiefReturn(plan);
                    break;
                default:
                    result = Result(false, "UnsupportedExecutor", 0);
                    break;
            }
            if (result.Executed)
            {
                _completedOperationIds.Add(plan.OperationId);
            }
            Log(plan, result);
            return result;
        }

        /// <summary>Rejects replayed, empty, or stale plans before any mutation.</summary>
        private string Validate(PartyExchangePlan plan)
        {
            if (plan?.ShouldExecute != true ||
                string.IsNullOrWhiteSpace(plan.OperationId))
                return "MissingApprovedPlan";
            if (_completedOperationIds.Contains(plan.OperationId))
                return "OperationAlreadyCompleted";
            if (plan.Operation == PartyExchangeOperation.BidirectionalSwap)
                return "BidirectionalSwapDisabled";
            if (plan.SourceToDestination.TotalCount <= 0)
                return "EmptyManifest";
            if (!MatchesSnapshot(plan.Source, plan.ExpectedSource) ||
                !MatchesSnapshot(plan.Destination, plan.ExpectedDestination))
                return "RosterChanged";
            string boundary = ValidateRosterBoundaries(plan);
            if (boundary != "Valid") return boundary;
            return "Valid";
        }

        /// <summary>Rechecks ownership, locality, capacity, and garrison floors.</summary>
        private static string ValidateRosterBoundaries(PartyExchangePlan plan)
        {
            if (plan.Executor != PartyExchangeExecutorKind.MobileRosterTransfer)
                return "Valid";
            Settlement settlement = plan.TargetSettlement;
            MobileParty source = plan.Source?.Party;
            MobileParty destination = plan.Destination?.Party;
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            MobileParty field = plan.Operation == PartyExchangeOperation
                    .FieldToGarrison
                ? source
                : destination;
            if (source == null || destination == null || garrison == null ||
                field?.CurrentSettlement != settlement || settlement.IsUnderSiege ||
                source.ActualClan == null || source.ActualClan != destination.ActualClan ||
                source.ActualClan != settlement.OwnerClan)
                return "UnsafeAtExecution";
            int count = plan.SourceToDestination.TotalCount;
            if (plan.Operation == PartyExchangeOperation.FieldToGarrison)
            {
                if (destination != garrison ||
                    ForceManagementFacade.Current.GetSafeFieldSurplus(source) < count ||
                    GarrisonRecruitmentCapacity
                        .GetTransferFreeSlots(destination) < count)
                    return "CapacityChanged";
            }
            else if (plan.Operation == PartyExchangeOperation.GarrisonToField)
            {
                GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                    .GarrisonDefense.Evaluate(settlement);
                if (source != garrison || destination.Party.NumberOfAllMembers +
                        count > destination.Party.PartySizeLimit ||
                    ForceManagementFacade.Current.GarrisonDefense
                        .GetSafeTransferOutCount(defense, source.MemberRoster) < count)
                    return "CapacityChanged";
            }
            else
            {
                return "OperationKindMismatch";
            }
            return "Valid";
        }

        /// <summary>Moves the exact manifest and reverses all mutations on failure.</summary>
        private static PartyExchangeExecutionResult ExecuteRosterTransfer(
            PartyExchangePlan plan)
        {
            MobileParty source = plan.Source?.Party;
            MobileParty destination = plan.Destination?.Party;
            if (source == null || destination == null || !source.IsActive ||
                !destination.IsActive || source.ActualClan != destination.ActualClan ||
                source.MapEvent != null || destination.MapEvent != null)
                return Result(false, "UnsafeAtExecution", 0);

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            Dictionary<CharacterObject, int> protectedCounts = manager?
                .GetServingTroopCounts(source) ?? new Dictionary<CharacterObject, int>();
            if (!ManifestAvailable(source.MemberRoster,
                    plan.SourceToDestination,
                    protectedCounts))
                return Result(false, "ManifestNoLongerAvailable", 0);

            var rollback = new List<Action>();
            try
            {
                foreach (var pair in plan.SourceToDestination.Troops)
                {
                    CharacterObject troop = pair.Key;
                    int count = pair.Value.Count;
                    int wounded = pair.Value.WoundedCount;
                    source.MemberRoster.AddToCounts(
                        troop, -count, false, -wounded, 0, true, -1);
                    rollback.Add(() => source.MemberRoster.AddToCounts(
                        troop, count, false, wounded, 0, true, -1));
                    destination.MemberRoster.AddToCounts(
                        troop, count, false, wounded, 0, true, -1);
                    rollback.Add(() => destination.MemberRoster.AddToCounts(
                        troop, -count, false, -wounded, 0, true, -1));
                }
                return Result(true, "Executed", plan.SourceToDestination.TotalCount);
            }
            catch (Exception exception)
            {
                for (int index = rollback.Count - 1; index >= 0; index--)
                {
                    try { rollback[index](); } catch { }
                }
                return Result(false,
                    "ExecutionRolledBack:" + exception.GetType().Name,
                    0);
            }
        }

        /// <summary>Uses the authoritative fief transaction after revalidation.</summary>
        private static PartyExchangeExecutionResult ExecuteFiefMobilization(
            PartyExchangePlan plan)
        {
            Settlement source = plan.Source?.Settlement;
            MobileParty destination = plan.Destination?.Party;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (source == null || destination == null || manager == null ||
                source.OwnerClan != destination.ActualClan ||
                destination.CurrentSettlement != source ||
                destination.MapEvent != null || source.IsUnderSiege)
                return Result(false, "UnsafeAtExecution", 0);
            int recruited = manager.RecruitFiefTroopsFromSettlement(
                source,
                destination,
                plan.SourceToDestination.TotalCount);
            return recruited == plan.SourceToDestination.TotalCount
                ? Result(true, "Executed", recruited)
                : Result(false, "AuthoritativeFiefResultChanged", recruited);
        }

        /// <summary>Returns exactly one approved service batch through D2.</summary>
        private static PartyExchangeExecutionResult ExecuteFiefReturn(
            PartyExchangePlan plan)
        {
            FiefBatchTransferApproval approval = plan.FiefBatches?
                .SingleOrDefault();
            MobileParty source = plan.Source?.Party;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (approval == null || source == null || manager == null)
                return Result(false, "InvalidFiefReturnPlan", 0);
            if (plan.Purpose != PartyExchangePurpose.PartyDisbandment &&
                source.CurrentSettlement != approval.SourceFief)
                return Result(false, "NotAtSourceFief", 0);
            FiefReturnResult returned = manager.TryReturnFiefTroops(
                source,
                approval.SourceFief,
                approval.BatchId,
                approval.ApprovedCount);
            if (returned.ActualReturnedCount > 0 &&
                plan.Purpose == PartyExchangePurpose.WarRest)
                ForceManagementFacade.Current.WarRestPlanning.RecordCompleted(
                    source,
                    returned.ActualReturnedCount);
            return returned.ActualReturnedCount == approval.ApprovedCount
                ? Result(true, "Executed", returned.ActualReturnedCount)
                : Result(false,
                    returned.ReasonCode ?? "AuthoritativeFiefResultChanged",
                    returned.ActualReturnedCount);
        }

        /// <summary>Checks exact healthy, wounded, and protected availability.</summary>
        private static bool ManifestAvailable(
            TroopRoster roster,
            TroopExchangeManifest manifest,
            IReadOnlyDictionary<CharacterObject, int> protectedCounts)
        {
            if (roster == null || manifest == null) return false;
            foreach (var pair in manifest.Troops)
            {
                TroopRosterElement element = roster.GetTroopRoster()
                    .FirstOrDefault(item => item.Character == pair.Key);
                int protectedCount = protectedCounts.TryGetValue(
                    pair.Key,
                    out int value) ? Math.Max(0, value) : 0;
                int healthy = Math.Max(0, element.Number - element.WoundedNumber);
                if (pair.Key == null || pair.Key.IsHero || pair.Value == null ||
                    pair.Value.Count <= 0 || pair.Value.WoundedCount < 0 ||
                    pair.Value.WoundedCount > element.WoundedNumber ||
                    pair.Value.Count - pair.Value.WoundedCount > healthy ||
                    pair.Value.Count > element.Number - protectedCount)
                    return false;
            }
            return true;
        }

        /// <summary>Matches a current live roster to a planning fingerprint.</summary>
        private static bool MatchesSnapshot(
            PartyTroopSource source,
            PartyRosterSnapshot snapshot)
        {
            if (snapshot == null) return true;
            if (source?.Party != null)
                return TroopTransferService.GetRosterFingerprint(source.Party) ==
                    snapshot.Fingerprint;
            if (source?.Kind == PartyTroopSourceKind.Fief && source.Roster != null)
                return GetFingerprint(source.Roster) == snapshot.Fingerprint;
            return false;
        }

        /// <summary>Builds the stable fingerprint format used for fief rosters.</summary>
        private static string GetFingerprint(TroopRoster roster) => string.Join(
            ";",
            roster.GetTroopRoster()
                .Where(item => item.Character != null)
                .OrderBy(item => item.Character.StringId)
                .Select(item => string.Format(
                    "{0}:{1}:{2}",
                    item.Character.StringId,
                    item.Number,
                    item.WoundedNumber))) + ";";

        /// <summary>Creates a compact execution result.</summary>
        private static PartyExchangeExecutionResult Result(
            bool executed,
            string reason,
            int count) => new PartyExchangeExecutionResult
        {
            Executed = executed,
            ReasonCode = reason,
            TransferredCount = Math.Max(0, count)
        };

        /// <summary>Writes one audit record without exposing raw roster contents.</summary>
        private static void Log(
            PartyExchangePlan plan,
            PartyExchangeExecutionResult result)
        {
            ModLogger.FileNotice(string.Format(
                "[统一调兵] 任务={0}，类型={1}，家族={2}，从{3}调往{4}；" +
                "计划={5}，实际={6}人，执行结果={7}；" +
                "来源计划前={8}，来源当前={9}，目标计划前={10}，目标当前={11}。",
                plan?.OperationId ?? "unknown",
                plan?.Operation.ToString() ?? "unknown",
                plan?.Source?.Party?.ActualClan?.Name?.ToString() ??
                    plan?.Source?.Settlement?.OwnerClan?.Name?.ToString() ??
                    "unknown",
                plan?.Source?.Party?.Name?.ToString() ??
                    plan?.Source?.Settlement?.Name?.ToString() ?? "unknown",
                plan?.Destination?.Party?.Name?.ToString() ??
                    plan?.Destination?.Settlement?.Name?.ToString() ?? "unknown",
                FormatManifest(plan?.SourceToDestination),
                result?.TransferredCount ?? 0,
                result?.ReasonCode ?? "unknown",
                plan?.ExpectedSource?.Fingerprint ?? "none",
                CurrentFingerprint(plan?.Source),
                plan?.ExpectedDestination?.Fingerprint ?? "none",
                CurrentFingerprint(plan?.Destination)));
        }

        /// <summary>Formats approved troop types and wounded counts for audit.</summary>
        private static string FormatManifest(TroopExchangeManifest manifest)
        {
            if (manifest?.Troops == null || manifest.Troops.Count == 0)
                return "无";
            return string.Join(", ", manifest.Troops
                .OrderBy(pair => pair.Key?.StringId, StringComparer.Ordinal)
                .Select(pair => string.Format(
                    "{0}×{1}(伤{2})",
                    pair.Key?.Name?.ToString() ?? "unknown",
                    pair.Value?.Count ?? 0,
                    pair.Value?.WoundedCount ?? 0)));
        }

        /// <summary>Reads the post-attempt fingerprint for conservation logs.</summary>
        private static string CurrentFingerprint(PartyTroopSource source)
        {
            if (source?.Party != null)
                return TroopTransferService.GetRosterFingerprint(source.Party);
            return source?.Roster != null ? GetFingerprint(source.Roster) : "none";
        }
    }
}
