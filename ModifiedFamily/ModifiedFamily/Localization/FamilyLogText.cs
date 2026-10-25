// Builds localized, scenario-specific log messages from campaign objects and proposal state.
using System.Linq;
using ModifiedFamily.Proposals;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedFamily.Localization
{
    internal static class FamilyLogText
    {
        internal static string Simple(string id, string fallback) => T(id, fallback).ToString();
        internal static string Persistence(int proposals, int cooldowns, int adoptions)
        {
            var t = T("MF_Log_Persistence", "Family data synchronized: {PROPOSALS} proposals, {COOLDOWNS} cooldowns, {ADOPTIONS} adoptions.");
            t.SetTextVariable("PROPOSALS", proposals); t.SetTextVariable("COOLDOWNS", cooldowns); t.SetTextVariable("ADOPTIONS", adoptions); return t.ToString();
        }
        internal static string SessionReady(bool model, int proposals, int cooldowns, int adoptions)
        {
            var t = T("MF_Log_SessionReady", "Family system ready. Marriage model: {MODEL_STATE}; {PROPOSALS} proposals, {COOLDOWNS} cooldowns, {ADOPTIONS} adoptions.");
            t.SetTextVariable("MODEL_STATE", T(model ? "MF_Log_ModelActive" : "MF_Log_ModelInactive", model ? "active" : "inactive"));
            t.SetTextVariable("PROPOSALS", proposals); t.SetTextVariable("COOLDOWNS", cooldowns); t.SetTextVariable("ADOPTIONS", adoptions); return t.ToString();
        }
        internal static string Count(string id, string fallback, int count) { var t = T(id, fallback); t.SetTextVariable("COUNT", count); return t.ToString(); }
        internal static string AiCandidate(Clan clan, Hero own, Hero other, int tier, float score)
        {
            var t = T("MF_Log_AiCandidate", "{CLAN} selected {OTHER} for {OWN}; tier {TIER}, score {SCORE}.");
            t.SetTextVariable("CLAN", clan.Name); t.SetTextVariable("OWN", own.Name); t.SetTextVariable("OTHER", other.Name); t.SetTextVariable("TIER", tier); t.SetTextVariable("SCORE", score.ToString("0.00")); return t.ToString();
        }
        internal static string AiResponse(FamilyProposal p, float score)
        {
            var t = T("MF_Log_AiResponse", "{CLAN} rescored the proposal at {SCORE}. (Request: {ID})");
            t.SetTextVariable("CLAN", p.ReceiverClan.Name); t.SetTextVariable("SCORE", score.ToString("0.00")); t.SetTextVariable("ID", p.ProposalId); return t.ToString();
        }
        internal static string AiPoliticalReassessment(FamilyProposal p, Clan clan, float score)
        {
            var t = T("MF_Log_AiPoliticalReassessment", "{CLAN} reassessed the accepted marriage after a political change: {SCORE}. (Request: {ID})");
            t.SetTextVariable("CLAN", clan?.Name ?? new TextObject(string.Empty)); t.SetTextVariable("SCORE", score.ToString("0.00")); t.SetTextVariable("ID", p?.ProposalId ?? "-"); return t.ToString();
        }
        internal static string MarriageProposal(FamilyProposal p, bool counter)
        {
            string id;
            if (counter) id = p.GiftAmount == 0 ? "MF_Log_CounterPlain" : p.GoldPayerClan == p.ProposerClan ? "MF_Log_CounterOfferingGift" : "MF_Log_CounterAskingGift";
            else id = p.GiftAmount == 0 ? "MF_Log_MarriageProposalPlain" : p.GoldPayerClan == p.ProposerClan ? "MF_Log_MarriageProposalOfferingGift" : "MF_Log_MarriageProposalAskingGift";
            var text = T(id, "A marriage proposal was made by {PROPOSER} to {RECEIVER}: {HERO_A} and {HERO_B}; {MOVING_HERO} will join {DESTINATION}; gift {GOLD}. (Request: {ID})");
            Common(text, p); return text.ToString();
        }

        internal static string MarriageAccepted(FamilyProposal p) { var t = T("MF_Log_MarriageAccepted", "{RECEIVER} accepted the marriage. {HERO_A} and {HERO_B} will marry when available. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string MarriageRejected(FamilyProposal p) { string key = p.ReasonCode == FamilyReasonCode.Expired ? "MF_Log_ProposalTimedOut" : "MF_Log_MarriageRejected"; var t = T(key, "{RECEIVER} declined the proposal from {PROPOSER}. Reason: {REASON}. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string Withdrawn(FamilyProposal p) { var t = T("MF_Log_ProposalWithdrawn", "{PROPOSER} withdrew the proposal. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string Invalidated(FamilyProposal p) { var t = T("MF_Log_ProposalInvalidated", "The proposal can no longer continue: {REASON}. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string MarriageWaiting(FamilyProposal p, FamilyReasonCode reason) { var t = T("MF_Log_MarriageWaiting", "{HERO_A} and {HERO_B} cannot marry yet: {REASON}. (Request: {ID})"); Common(t, p, reason); return t.ToString(); }
        internal static string MarriageStarting(FamilyProposal p) { var t = T("MF_Log_MarriageStarting", "Marriage processing started for {HERO_A} and {HERO_B}. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string MarriageCompleted(FamilyProposal p) { var t = T(p.GiftAmount > 0 ? "MF_Log_MarriageCompletedGift" : "MF_Log_MarriageCompletedNoGift", "{HERO_A} and {HERO_B} are married in {DESTINATION}; gift {GOLD}. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string PaymentUncertain(FamilyProposal p) { var t = T("MF_Log_PaymentUncertain", "The marriage completed, but the gift payment could not be confirmed. Automatic processing stopped. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string MarriageFailed(FamilyProposal p) { var t = T("MF_Log_MarriageFailed", "Marriage execution failed for {HERO_A} and {HERO_B}; retries are stopped. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string AdoptionProposal(FamilyProposal p) { var t = T("MF_Log_AdoptionProposal", "{PROPOSER} proposed that {ADOPTER} adopt {ADOPTEE} into {DESTINATION}. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string AdoptionAccepted(FamilyProposal p) { var t = T("MF_Log_AdoptionAccepted", "{RECEIVER} accepted the adoption of {ADOPTEE} into {DESTINATION}. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string AdoptionWaiting(FamilyProposal p) { var t = T("MF_Log_AdoptionWaiting", "{ADOPTEE} is busy with a party or governorship; adoption will wait. (Request: {ID})"); Common(t, p); return t.ToString(); }
        internal static string AdoptionCompleted(FamilyProposal p) { var t = T("MF_Log_AdoptionCompleted", "{ADOPTEE} was adopted by {ADOPTER} into {DESTINATION}. (Request: {ID})"); Common(t, p); return t.ToString(); }

        private static TextObject T(string id, string fallback) => new TextObject("{=" + id + "}" + fallback);
        private static void Common(TextObject t, FamilyProposal p, FamilyReasonCode? reason = null)
        {
            Hero moving = p.HeroA?.Clan == p.DestinationClan ? p.HeroB : p.HeroA;
            var empty = new TextObject(string.Empty);
            t.SetTextVariable("ID", p.ProposalId ?? "-"); t.SetTextVariable("PROPOSER", p.ProposerClan?.Name ?? empty);
            t.SetTextVariable("RECEIVER", p.ReceiverClan?.Name ?? empty); t.SetTextVariable("HERO_A", p.HeroA?.Name ?? empty);
            t.SetTextVariable("PROPOSER_KINGDOM", p.ProposerClan?.Kingdom?.Name ?? empty);
            t.SetTextVariable("RECEIVER_KINGDOM", p.ReceiverClan?.Kingdom?.Name ?? empty);
            t.SetTextVariable("HERO_B", p.HeroB?.Name ?? empty); t.SetTextVariable("MOVING_HERO", moving?.Name ?? empty);
            t.SetTextVariable("DESTINATION", p.DestinationClan?.Name ?? empty); t.SetTextVariable("GOLD", p.GiftAmount);
            t.SetTextVariable("ADOPTER", p.AdopterHero?.Name ?? empty); t.SetTextVariable("ADOPTEE", p.AdopteeHero?.Name ?? empty);
            t.SetTextVariable("REASON", Reason(reason ?? p.ReasonCode));
        }
        private static TextObject Reason(FamilyReasonCode reason)
        {
            switch (reason)
            {
                case FamilyReasonCode.Expired: return T("MF_Reason_Expired", "the agreed deadline passed");
                case FamilyReasonCode.Dead: return T("MF_Reason_Dead", "a participant died");
                case FamilyReasonCode.AlreadyMarried: return T("MF_Reason_AlreadyMarried", "a participant already married");
                case FamilyReasonCode.InvalidClan: return T("MF_Reason_InvalidClan", "a participating clan is no longer eligible");
                case FamilyReasonCode.ClanChanged: return T("MF_Reason_ClanChanged", "a participant changed clan");
                case FamilyReasonCode.AtWar: return T("MF_Reason_AtWar", "the clans are at war");
                case FamilyReasonCode.CultureForbidden: return T("MF_Reason_CultureForbidden", "culture and kingdom rules no longer allow it");
                case FamilyReasonCode.InsufficientFunds: return T("MF_Reason_InsufficientFunds", "the agreed gift is unavailable");
                case FamilyReasonCode.InBattle: return T("MF_Reason_InBattle", "someone is in battle");
                case FamilyReasonCode.InArmy: return T("MF_Reason_InArmy", "someone is in an army");
                case FamilyReasonCode.Prisoner: return T("MF_Reason_Prisoner", "someone is a prisoner");
                case FamilyReasonCode.Fugitive: return T("MF_Reason_Fugitive", "someone is a fugitive");
                case FamilyReasonCode.TemporaryInactive: return T("MF_Reason_TemporaryInactive", "someone is temporarily unavailable");
                case FamilyReasonCode.ExternalMarriage: return T("MF_Reason_ExternalMarriage", "a participant married elsewhere");
                case FamilyReasonCode.PlayerDeclined: return T("MF_Reason_PlayerDeclined", "the receiving clan declined the proposal");
                case FamilyReasonCode.PoliticalInterestChanged: return T("MF_Reason_PoliticalInterestChanged", "political changes made the accepted agreement unacceptable");
                default: return T("MF_Reason_Default", "the current conditions no longer satisfy the agreement");
            }
        }
    }
}
