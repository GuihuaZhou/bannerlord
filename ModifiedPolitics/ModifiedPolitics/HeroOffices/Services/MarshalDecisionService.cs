using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.HeroOffices.Decisions;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;
using ModifiedPolitics.Tool;

namespace ModifiedPolitics.HeroOffices.Services
{
    /// <summary>
    /// Shared entry point for AI and the future office management UI.
    /// </summary>
    public static class MarshalDecisionService
    {
        public static bool TryProposeAppointment(Kingdom kingdom, Hero nominatedHero = null)
        {
            if (!CanProcessMarshalAction(kingdom)
                || HeroOfficeBehavior.Current.GetAssignments(kingdom, OfficeType.Marshal).Count()
                   >= OfficeRules.GetOfficeLimit(kingdom, OfficeType.Marshal)
                || (string.Equals(kingdom.Culture?.StringId, "vlandia", System.StringComparison.OrdinalIgnoreCase)
                    && nominatedHero != null)
                || (nominatedHero != null
                    && (HeroOfficeBehavior.Current.GetAssignment(nominatedHero) != null
                        || !OfficeRules.IsEligible(nominatedHero, kingdom, OfficeType.Marshal)
                        || !OfficeRules.IsMarshalClanAvailable(nominatedHero, kingdom))))
                return false;

            List<Hero> candidates = kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => HeroOfficeBehavior.Current.GetAssignment(hero) == null
                               && OfficeRules.IsEligible(hero, kingdom, OfficeType.Marshal)
                               && OfficeRules.IsMarshalClanAvailable(hero, kingdom))
                .ToList();
            if (candidates.Count == 0)
                return false;

            if (!RequiresAppointmentVote(kingdom))
            {
                // In ruler-led cultures the ruler's accepted nominee takes office immediately.
                // Autonomous appointments without a nominee use the same central marshal score.
                Hero candidate = nominatedHero ?? candidates
                    .OrderByDescending(hero => OfficeRules.GetMarshalAppointmentScore(hero, kingdom))
                    .ThenBy(hero => hero.StringId)
                    .FirstOrDefault();
                return candidate != null
                       && OfficeAppointmentService.TryApplyMarshalDecision(kingdom, candidate);
            }

            kingdom.AddDecision(
                new MarshalOfficeDecision(kingdom.RulingClan, nominatedHero, false),
                true);
            LogProposal(kingdom, false, nominatedHero);
            return true;
        }

        public static bool TryProposeDismissal(Kingdom kingdom, Hero marshalToDismiss = null)
        {
            if (!CanProcessMarshalAction(kingdom))
                return false;

            OfficeAssignment marshal = marshalToDismiss == null
                ? HeroOfficeBehavior.Current.GetAssignments(kingdom, OfficeType.Marshal).FirstOrDefault()
                : HeroOfficeBehavior.Current.GetAssignment(marshalToDismiss);
            if (marshal?.Hero == null || marshal.Kingdom != kingdom
                                      || marshal.OfficeType != OfficeType.Marshal)
                return false;

            kingdom.AddDecision(
                new MarshalOfficeDecision(kingdom.RulingClan, marshal.Hero, true),
                true);
            LogProposal(kingdom, true, marshal.Hero);
            return true;
        }

        public static bool RequiresAppointmentVote(Kingdom kingdom)
        {
            return OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config)
                   && config.MarshalAppointmentNormalVote;
        }

        private static bool CanProcessMarshalAction(Kingdom kingdom)
        {
            return kingdom != null
                   && !kingdom.IsEliminated
                   && kingdom.RulingClan != null
                   && HeroOfficeBehavior.Current != null
                   && OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config)
                   && config.IsEnabled(OfficeType.Marshal)
                   && !kingdom.UnresolvedDecisions.Any(decision => decision is MarshalOfficeDecision);
        }

        private static void LogProposal(Kingdom kingdom, bool dismissal, Hero hero)
        {
            if (kingdom == null || Clan.PlayerClan?.Kingdom != kingdom)
                return;

            TextObject message = new TextObject(dismissal
                ? "{=MP_MarshalDismissDecisionStarted}[Hero Offices] {KINGDOM} has opened a vote to dismiss marshal {HERO}."
                : "{=MP_MarshalAppointmentDecisionStarted}[Hero Offices] {KINGDOM} has opened a marshal appointment vote; ruler nominee: {HERO}.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable(
                "HERO",
                hero?.Name ?? new TextObject("{=MP_MarshalSystemCandidates}system-generated candidates"));
            ModLogger.Info(message.ToString());
        }
    }
}
