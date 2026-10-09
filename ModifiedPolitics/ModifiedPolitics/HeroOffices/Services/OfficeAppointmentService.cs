using System;
using System.Linq;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.Services
{
    /// <summary>
    /// Validates and executes all explicit appointments and dismissals.
    /// </summary>
    public static class OfficeAppointmentService
    {
        public const int LowCompensation = 15000;
        public const int MediumCompensation = 25000;
        public const int HighCompensation = 35000;

        public static bool TryAppoint(
            Kingdom kingdom,
            Hero appointer,
            Hero candidate,
            OfficeType officeType,
            out string failureReason)
        {
            failureReason = null;
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            if (behavior == null || kingdom == null || appointer != kingdom.Leader)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureRulerOnly}Only the kingdom ruler may appoint ordinary officers.").ToString();
                return false;
            }

            if (officeType == OfficeType.Marshal)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureMarshalDecision}Marshal appointments must use the kingdom decision system.").ToString();
                return false;
            }

            if (!OfficeRules.IsEligible(candidate, kingdom, officeType))
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureIneligible}The selected hero is no longer eligible for this office.").ToString();
                return false;
            }

            if (behavior.GetAssignment(candidate) != null)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureAlreadyHoldsOffice}A hero may hold only one office at a time.").ToString();
                return false;
            }

            int limit = OfficeRules.GetOfficeLimit(kingdom, officeType);
            if (limit <= behavior.GetAssignments(kingdom, officeType).Count())
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureNoVacancy}No vacant seat remains for this office.").ToString();
                return false;
            }

            behavior.AddAssignment(kingdom, candidate, officeType);
            int relationGain = OfficeRules.IsLocal(officeType) ? 3 : 5;
            ApplyClanRelationChange(kingdom.Leader, candidate, relationGain);
            return true;
        }

        public static bool TryApplyMarshalDecision(Kingdom kingdom, Hero candidate)
        {
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            if (behavior == null
                || behavior.GetAssignments(kingdom, OfficeType.Marshal).Any()
                || behavior.GetAssignment(candidate) != null
                || !OfficeRules.IsEligible(candidate, kingdom, OfficeType.Marshal))
                return false;

            behavior.AddAssignment(kingdom, candidate, OfficeType.Marshal);
            return true;
        }

        public static bool TryApplyMarshalDismissal(Kingdom kingdom, Hero marshal)
        {
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            OfficeAssignment assignment = behavior?.GetAssignment(marshal);
            if (assignment == null
                || assignment.Kingdom != kingdom
                || assignment.OfficeType != OfficeType.Marshal)
                return false;

            behavior.RemoveAssignment(assignment, false);
            return true;
        }

        public static bool TryDismiss(
            Kingdom kingdom,
            Hero appointer,
            Hero officer,
            OfficeCompensation compensation,
            out string failureReason)
        {
            failureReason = null;
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            OfficeAssignment assignment = behavior?.GetAssignment(officer);
            if (assignment == null || assignment.Kingdom != kingdom || appointer != kingdom?.Leader)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureInvalidDismissal}The office or ruler is no longer valid.").ToString();
                return false;
            }

            if (assignment.OfficeType == OfficeType.Marshal)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureMarshalDismissalDecision}Marshal dismissal must use the kingdom decision system.").ToString();
                return false;
            }

            int compensationAmount = GetCompensationAmount(compensation);
            if (compensationAmount > 0 && appointer.Gold < compensationAmount)
            {
                failureReason = new TextObject(
                    "{=MP_OfficeFailureCannotAfford}The ruler cannot afford the selected compensation.").ToString();
                return false;
            }

            if (compensationAmount > 0)
                GiveGoldAction.ApplyBetweenCharacters(appointer, officer, compensationAmount, true);

            int relationLoss = GetDismissalRelationLoss(assignment.OfficeType, compensation);
            ApplyClanRelationChange(kingdom.Leader, officer, relationLoss);
            behavior.RemoveAssignment(assignment, false);
            return true;
        }

        public static int GetCompensationAmount(OfficeCompensation compensation)
        {
            switch (compensation)
            {
                case OfficeCompensation.Low:
                    return LowCompensation;
                case OfficeCompensation.Medium:
                    return MediumCompensation;
                case OfficeCompensation.High:
                    return HighCompensation;
                default:
                    return 0;
            }
        }

        public static int GetDismissalRelationLoss(
            OfficeType officeType,
            OfficeCompensation compensation)
        {
            int baseLoss = OfficeRules.IsLocal(officeType) ? 5 : 10;
            return -(int)Math.Ceiling(
                baseLoss * (1f - GetCompensationReduction(compensation)));
        }

        private static float GetCompensationReduction(OfficeCompensation compensation)
        {
            switch (compensation)
            {
                case OfficeCompensation.Low:
                    return 0.25f;
                case OfficeCompensation.Medium:
                    return 0.5f;
                case OfficeCompensation.High:
                    return 0.75f;
                default:
                    return 0f;
            }
        }

        private static void ApplyClanRelationChange(Hero ruler, Hero officer, int change)
        {
            Hero officerClanLeader = officer?.Clan?.Leader;
            if (ruler == null || officerClanLeader == null || ruler.Clan == officer.Clan)
                return;

            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(ruler, officerClanLeader, change, true);
        }
    }
}
