using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.ForceManagement.Diagnostics;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.Garrison.Behaviors;
using ModifiedArmy.Models.Fief;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Missions
{
    /// <summary>Owns the one resource transaction selected for a legal visit.</summary>
    public sealed class PartyVisitExecutionService
    {
        private readonly UnifiedRecruitmentExecutionService _recruitment =
            new UnifiedRecruitmentExecutionService();

        public PartyExchangeExecutionResult Execute(
            MobileParty party,
            Settlement settlement,
            PartyForceTask task)
        {
            if (!ManagedPartyPolicy.IsModManagedAiParty(party))
                return new PartyExchangeExecutionResult
                    { ReasonCode = "PartyOutsideManagedLordScope" };
            int before = party?.MemberRoster?.TotalRegulars ?? 0;
            ForceRecruitmentDiagnostics.LogVisitTaskMatch(
                party, settlement, task);
            if (task == null || task.TargetSettlement != settlement)
            {
                PartyExchangeExecutionResult donation =
                    ExecuteRoutineDonation(party, settlement);
                if (donation.Executed)
                {
                    ForceRecruitmentDiagnostics.LogExecution(
                        party, settlement, task, donation, before,
                        party?.MemberRoster?.TotalRegulars ?? 0);
                    return donation;
                }
                TryPeaceDemobilization(party, settlement);
                PartyExchangeExecutionResult noMatch =
                    new PartyExchangeExecutionResult
                    { ReasonCode = donation.ReasonCode ?? "NoMatchingForceTask" };
                ForceRecruitmentDiagnostics.LogExecution(
                    party, settlement, task, noMatch, before,
                    party?.MemberRoster?.TotalRegulars ?? 0);
                return noMatch;
            }
            if (task.Intent == PartyForceIntent.Recruit &&
                task.SourceKind != PartyForceSourceKind.Mercenary &&
                Campaign.Current?
                    .GetCampaignBehavior<PartySettlementBindingBehavior>()?
                    .IsBoundSettlement(party, settlement) != true)
            {
                task.Status = PartyForceTaskStatus.Cancelled;
                task.OwnsMovementOrder = false;
                task.ReasonCode = "RecruitmentSettlementBindingInvalid";
                return new PartyExchangeExecutionResult
                    { ReasonCode = task.ReasonCode };
            }
            task.Status = PartyForceTaskStatus.Executing;
            string operationId = BuildOperationId(party, task);
            PartyExchangeExecutionResult result = task.Intent switch
            {
                PartyForceIntent.Recruit => _recruitment.Execute(
                    party, settlement, task.SourceKind, operationId,
                    task.TaskId, task.CreationAttemptId),
                PartyForceIntent.WarRest => ExecuteWarRest(
                    party, task, operationId),
                _ => new PartyExchangeExecutionResult
                    { ReasonCode = "TaskHasNoArrivalOperation" }
            };
            ApplyResult(task, result);
            ForceRecruitmentDiagnostics.LogExecution(
                party, settlement, task, result, before,
                party?.MemberRoster?.TotalRegulars ?? 0);
            return result;
        }

        internal static string BuildOperationId(
            MobileParty party, PartyForceTask task)
        {
            if (task?.UsesLegacyOperationId == true)
                return string.Format("force:{0}:{1}:{2}:{3}",
                    party?.StringId ?? task.PartyId, task.CreatedDay,
                    task.Version, task.Intent);
            string step = string.Format("{0}:{1}:{2}", task?.Intent,
                task?.SourceKind,
                task?.TargetSettlement?.StringId ?? "none");
            return string.Format("force-task:{0}:{1}",
                task?.TaskId ?? "missing-task-id", step);
        }

        public PartyExchangeExecutionResult ExecuteCaptureDonation(
            MobileParty party,
            Settlement settlement,
            GarrisonCaptureSupplyBehavior capture)
        {
            if (!ManagedPartyPolicy.IsModManagedAiParty(party))
                return new PartyExchangeExecutionResult
                    { ReasonCode = "PartyOutsideManagedLordScope" };
            if (capture?.TryConsumeTroopDonationPermit(
                    party, settlement) != true)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "CapturePermitUnavailable" };
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            int requested = ForceManagementFacade.Current
                .GetSafeFieldSurplus(party);
            if (garrison == null || requested <= 0)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "NoSafeDonationSurplus" };
            return ExecuteRosterPlan(party, settlement, garrison, requested,
                PartyExchangePurpose.NewlyCapturedSettlement,
                string.Format("capture:{0}:{1}:{2}", party.StringId,
                    settlement.StringId, (int)CampaignTime.Now.ToDays));
        }

        /// <summary>
        /// Executes one army member's contribution to a newly captured
        /// garrison. The capture behavior owns the one-shot army workflow;
        /// this method owns the transactional roster mutation.
        /// </summary>
        public PartyExchangeExecutionResult ExecuteCaptureContribution(
            MobileParty party,
            Settlement settlement,
            int requested)
        {
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            bool eligibleCaptureDonor = party?.IsActive == true &&
                party.IsLordParty && !party.IsDisbanding &&
                party.LeaderHero?.IsAlive == true &&
                party.CurrentSettlement == settlement &&
                party.ActualClan?.Kingdom != null &&
                party.ActualClan.Kingdom == settlement?.OwnerClan?.Kingdom;
            if (!eligibleCaptureDonor || garrison == null || requested <= 0)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "NoSafeDonationSurplus" };
            return ExecuteRosterPlan(party, settlement, garrison, requested,
                PartyExchangePurpose.NewlyCapturedSettlement,
                string.Format("capture-army:{0}:{1}:{2}", party.StringId,
                    settlement.StringId, (int)CampaignTime.Now.ToDays));
        }

        private static PartyExchangeExecutionResult ExecuteRoutineDonation(
            MobileParty party,
            Settlement settlement)
        {
            PartySettlementBindingBehavior bindings = Campaign.Current?
                .GetCampaignBehavior<PartySettlementBindingBehavior>();
            if (bindings?.IsBoundSettlement(party, settlement) != true)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "SettlementNotBoundToParty" };
            MobileParty garrison = settlement?.Town?.GarrisonParty;
            int surplus = ForceManagementFacade.Current.GetSafeFieldSurplus(party);
            if (garrison == null || settlement.OwnerClan != party?.ActualClan ||
                surplus <= 0)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "NoSafeDonationSurplus" };
            return ExecuteRosterPlan(party, settlement, garrison, surplus,
                PartyExchangePurpose.NaturalVisit,
                string.Format("visit:{0}:{1}:{2}", party.StringId,
                    settlement.StringId, (int)CampaignTime.Now.ToDays));
        }

        private static PartyExchangeExecutionResult ExecuteRosterPlan(
            MobileParty party,
            Settlement settlement,
            MobileParty garrison,
            int requested,
            PartyExchangePurpose purpose,
            string operationId)
        {
            PartyExchangeService runner = ForceManagementFacade.Current
                .PartyExchangeRunner;
            PartyExchangeContext context = new PartyExchangeContext
            {
                OperationId = operationId,
                Initiator = party,
                Operation = PartyExchangeOperation.FieldToGarrison,
                Intent = PartyForceIntent.Maintain,
                Purpose = purpose,
                Source = new PartyTroopSource
                {
                    Kind = PartyTroopSourceKind.Mobile, Party = party,
                    Settlement = settlement, Roster = party.MemberRoster
                },
                Destination = new PartyTroopSource
                {
                    Kind = PartyTroopSourceKind.Garrison, Party = garrison,
                    Settlement = settlement, Roster = garrison.MemberRoster
                },
                TargetSettlement = settlement,
                RequestedCount = requested,
                Today = (int)CampaignTime.Now.ToDays
            };
            runner.PrepareContext(context);
            PartyExchangePlan plan = ForceManagementFacade.Current
                .PartyExchange.BuildPlan(context);
            return runner.Execute(plan);
        }

        private static PartyExchangeExecutionResult ExecuteWarRest(
            MobileParty party,
            PartyForceTask task,
            string operationId)
        {
            PartyForceTargetResult target = ForceManagementFacade.Current
                .GetPartyTarget(party);
            WarRestDecision decision = ForceManagementFacade.Current.WarRest
                .Evaluate(ForceManagementFacade.Current.WarRestPlanning
                    .BuildContext(party, target, false));
            FiefBatchTransferApproval approval = decision.CandidateFiefBatches
                .FirstOrDefault(item => item.BatchId == task.SourceBatchId &&
                    item.SourceFief == task.TargetSettlement);
            PartyExchangeContext context = ForceManagementFacade.Current
                .WarRestPlanning.BuildReturnExchangeContext(
                    party, decision, approval, operationId);
            if (context == null)
                return new PartyExchangeExecutionResult
                    { ReasonCode = "WarRestNoLongerApproved" };
            PartyExchangeService runner = ForceManagementFacade.Current
                .PartyExchangeRunner;
            runner.PrepareContext(context);
            PartyExchangeExecutionResult result = runner.Execute(
                ForceManagementFacade.Current.PartyExchange.BuildPlan(context));
            if (result.TransferredCount <= 0 || !result.HasCommittedChanges)
                return result;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            int limit = Math.Min(
                ForceManagementFacade.Current.GetSafeFieldSurplus(party),
                ForceManagementFacade.Current.WarRestPlanning
                    .GetRemainingDailyQuota(party));
            if (manager != null && limit > 0)
            {
                FiefPlacementResult placement = FiefTroopPlacementService
                    .TryPlaceAfterReturn(manager, party,
                        task.TargetSettlement, FiefPlacementPurpose.WarRest,
                        limit);
                result.PlacedCount = placement.PlacedCount;
                ForceManagementFacade.Current.WarRestPlanning.RecordCompleted(
                    party, placement.PlacedCount);
            }
            return result;
        }

        private static void ApplyResult(
            PartyForceTask task,
            PartyExchangeExecutionResult result)
        {
            task.Progress += Math.Max(0, result.TransferredCount);
            task.ReasonCode = result.ReasonCode;
            switch (result.Status)
            {
                case TransferCommitStatus.Committed:
                case TransferCommitStatus.PartiallyCommitted:
                    task.Status = PartyForceTaskStatus.Completed;
                    break;
                case TransferCommitStatus.RecoveryRequired:
                    task.Status = PartyForceTaskStatus.Suspended;
                    task.ReasonCode = "RecoveryRequired:" +
                        (result.ReasonCode ?? "Unknown");
                    break;
                default:
                    task.Status = PartyForceTaskStatus.Cancelled;
                    break;
            }
            task.OwnsMovementOrder = false;
        }

        private static void TryPeaceDemobilization(
            MobileParty party,
            Settlement settlement)
        {
            PartySettlementBindingBehavior bindings = Campaign.Current?
                .GetCampaignBehavior<PartySettlementBindingBehavior>();
            if (!ForceManagementFacade.IsAtFormalWar(party?.ActualClan) &&
                bindings?.IsBoundSettlement(party, settlement) == true)
                Campaign.Current?.GetCampaignBehavior<DemobilizationBehavior>()?
                    .OnNaturalVisit(party, settlement);
        }
    }
}
