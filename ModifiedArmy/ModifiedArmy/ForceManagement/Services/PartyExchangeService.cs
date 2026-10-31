using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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
            PartyExchangeExecutionResult result = Result(false, reason, 0);
            CompleteResultMetadata(plan, result);
            return result;
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
                    : ModifiedArmy.PartyManagement.Finance.Models
                        .AiRecruitmentFinancialModel
                        .GetLongTermDailyWage(source.Party)
            };
        }

        /// <summary>
        /// Performs the mutable fief-identity reconciliation before planning,
        /// then refreshes both fingerprints. Models remain side-effect free and
        /// execution can reliably reject any later roster change.
        /// </summary>
        public void PrepareContext(PartyExchangeContext context)
        {
            if (context == null) return;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (context.Source?.Party != null)
                manager?.ReconcilePartyFiefTroops(context.Source.Party);
            if (context.Destination?.Party != null &&
                context.Destination.Party != context.Source?.Party)
                manager?.ReconcilePartyFiefTroops(context.Destination.Party);
            context.SourceSnapshot = CaptureSnapshot(context.Source);
            context.DestinationSnapshot = CaptureSnapshot(context.Destination);
        }

        /// <summary>Executes an approved plan once, with stale-state checks.</summary>
        public PartyExchangeExecutionResult Execute(PartyExchangePlan plan)
        {
            string validation = Validate(plan);
            if (validation != "Valid")
            {
                PartyExchangeExecutionResult rejected = Result(
                    false, validation, 0);
                CompleteResultMetadata(plan, rejected);
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
            CompleteResultMetadata(plan, result);
            if (result.HasCommittedChanges)
            {
                _completedOperationIds.Add(plan.OperationId);
                Campaign.Current?
                    .GetCampaignBehavior<TransferOperationJournalBehavior>()?
                    .Record(result, (int)CampaignTime.Now.ToDays);
            }
            Log(plan, result);
            return result;
        }

        /// <summary>Rejects replayed, empty, or stale plans before any mutation.</summary>
        private string Validate(PartyExchangePlan plan)
        {
            if (!CampaignRecoveryCoordinator.CanMutateCampaignState())
                return "CampaignRecoveryNotReady";
            if (plan == null || string.IsNullOrWhiteSpace(plan.OperationId))
                return "MissingApprovedPlan";
            if (!plan.ShouldExecute)
                return string.IsNullOrWhiteSpace(plan.ReasonCode)
                    ? "PlanRejected"
                    : plan.ReasonCode;
            TransferOperationJournalBehavior journal = Campaign.Current?
                .GetCampaignBehavior<TransferOperationJournalBehavior>();
            if (_completedOperationIds.Contains(plan.OperationId) ||
                journal?.Contains(plan.OperationId) == true)
                return "OperationAlreadyCompleted";
            if (journal?.IsDomainFrozen(GetFreezeDomain(plan)) == true)
                return "TransferDomainFrozenForRecovery";
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
                !IsAuthorizedRosterPair(plan, source, destination, settlement))
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
                !destination.IsActive ||
                !IsAuthorizedRosterPair(
                    plan, source, destination, plan.TargetSettlement) ||
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
            int sourceBefore = source.MemberRoster.TotalRegulars;
            int destinationBefore = destination.MemberRoster.TotalRegulars;
            string sourceFingerprintBefore = GetFingerprint(source.MemberRoster);
            string destinationFingerprintBefore =
                GetFingerprint(destination.MemberRoster);
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
                int moved = plan.SourceToDestination.TotalCount;
                if (source.MemberRoster.TotalRegulars != sourceBefore - moved ||
                    destination.MemberRoster.TotalRegulars !=
                        destinationBefore + moved)
                {
                    throw new InvalidOperationException(
                        "RosterConservationCheckFailed");
                }
                return Result(
                    TransferCommitStatus.Committed,
                    "Executed",
                    plan.SourceToDestination.TotalCount);
            }
            catch (Exception exception)
            {
                bool rollbackFailed = false;
                for (int index = rollback.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        rollback[index]();
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackFailed = true;
                        ModLogger.Error(
                            "Party exchange rollback action failed: " +
                            rollbackException);
                    }
                }
                bool restored = !rollbackFailed &&
                    source.MemberRoster.TotalRegulars == sourceBefore &&
                    destination.MemberRoster.TotalRegulars == destinationBefore &&
                    GetFingerprint(source.MemberRoster) ==
                        sourceFingerprintBefore &&
                    GetFingerprint(destination.MemberRoster) ==
                        destinationFingerprintBefore;
                if (!restored)
                {
                    ModLogger.Error(
                        "Party exchange rollback could not restore roster " +
                        "counts. Manual recovery is required. Operation=" +
                        (plan.OperationId ?? "unknown"));
                    int observedChange = Math.Max(
                        Math.Abs(sourceBefore - source.MemberRoster.TotalRegulars),
                        Math.Abs(destinationBefore -
                            destination.MemberRoster.TotalRegulars));
                    return Result(
                        TransferCommitStatus.RecoveryRequired,
                        "ExecutionRecoveryRequired:" +
                            exception.GetType().Name,
                        observedChange);
                }
                string reason = exception.Message ==
                        "RosterConservationCheckFailed"
                    ? "RosterConservationCheckFailedRolledBack"
                    : "ExecutionRolledBack:" + exception.GetType().Name;
                return Result(TransferCommitStatus.RolledBack, reason, 0);
            }
        }

        /// <summary>Rechecks the narrow ownership exception for captured fiefs.</summary>
        private static bool IsAuthorizedRosterPair(
            PartyExchangePlan plan,
            MobileParty source,
            MobileParty destination,
            Settlement settlement)
        {
            return PartyTransferOwnershipPolicy.IsAuthorizedRosterPair(
                source,
                destination,
                settlement,
                plan?.Purpose ?? PartyExchangePurpose.NaturalVisit);
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
            IReadOnlyDictionary<CharacterObject, int> liveApprovals = manager
                .PreviewFiefRecruitmentApprovals(
                    source,
                    destination,
                    plan.RequestedCount);
            if (!FiefManifestMatches(plan.SourceToDestination, liveApprovals))
                return Result(false, "AuthoritativeFiefManifestChanged", 0);
            int recruited = manager.RecruitFiefTroopsFromSettlement(
                source,
                destination,
                plan.RequestedCount,
                liveApprovals);
            if (recruited <= 0)
                return Result(false, "AuthoritativeFiefResultChanged", 0);
            return recruited == plan.SourceToDestination.TotalCount
                ? Result(true, "Executed", recruited)
                : Result(
                    TransferCommitStatus.PartiallyCommitted,
                    "AuthoritativeFiefResultChanged",
                    recruited);
        }

        private static bool FiefManifestMatches(
            TroopExchangeManifest planned,
            IReadOnlyDictionary<CharacterObject, int> liveApprovals)
        {
            if (planned?.Troops == null || liveApprovals == null ||
                planned.Troops.Count != liveApprovals.Count)
                return false;
            foreach (var pair in planned.Troops)
            {
                if (pair.Key == null || pair.Value == null ||
                    pair.Value.Count <= 0 || pair.Value.WoundedCount != 0 ||
                    !liveApprovals.TryGetValue(pair.Key, out int count) ||
                    count != pair.Value.Count)
                    return false;
            }
            return liveApprovals.All(pair =>
                pair.Key != null && pair.Value > 0);
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
            if (returned.ActualReturnedCount <= 0)
                return Result(
                    false,
                    returned.ReasonCode ?? "AuthoritativeFiefResultChanged",
                    0);
            return returned.ActualReturnedCount == approval.ApprovedCount
                ? Result(true, "Executed", returned.ActualReturnedCount)
                : Result(
                    TransferCommitStatus.PartiallyCommitted,
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
            int count) => Result(
                executed
                    ? TransferCommitStatus.Committed
                    : TransferCommitStatus.Rejected,
                reason,
                count);

        private static PartyExchangeExecutionResult Result(
            TransferCommitStatus status,
            string reason,
            int count) => new PartyExchangeExecutionResult
        {
            Status = status,
            ReasonCode = reason,
            TransferredCount = Math.Max(0, count)
        };

        private static void CompleteResultMetadata(
            PartyExchangePlan plan,
            PartyExchangeExecutionResult result)
        {
            if (plan == null || result == null) return;
            result.OperationId = plan.OperationId;
            result.RequestedCount = Math.Max(0, plan.RequestedCount);
            result.ApprovedCount = Math.Max(
                0,
                plan.SourceToDestination?.TotalCount ?? 0);
            result.SourceFingerprint = CaptureFingerprint(plan.Source);
            result.DestinationFingerprint = CaptureFingerprint(
                plan.Destination);
            result.FreezeDomain = GetFreezeDomain(plan);
        }

        private static string CaptureFingerprint(PartyTroopSource source)
        {
            TroopRoster roster = source?.Party?.MemberRoster ?? source?.Roster;
            return roster == null ? string.Empty : GetFingerprint(roster);
        }

        private static string GetFreezeDomain(PartyExchangePlan plan) =>
            plan?.Initiator?.StringId ?? plan?.Destination?.Party?.StringId ??
            plan?.Source?.Party?.StringId ?? string.Empty;

        /// <summary>Writes one audit record without exposing raw roster contents.</summary>
        private static void Log(
            PartyExchangePlan plan,
            PartyExchangeExecutionResult result)
        {
            MobileParty party = plan?.Initiator ??
                plan?.Destination?.Party ?? plan?.Source?.Party;
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject partyName = PartyLogFormatter.GetDisplayName(party);
            TextObject settlementName = plan?.TargetSettlement?.Name ??
                plan?.Source?.Settlement?.Name ??
                plan?.Destination?.Settlement?.Name ??
                new TextObject("Unknown settlement");
            if (result?.HasCommittedChanges == true)
            {
                string templateId = plan?.Operation switch
                {
                    PartyExchangeOperation.GarrisonToField =>
                        "str_modifiedarmy_exchange_withdraw_executed",
                    PartyExchangeOperation.FieldToGarrison =>
                        "str_modifiedarmy_exchange_donate_executed",
                    _ => "str_modifiedarmy_exchange_executed"
                };
                TextObject message = GameTexts.FindText(
                    templateId);
                message.SetTextVariable("PARTY_NAME", partyName);
                message.SetTextVariable("SETTLEMENT_NAME", settlementName);
                message.SetTextVariable(
                    "MANIFEST", FormatManifest(plan.SourceToDestination));
                message.SetTextVariable("REQUESTED", plan.RequestedCount);
                message.SetTextVariable(
                    "APPROVED",
                    plan.SourceToDestination.TotalCount);
                message.SetTextVariable("ACTUAL", result.TransferredCount);
                message.SetTextVariable(
                    "SOURCE_BEFORE",
                    plan.ExpectedSource?.RegularCount ?? 0);
                message.SetTextVariable(
                    "SOURCE_AFTER",
                    plan.ExpectedSourceFinalCount);
                message.SetTextVariable(
                    "DESTINATION_BEFORE",
                    plan.ExpectedDestination?.RegularCount ?? 0);
                message.SetTextVariable(
                    "DESTINATION_AFTER",
                    plan.ExpectedDestinationFinalCount);
                ModLogger.Info(message.ToString());
                return;
            }
            TextObject rejected = GameTexts.FindText(
                "str_modifiedarmy_exchange_rejected");
            rejected.SetTextVariable("PARTY_NAME", partyName);
            rejected.SetTextVariable("SETTLEMENT_NAME", settlementName);
            rejected.SetTextVariable("REQUESTED", plan?.RequestedCount ?? 0);
            rejected.SetTextVariable(
                "APPROVED",
                plan?.SourceToDestination?.TotalCount ?? 0);
            rejected.SetTextVariable(
                "REASON", GetReasonText(result?.ReasonCode));
            // Expected denials such as a full roster or a changed plan belong in
            // Debug. Transaction failures are exceptional because execution either
            // detected a conservation problem or had to roll back mutations.
            if (result?.ReasonCode?.Contains("RolledBack") == true ||
                result?.ReasonCode == "RosterConservationCheckFailed")
                ModLogger.Error(rejected.ToString());
            else
                ModLogger.Debug(rejected.ToString());
        }

        /// <summary>Converts common refusal codes into readable Chinese.</summary>
        private static TextObject GetReasonText(string reason)
        {
            string key;
            switch (reason)
            {
                case "NoApprovedCapacity":
                case "NoApprovedTroops":
                case "EmptyManifest":
                    key = "str_modifiedarmy_exchange_reason_no_troops";
                    break;
                case "RosterChanged":
                    key = "str_modifiedarmy_exchange_reason_roster_changed";
                    break;
                case "OperationAlreadyCompleted":
                    key = "str_modifiedarmy_exchange_reason_completed";
                    break;
                case "UnsafeAtExecution":
                case "InvalidOrUnsafeTransfer":
                    key = "str_modifiedarmy_exchange_reason_unsafe";
                    break;
                case "CapacityChanged":
                    key = "str_modifiedarmy_exchange_reason_capacity";
                    break;
                case "ManifestNoLongerAvailable":
                    key = "str_modifiedarmy_exchange_reason_unavailable";
                    break;
                case "MissingApprovedPlan":
                    key = "str_modifiedarmy_exchange_reason_missing_plan";
                    break;
                default:
                    TextObject fallback = GameTexts.FindText(
                        "str_modifiedarmy_exchange_reason_other");
                    fallback.SetTextVariable("REASON_CODE", reason ?? "Unknown");
                    return fallback;
            }
            return GameTexts.FindText(key);
        }

        /// <summary>
        /// Formats transferred troop types and total counts. Wounded counts remain
        /// part of the authoritative transfer manifest but are intentionally omitted
        /// from the player-facing summary.
        /// </summary>
        private static string FormatManifest(TroopExchangeManifest manifest)
        {
            if (manifest?.Troops == null || manifest.Troops.Count == 0)
                return GameTexts.FindText(
                    "str_modifiedarmy_exchange_manifest_none").ToString();
            return string.Join(", ", manifest.Troops
                .OrderBy(pair => pair.Key?.StringId, StringComparer.Ordinal)
                .Select(pair =>
                {
                    TextObject item = GameTexts.FindText(
                        "str_modifiedarmy_exchange_manifest_item");
                    item.SetTextVariable(
                        "TROOP_NAME",
                        pair.Key?.Name ?? new TextObject("Unknown troop"));
                    item.SetTextVariable("COUNT", pair.Value?.Count ?? 0);
                    return item.ToString();
                }));
        }

    }
}
