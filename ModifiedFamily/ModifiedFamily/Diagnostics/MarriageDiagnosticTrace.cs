using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MCM.Abstractions.Base.Global;
using ModifiedFamily.Core;
using ModifiedFamily.Proposals;
using ModifiedFamily.Utils;
using TaleWorlds.CampaignSystem;

namespace ModifiedFamily.Diagnostics
{
    internal static class MarriageDiagnosticTrace
    {
        internal static bool Enabled
        {
            get
            {
                try { return GlobalSettings<Settings>.Instance?.EnableMarriageDiagnostics == true; }
                catch { return false; }
            }
        }

        internal static string NewUiTraceId() =>
            Guid.NewGuid().ToString("N").Substring(0, 8);

        internal static void Write(string eventName,
            params KeyValuePair<string, object>[] fields)
        {
            if (!Enabled) return;
            try
            {
                string body = string.Join(" | ", (fields ?? Array.Empty<KeyValuePair<string, object>>())
                    .Select(field => Escape(field.Key) + "=" + Value(field.Value)));
                FamilyDiagnostics.DebugLog("[MF_DIAG][" + Escape(eventName) + "] " + body);
            }
            catch { }
        }

        internal static KeyValuePair<string, object> F(string key, object value) =>
            new KeyValuePair<string, object>(key, value);

        internal static string Id(Hero hero) => hero?.StringId ?? "none";
        internal static string Id(Clan clan) => clan?.StringId ?? "none";
        internal static string Id(Kingdom kingdom) => kingdom?.StringId ?? "none";
        internal static string Name(Hero hero) => hero?.Name?.ToString() ?? "none";
        internal static string Name(Clan clan) => clan?.Name?.ToString() ?? "none";
        internal static string CultureId(Hero hero) => hero?.Culture?.StringId ?? "none";
        internal static string CultureId(Clan clan) => clan?.Culture?.StringId ?? "none";
        internal static string Blockers(FamilyResult result) => result == null || result.TemporaryBlockers.Count == 0
            ? "none"
            : string.Join(",", result.TemporaryBlockers.Select(x => x.ToString()));
        internal static string Direction(FamilyProposal proposal)
        {
            if (proposal?.HeroA == null || proposal.HeroB == null) return "unknown";
            Hero female = proposal.HeroA.IsFemale ? proposal.HeroA : proposal.HeroB;
            return proposal.DestinationClan == female.Clan
                ? MarriageDirection.MaleJoinsFemaleClan.ToString()
                : MarriageDirection.FemaleJoinsMaleClan.ToString();
        }

        internal static bool IsPlayerRelated(FamilyProposal proposal) =>
            proposal != null && (proposal.ProposerClan == Clan.PlayerClan ||
                proposal.ReceiverClan == Clan.PlayerClan ||
                proposal.OriginClanA == Clan.PlayerClan ||
                proposal.OriginClanB == Clan.PlayerClan);

        internal static void ProposalEvent(string eventName,
            FamilyProposal proposal, FamilyProposalStatus? statusBefore,
            FamilyResult result = null, string reason = null)
        {
            if (!Enabled || proposal?.ProposalType != FamilyProposalType.Marriage ||
                !IsPlayerRelated(proposal)) return;
            Write(eventName,
                F("proposalId", proposal.ProposalId),
                F("chainId", proposal.ChainId),
                F("parentProposalId", proposal.ParentProposalId ?? "none"),
                F("source", proposal.ProposerClan == Clan.PlayerClan ? "Player" : "AI"),
                F("proposerClanName", Name(proposal.ProposerClan)),
                F("proposerClanId", Id(proposal.ProposerClan)),
                F("receiverClanName", Name(proposal.ReceiverClan)),
                F("receiverClanId", Id(proposal.ReceiverClan)),
                F("heroAName", Name(proposal.HeroA)),
                F("heroAId", Id(proposal.HeroA)),
                F("heroBName", Name(proposal.HeroB)),
                F("heroBId", Id(proposal.HeroB)),
                F("direction", Direction(proposal)),
                F("destinationClanId", Id(proposal.DestinationClan)),
                F("goldPayerClanId", Id(proposal.GoldPayerClan)),
                F("giftAmount", proposal.GiftAmount),
                F("statusBefore", statusBefore?.ToString() ?? "none"),
                F("operationAllowed", result?.IsAllowed),
                F("reason", reason ?? result?.ReasonCode.ToString() ?? proposal.ReasonCode.ToString()),
                F("statusAfter", proposal.Status),
                F("executionStep", proposal.ExecutionStep));
        }

        private static string Value(object value)
        {
            if (value == null) return "none";
            if (value is float single) return single.ToString("0.00", CultureInfo.InvariantCulture);
            if (value is double number) return number.ToString("0.00", CultureInfo.InvariantCulture);
            if (value is bool boolean) return boolean ? "true" : "false";
            return Escape(Convert.ToString(value, CultureInfo.InvariantCulture));
        }

        private static string Escape(string value) => (value ?? "none")
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("=", "\\=");
    }
}
