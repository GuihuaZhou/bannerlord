using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Preferences.Diagnostics
{
    internal static class ProfessionalPreferenceDiagnostics
    {
        public static void LogReconciled(Settlement settlement, string reason,
            int beforeCount, int afterCount, int added, int removed)
        {
            if (!RecruitmentLogFilter.ShouldLog(settlement?.OwnerClan)) return;
            var message = GameTexts.FindText(
                "str_modifiedarmy_professional_preference_reconciled");
            message.SetTextVariable("SETTLEMENT_ID", settlement?.StringId ?? "none");
            message.SetTextVariable("REASON", reason ?? "Unknown");
            message.SetTextVariable("BEFORE", beforeCount);
            message.SetTextVariable("AFTER", afterCount);
            message.SetTextVariable("ADDED", added);
            message.SetTextVariable("REMOVED", removed);
            ModLogger.Info(message.ToString());
        }

        public static void LogWriteRejected(Settlement settlement, string reason)
        {
            if (!RecruitmentLogFilter.ShouldLog(settlement?.OwnerClan)) return;
            var message = GameTexts.FindText(
                "str_modifiedarmy_professional_preference_rejected");
            message.SetTextVariable("SETTLEMENT_ID", settlement?.StringId ?? "none");
            message.SetTextVariable("REASON", reason ?? "Unknown");
            ModLogger.Warn(message.ToString());
        }
    }
}
