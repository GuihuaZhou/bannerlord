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
        public string ReasonCode { get; set; }
        public bool IsCommitted => Status == PopulationTransactionStatus.Committed;
    }
    public interface ISettlementPopulationService
    {
        float GetHearth(Settlement settlement);
        float GetNaturalCapacity(Settlement settlement);
        int GetRecruitableHearth(Settlement settlement);
        PopulationTransactionResult EvaluateConsumption(
            Settlement settlement, float amount);
        PopulationTransactionResult TryConsume(Settlement settlement, float amount, string operationId);
        PopulationTransactionResult TryRestore(Settlement settlement, float amount, string operationId);
        PopulationTransactionResult TryCompensate(Settlement settlement, string operationId);
        PopulationTransactionResult GetTransactionResult(string operationId);
        bool IsRecoveryRequired(Settlement settlement);
    }
    public static class SettlementPopulationServiceRegistry
    {
        public static ISettlementPopulationService Current { get; private set; }
        public static void Register(ISettlementPopulationService service) { Current = service ?? throw new ArgumentNullException(nameof(service)); }
        public static void Clear() { Current = null; }
    }
}
