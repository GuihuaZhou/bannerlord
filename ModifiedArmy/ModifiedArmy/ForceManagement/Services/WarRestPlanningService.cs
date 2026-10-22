using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyFinance.Models;
using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Captures authoritative wartime-rest inputs and runtime daily accounting.
    /// It does not issue routes or execute returns.
    /// </summary>
    public sealed class WarRestPlanningService
    {
        private readonly Dictionary<string, DailyWarRestProgress> _progress =
            new Dictionary<string, DailyWarRestProgress>(StringComparer.Ordinal);

        /// <summary>Builds a pure policy context from real service batches.</summary>
        public WarRestContext BuildContext(
            MobileParty party,
            PartyForceTargetResult target,
            bool isMilitaryPriority)
        {
            int today = CurrentDay();
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            List<WarRestFiefBatchCandidate> batches = manager == null
                ? new List<WarRestFiefBatchCandidate>()
                : manager.GetServingDetachments(party)
                    .Where(item => item.Detachment != null &&
                        !item.Detachment.IsEmpty())
                    .Select(item => BuildBatchCandidate(party, item))
                    .ToList();
            return new WarRestContext
            {
                Party = party,
                ForceTarget = target,
                IsAtFormalWar = ForceManagementFacade.IsAtFormalWar(
                    party?.ActualClan),
                IsOwnerValid = party?.ActualClan != null &&
                    party.LeaderHero?.Clan == party.ActualClan,
                IsMilitaryPriority = isMilitaryPriority,
                Finance = new ClanFinanceSnapshot
                {
                    Clan = party?.ActualClan,
                    OperatingFunds = RecruitmentBudget.GetOperatingFunds(party),
                    CurrentDailyWage = party == null
                        ? 0f
                        : AiRecruitmentFinancialModel
                            .GetLongTermDailyWage(party),
                    AvailableRecruitmentBudget = party == null
                        ? 0f
                        : AiRecruitmentFinancialModel
                            .GetAvailableAdditionalDailyWage(party)
                },
                Settings = new WarRestPolicySettings
                {
                    Enabled = false
                },
                FiefBatches = batches,
                DestinationOptions = batches
                    .Where(item => item.IsSafe)
                    .Select(item => new PartyForceCandidate
                    {
                        Settlement = item.SourceFief,
                        SourceKind = PartyForceSourceKind.Fief,
                        AvailableCount = item.AvailableCount,
                        GapCovered = item.AvailableCount,
                        Distance = item.Distance,
                        IsSafe = true,
                        ReasonCode = "RealFiefBatchSource",
                        Score = GetBatchDestinationScore(item)
                    })
                    .ToList(),
                CompletedToday = GetCompletedToday(party, today),
                Today = today
            };
        }

        /// <summary>Records only soldiers successfully returned by the executor.</summary>
        public void RecordCompleted(MobileParty party, int actualCount)
        {
            if (party == null || actualCount <= 0) return;
            int today = CurrentDay();
            string key = party.StringId ?? string.Empty;
            if (!_progress.TryGetValue(key, out DailyWarRestProgress item) ||
                item.Day != today)
            {
                item = new DailyWarRestProgress { Day = today };
                _progress[key] = item;
            }
            item.CompletedCount += actualCount;
        }

        /// <summary>Reports whether a successful return blocks same-day recruitment.</summary>
        public bool ShouldSuppressRecruitment(MobileParty party) =>
            GetCompletedToday(party, CurrentDay()) > 0;

        /// <summary>Clears stale runtime progress when war or ownership ends.</summary>
        public void Cancel(MobileParty party)
        {
            if (party?.StringId != null) _progress.Remove(party.StringId);
        }

        /// <summary>
        /// Creates an exact phase-two return request only at the real source
        /// fief; callers still must ask PartyExchangeModel for final approval.
        /// </summary>
        public PartyExchangeContext BuildReturnExchangeContext(
            MobileParty party,
            WarRestDecision decision,
            FiefBatchTransferApproval approval,
            string operationId)
        {
            if (party == null || decision?.IsApproved != true ||
                approval?.SourceFief == null ||
                party.CurrentSettlement != approval.SourceFief)
                return null;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null) return null;
            PartyTroopSource source = new PartyTroopSource
            {
                Kind = PartyTroopSourceKind.Mobile,
                Party = party,
                Settlement = approval.SourceFief,
                BatchId = approval.BatchId,
                Roster = party.MemberRoster
            };
            PartyTroopSource destination = new PartyTroopSource
            {
                Kind = PartyTroopSourceKind.Fief,
                Settlement = approval.SourceFief,
                Roster = manager.GetFiefTroopRoster(approval.SourceFief)
            };
            PartyExchangeService runner = ForceManagementFacade.Current
                .PartyExchangeRunner;
            return new PartyExchangeContext
            {
                OperationId = operationId,
                Initiator = party,
                Operation = PartyExchangeOperation.FiefReturn,
                Intent = PartyForceIntent.WarRest,
                Purpose = PartyExchangePurpose.WarRest,
                Source = source,
                Destination = destination,
                TargetSettlement = approval.SourceFief,
                SourceSnapshot = runner.CaptureSnapshot(source),
                DestinationSnapshot = runner.CaptureSnapshot(destination),
                RequestedCount = approval.ApprovedCount,
                Today = CurrentDay()
            };
        }

        /// <summary>Creates one candidate bound to its true source fief.</summary>
        private static WarRestFiefBatchCandidate BuildBatchCandidate(
            MobileParty party,
            ServingDetachmentRef item)
        {
            bool sameClan = item.SourceFief?.OwnerClan != null &&
                item.SourceFief.OwnerClan == party?.ActualClan;
            bool threatened = sameClan &&
                (TransportThreatModel.HasSuperiorEnemy(
                    party,
                    party.Position,
                    party.MapFaction) ||
                 TransportThreatModel.HasSuperiorEnemy(
                    party,
                    item.SourceFief.GatePosition,
                    party.MapFaction));
            bool safe = sameClan && !item.SourceFief.IsUnderSiege &&
                !threatened;
            return new WarRestFiefBatchCandidate
            {
                SourceFief = item.SourceFief,
                BatchId = item.Detachment.BatchId,
                AvailableCount = item.Detachment.GetTotalCount(),
                ServiceDays = item.Detachment.ServiceDays,
                FreeWageDays = item.Detachment.FreeWageDays,
                Troops = new Dictionary<CharacterObject, int>(
                    item.Detachment.Troops),
                Distance = item.SourceFief == null || party == null
                    ? float.MaxValue
                    : party.Position.Distance(item.SourceFief.GatePosition),
                IsSafe = safe,
                ReasonCode = !sameClan
                    ? "SourceFiefOwnershipChanged"
                    : threatened
                        ? "SuperiorEnemyOnRoute"
                        : safe
                        ? "SafeSourceFief"
                        : "SourceFiefUnderSiege"
            };
        }

        /// <summary>Ranks overdue nearby batches without merging destinations.</summary>
        private static float GetBatchDestinationScore(
            WarRestFiefBatchCandidate item)
        {
            float overdue = item.ServiceDays >= item.FreeWageDays ? 100f : 0f;
            return overdue + item.ServiceDays - item.Distance * 0.05f;
        }

        /// <summary>Reads today's actual completed count.</summary>
        private int GetCompletedToday(MobileParty party, int today)
        {
            string key = party?.StringId;
            return key != null && _progress.TryGetValue(
                key,
                out DailyWarRestProgress item) && item.Day == today
                    ? Math.Max(0, item.CompletedCount)
                    : 0;
        }

        /// <summary>Returns the current campaign day.</summary>
        private static int CurrentDay() => Campaign.Current == null
            ? 0
            : (int)Math.Floor(CampaignTime.Now.ToDays);

        private sealed class DailyWarRestProgress
        {
            public int Day { get; set; }
            public int CompletedCount { get; set; }
        }
    }
}
