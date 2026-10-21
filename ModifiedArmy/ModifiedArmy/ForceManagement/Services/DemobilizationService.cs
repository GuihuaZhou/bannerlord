using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Models.Fief;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>Runtime-only actual result of one demobilization attempt.</summary>
    public sealed class DemobilizationExecutionResult
    {
        public PeaceDemobilizationDecision Decision { get; set; }
        public int ReturnedFiefCount { get; set; }
        public int TransferredRegularCount { get; set; }
        public int ActualCount => ReturnedFiefCount + TransferredRegularCount;
        public string ReasonCode { get; set; }
    }

    /// <summary>
    /// Executes an approved natural-visit demobilization, returning eligible
    /// fief-service batches before transferring ordinary troops.
    /// </summary>
    public sealed class DemobilizationService
    {
        /// <summary>
        /// Re-evaluates all live gates, applies only actual transfers, and
        /// never removes soldiers that cannot be legally settled.
        /// </summary>
        public DemobilizationExecutionResult TryExecuteAtSettlement(
            MobileParty party,
            Settlement settlement,
            PeaceDemobilizationContext context)
        {
            context ??= new PeaceDemobilizationContext();
            context.VisitedSettlement = settlement;
            PeaceDemobilizationDecision decision = ForceManagementFacade
                .Current.PeaceDemobilization.Evaluate(party, context);
            DemobilizationExecutionResult result =
                new DemobilizationExecutionResult
                {
                    Decision = decision,
                    ReasonCode = decision.ReasonCode
                };
            if (!decision.CanDemobilize) return result;

            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            int remaining = decision.ApprovedToday;
            if (manager != null)
            {
                manager.AdvanceServiceDays(party);
                manager.ReconcilePartyFiefTroops(party);
                foreach (ServingDetachmentRef item in manager
                    .GetServingDetachments(party)
                    .OrderByDescending(entry =>
                        entry.Detachment.ServiceDays >=
                        entry.Detachment.FreeWageDays)
                    .ThenByDescending(entry =>
                        entry.Detachment.ServiceDays)
                    .ThenBy(entry => entry.Detachment.BatchId)
                    .ToList())
                {
                    if (remaining <= 0) break;
                    if (item.Detachment == null ||
                        item.SourceFief?.OwnerClan != party.ActualClan)
                    {
                        continue;
                    }
                    FiefReturnResult returned = manager.TryReturnFiefTroops(
                        party,
                        item.SourceFief,
                        item.Detachment.BatchId,
                        remaining);
                    int actual = Math.Max(
                        0,
                        returned?.ActualReturnedCount ?? 0);
                    result.ReturnedFiefCount += actual;
                    remaining -= actual;
                }
            }

            PartyForceTargetResult current = ForceManagementFacade.Current
                .GetPartyTarget(party);
            int liveSurplus = current.Scenario == ForceScenario.Peace
                ? Math.Max(0, current.CurrentCount - current.TargetCount)
                : 0;
            remaining = Math.Min(remaining, liveSurplus);
            if (remaining > 0)
            {
                result.TransferredRegularCount = ForceManagementFacade
                    .Current.TroopTransfers.TransferFieldToGarrison(
                        party,
                        settlement,
                        remaining,
                        GarrisonDonationPurpose.PeaceDemobilization);
            }
            result.ReasonCode = result.ActualCount > 0
                ? "Completed"
                : "NoEligibleTroopsOrCapacity";
            return result;
        }
    }
}
