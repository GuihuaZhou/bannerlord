// Population boundary shared with ModifiedPolitics. ModifiedPolitics owns the
// authoritative Hearth ledger; ModifiedArmy consumes it through idempotent
// transactions and must never introduce a second population balance.
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Population.Contracts
{
    /// <summary>Persistent lifecycle states shared by all population mutations.</summary>
    public enum PopulationTransactionStatus { Rejected, Committed, Compensated, RecoveryRequired }

    /// <summary>
    /// Detached result returned by population evaluation, commit, and
    /// compensation operations. <see cref="Amount"/> retains the historical
    /// transaction meaning while <see cref="HearthConsumed"/> records the
    /// actual fractional Hearth cost of professional recruitment.
    /// </summary>
    public sealed class PopulationTransactionResult
    {
        public string OperationId { get; set; }
        public PopulationTransactionStatus Status { get; set; }
        public float Amount { get; set; }
        public float HearthBefore { get; set; }
        public float HearthAfter { get; set; }
        public float HearthConsumed { get; set; }
        public int ApprovedCount { get; set; }
        public string ReasonCode { get; set; }
        public bool IsCommitted => Status == PopulationTransactionStatus.Committed;
    }
    /// <summary>
    /// Authoritative cross-module population API. Implementations must re-read
    /// Hearth during commit and preserve operation-id idempotency.
    /// </summary>
    public interface ISettlementPopulationService
    {
        float GetHearth(Settlement settlement);
        float GetNaturalCapacity(Settlement settlement);
        int GetRecruitableHearth(Settlement settlement);
        int GetMaxAffordableProfessionalCount(Settlement settlement, CharacterObject troop, int requested);
        PopulationTransactionResult EvaluateProfessionalRecruitment(Settlement settlement, CharacterObject troop, int count);
        PopulationTransactionResult TryRecruitProfessional(Settlement settlement, CharacterObject troop, int count, string operationId);
        PopulationTransactionResult EvaluateConsumption(Settlement settlement, float amount);
        PopulationTransactionResult TryConsume(Settlement settlement, float amount, string operationId);
        PopulationTransactionResult TryRestore(Settlement settlement, float amount, string operationId);
        PopulationTransactionResult TryCompensate(Settlement settlement, string operationId);
        PopulationTransactionResult GetTransactionResult(string operationId);
        bool IsRecoveryRequired(Settlement settlement);
    }
    /// <summary>
    /// Side-effect-free professional population calculations shared by preview,
    /// offer aggregation, and transaction commit validation.
    /// </summary>
    public static class ProfessionalPopulationMath
    {
        private const double Epsilon = 0.0001d;
        /// <summary>
        /// Returns the requested count affordable above the protected civilian
        /// reserve. Fractional per-troop costs are intentionally preserved.
        /// </summary>
        public static int GetMaxAffordableCount(float hearth, float capacity, float reserveRatio, float cost, int requested)
        {
            if (!IsFiniteNonNegative(hearth) || !IsFiniteNonNegative(capacity) || reserveRatio < 0f || reserveRatio >= 1f || !IsFinitePositive(cost) || requested <= 0) return 0;
            double available = Math.Max(0d, hearth - capacity * reserveRatio);
            return (int)Math.Min(requested, Math.Max(0d, Math.Min(int.MaxValue, Math.Floor((available + Epsilon) / cost))));
        }
        /// <summary>Checks whether the exact batch leaves the reserve intact.</summary>
        public static bool CanRecruitExact(float hearth, float capacity, float reserveRatio, float cost, int count) =>
            count > 0 && IsFinitePositive(cost) && hearth - count * (double)cost + Epsilon >= capacity * reserveRatio;
        /// <summary>
        /// Maps the reserve-to-full-efficiency population interval continuously
        /// to [0, 1], clamping invalid or out-of-range campaign state safely.
        /// </summary>
        public static float CalculatePopulationEfficiency(float hearth, float capacity, float reserveRatio, float fullRatio)
        {
            if (!IsFiniteNonNegative(hearth) || capacity <= 0f || reserveRatio < 0f || fullRatio <= reserveRatio) return 0f;
            return Math.Max(0f, Math.Min(1f, (hearth / capacity - reserveRatio) / (fullRatio - reserveRatio)));
        }
        public static bool IsFiniteNonNegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        private static bool IsFinitePositive(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
    /// <summary>
    /// Campaign-scoped service locator cleared and re-registered at campaign
    /// lifecycle boundaries; it must not retain Settlement state itself.
    /// </summary>
    public static class SettlementPopulationServiceRegistry
    {
        public static ISettlementPopulationService Current { get; private set; }
        public static void Register(ISettlementPopulationService service) => Current = service ?? throw new ArgumentNullException(nameof(service));
        public static void Clear() => Current = null;
    }
}
