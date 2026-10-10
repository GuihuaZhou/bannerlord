using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.HeroOffices.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Behaviors
{
    /// <summary>
    /// Owns persisted assignments, applies daily influence and removes invalid or excess offices.
    /// </summary>
    public sealed class HeroOfficeBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "_modifiedPoliticsHeroOfficeAssignments";

        [SaveableField(1)]
        private List<OfficeAssignment> _assignments = new List<OfficeAssignment>();

        public static HeroOfficeBehavior Current { get; private set; }

        /// <summary>
        /// Notifies open views after persisted office membership changes.
        /// </summary>
        public event Action AssignmentsChanged;

        public override void RegisterEvents()
        {
            Current = this;
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
            CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
            CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, OnClanChangedKingdom);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _assignments);
            if (_assignments == null)
                _assignments = new List<OfficeAssignment>();
        }

        public IEnumerable<OfficeAssignment> GetAssignments(Kingdom kingdom, OfficeType officeType)
        {
            return _assignments.Where(item => item?.Kingdom == kingdom && item.OfficeType == officeType);
        }

        public OfficeAssignment GetAssignment(Hero hero)
        {
            return hero == null ? null : _assignments.FirstOrDefault(item => item?.Hero == hero);
        }

        internal void AddAssignment(Kingdom kingdom, Hero hero, OfficeType officeType)
        {
            // Services validate first; this final guard protects save integrity from other callers.
            if (GetAssignment(hero) != null)
                return;

            _assignments.Add(new OfficeAssignment(kingdom, hero, officeType));
            LogAssignment(kingdom, hero, officeType, "MP_OfficeAppointed", "appointed");
            AssignmentsChanged?.Invoke();
        }

        internal void RemoveAssignment(OfficeAssignment assignment, bool automatic)
        {
            if (assignment == null || !_assignments.Remove(assignment))
                return;

            if (assignment.OfficeType == OfficeType.Marshal)
                OfficeArmyService.DisbandLedArmy(assignment.Hero);

            LogAssignment(
                assignment.Kingdom,
                assignment.Hero,
                assignment.OfficeType,
                automatic ? "MP_OfficeInvalidated" : "MP_OfficeDismissed",
                automatic ? "automatically removed" : "dismissed");
            AssignmentsChanged?.Invoke();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            CleanupInvalidAssignments();
        }

        private void OnDailyTick()
        {
            CleanupInvalidAssignments();
            RemoveExcessLocalAssignments();
            ApplyDailyInfluence();
            LogPlayerKingdomSnapshot();

            // Territory changes can alter local seat limits without adding or removing an assignment.
            AssignmentsChanged?.Invoke();
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            RemoveHeroOffice(prisoner);
        }

        private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
        {
            RemoveHeroOffice(victim);
        }

        private void OnClanChangedKingdom(
            Clan clan,
            Kingdom oldKingdom,
            Kingdom newKingdom,
            ChangeKingdomAction.ChangeKingdomActionDetail detail,
            bool showNotification)
        {
            foreach (OfficeAssignment assignment in _assignments
                         .Where(item => item?.Hero?.Clan == clan && item.Kingdom != newKingdom)
                         .ToList())
            {
                RemoveAssignment(assignment, true);
            }
        }

        private void RemoveHeroOffice(Hero hero)
        {
            OfficeAssignment assignment = GetAssignment(hero);
            if (assignment != null)
                RemoveAssignment(assignment, true);
        }

        private void CleanupInvalidAssignments()
        {
            HashSet<Hero> assignedHeroes = new HashSet<Hero>();
            foreach (OfficeAssignment assignment in _assignments.ToList())
            {
                bool duplicate = assignment?.Hero != null && !assignedHeroes.Add(assignment.Hero);
                if (duplicate || !IsAssignmentStillValid(assignment))
                    RemoveAssignment(assignment, true);
            }
        }

        private static bool IsAssignmentStillValid(OfficeAssignment assignment)
        {
            if (assignment?.Kingdom == null || assignment.Kingdom.IsEliminated
                || assignment.Hero == null || !assignment.Hero.IsAlive
                || !assignment.Hero.IsActive || assignment.Hero.IsPrisoner
                || assignment.Hero.Clan?.Kingdom != assignment.Kingdom)
                return false;

            if (assignment.OfficeType == OfficeType.Marshal)
                return OfficeRules.IsMarshalAssignmentValid(assignment.Hero, assignment.Kingdom);

            return OfficeRules.IsEligible(assignment.Hero, assignment.Kingdom, assignment.OfficeType);
        }

        private void RemoveExcessLocalAssignments()
        {
            foreach (Kingdom kingdom in Kingdom.All.ToList())
            {
                foreach (OfficeType officeType in new[]
                         {
                             OfficeType.TaxOfficer,
                             OfficeType.AgricultureOfficer,
                             OfficeType.MilitaryOfficer,
                             OfficeType.SecurityOfficer
                         })
                {
                    int limit = OfficeRules.GetOfficeLimit(kingdom, officeType);
                    List<OfficeAssignment> ordered = GetAssignments(kingdom, officeType)
                        .OrderByDescending(item => OfficeRules.GetCandidateScore(item.Hero, officeType))
                        .ThenBy(item => item.Hero.StringId)
                        .ToList();

                    foreach (OfficeAssignment excess in ordered.Skip(limit).ToList())
                        RemoveAssignment(excess, true);
                }
            }
        }

        private void ApplyDailyInfluence()
        {
            foreach (IGrouping<Clan, OfficeAssignment> clanAssignments in _assignments
                         .Where(item => IsAssignmentStillValid(item))
                         .GroupBy(item => item.Hero.Clan))
            {
                float influence = clanAssignments.Sum(item => OfficeRules.IsLocal(item.OfficeType) ? 1f : 5f);
                if (influence > 0f)
                    ChangeClanInfluenceAction.Apply(clanAssignments.Key, influence);
            }
        }

        private void LogPlayerKingdomSnapshot()
        {
            Kingdom kingdom = Clan.PlayerClan?.Kingdom;
            if (kingdom == null || kingdom.IsEliminated
                || !OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config))
                return;

            foreach (OfficeType officeType in System.Enum.GetValues(typeof(OfficeType)))
            {
                if (!config.IsEnabled(officeType))
                    continue;

                List<OfficeAssignment> assignments = GetAssignments(kingdom, officeType).ToList();
                int limit = OfficeRules.GetOfficeLimit(kingdom, officeType);
                float dailyInfluence = OfficeRules.IsLocal(officeType) ? 1f : 5f;
                string holders = assignments.Count == 0
                    ? new TextObject("{=MP_OfficeVacant}vacant").ToString()
                    : string.Join(", ", assignments.Select(FormatAssignmentForLog));

                TextObject message = new TextObject(
                    "{=MP_OfficeDailySummary}[Hero Offices] {KINGDOM}: {OFFICE} {USED}/{LIMIT}; holders: {HOLDERS}; daily influence per holder: +{INFLUENCE}.");
                message.SetTextVariable("KINGDOM", kingdom.Name);
                message.SetTextVariable("OFFICE", OfficeText.GetName(officeType));
                message.SetTextVariable("USED", assignments.Count);
                message.SetTextVariable("LIMIT", limit);
                message.SetTextVariable("HOLDERS", holders);
                message.SetTextVariable("INFLUENCE", dailyInfluence);
                ModLogger.Info(message.ToString());
            }
        }

        private static string FormatAssignmentForLog(OfficeAssignment assignment)
        {
            if (assignment?.Hero == null)
                return new TextObject("{=MP_OfficeUnknownHero}unknown hero").ToString();

            if (!OfficeRules.IsLocal(assignment.OfficeType))
                return assignment.Hero.Name.ToString();

            TextObject holder = new TextObject("{=MP_OfficeLocalHolder}{HERO} ({SETTLEMENT})");
            holder.SetTextVariable("HERO", assignment.Hero.Name);
            holder.SetTextVariable(
                "SETTLEMENT",
                assignment.Hero.GovernorOf?.Settlement?.Name ?? TextObject.GetEmpty());
            return holder.ToString();
        }

        private static void LogAssignment(
            Kingdom kingdom,
            Hero hero,
            OfficeType officeType,
            string textId,
            string fallbackAction)
        {
            if (kingdom == null || Clan.PlayerClan?.Kingdom != kingdom)
                return;

            TextObject message = new TextObject(
                $"{{={textId}}}[Hero Offices] {{HERO}} was {fallbackAction} as {{OFFICE}} in {{KINGDOM}}.");
            message.SetTextVariable("HERO", hero?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable("OFFICE", OfficeText.GetName(officeType));
            message.SetTextVariable("KINGDOM", kingdom.Name);
            ModLogger.Notice(message.ToString());
        }
    }
}
