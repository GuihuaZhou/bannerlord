// Read-only initial mobilization planning. Fief and garrison admissions share
// one local simulation; authoritative campaign resources remain untouched.
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Models;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyManagement.Bindings;
using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Services
{
    /// <summary>Evaluates creation eligibility without reserving resources.</summary>
    public sealed class InitialPartyMobilizationPlanner
    {
        private readonly PlannedLordPartySubjectFactory _subjects =
            new PlannedLordPartySubjectFactory();

        public InitialMobilizationPreview DryRun(
            Hero leader,
            Clan clan,
            Settlement settlement,
            PendingPartyCreation reservation,
            string attemptId,
            int requestedMinimumPartyMembers)
        {
            int minimumPartyMembers = Math.Max(
                1, requestedMinimumPartyMembers);
            var result = new InitialMobilizationPreview
            {
                AttemptId = attemptId,
                Leader = leader,
                Clan = clan,
                BaseSettlement = settlement,
                MinimumPartyMembers = minimumPartyMembers,
                FiefByVillage = Array.Empty<PlannedVillageWithdrawal>(),
                GarrisonManifest = new TroopExchangeManifest()
            };
            PartySettlementBindingBehavior bindings = Campaign.Current?
                .GetCampaignBehavior<PartySettlementBindingBehavior>();
            if (bindings?.IsCreationReservationValid(
                    reservation, leader, clan, settlement) != true)
                return Reject(result, "NoCreationBinding");
            RecruitmentSubject subject = _subjects.Create(
                leader, clan, settlement);
            if (subject == null)
                return Reject(result, "InvalidPlannedParty");
            result.InitialMemberCount = subject.CurrentMemberCount;
            result.AssumedPartyLimit = subject.PartySizeLimit;
            result.TargetCount = subject.TargetCount;
            result.PaymentLimit = subject.PaymentLimit;
            if (subject.PartySizeLimit < minimumPartyMembers)
                return Reject(result, "PartyLimitBelowMinimum");
            if (subject.TargetCount < minimumPartyMembers)
                return Reject(result, "TargetBelowMinimum");

            FiefPartyData fief = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetFiefData(settlement);
            RecruitmentSimulationState state =
                RecruitmentSimulationState.Create(subject);
            string fiefReason = null;
            if (fief != null)
            {
                result.FiefByVillage = fief.PreviewPlannedFiefRecruitment(
                    subject,
                    state,
                    subject.TargetCount,
                    out RecruitmentSimulationState accepted,
                    out fiefReason);
                if (result.FiefByVillage.Count > 0) state = accepted;
            }
            result.ApprovedFiefCount = result.FiefByVillage.Sum(item =>
                Math.Max(0, item.Count));
            int remaining = Math.Max(
                0,
                subject.TargetCount - state.ProjectedMemberCount);
            if (remaining > 0 && ForceManagementFacade.Current.PartyExchange
                is PartyExchangeModel exchange)
            {
                result.GarrisonManifest = exchange
                    .BuildGarrisonWithdrawalPreview(
                        subject, settlement, state, remaining);
            }
            result.ApprovedGarrisonCount =
                result.GarrisonManifest.TotalCount;
            if (result.ApprovedTotal < result.RequiredNewTroops)
                return Reject(result, result.ApprovedTotal == 0 &&
                    !string.IsNullOrEmpty(fiefReason)
                    ? fiefReason
                    : "InsufficientApprovedRegulars");
            return result;
        }

        private static InitialMobilizationPreview Reject(
            InitialMobilizationPreview preview,
            string reason)
        {
            preview.RejectReason = reason;
            return preview;
        }
    }
}
