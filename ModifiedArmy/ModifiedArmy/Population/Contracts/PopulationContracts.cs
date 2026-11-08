using System;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Population.Contracts
{
    public enum PopulationTransactionStatus { Rejected, Committed, Compensated, RecoveryRequired }
    public sealed class PopulationTransactionResult
    {
        public string OperationId { get; set; }
        public PopulationTransactionStatus Status { get; set; }
        public float Amount { get; set; }
        public float HearthBefore { get; set; }
        public float HearthAfter { get; set; }
        public float BurdenBefore { get; set; }
        public float BurdenAfter { get; set; }
        public int ApprovedCount { get; set; }
        public string ReasonCode { get; set; }
        public bool IsCommitted => Status == PopulationTransactionStatus.Committed;
    }
    public interface ISettlementPopulationService
    {
        float GetHearth(Settlement settlement);
        float GetNaturalCapacity(Settlement settlement);
        int GetRecruitableHearth(Settlement settlement);
        float GetRecruitmentBurden(Settlement settlement);
        float GetRecruitmentCapacity(Settlement settlement);
        float GetEffectiveHearth(Settlement settlement);
        int GetMaxAffordableProfessionalCount(Settlement settlement,
            int burdenCostPerTroop, int requested);
        PopulationTransactionResult EvaluateProfessionalRecruitment(
            Settlement settlement, int count, int burdenCostPerTroop);
        PopulationTransactionResult TryRecruitProfessional(
            Settlement settlement, int count, int burdenCostPerTroop,
            string operationId);
        PopulationTransactionResult EvaluateConsumption(
            Settlement settlement, float amount);
        PopulationTransactionResult TryConsume(Settlement settlement, float amount, string operationId);
        PopulationTransactionResult TryRestore(Settlement settlement, float amount, string operationId);
        PopulationTransactionResult TryCompensate(Settlement settlement, string operationId);
        PopulationTransactionResult GetTransactionResult(string operationId);
        bool IsRecoveryRequired(Settlement settlement);
    }

    public interface IRecruitmentBurdenPolicy
    {
        int GetDailyRecovery(Settlement settlement);
    }

    public static class RecruitmentBurdenPolicyRegistry
    {
        public static IRecruitmentBurdenPolicy Current { get; private set; }
        public static void Register(IRecruitmentBurdenPolicy policy) =>
            Current = policy ?? throw new ArgumentNullException(nameof(policy));
        public static void Clear() => Current = null;
    }

    public static class RecruitmentBurdenMath
    {
        private const double Epsilon = 0.0001d;

        public static int GetMaxAffordableCount(float hearth, float burden,
            int burdenCostPerTroop, int requested)
        {
            if (!IsFiniteNonNegative(hearth) ||
                !IsFiniteNonNegative(burden) ||
                burdenCostPerTroop <= 0 || requested <= 0)
                return 0;
            double capacity = (double)hearth - burden;
            if (capacity + Epsilon < 1d) return 0;
            double raw = 1d + Math.Floor(
                (capacity - 1d + Epsilon) /
                (1d + burdenCostPerTroop));
            int approved = (int)Math.Min(requested,
                Math.Max(0d, Math.Min(int.MaxValue, raw)));
            while (approved > 0 && !CanRecruitExact(
                hearth, burden, burdenCostPerTroop, approved)) approved--;
            while (approved < requested && CanRecruitExact(
                hearth, burden, burdenCostPerTroop, approved + 1)) approved++;
            return approved;
        }

        public static bool CanRecruitExact(float hearth, float burden,
            int burdenCostPerTroop, int count)
        {
            if (!IsFiniteNonNegative(hearth) ||
                !IsFiniteNonNegative(burden) ||
                burdenCostPerTroop <= 0 || count <= 0) return false;
            double currentHearth = hearth;
            double currentBurden = burden;
            for (int i = 0; i < count; i++)
            {
                if (currentHearth - currentBurden + Epsilon < 1d)
                    return false;
                currentHearth -= 1d;
                currentBurden += burdenCostPerTroop;
            }
            return currentHearth >= -Epsilon;
        }

        public static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
    public static class SettlementPopulationServiceRegistry
    {
        public static ISettlementPopulationService Current { get; private set; }
        public static void Register(ISettlementPopulationService service) { Current = service ?? throw new ArgumentNullException(nameof(service)); }
        public static void Clear() { Current = null; }
    }
}
