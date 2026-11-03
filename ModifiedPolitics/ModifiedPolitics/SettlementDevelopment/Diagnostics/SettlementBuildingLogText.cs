using TaleWorlds.Localization;

namespace ModifiedPolitics.SettlementDevelopment.Diagnostics
{
    internal static class SettlementBuildingLogText
    {
        internal static string BuildDecision(TextObject settlement, TextObject building)
        {
            TextObject text = new TextObject(
                "{=MP_BuildingDecision}[Construction] {SETTLEMENT} selected {BUILDING}.");
            text.SetTextVariable("SETTLEMENT", settlement);
            text.SetTextVariable("BUILDING", building);
            return text.ToString();
        }

        internal static string RecoveryFailed(TextObject settlement)
        {
            TextObject text = new TextObject(
                "{=MP_BuildingRecoveryFailed}[Construction] {SETTLEMENT} has no queued building or available daily project; recovery failed.");
            text.SetTextVariable("SETTLEMENT", settlement);
            return text.ToString();
        }

        internal static string RecoveryStillNull(TextObject settlement, TextObject project)
        {
            TextObject text = new TextObject(
                "{=MP_BuildingRecoveryStillNull}[Construction] {SETTLEMENT} selected daily project {PROJECT}, but its current building is still missing.");
            text.SetTextVariable("SETTLEMENT", settlement);
            text.SetTextVariable("PROJECT", project);
            return text.ToString();
        }

        internal static string Recovered(TextObject settlement, TextObject project)
        {
            TextObject text = new TextObject(
                "{=MP_BuildingRecovered}[Construction] {SETTLEMENT} restored its missing current building with daily project {PROJECT}.");
            text.SetTextVariable("SETTLEMENT", settlement);
            text.SetTextVariable("PROJECT", project);
            return text.ToString();
        }

        internal static string BoostInvested(
            TextObject settlement,
            TextObject clan,
            int amount,
            int goldBefore,
            int goldAfter,
            int reserve)
        {
            TextObject text = new TextObject(
                "{=MP_BuildingBoostInvested}[Construction] {CLAN} invested {AMOUNT} in construction at {SETTLEMENT}; treasury {GOLD_BEFORE} -> {GOLD_AFTER}; reserve {RESERVE}.");
            text.SetTextVariable("SETTLEMENT", settlement);
            text.SetTextVariable("CLAN", clan);
            text.SetTextVariable("AMOUNT", amount);
            text.SetTextVariable("GOLD_BEFORE", goldBefore);
            text.SetTextVariable("GOLD_AFTER", goldAfter);
            text.SetTextVariable("RESERVE", reserve);
            return text.ToString();
        }

        internal static string ReconciliationSummary(
            int checkedCount,
            int repairedCount,
            int failedCount)
        {
            TextObject text = new TextObject(
                "{=MP_BuildingReconciliationSummary}[Construction] Startup reconciliation checked {CHECKED} settlements, repaired {REPAIRED}, and failed {FAILED}.");
            text.SetTextVariable("CHECKED", checkedCount);
            text.SetTextVariable("REPAIRED", repairedCount);
            text.SetTextVariable("FAILED", failedCount);
            return text.ToString();
        }
    }
}
