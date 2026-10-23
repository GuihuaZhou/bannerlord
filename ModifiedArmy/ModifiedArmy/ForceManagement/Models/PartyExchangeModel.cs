using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

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
                !IsLocalClanPair(source, destination, settlement))
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
            manager?.ReconcilePartyFiefTroops(source);
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
            RecruitmentPlan admission = RecruitmentModelManager.Model.BuildPlan(
                destination,
                candidates);
            PartyExchangePlan result = CreatePlan(
                context,
                PartyExchangeExecutorKind.MobileRosterTransfer);
            FillApprovedManifest(
                result.SourceToDestination,
                source.MemberRoster,
                admission,
                allowed,
                protectedCounts);
            return Finish(context, result);
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
            TroopRosterElement element = roster.GetTroopRoster()
                .FirstOrDefault(item => item.Character == troop);
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
        private static bool IsLocalClanPair(
            MobileParty source,
            MobileParty destination,
            Settlement settlement) => source != null && destination != null &&
            settlement?.Town != null && source.IsActive && destination.IsActive &&
            source.ActualClan != null && source.ActualClan == destination.ActualClan &&
            source.ActualClan == settlement.OwnerClan && source.MapEvent == null &&
            destination.MapEvent == null && !settlement.IsUnderSiege;

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
            plan.Source = context?.Source;
            plan.Destination = context?.Destination;
            plan.TargetSettlement = context?.TargetSettlement;
            plan.ExpectedSource = context?.SourceSnapshot;
            plan.ExpectedDestination = context?.DestinationSnapshot;
            return plan;
        }
    }
}
