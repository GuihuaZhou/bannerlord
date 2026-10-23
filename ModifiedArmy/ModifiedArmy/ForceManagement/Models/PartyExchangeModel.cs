using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Models;
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

namespace ModifiedArmy.ForceManagement.Models
{
    /// <summary>
    /// Builds side-effect-free, one-way troop exchange plans. Live campaign
    /// state is captured in fingerprints and must be revalidated by the executor.
    /// </summary>
    public sealed class PartyExchangeModel : IPartyExchangeModel
    {
        /// <summary>Builds one precise transfer manifest or an explicit rejection.</summary>
        public PartyExchangePlan BuildPlan(PartyExchangeContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.OperationId) ||
                context.RequestedCount <= 0)
            {
                return Reject(context, "InvalidRequest");
            }
            if (context.SourceSnapshot == null ||
                context.DestinationSnapshot == null)
                return Reject(context, "MissingRosterSnapshots");
            if (context.Operation == PartyExchangeOperation.BidirectionalSwap)
            {
                return Reject(context, "BidirectionalSwapDisabled");
            }

            switch (context.Operation)
            {
                case PartyExchangeOperation.FieldToGarrison:
                    return BuildRosterPlan(context, true);
                case PartyExchangeOperation.GarrisonToField:
                    return BuildRosterPlan(context, false);
                case PartyExchangeOperation.FiefMobilization:
                    return BuildFiefMobilizationPlan(context);
                case PartyExchangeOperation.FiefReturn:
                    return BuildFiefReturnPlan(context);
                default:
                    return Reject(context, "UnsupportedOperation");
            }
        }

        /// <summary>Plans an ordinary one-way transfer while protecting fief identities.</summary>
        private static PartyExchangePlan BuildRosterPlan(
            PartyExchangeContext context,
            bool fieldToGarrison)
        {
            MobileParty source = context.Source?.Party;
            MobileParty destination = context.Destination?.Party;
            Settlement settlement = context.TargetSettlement;
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            MobileParty field = fieldToGarrison ? source : destination;
            bool kindsMatch = fieldToGarrison
                ? context.Source?.Kind == PartyTroopSourceKind.Mobile &&
                    context.Destination?.Kind == PartyTroopSourceKind.Garrison &&
                    destination == garrison
                : context.Source?.Kind == PartyTroopSourceKind.Garrison &&
                    context.Destination?.Kind == PartyTroopSourceKind.Mobile &&
                    source == garrison;
            if (!kindsMatch || field?.CurrentSettlement != settlement ||
                !IsAuthorizedRosterPair(
                    source, destination, settlement, context.Purpose))
            {
                return Reject(context, "InvalidOrUnsafeTransfer");
            }

            int allowed = context.RequestedCount;
            if (fieldToGarrison)
            {
                allowed = Math.Min(allowed,
                    ForceManagementFacade.Current.GetSafeFieldSurplus(source));
                allowed = Math.Min(allowed,
                    GarrisonRecruitmentCapacity.GetTransferFreeSlots(destination));
            }
            else
            {
                GarrisonDefenseSnapshot defense = ForceManagementFacade.Current
                    .GarrisonDefense.Evaluate(settlement);
                allowed = Math.Min(allowed, ForceManagementFacade.Current
                    .GarrisonDefense.GetSafeTransferOutCount(
                        defense,
                        source.MemberRoster));
            }
            if (allowed <= 0)
            {
                return Reject(context, "NoApprovedCapacity");
            }

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            Dictionary<CharacterObject, int> protectedCounts = manager?
                .GetServingTroopCounts(source) ?? new Dictionary<CharacterObject, int>();
            List<RecruitmentCandidate> candidates = source.MemberRoster
                .GetTroopRoster()
                .Where(item => item.Character != null && !item.Character.IsHero)
                .Select(item => new RecruitmentCandidate(
                    item.Character,
                    Math.Max(0, item.Number - GetCount(protectedCounts, item.Character)),
                    RecruitmentSource.Transfer))
                .Where(item => item.AvailableCount > 0)
                .ToList();
            PartyExchangePlan result = CreatePlan(
                context,
                PartyExchangeExecutorKind.MobileRosterTransfer);
            if (fieldToGarrison)
            {
                RecruitmentPlan admission = RecruitmentModelManager.Model
                    .BuildPlan(destination, candidates);
                FillApprovedManifest(
                    result.SourceToDestination,
                    source.MemberRoster,
                    admission,
                    allowed,
                    protectedCounts);
            }
            else
            {
                FillSoftGarrisonWithdrawalManifest(
                    result.SourceToDestination,
                    source,
                    destination,
                    candidates,
                    allowed,
                    protectedCounts);
            }
            return Finish(context, result);
        }

        /// <summary>
        /// Selects garrison troops one at a time. Source abundance and receiver
        /// usefulness are soft marginal preferences only: they influence which
        /// legal soldier moves next but never introduce a role or tier quota.
        /// Re-evaluating after every soldier prevents one initial ordering from
        /// draining a branch before the model observes the changed rosters.
        /// </summary>
        private static void FillSoftGarrisonWithdrawalManifest(
            TroopExchangeManifest manifest,
            MobileParty source,
            MobileParty destination,
            IEnumerable<RecruitmentCandidate> candidates,
            int limit,
            IReadOnlyDictionary<CharacterObject, int> protectedCounts)
        {
            RecruitmentSimulationState destinationState =
                RecruitmentSimulationState.Create(destination);
            Dictionary<CharacterObject, int> available = candidates
                .Where(candidate => candidate?.Troop != null &&
                    candidate.AvailableCount > 0)
                .GroupBy(candidate => candidate.Troop)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(candidate => candidate.AvailableCount));
            Dictionary<TransferRole, int> sourceRoles = CountRoles(
                source.MemberRoster,
                protectedCounts);
            Dictionary<TransferRole, int> destinationRoles = CountRoles(
                destination.MemberRoster,
                null);
            int sourceTotal = available.Values.Sum();
            int remaining = Math.Max(0, limit);

            while (remaining > 0 && available.Count > 0)
            {
                List<WithdrawalCandidate> approved = new List<WithdrawalCandidate>();
                foreach (var pair in available)
                {
                    if (pair.Value <= 0)
                    {
                        continue;
                    }

                    RecruitmentEvaluationResult evaluation =
                        RecruitmentModelManager.Model.EvaluateRecruitment(
                            destination,
                            pair.Key,
                            1,
                            RecruitmentSource.Transfer,
                            destinationState);
                    if (evaluation.RecruitableCount <= 0)
                    {
                        continue;
                    }

                    approved.Add(new WithdrawalCandidate
                    {
                        Troop = pair.Key,
                        Evaluation = evaluation,
                        Score = GetWithdrawalScore(
                            pair.Key,
                            pair.Value,
                            sourceTotal,
                            sourceRoles,
                            destinationRoles)
                    });
                }

                WithdrawalCandidate selected = approved
                    .OrderByDescending(candidate => candidate.Score)
                    .ThenBy(candidate => candidate.Troop.StringId)
                    .FirstOrDefault();
                if (selected == null)
                {
                    break;
                }

                int newManifestCount = GetManifestCount(
                    manifest,
                    selected.Troop) + 1;
                AddAmount(
                    manifest,
                    selected.Troop,
                    source.MemberRoster,
                    newManifestCount);
                available[selected.Troop]--;
                if (available[selected.Troop] <= 0)
                {
                    available.Remove(selected.Troop);
                }

                TransferRole role = GetTransferRole(selected.Troop);
                sourceRoles[role] = Math.Max(0, sourceRoles[role] - 1);
                destinationRoles[role] = destinationRoles[role] + 1;
                sourceTotal = Math.Max(0, sourceTotal - 1);
                destinationState.Commit(
                    selected.Troop,
                    1,
                    selected.Evaluation.UnitDailyWage,
                    selected.Evaluation.UnitRecruitmentCost);
                remaining--;
            }
        }

        /// <summary>
        /// Rewards troops that are common at the source and useful to a role
        /// scarce at the receiver. Last-of-type and last-of-role penalties are
        /// deliberately finite, so a valid transfer is never blocked solely
        /// because the source roster is homogeneous.
        /// </summary>
        private static double GetWithdrawalScore(
            CharacterObject troop,
            int troopAvailable,
            int sourceTotal,
            IReadOnlyDictionary<TransferRole, int> sourceRoles,
            IReadOnlyDictionary<TransferRole, int> destinationRoles)
        {
            TransferRole role = GetTransferRole(troop);
            int sourceRoleCount = GetRoleCount(sourceRoles, role);
            int destinationRoleCount = GetRoleCount(destinationRoles, role);
            double denominator = Math.Max(1, sourceTotal);
            double sourceTypeAbundance = troopAvailable / denominator;
            double sourceRoleAbundance = sourceRoleCount / denominator;
            double receiverNeed = 1d / (destinationRoleCount + 1d);
            double lastTypePenalty = troopAvailable <= 1 ? 2d : 0d;
            double lastRolePenalty = sourceRoleCount <= 1 ? 3d : 0d;

            return sourceTypeAbundance * 2d +
                sourceRoleAbundance * 2d +
                receiverNeed +
                Math.Max(0, troop?.Tier ?? 0) * 0.05d -
                lastTypePenalty -
                lastRolePenalty;
        }

        /// <summary>
        /// Counts only transferable regulars. Protected fief-service soldiers
        /// are excluded before rarity is evaluated, so identity protection and
        /// diversity preference use the same source population.
        /// </summary>
        private static Dictionary<TransferRole, int> CountRoles(
            TroopRoster roster,
            IReadOnlyDictionary<CharacterObject, int> protectedCounts)
        {
            Dictionary<TransferRole, int> result = Enum
                .GetValues(typeof(TransferRole))
                .Cast<TransferRole>()
                .ToDictionary(role => role, role => 0);
            if (roster == null)
            {
                return result;
            }

            foreach (TroopRosterElement element in roster.GetTroopRoster())
            {
                if (element.Character == null || element.Character.IsHero)
                {
                    continue;
                }

                int count = Math.Max(
                    0,
                    element.Number - GetCount(
                        protectedCounts,
                        element.Character));
                TransferRole role = GetTransferRole(element.Character);
                result[role] += count;
            }

            return result;
        }

        /// <summary>
        /// Uses broad battlefield traits only to compare marginal rarity. This
        /// classification does not define a desired composition percentage.
        /// </summary>
        private static TransferRole GetTransferRole(CharacterObject troop)
        {
            if (troop?.IsMounted == true && troop.IsRanged)
            {
                return TransferRole.HorseArcher;
            }
            if (troop?.IsMounted == true)
            {
                return TransferRole.Cavalry;
            }
            return troop?.IsRanged == true
                ? TransferRole.Ranged
                : TransferRole.Infantry;
        }

        /// <summary>Reads one role count from a complete or partial map.</summary>
        private static int GetRoleCount(
            IReadOnlyDictionary<TransferRole, int> counts,
            TransferRole role)
        {
            return counts != null && counts.TryGetValue(role, out int count)
                ? Math.Max(0, count)
                : 0;
        }

        /// <summary>Reads an already approved count from the manifest.</summary>
        private static int GetManifestCount(
            TroopExchangeManifest manifest,
            CharacterObject troop)
        {
            return manifest?.Troops != null && troop != null &&
                manifest.Troops.TryGetValue(troop, out TroopExchangeAmount amount)
                    ? Math.Max(0, amount?.Count ?? 0)
                    : 0;
        }

        /// <summary>
        /// Broad traits used only for relative source rarity during one plan.
        /// They are never persisted or interpreted as composition targets.
        /// </summary>
        private enum TransferRole
        {
            Infantry,
            Ranged,
            Cavalry,
            HorseArcher
        }

        /// <summary>One currently legal marginal withdrawal choice.</summary>
        private sealed class WithdrawalCandidate
        {
            public CharacterObject Troop { get; set; }

            public RecruitmentEvaluationResult Evaluation { get; set; }

            public double Score { get; set; }
        }

        /// <summary>Plans a fief mobilization without mutating Hearth or rosters.</summary>
        private static PartyExchangePlan BuildFiefMobilizationPlan(
            PartyExchangeContext context)
        {
            Settlement sourceFief = context.Source?.Settlement;
            MobileParty destination = context.Destination?.Party;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (sourceFief == null || destination == null || manager == null ||
                sourceFief.OwnerClan != destination.ActualClan ||
                destination.CurrentSettlement != sourceFief ||
                destination.MapEvent != null || sourceFief.IsUnderSiege)
            {
                return Reject(context, "InvalidFiefMobilization");
            }
            TroopRoster roster = manager.GetFiefTroopRoster(sourceFief);
            IReadOnlyDictionary<CharacterObject, int> approvals = manager
                .PreviewFiefRecruitmentApprovals(
                    sourceFief,
                    destination,
                    context.RequestedCount);
            PartyExchangePlan result = CreatePlan(
                context,
                PartyExchangeExecutorKind.FiefMobilization);
            foreach (var approval in approvals)
                AddAmount(
                    result.SourceToDestination,
                    approval.Key,
                    roster,
                    approval.Value);
            return Finish(context, result);
        }

        /// <summary>Plans a return from one authoritative fief service batch.</summary>
        private static PartyExchangePlan BuildFiefReturnPlan(
            PartyExchangeContext context)
        {
            MobileParty source = context.Source?.Party;
            Settlement fief = context.Destination?.Settlement;
            string batchId = context.Source?.BatchId;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (source == null || fief == null || manager == null ||
                source.ActualClan != fief.OwnerClan || string.IsNullOrEmpty(batchId))
            {
                return Reject(context, "InvalidFiefReturn");
            }
            if (context.Purpose != PartyExchangePurpose.PartyDisbandment &&
                source.CurrentSettlement != fief)
                return Reject(context, "NotAtSourceFief");
            ServingDetachmentRef selected = manager.GetServingDetachments(source)
                .FirstOrDefault(item => item.SourceFief == fief &&
                    item.Detachment?.BatchId == batchId);
            if (selected.Detachment == null)
            {
                return Reject(context, "NoEligibleBatch");
            }
            FiefBatchTransferApproval approval = new FiefBatchTransferApproval
            {
                SourceFief = fief,
                BatchId = batchId
            };
            int remaining = context.RequestedCount;
            foreach (var pair in selected.Detachment.Troops
                .OrderBy(item => item.Key?.StringId, StringComparer.Ordinal))
            {
                int count = Math.Min(remaining, Math.Max(0, pair.Value));
                if (pair.Key == null || count <= 0) continue;
                AddAmount(approval.Troops, pair.Key, source.MemberRoster, count);
                remaining -= count;
                if (remaining <= 0) break;
            }
            approval.ApprovedCount = approval.Troops.TotalCount;
            PartyExchangePlan result = CreatePlan(
                context,
                PartyExchangeExecutorKind.FiefReturn);
            result.FiefBatches = new[] { approval };
            foreach (var pair in approval.Troops.Troops)
                result.SourceToDestination.Troops[pair.Key] = pair.Value;
            return Finish(context, result);
        }

        /// <summary>Creates the common immutable-by-convention plan envelope.</summary>
        private static PartyExchangePlan CreatePlan(
            PartyExchangeContext context,
            PartyExchangeExecutorKind executor) => new PartyExchangePlan
        {
            Initiator = context.Initiator,
            OperationId = context.OperationId,
            Operation = context.Operation,
            Purpose = context.Purpose,
            Executor = executor,
            RequestedCount = Math.Max(0, context.RequestedCount),
            Source = context.Source,
            Destination = context.Destination,
            TargetSettlement = context.TargetSettlement,
            ExpectedSource = context.SourceSnapshot,
            ExpectedDestination = context.DestinationSnapshot
        };

        /// <summary>Finalizes count expectations and rejects empty manifests.</summary>
        private static PartyExchangePlan Finish(
            PartyExchangeContext context,
            PartyExchangePlan plan)
        {
            int count = plan.SourceToDestination.TotalCount;
            if (count <= 0) return Reject(context, "NoApprovedTroops");
            plan.ShouldExecute = true;
            plan.ReasonCode = "Approved";
            plan.ExpectedSourceFinalCount = Math.Max(
                0,
                (context.SourceSnapshot?.RegularCount ?? 0) - count);
            plan.ExpectedDestinationFinalCount = Math.Max(
                0,
                (context.DestinationSnapshot?.RegularCount ?? 0) + count);
            return plan;
        }

        /// <summary>Copies model approvals into an exact healthy/wounded manifest.</summary>
        private static void FillApprovedManifest(
            TroopExchangeManifest manifest,
            TroopRoster source,
            RecruitmentPlan admission,
            int limit,
            IReadOnlyDictionary<CharacterObject, int> protectedCounts)
        {
            int remaining = Math.Max(0, limit);
            foreach (RecruitmentEvaluationResult evaluation in
                admission?.Evaluations ?? Array.Empty<RecruitmentEvaluationResult>())
            {
                CharacterObject troop = evaluation.Troop;
                int available = troop == null ? 0 : Math.Max(
                    0,
                    source.GetTroopCount(troop) - GetCount(protectedCounts, troop));
                int count = Math.Min(remaining,
                    Math.Min(evaluation.RecruitableCount, available));
                if (count <= 0) continue;
                AddAmount(manifest, troop, source, count);
                remaining -= count;
                if (remaining <= 0) break;
            }
        }

        /// <summary>Adds one troop type while preferring healthy soldiers.</summary>
        private static void AddAmount(
            TroopExchangeManifest manifest,
            CharacterObject troop,
            TroopRoster roster,
            int count)
        {
            // A plan is only valid for troops present in the exact source
            // snapshot. Keep this final boundary defensive because previews
            // and rosters may originate from different game systems.
            if (manifest == null || troop == null || roster == null || count <= 0)
            {
                ModLogger.Error(GameTexts.FindText(
                    "str_modifiedarmy_force_manifest_invalid").ToString());
                return;
            }
            TroopRosterElement element = roster.GetTroopRoster()
                .FirstOrDefault(item => item.Character == troop);
            if (element.Character == null || element.Number <= 0)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_force_manifest_missing_troop");
                message.SetTextVariable(
                    "TROOP_ID", troop.StringId ?? "<unknown>");
                ModLogger.Error(message.ToString());
                return;
            }
            count = Math.Min(count, element.Number);
            int healthy = Math.Max(0, element.Number - element.WoundedNumber);
            manifest.Troops[troop] = new TroopExchangeAmount
            {
                Count = count,
                WoundedCount = Math.Max(0, count - healthy)
            };
        }

        /// <summary>Reads a protected count without leaking dictionary assumptions.</summary>
        private static int GetCount(
            IReadOnlyDictionary<CharacterObject, int> counts,
            CharacterObject troop) => counts != null && troop != null &&
            counts.TryGetValue(troop, out int value) ? Math.Max(0, value) : 0;

        /// <summary>Checks the shared ownership and safety boundary of roster moves.</summary>
        private static bool IsAuthorizedRosterPair(
            MobileParty source,
            MobileParty destination,
            Settlement settlement,
            PartyExchangePurpose purpose)
        {
            if (source == null || destination == null || settlement?.Town == null ||
                !source.IsActive || !destination.IsActive ||
                source.MapEvent != null || destination.MapEvent != null ||
                settlement.IsUnderSiege)
            {
                return false;
            }

            if (source.ActualClan != null &&
                source.ActualClan == destination.ActualClan &&
                source.ActualClan == settlement.OwnerClan)
            {
                return true;
            }

            // A newly captured fief can belong to another clan in the same
            // kingdom. Only this explicit one-shot purpose crosses the normal
            // clan boundary, and only into that settlement's real garrison.
            return purpose == PartyExchangePurpose.NewlyCapturedSettlement &&
                source.ActualClan?.Kingdom != null &&
                source.ActualClan.Kingdom == settlement.OwnerClan?.Kingdom &&
                destination == settlement.Town.GarrisonParty;
        }

        /// <summary>Creates a rejection while retaining request identity for diagnostics.</summary>
        private static PartyExchangePlan Reject(
            PartyExchangeContext context,
            string reason)
        {
            PartyExchangePlan plan = PartyExchangePlan.Rejected(reason);
            plan.Initiator = context?.Initiator;
            plan.OperationId = context?.OperationId;
            plan.Operation = context?.Operation ?? PartyExchangeOperation.None;
            plan.Purpose = context?.Purpose ?? PartyExchangePurpose.NaturalVisit;
            plan.RequestedCount = Math.Max(0, context?.RequestedCount ?? 0);
            plan.Source = context?.Source;
            plan.Destination = context?.Destination;
            plan.TargetSettlement = context?.TargetSettlement;
            plan.ExpectedSource = context?.SourceSnapshot;
            plan.ExpectedDestination = context?.DestinationSnapshot;
            return plan;
        }
    }
}
