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
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Services
{
    /// <summary>Produces deterministic manifests without creating a party.</summary>
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
            int requiredRegulars)
        {
            var result = new InitialMobilizationPreview
            {
                AttemptId = attemptId,
                Leader = leader,
                Clan = clan,
                BaseSettlement = settlement,
                RequiredRegulars = Math.Max(1, requiredRegulars),
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
            result.AssumedPartyLimit = subject.PartySizeLimit;
            result.TargetCount = subject.TargetCount;
            result.PaymentLimit = subject.PaymentLimit;
            if (subject.PartySizeLimit - subject.CurrentMemberCount <
                result.RequiredRegulars)
                return Reject(result, "PartyLimitBelowMinimum");
            if (subject.TargetCount - subject.CurrentMemberCount <
                result.RequiredRegulars)
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
            result.SourceFingerprint = BuildSourceFingerprint(
                settlement, result.FiefByVillage);
            if (result.ApprovedTotal < result.RequiredRegulars)
                return Reject(result, result.ApprovedTotal == 0 &&
                    !string.IsNullOrEmpty(fiefReason)
                    ? fiefReason
                    : "InsufficientApprovedRegulars");
            return result;
        }

        internal static string BuildSourceFingerprint(
            Settlement settlement,
            IEnumerable<PlannedVillageWithdrawal> villages)
        {
            var builder = new StringBuilder();
            AppendRoster(builder, "g", settlement?.Town?.GarrisonParty?
                .MemberRoster);
            foreach (Settlement village in (villages ??
                Enumerable.Empty<PlannedVillageWithdrawal>())
                .Select(item => item.Village).Where(item => item != null)
                .Distinct().OrderBy(item => item.StringId,
                    StringComparer.Ordinal))
            {
                builder.Append("|v:").Append(village.StringId)
                    .Append(':').Append((int)Math.Floor(
                        village.Village?.Hearth ?? 0f)).Append(':');
                AppendRoster(builder, "r", village.MilitiaPartyComponent?
                    .MobileParty?.MemberRoster);
            }
            return builder.ToString();
        }

        private static void AppendRoster(
            StringBuilder builder,
            string prefix,
            TroopRoster roster)
        {
            builder.Append('|').Append(prefix).Append(':');
            if (roster == null) return;
            foreach (TroopRosterElement item in roster.GetTroopRoster()
                .Where(item => item.Character != null)
                .OrderBy(item => item.Character.StringId,
                    StringComparer.Ordinal))
                builder.Append(item.Character.StringId).Append('=')
                    .Append(item.Number).Append('/')
                    .Append(item.WoundedNumber).Append(';');
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
