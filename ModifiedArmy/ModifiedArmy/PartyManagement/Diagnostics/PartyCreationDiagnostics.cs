using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace ModifiedArmy.PartyManagement.Diagnostics
{
    internal static class PartyCreationDiagnostics
    {
        public static void Log(Hero leader, MobileParty party,
            string settlementId, string phase, string reason,
            int garrison, int fief, int total, string source = "None")
        {
            if (!RecruitmentLogFilter.ShouldLog(leader?.Clan)) return;
            var message = GameTexts.FindText(
                "str_modifiedarmy_party_creation_transaction");
            message.SetTextVariable("CLAN_ID", leader?.Clan?.StringId ?? "none");
            message.SetTextVariable("LEADER_ID", leader?.StringId ?? "none");
            message.SetTextVariable("PARTY_ID", party?.StringId ?? "none");
            message.SetTextVariable("SETTLEMENT_ID", settlementId ?? "none");
            message.SetTextVariable("PHASE", phase ?? "Unknown");
            message.SetTextVariable("REASON", reason ?? "Unknown");
            message.SetTextVariable("GARRISON", garrison);
            message.SetTextVariable("FIEF", fief);
            message.SetTextVariable("TOTAL", total);
            message.SetTextVariable("SOURCE", source ?? "None");
            if (phase == "Failed" || phase == "Rejected")
                ModLogger.Warn(message.ToString());
            else
                ModLogger.Info(message.ToString());
        }
    }
}
