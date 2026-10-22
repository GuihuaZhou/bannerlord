using ModifiedArmy.ForceManagement.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.ForceManagement.Models
{
    /// <summary>
    /// Pure wartime rest policy. It ranks real fief batches but never routes
    /// or transfers soldiers, and remains conservatively disabled by default.
    /// </summary>
    public sealed class WarRestPolicyModel : IWarRestPolicyModel
    {
        /// <summary>Evaluates hard gates, urgency, safe floor, and daily quota.</summary>
        public WarRestDecision Evaluate(WarRestContext context)
        {
            WarRestDecision result = new WarRestDecision
            {
                MilitarySafeFloor = Math.Max(
                    1,
                    context?.ForceTarget?.TargetCount ?? 0),
                DestinationOptions = context?.DestinationOptions ??
                    Array.Empty<PartyForceCandidate>()
            };
            string hardRejection = GetHardRejection(context);
            if (hardRejection != null)
            {
                result.ReasonCode = hardRejection;
                return result;
            }

            WarRestPolicySettings settings = context.Settings;
            result.UrgencyScore = CalculateUrgency(context, settings);
            if (!settings.Enabled)
            {
                result.ReasonCode = "AutomaticWarRestDisabled";
                return result;
            }
            if (result.UrgencyScore < settings.MinimumUrgencyScore)
            {
                result.ReasonCode = "WarRestUrgencyBelowThreshold";
                return result;
            }

            int militarySurplus = Math.Max(
                0,
                context.ForceTarget.CurrentCount - result.MilitarySafeFloor);
            int quota = Math.Max(
                0,
                settings.MaximumDailyCount - context.CompletedToday);
            int remaining = Math.Min(militarySurplus, quota);
            if (remaining <= 0)
            {
                result.ReasonCode = militarySurplus <= 0
                    ? "NoWartimeSafeSurplus"
                    : "WarRestDailyQuotaReached";
                return result;
            }

            var approvals = new List<FiefBatchTransferApproval>();
            foreach (WarRestFiefBatchCandidate batch in context.FiefBatches
                .Where(item => item?.IsSafe == true &&
                    item.AvailableCount > 0 && item.SourceFief != null)
                .OrderByDescending(item =>
                    item.ServiceDays >= item.FreeWageDays)
                .ThenByDescending(item => item.ServiceDays)
                .ThenBy(item => item.Distance)
                .ThenBy(item => item.SourceFief.StringId,
                    StringComparer.Ordinal)
                .ThenBy(item => item.BatchId, StringComparer.Ordinal))
            {
                int approved = Math.Min(remaining, batch.AvailableCount);
                FiefBatchTransferApproval item = BuildApproval(batch, approved);
                if (item.ApprovedCount <= 0) continue;
                approvals.Add(item);
                remaining -= item.ApprovedCount;
                if (remaining <= 0) break;
            }
            result.CandidateFiefBatches = approvals;
            result.ApprovedReturnCount = approvals.Sum(item =>
                item.ApprovedCount);
            result.IsApproved = result.ApprovedReturnCount > 0;
            result.ReasonCode = result.IsApproved
                ? "WarRestApproved"
                : "NoSafeFiefReturnBatch";
            return result;
        }

        /// <summary>Rejects unsafe or non-wartime contexts before scoring.</summary>
        private static string GetHardRejection(WarRestContext context)
        {
            if (context?.Party == null || !context.Party.IsActive)
                return "InvalidParty";
            if (!context.IsAtFormalWar)
                return "WarRestRequiresFormalWar";
            if (!context.IsOwnerValid || context.Party.ActualClan == null)
                return "InvalidOwner";
            if (context.IsMilitaryPriority || context.Party.Army != null ||
                context.Party.MapEvent != null ||
                context.Party.TargetParty != null ||
                context.Party.ShortTermTargetParty != null)
                return "HigherPriorityMilitaryTask";
            if (context.ForceTarget == null ||
                context.ForceTarget.TargetCount <= 0)
                return "MissingMilitarySafeFloor";
            if (context.Finance == null)
                return "MissingFinanceSnapshot";
            if (context.FiefBatches == null || context.FiefBatches.Count == 0)
                return "NoServingFiefBatch";
            if (context.Settings == null)
                return "MissingWarRestSettings";
            return null;
        }

        /// <summary>Combines disposition, finance, and real service pressure.</summary>
        private static float CalculateUrgency(
            WarRestContext context,
            WarRestPolicySettings settings)
        {
            float dispositionPressure = context.ForceTarget
                    .IsAggressiveWarClan
                ? 0f
                : Math.Max(0f, Math.Min(1f,
                    (100f - context.ForceTarget.WarDisposition) / 200f));
            float wage = Math.Max(0f, context.Finance.CurrentDailyWage);
            float funds = Math.Max(0f, context.Finance.OperatingFunds);
            float financePressure = wage <= 0f
                ? 0f
                : 1f - Math.Min(1f, funds / (wage * 30f));
            float servicePressure = context.FiefBatches.Max(batch =>
            {
                if (batch == null) return 0f;
                int free = Math.Max(1, batch.FreeWageDays);
                return Math.Min(1f, (float)batch.ServiceDays / free);
            });
            float totalWeight = Math.Max(
                0.0001f,
                settings.DispositionWeight + settings.FinanceWeight +
                    settings.ServiceWeight);
            return (dispositionPressure * settings.DispositionWeight +
                financePressure * settings.FinanceWeight +
                servicePressure * settings.ServiceWeight) / totalWeight;
        }

        /// <summary>Creates an exact manifest bounded to one source batch.</summary>
        private static FiefBatchTransferApproval BuildApproval(
            WarRestFiefBatchCandidate batch,
            int maximumCount)
        {
            var result = new FiefBatchTransferApproval
            {
                SourceFief = batch.SourceFief,
                BatchId = batch.BatchId
            };
            int remaining = Math.Max(0, maximumCount);
            IEnumerable<KeyValuePair<CharacterObject, int>> troops =
                batch.Troops == null
                    ? Enumerable.Empty<KeyValuePair<CharacterObject, int>>()
                    : batch.Troops;
            foreach (var pair in troops
                .OrderBy(item => item.Key?.StringId, StringComparer.Ordinal))
            {
                int count = Math.Min(remaining, Math.Max(0, pair.Value));
                if (pair.Key == null || count <= 0) continue;
                result.Troops.Troops[pair.Key] = new TroopExchangeAmount
                {
                    Count = count,
                    WoundedCount = 0
                };
                remaining -= count;
                if (remaining <= 0) break;
            }
            result.ApprovedCount = result.Troops.TotalCount;
            return result;
        }
    }
}
