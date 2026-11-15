// Shadow dry-run diagnostics. Captures authoritative campaign resources before
// and after preview without changing creation, roster, Hearth, budget, or journal ownership.
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.PartyManagement.Diagnostics
{
    internal static class PartyCreationDryRunDiagnostics
    {
        internal sealed class ResourceSnapshot
        {
            public string Fingerprint { get; set; }
            public float PurchaseBudget { get; set; }
            public float CommitmentBudget { get; set; }
            public int ClanGold { get; set; }
            public int LeaderGold { get; set; }
            public string GovernorSettlementId { get; set; }

            public bool SameAs(ResourceSnapshot other) => other != null &&
                Fingerprint == other.Fingerprint &&
                PurchaseBudget.Equals(other.PurchaseBudget) &&
                CommitmentBudget.Equals(other.CommitmentBudget) &&
                ClanGold == other.ClanGold && LeaderGold == other.LeaderGold &&
                GovernorSettlementId == other.GovernorSettlementId;
        }

        internal static ResourceSnapshot Capture(
            Hero leader,
            Settlement settlement)
        {
            Clan clan = leader?.Clan;
            ClanRecruitmentBudgetSnapshot budget =
                ClanRecruitmentBudgetManager.GetSnapshot(clan);
            var builder = new StringBuilder();
            AppendRoster(builder, "garrison",
                settlement?.Town?.GarrisonParty?.MemberRoster);
            FiefPartyData fief = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetFiefData(settlement);
            foreach (VillageFiefData village in fief?.OrderedVillages() ??
                Enumerable.Empty<VillageFiefData>())
            {
                Settlement source = village.VillageSettlement;
                builder.Append("|v=").Append(source?.StringId ?? "none")
                    .Append(",hearth=").Append(source?.Village?.Hearth ?? 0f)
                    .Append(",mobilized=").Append(village.MobilizedHearth)
                    .Append(",recovery=").Append(village.RecoveryRequired)
                    .Append(",batches=");
                foreach (FiefServiceBatch batch in (village.ServiceBatches ??
                    new List<FiefServiceBatch>()).OrderBy(item =>
                        item?.BatchId, StringComparer.Ordinal))
                    builder.Append(batch?.BatchId ?? "null").Append(';');
                AppendRoster(builder, "ready", village.GetReadyRoster());
            }
            return new ResourceSnapshot
            {
                Fingerprint = builder.ToString(),
                PurchaseBudget = budget.RemainingPurchaseBudget,
                CommitmentBudget = budget.RemainingCommitmentBudget,
                ClanGold = clan?.Gold ?? 0,
                LeaderGold = leader?.Gold ?? 0,
                GovernorSettlementId = leader?.GovernorOf?.StringId
            };
        }

        internal static string GetPlanFingerprint(
            InitialMobilizationPreview preview)
        {
            if (preview == null) return "null";
            var builder = new StringBuilder();
            builder.Append("approved=").Append(preview.Approved)
                .Append("|reason=").Append(preview.RejectReason ?? "none")
                .Append("|limit=").Append(preview.AssumedPartyLimit)
                .Append("|target=").Append(preview.TargetCount)
                .Append("|payment=").Append(preview.PaymentLimit)
                .Append("|fief=");
            foreach (PlannedVillageWithdrawal item in
                (preview.FiefByVillage ?? Array.Empty<PlannedVillageWithdrawal>())
                .OrderBy(item => item.Village?.StringId, StringComparer.Ordinal)
                .ThenBy(item => item.Troop?.StringId, StringComparer.Ordinal))
                builder.Append(item.Village?.StringId ?? "none").Append('/')
                    .Append(item.Troop?.StringId ?? "none").Append('=')
                    .Append(item.Count).Append(';');
            builder.Append("|garrison=");
            IEnumerable<KeyValuePair<CharacterObject, TroopExchangeAmount>>
                garrison = preview.GarrisonManifest?.Troops ??
                    Enumerable.Empty<KeyValuePair<CharacterObject,
                        TroopExchangeAmount>>();
            foreach (var item in garrison.OrderBy(item =>
                        item.Key?.StringId, StringComparer.Ordinal))
                builder.Append(item.Key?.StringId ?? "none").Append('=')
                    .Append(item.Value?.Count ?? 0).Append('/')
                    .Append(item.Value?.WoundedCount ?? 0).Append(';');
            builder.Append("|source=").Append(preview.SourceFingerprint ?? "none");
            return builder.ToString();
        }

        internal static void Log(
            Hero leader,
            Settlement settlement,
            string attemptId,
            InitialMobilizationPreview preview,
            bool? deterministic,
            bool noMutation,
            ResourceSnapshot before,
            ResourceSnapshot after,
            string failure = null)
        {
            string text = string.Format(
                "[PartyCreationDryRun] Leader={0} LeaderId={1} Clan={2} " +
                "ClanId={3} Site={4} SiteId={5} AttemptId={6} " +
                "Approved={7} RejectReason={8} AssumedLimit={9} " +
                "TargetCount={10} PaymentLimit={11} PreviewFief={12} " +
                "PreviewGarrison={13} PreviewTotal={14} MinimumRegulars={15} " +
                "Deterministic={16} NoMutation={17} BudgetBefore={18}/{19} " +
                "BudgetAfter={20}/{21} Failure={22} PlanFingerprint=\"{23}\"",
                leader?.Name?.ToString() ?? "unknown",
                leader?.StringId ?? "none",
                leader?.Clan?.Name?.ToString() ?? "unknown",
                leader?.Clan?.StringId ?? "none",
                settlement?.Name?.ToString() ?? "unknown",
                settlement?.StringId ?? "none",
                attemptId ?? "none",
                preview?.Approved == true,
                preview?.RejectReason ?? "none",
                preview?.AssumedPartyLimit ?? 0,
                preview?.TargetCount ?? 0,
                preview?.PaymentLimit ?? 0,
                preview?.ApprovedFiefCount ?? 0,
                preview?.ApprovedGarrisonCount ?? 0,
                preview?.ApprovedTotal ?? 0,
                preview?.RequiredRegulars ?? 0,
                deterministic.HasValue
                    ? deterministic.Value.ToString()
                    : "NotChecked",
                noMutation,
                before?.PurchaseBudget ?? 0f,
                before?.CommitmentBudget ?? 0f,
                after?.PurchaseBudget ?? 0f,
                after?.CommitmentBudget ?? 0f,
                failure ?? "none",
                GetPlanFingerprint(preview));
            if (!noMutation || !string.IsNullOrEmpty(failure))
                ModLogger.Error(text);
            else
                ModLogger.Info(text);
        }

        private static void AppendRoster(
            StringBuilder builder,
            string name,
            TroopRoster roster)
        {
            builder.Append('|').Append(name).Append('=');
            if (roster == null) return;
            foreach (TroopRosterElement item in roster.GetTroopRoster()
                .Where(item => item.Character != null)
                .OrderBy(item => item.Character.StringId,
                    StringComparer.Ordinal))
                builder.Append(item.Character.StringId).Append(':')
                    .Append(item.Number).Append('/')
                    .Append(item.WoundedNumber).Append(';');
        }
    }
}
