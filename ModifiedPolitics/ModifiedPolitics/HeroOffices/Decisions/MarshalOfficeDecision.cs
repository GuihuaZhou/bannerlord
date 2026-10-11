using System.Collections.Generic;
using System.Linq;
using Helpers;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.HeroOffices.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Decisions
{
    /// <summary>
    /// Runs marshal appointment and dismissal through Bannerlord's kingdom decision platform.
    /// </summary>
    public sealed class MarshalOfficeDecision : KingdomDecision
    {
        [SaveableField(1)]
        private readonly Hero _nominatedHero;

        [SaveableField(2)]
        private readonly Hero _marshalToDismiss;

        [SaveableField(3)]
        private readonly bool _isDismissal;

        private bool _candidateScoresLogged;

        public MarshalOfficeDecision(Clan proposerClan, Hero nominatedHero, bool isDismissal)
            : base(proposerClan)
        {
            _isDismissal = isDismissal;
            if (isDismissal)
                _marshalToDismiss = nominatedHero;
            else
                _nominatedHero = nominatedHero;
        }

        public bool IsDismissal => _isDismissal;

        public override bool IsAllowed()
        {
            Kingdom kingdom = Kingdom;
            if (kingdom == null || kingdom.IsEliminated || ProposerClan != kingdom.RulingClan
                || !OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config)
                || !config.IsEnabled(OfficeType.Marshal))
                return false;

            if (_isDismissal)
            {
                OfficeAssignment assignment = HeroOfficeBehavior.Current?.GetAssignment(_marshalToDismiss);
                return assignment != null
                       && assignment.Kingdom == kingdom
                       && assignment.OfficeType == OfficeType.Marshal;
            }

            if ((HeroOfficeBehavior.Current?.GetAssignments(kingdom, OfficeType.Marshal).Count() ?? 0)
                >= OfficeRules.GetOfficeLimit(kingdom, OfficeType.Marshal))
                return false;

            return _nominatedHero == null
                   || (HeroOfficeBehavior.Current?.GetAssignment(_nominatedHero) == null
                       && OfficeRules.IsEligible(_nominatedHero, kingdom, OfficeType.Marshal)
                       && OfficeRules.IsMarshalClanAvailable(_nominatedHero, kingdom));
        }

        public override int GetProposalInfluenceCost() => 0;

        public override TextObject GetGeneralTitle() => _isDismissal
            ? new TextObject("{=MP_MarshalDismissTitle}Dismiss the Marshal")
            : new TextObject("{=MP_MarshalAppointmentTitle}Appoint the Marshal");

        public override TextObject GetSupportTitle() => GetGeneralTitle();
        public override TextObject GetChooseTitle() => GetGeneralTitle();

        public override TextObject GetSupportDescription() => _isDismissal
            ? new TextObject("{=MP_MarshalDismissDescription}The council will decide whether the current marshal should be dismissed.")
            : new TextObject("{=MP_MarshalAppointmentDescription}The council will decide who should serve as marshal.");

        public override TextObject GetChooseDescription() => GetSupportDescription();

        public override IEnumerable<DecisionOutcome> DetermineInitialCandidates()
        {
            if (_isDismissal)
            {
                yield return new MarshalOfficeOutcome(_marshalToDismiss, false);
                yield return new MarshalOfficeOutcome(_marshalToDismiss, true);
                yield break;
            }

            List<Hero> candidates = GetAppointmentCandidates().ToList();
            LogCandidateScores(candidates);
            foreach (Hero hero in candidates)
                yield return new MarshalOfficeOutcome(hero, false);
        }

        public override float CalculateMeritOfOutcome(DecisionOutcome candidateOutcome)
        {
            MarshalOfficeOutcome outcome = (MarshalOfficeOutcome)candidateOutcome;
            if (_isDismissal)
                return outcome.RemoveMarshal ? 100f : 50f;
            return OfficeRules.GetMarshalAppointmentScore(outcome.Hero);
        }

        public override Clan DetermineChooser() => Kingdom.RulingClan;

        public override float DetermineSupport(Clan clan, DecisionOutcome possibleOutcome)
        {
            MarshalOfficeOutcome outcome = (MarshalOfficeOutcome)possibleOutcome;
            if (clan?.Leader == null || outcome == null)
                return 0f;

            bool normalVote = IsNormalVote();
            if (!normalVote && clan != Kingdom.RulingClan)
                return 0f;

            if (_isDismissal)
            {
                if (_marshalToDismiss == null)
                    return 0f;

                int relation = clan.Leader.GetRelation(_marshalToDismiss);
                if (clan == Kingdom.RulingClan)
                    return outcome.RemoveMarshal ? 100f : 0f;
                return outcome.RemoveMarshal ? -relation : relation;
            }

            if (_nominatedHero != null && clan == Kingdom.RulingClan)
                return outcome.Hero == _nominatedHero ? 100f : 0f;

            if (outcome.Hero == null)
                return 0f;

            float militaryMerit = OfficeRules.GetMarshalAppointmentScore(outcome.Hero) / 20f;
            return militaryMerit + clan.Leader.GetRelation(outcome.Hero) * 0.5f;
        }

        public override void DetermineSponsors(MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            foreach (DecisionOutcome outcome in possibleOutcomes)
            {
                MarshalOfficeOutcome marshalOutcome = (MarshalOfficeOutcome)outcome;
                outcome.SetSponsor(_isDismissal || marshalOutcome.Hero?.Clan == null
                    ? Kingdom.RulingClan
                    : marshalOutcome.Hero.Clan);
            }
        }

        public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
        {
            MarshalOfficeOutcome outcome = chosenOutcome as MarshalOfficeOutcome;
            if (outcome == null)
                return;

            if (_isDismissal)
            {
                if (outcome.RemoveMarshal)
                    OfficeAppointmentService.TryApplyMarshalDismissal(Kingdom, _marshalToDismiss);
                return;
            }

            if (outcome.Hero != null)
                OfficeAppointmentService.TryApplyMarshalDecision(Kingdom, outcome.Hero);
        }

        public override TextObject GetSecondaryEffects() => TextObject.GetEmpty();

        public override void ApplySecondaryEffects(
            MBReadOnlyList<DecisionOutcome> possibleOutcomes,
            DecisionOutcome chosenOutcome)
        {
        }

        public override TextObject GetChosenOutcomeText(
            DecisionOutcome chosenOutcome,
            SupportStatus supportStatus,
            bool isShortVersion = false)
        {
            MarshalOfficeOutcome outcome = (MarshalOfficeOutcome)chosenOutcome;
            TextObject text = _isDismissal
                ? new TextObject(outcome.RemoveMarshal
                    ? "{=MP_MarshalDismissedOutcome}{HERO} will be dismissed as marshal."
                    : "{=MP_MarshalRetainedOutcome}{HERO} will remain marshal.")
                : new TextObject("{=MP_MarshalAppointedOutcome}{HERO} will serve as marshal.");
            text.SetTextVariable("HERO", outcome.Hero?.Name ?? TextObject.GetEmpty());
            return text;
        }

        public override DecisionOutcome GetQueriedDecisionOutcome(MBReadOnlyList<DecisionOutcome> possibleOutcomes)
        {
            return possibleOutcomes.OrderByDescending(outcome => outcome.Merit).FirstOrDefault();
        }

        private IEnumerable<Hero> GetAppointmentCandidates()
        {
            if (_nominatedHero != null)
                return new[] { _nominatedHero };

            IEnumerable<Hero> eligible = Kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => HeroOfficeBehavior.Current?.GetAssignment(hero) == null)
                .Where(hero => OfficeRules.IsEligible(hero, Kingdom, OfficeType.Marshal))
                .Where(hero => OfficeRules.IsMarshalClanAvailable(hero, Kingdom));

            if (string.Equals(Kingdom.Culture?.StringId, "vlandia", System.StringComparison.OrdinalIgnoreCase))
            {
                return eligible
                    .GroupBy(hero => hero.Clan)
                    .Select(group => group
                        .OrderByDescending(OfficeRules.GetMarshalAppointmentScore)
                        .ThenBy(hero => hero.StringId)
                        .First())
                    .OrderByDescending(OfficeRules.GetMarshalAppointmentScore)
                    .ThenBy(hero => hero.StringId)
                    .Take(3)
                    .ToList();
            }

            return eligible
                .OrderByDescending(GetAppointmentScore)
                .ThenBy(hero => hero.StringId)
                .Take(1)
                .ToList();
        }

        private float GetAppointmentScore(Hero hero)
        {
            if (hero == null || Kingdom?.Leader == null)
                return 0f;

            return OfficeRules.GetMarshalAppointmentScore(hero)
                   + hero.GetRelation(Kingdom.Leader) * 2f;
        }

        private void LogCandidateScores(IEnumerable<Hero> candidates)
        {
            if (_candidateScoresLogged || Clan.PlayerClan?.Kingdom != Kingdom)
                return;

            _candidateScoresLogged = true;
            string scores = string.Join(
                "; ",
                candidates.Where(hero => hero != null).Select(hero => string.Format(
                    "{0}={1:0}->{2:0}",
                    hero.Name,
                    OfficeRules.GetCandidateScore(hero, OfficeType.Marshal),
                    OfficeRules.GetMarshalAppointmentScore(hero))));
            TextObject message = new TextObject(
                "{=MP_MarshalCandidateScores}[Hero Offices] {KINGDOM} marshal appointment scores: {SCORES}.");
            message.SetTextVariable("KINGDOM", Kingdom.Name);
            message.SetTextVariable("SCORES", scores);
            ModLogger.Info(message.ToString());
        }

        private bool IsNormalVote()
        {
            if (!OfficeConfigManager.Instance.TryGet(Kingdom, out OfficeCultureConfig config))
                return false;
            return _isDismissal
                ? config.MarshalDismissalNormalVote
                : config.MarshalAppointmentNormalVote;
        }

        public sealed class MarshalOfficeOutcome : DecisionOutcome
        {
            public MarshalOfficeOutcome(Hero hero, bool removeMarshal)
            {
                Hero = hero;
                RemoveMarshal = removeMarshal;
            }

            [SaveableField(1)]
            public readonly Hero Hero;

            [SaveableField(2)]
            public readonly bool RemoveMarshal;

            public override TextObject GetDecisionTitle()
            {
                if (RemoveMarshal)
                    return new TextObject("{=MP_MarshalRemoveOption}Dismiss Marshal");
                return Hero?.Name ?? TextObject.GetEmpty();
            }

            public override TextObject GetDecisionDescription() => GetDecisionTitle();
            public override string GetDecisionLink() => null;
            public override ImageIdentifier GetDecisionImageIdentifier() => null;
        }
    }
}
