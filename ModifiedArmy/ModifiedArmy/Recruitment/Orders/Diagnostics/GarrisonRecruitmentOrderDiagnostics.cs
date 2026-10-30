using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Orders.Models;
using ModifiedArmy.Recruitment.Services;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Orders.Diagnostics
{
    internal static class GarrisonRecruitmentOrderDiagnostics
    {
        public static void LogPlan(
            string eventCode,
            Town town,
            GarrisonRecruitmentOrderRecord order,
            GarrisonStockPlan plan,
            ClanRecruitmentBudgetSnapshot budget,
            float outstanding,
            string reason,
            bool important)
        {
            if (!RecruitmentLogFilter.ShouldLog(town?.OwnerClan)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_garrison_order_plan");
            message.SetTextVariable("EVENT", eventCode ?? "Unknown");
            message.SetTextVariable("CLAN_ID", town.OwnerClan?.StringId ?? "");
            message.SetTextVariable("SETTLEMENT_ID", town.Settlement?.StringId ?? "");
            message.SetTextVariable("ORDER_ID", order?.OrderId ?? "");
            message.SetTextVariable("DAY", (int)TaleWorlds.CampaignSystem.CampaignTime.Now.ToDays);
            message.SetTextVariable("STOCK", plan?.StockOnHand ?? 0);
            message.SetTextVariable("TARGET", plan?.Target ?? 0);
            message.SetTextVariable("REORDER", plan?.ReorderPoint ?? 0);
            message.SetTextVariable("SAFE_CAPACITY", budget?.SafeCapacity ?? 0f);
            message.SetTextVariable("OUTSTANDING", outstanding);
            message.SetTextVariable("REMAINING", order?.Remaining ?? 0);
            message.SetTextVariable("REASON", reason ?? "None");
            if (important) ModLogger.Info(message.ToString());
            else ModLogger.Debug(message.ToString());
        }

        public static void LogExecution(
            Town town,
            GarrisonRecruitmentOrderRecord order,
            GarrisonRecruitmentExecutionResult result,
            float budgetRemaining)
        {
            if (!RecruitmentLogFilter.ShouldLog(town?.OwnerClan)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_garrison_order_execution");
            message.SetTextVariable("CLAN_ID", town.OwnerClan?.StringId ?? "");
            message.SetTextVariable("SETTLEMENT_ID", town.Settlement?.StringId ?? "");
            message.SetTextVariable("ORDER_ID", order?.OrderId ?? "");
            message.SetTextVariable("OPERATION_ID", result?.OperationId ?? "");
            message.SetTextVariable("BEFORE", order?.Remaining ?? 0);
            message.SetTextVariable("PROFESSIONAL", result?.ProfessionalRecruited ?? 0);
            message.SetTextVariable("PRISONER", result?.PrisonerRecruited ?? 0);
            message.SetTextVariable("AFTER", order == null ? 0 :
                System.Math.Max(0, order.Remaining - (result?.TotalCommitted ?? 0)));
            message.SetTextVariable("BUDGET", budgetRemaining);
            message.SetTextVariable("REASON", result?.FailureReason ?? "Committed");
            if (result?.RecoveryRequired == true) ModLogger.Warn(message.ToString());
            else if (result?.TotalCommitted > 0) ModLogger.Info(message.ToString());
            else ModLogger.Debug(message.ToString());
        }
    }
}
