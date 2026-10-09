using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Decisions;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.Services
{
    public enum OfficeApplicationResult
    {
        Rejected,
        Appointed,
        DecisionStarted
    }

    /// <summary>
    /// Validates office applications and lets the ruler retain the final appointment authority.
    /// </summary>
    public static class OfficeApplicationService
    {
        public static bool CanApply(
            Kingdom kingdom,
            Hero applicant,
            OfficeType officeType,
            out string failureReason)
        {
            failureReason = null;
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            if (behavior == null || kingdom == null || kingdom.IsEliminated || kingdom.Leader == null)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeApplicationInvalidKingdom}The kingdom or its ruler is no longer available.").ToString();
                return false;
            }

            if (applicant == null || applicant == kingdom.Leader)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeApplicationRulerCannotApply}The kingdom ruler cannot apply for an office.").ToString();
                return false;
            }

            if (!applicant.IsAlive || !applicant.IsActive || applicant.IsPrisoner)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeApplicationHeroUnavailable}The applicant is dead, inactive or imprisoned.").ToString();
                return false;
            }

            if (applicant.Clan == null || applicant.Clan.Kingdom != kingdom
                || applicant.Clan.IsEliminated || applicant.Clan.IsClanTypeMercenary)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeApplicationNotKingdomMember}Only an eligible member of this kingdom may apply.").ToString();
                return false;
            }

            if (behavior.GetAssignment(applicant) != null)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureAlreadyHoldsOffice}A hero may hold only one office at a time.").ToString();
                return false;
            }

            if (OfficeRules.IsLocal(officeType))
            {
                if (applicant.GovernorOf == null)
                {
                    failureReason = new TextObject(
                        "{=MP_OfficeApplicationGovernorRequired}A local office requires the applicant to be a governor.").ToString();
                    return false;
                }

                if (officeType == OfficeType.MilitaryOfficer && !applicant.GovernorOf.IsCastle)
                {
                    failureReason = new TextObject(
                        "{=MP_OfficeApplicationCastleGovernorRequired}The military officer must govern a castle.").ToString();
                    return false;
                }

                if (officeType != OfficeType.MilitaryOfficer && !applicant.GovernorOf.IsTown)
                {
                    failureReason = new TextObject(
                        "{=MP_OfficeApplicationTownGovernorRequired}This office must be held by a town governor.").ToString();
                    return false;
                }
            }

            if (!OfficeRules.IsEligible(applicant, kingdom, officeType))
            {
                failureReason = new TextObject(
                    "{=MP_OfficeApplicationIneligible}You do not meet the requirements for this office.").ToString();
                return false;
            }

            int limit = OfficeRules.GetOfficeLimit(kingdom, officeType);
            if (limit <= behavior.GetAssignments(kingdom, officeType).Count())
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureNoVacancy}No vacant seat remains for this office.").ToString();
                return false;
            }

            if (officeType == OfficeType.Marshal
                && kingdom.UnresolvedDecisions.Any(decision => decision is MarshalOfficeDecision))
            {
                failureReason = new TextObject(
                    "{=MP_OfficeDecisionPending}An office decision is already in progress.").ToString();
                return false;
            }

            return true;
        }

        public static bool TrySubmit(
            Kingdom kingdom,
            Hero applicant,
            OfficeType officeType,
            out OfficeApplicationResult result,
            out string message)
        {
            result = OfficeApplicationResult.Rejected;
            if (!CanApply(kingdom, applicant, officeType, out message))
                return false;

            if (!ShouldRulerAccept(kingdom, applicant, officeType))
            {
                message = new TextObject(
                    "{=MP_OfficeApplicationRejected}The ruler rejected the office application.").ToString();
                LogResult(kingdom, applicant, officeType, false);
                return true;
            }

            return TryApprove(kingdom, applicant, officeType, out result, out message);
        }

        public static bool TryApprove(
            Kingdom kingdom,
            Hero applicant,
            OfficeType officeType,
            out OfficeApplicationResult result,
            out string message)
        {
            result = OfficeApplicationResult.Rejected;
            if (!CanApply(kingdom, applicant, officeType, out message))
                return false;

            if (officeType == OfficeType.Marshal)
            {
                // Vlandian marshal elections generate candidates internally and cannot take a ruler nominee.
                Hero nominee = string.Equals(
                    kingdom.Culture?.StringId,
                    "vlandia",
                    StringComparison.OrdinalIgnoreCase)
                    ? null
                    : applicant;
                if (!MarshalDecisionService.TryProposeAppointment(kingdom, nominee))
                {
                    message = new TextObject(
                        "{=MP_OfficeFailureDecisionUnavailable}The kingdom decision cannot be started right now.").ToString();
                    return false;
                }

                result = OfficeApplicationResult.DecisionStarted;
                message = new TextObject(
                    "{=MP_OfficeApplicationDecisionStarted}The ruler accepted the request and opened a marshal appointment decision.").ToString();
                LogResult(kingdom, applicant, officeType, true);
                return true;
            }

            if (!OfficeAppointmentService.TryAppoint(
                    kingdom,
                    kingdom.Leader,
                    applicant,
                    officeType,
                    out message))
                return false;

            result = OfficeApplicationResult.Appointed;
            message = new TextObject(
                "{=MP_OfficeApplicationAccepted}The ruler accepted the application.").ToString();
            LogResult(kingdom, applicant, officeType, true);
            return true;
        }

        internal static void RecordRejection(
            Kingdom kingdom,
            Hero applicant,
            OfficeType officeType)
        {
            LogResult(kingdom, applicant, officeType, false);
        }

        private static bool ShouldRulerAccept(Kingdom kingdom, Hero applicant, OfficeType officeType)
        {
            Hero ruler = kingdom?.Leader;
            Hero clanLeader = applicant?.Clan?.Leader;
            if (ruler == null || clanLeader == null)
                return false;

            int relation = ruler.GetRelation(clanLeader);
            if (officeType == OfficeType.ChiefMinister || officeType == OfficeType.CourtSteward)
                return relation >= 0;

            float applicantScore = GetPoliticalCandidateScore(ruler, applicant, officeType);
            float bestScore = GetEligibleCandidates(kingdom, officeType)
                .Select(candidate => GetPoliticalCandidateScore(ruler, candidate, officeType))
                .DefaultIfEmpty(0f)
                .Max();

            // The ruler accepts a credible applicant without requiring them to be the absolute best candidate.
            return applicantScore >= bestScore * 0.8f;
        }

        internal static float GetPoliticalCandidateScore(
            Hero ruler,
            Hero candidate,
            OfficeType officeType)
        {
            Hero candidateClanLeader = candidate?.Clan?.Leader;
            float relation = ruler == null || candidateClanLeader == null
                ? 0f
                : ruler.GetRelation(candidateClanLeader);
            return OfficeRules.GetCandidateScore(candidate, officeType) + relation * 2f;
        }

        private static IEnumerable<Hero> GetEligibleCandidates(Kingdom kingdom, OfficeType officeType)
        {
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            if (kingdom == null || behavior == null)
                return Enumerable.Empty<Hero>();

            return kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => hero != kingdom.Leader && behavior.GetAssignment(hero) == null)
                .Where(hero => OfficeRules.IsEligible(hero, kingdom, officeType));
        }

        private static void LogResult(
            Kingdom kingdom,
            Hero applicant,
            OfficeType officeType,
            bool accepted)
        {
            if (kingdom == null || Clan.PlayerClan?.Kingdom != kingdom)
                return;

            TextObject text = new TextObject(accepted
                ? "{=MP_OfficeApplicationAcceptedLog}[Hero Offices] {HERO}'s application for {OFFICE} was accepted."
                : "{=MP_OfficeApplicationRejectedLog}[Hero Offices] {HERO}'s application for {OFFICE} was rejected.");
            text.SetTextVariable("HERO", applicant?.Name ?? TextObject.GetEmpty());
            text.SetTextVariable("OFFICE", OfficeText.GetName(officeType));
            ModLogger.Info(text.ToString());
        }
    }
}
