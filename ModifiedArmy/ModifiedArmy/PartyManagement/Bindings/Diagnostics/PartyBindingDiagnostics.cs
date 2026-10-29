using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.PartyManagement.Bindings.Diagnostics
{
    /// <summary>Localized, player-kingdom-scoped binding diagnostics.</summary>
    internal static class PartyBindingDiagnostics
    {
        internal static void LogReservation(
            Hero leader,
            Settlement primary,
            string tokenId,
            string action,
            string reason,
            bool rejected = false)
        {
            if (!RecruitmentLogFilter.ShouldLog(leader?.Clan)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_binding_reservation");
            message.SetTextVariable("ACTION", action ?? "Unknown");
            message.SetTextVariable("HERO_ID", leader?.StringId ?? "unknown");
            message.SetTextVariable(
                "CLAN_ID", leader?.Clan?.StringId ?? "unknown");
            message.SetTextVariable(
                "SETTLEMENT_ID", primary?.StringId ?? "none");
            message.SetTextVariable("TOKEN_ID", tokenId ?? "none");
            message.SetTextVariable("REASON", reason ?? "None");
            if (rejected) ModLogger.Warn(message.ToString());
            else ModLogger.Debug(message.ToString());
        }

        internal static void LogBindingRejected(
            MobileParty party,
            Settlement settlement,
            string operation,
            string reason)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_binding_rejected");
            message.SetTextVariable(
                "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable(
                "PARTY_ID", party?.StringId ?? "unknown");
            message.SetTextVariable(
                "SETTLEMENT_ID", settlement?.StringId ?? "none");
            message.SetTextVariable("OPERATION", operation ?? "Unknown");
            message.SetTextVariable("REASON", reason ?? "Unspecified");
            ModLogger.Warn(message.ToString());
        }

        internal static void LogCommitted(
            MobileParty party,
            Settlement primary,
            int version)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_binding_committed");
            message.SetTextVariable(
                "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("PARTY_ID", party.StringId ?? "unknown");
            message.SetTextVariable(
                "SETTLEMENT_ID", primary?.StringId ?? "none");
            message.SetTextVariable("VERSION", version);
            ModLogger.Info(message.ToString());
        }

        internal static void LogReleased(MobileParty party, string reason)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_binding_released");
            message.SetTextVariable(
                "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable("PARTY_ID", party.StringId ?? "unknown");
            message.SetTextVariable("REASON", reason ?? "Unspecified");
            ModLogger.Info(message.ToString());
        }

        internal static void LogRecovery(
            int records,
            int bound,
            int unbound,
            int conflicts)
        {
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_binding_recovery");
            message.SetTextVariable("RECORDS", records);
            message.SetTextVariable("BOUND", bound);
            message.SetTextVariable("UNBOUND", unbound);
            message.SetTextVariable("CONFLICTS", conflicts);
            if (conflicts > 0) ModLogger.Warn(message.ToString());
            else ModLogger.Info(message.ToString());
        }

        internal static void LogCreationProbe(
            string phase,
            Hero hero,
            MobileParty party,
            Settlement spawnSettlement,
            bool? isNewGame)
        {
            Clan clan = party?.ActualClan ?? hero?.Clan;
            if (!RecruitmentLogFilter.ShouldLog(clan)) return;
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_creation_probe");
            message.SetTextVariable("PHASE", phase ?? "Unknown");
            message.SetTextVariable("HERO_ID", hero?.StringId ?? "unknown");
            message.SetTextVariable("CLAN_ID", clan?.StringId ?? "unknown");
            message.SetTextVariable("PARTY_ID", party?.StringId ?? "none");
            message.SetTextVariable(
                "REGULARS", party?.MemberRoster?.TotalRegulars ?? 0);
            message.SetTextVariable(
                "SPAWN", spawnSettlement?.StringId ?? "none");
            message.SetTextVariable(
                "CURRENT", party?.CurrentSettlement?.StringId ?? "none");
            message.SetTextVariable(
                "TARGET", party?.TargetSettlement?.StringId ?? "none");
            message.SetTextVariable(
                "NEW_GAME", isNewGame.HasValue
                    ? isNewGame.Value.ToString()
                    : "unknown");
            ModLogger.Debug(message.ToString());
        }
    }
}
