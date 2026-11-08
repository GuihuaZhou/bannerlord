using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.PartyManagement.Diagnostics
{
    internal static class PartyCreationDiagnostics
    {
        public static void Log(Hero leader, MobileParty party,
            string settlementId, string phase, string reason,
            int garrison, int fief, int total, string source = "None",
            string attemptId = null, int minimumRequired = 0,
            int minimumFiefBatch = 0, bool bindingEligible = false,
            string taskId = null, string operationId = null,
            int actualRecruited = 0, int actualFief = 0,
            int actualProfessional = 0, int finalRegulars = 0,
            string finalState = null)
        {
            bool playerContext = RecruitmentLogFilter.ShouldLog(leader?.Clan);
            bool significantFailure = phase == "RecoveryRequired" ||
                phase == "Failed" || phase == "Rejected" ||
                phase == "Completed" && finalRegulars > 0 &&
                finalRegulars < 50;
            if (!playerContext && !significantFailure) return;
            var message = GameTexts.FindText(
                "str_modifiedarmy_party_creation_transaction");
            Settlement settlement = Settlement.All.FirstOrDefault(item =>
                item?.StringId == settlementId);
            message.SetTextVariable("CLAN_NAME",
                PartyLogFormatter.GetClanDisplayName(leader?.Clan));
            message.SetTextVariable("CLAN_ID", leader?.Clan?.StringId ?? "none");
            message.SetTextVariable("LEADER_NAME",
                leader?.Name ?? new TaleWorlds.Localization.TextObject("unknown"));
            message.SetTextVariable("LEADER_ID", leader?.StringId ?? "none");
            message.SetTextVariable("PARTY_NAME",
                party != null
                    ? PartyLogFormatter.GetDisplayName(party)
                    : leader?.Name ??
                        new TaleWorlds.Localization.TextObject("unknown"));
            message.SetTextVariable("PARTY_ID", party?.StringId ?? "none");
            message.SetTextVariable("SETTLEMENT_NAME",
                settlement?.Name ??
                    new TaleWorlds.Localization.TextObject("unknown"));
            message.SetTextVariable("SETTLEMENT_ID", settlementId ?? "none");
            message.SetTextVariable("PHASE", phase ?? "Unknown");
            message.SetTextVariable("REASON", reason ?? "Unknown");
            message.SetTextVariable("GARRISON", garrison);
            message.SetTextVariable("FIEF", fief);
            message.SetTextVariable("TOTAL", total);
            message.SetTextVariable("SOURCE", source ?? "None");
            message.SetTextVariable("ATTEMPT_ID", attemptId ?? "none");
            message.SetTextVariable("PARTY_TYPE", "LordParty");
            message.SetTextVariable("MINIMUM", minimumRequired);
            message.SetTextVariable("MINIMUM_FIEF_BATCH", minimumFiefBatch);
            message.SetTextVariable("BINDING_ELIGIBLE",
                bindingEligible ? "true" : "false");
            message.SetTextVariable("TASK_ID", taskId ?? "none");
            message.SetTextVariable("OPERATION_ID", operationId ?? "none");
            message.SetTextVariable("ACTUAL_RECRUITED", actualRecruited);
            message.SetTextVariable("ACTUAL_FIEF", actualFief);
            message.SetTextVariable("ACTUAL_PROFESSIONAL", actualProfessional);
            message.SetTextVariable("FINAL_REGULARS", finalRegulars);
            message.SetTextVariable("FINAL_STATE", finalState ?? phase ?? "Unknown");
            if (phase == "RecoveryRequired")
                ModLogger.Error(message.ToString());
            else if (phase == "Failed" || phase == "Rejected")
                ModLogger.Debug(message.ToString());
            else if (phase == "Completed" && finalRegulars > 0 &&
                finalRegulars < 50)
                ModLogger.Debug(message.ToString());
            else if (phase == "Evaluated")
                ModLogger.Debug(message.ToString());
            else
                ModLogger.Info(message.ToString());
        }
    }
}
