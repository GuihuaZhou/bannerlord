using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Behaviors
{
    /// <summary>
    /// Lets AI heroes request vacant offices while limiting each applicant to one request per 28 days.
    /// </summary>
    public sealed class OfficeApplicationAiBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "_modifiedPoliticsOfficeApplicationTimes";
        private const float ApplicationCooldownDays = 28f;

        [SaveableField(1)]
        private Dictionary<Hero, CampaignTime> _lastApplicationTimes =
            new Dictionary<Hero, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _lastApplicationTimes);
            if (_lastApplicationTimes == null)
                _lastApplicationTimes = new Dictionary<Hero, CampaignTime>();
        }

        private void OnDailyTick()
        {
            CleanupCooldowns();
            if (HeroOfficeBehavior.Current == null)
                return;

            foreach (Kingdom kingdom in Kingdom.All.Where(IsValidKingdom).ToList())
                TryCreateApplication(kingdom);
        }

        private void TryCreateApplication(Kingdom kingdom)
        {
            if (!OfficeConfigManager.Instance.TryGet(kingdom, out OfficeCultureConfig config))
                return;

            ApplicationCandidate application = GetApplications(kingdom, config)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Hero.StringId)
                .FirstOrDefault();
            if (application == null)
                return;

            _lastApplicationTimes[application.Hero] = CampaignTime.Now;
            if (kingdom.Leader == Hero.MainHero)
            {
                ShowApplicationToPlayerRuler(kingdom, application);
                return;
            }

            OfficeApplicationService.TrySubmit(
                kingdom,
                application.Hero,
                application.OfficeType,
                out _,
                out _);
        }

        private IEnumerable<ApplicationCandidate> GetApplications(
            Kingdom kingdom,
            OfficeCultureConfig config)
        {
            OfficeType[] supportedOffices =
            {
                OfficeType.Marshal,
                OfficeType.TaxOfficer,
                OfficeType.AgricultureOfficer,
                OfficeType.MilitaryOfficer,
                OfficeType.SecurityOfficer
            };

            foreach (Hero hero in kingdom.Clans
                         .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                         .SelectMany(clan => clan.Heroes)
                         .Where(hero => hero != Hero.MainHero && IsCooldownComplete(hero)))
            {
                foreach (OfficeType officeType in supportedOffices)
                {
                    if (!config.IsEnabled(officeType)
                        || !OfficeApplicationService.CanApply(kingdom, hero, officeType, out _))
                        continue;

                    yield return new ApplicationCandidate(
                        hero,
                        officeType,
                        OfficeApplicationService.GetPoliticalCandidateScore(
                            kingdom.Leader,
                            hero,
                            officeType));
                }
            }
        }

        private void ShowApplicationToPlayerRuler(
            Kingdom kingdom,
            ApplicationCandidate application)
        {
            TextObject description = new TextObject(
                OfficeRules.IsLocal(application.OfficeType)
                    ? "{=MP_OfficeAiLocalApplicationDescription}{HERO} of {CLAN} requests appointment as {OFFICE}.\nGoverns: {SETTLEMENT}\nCandidate score: {SCORE}"
                    : "{=MP_OfficeAiApplicationDescription}{HERO} of {CLAN} requests appointment as {OFFICE}.\nCandidate score: {SCORE}");
            description.SetTextVariable("HERO", application.Hero.Name);
            description.SetTextVariable("CLAN", application.Hero.Clan?.Name ?? TextObject.GetEmpty());
            description.SetTextVariable("OFFICE", OfficeText.GetName(application.OfficeType));
            description.SetTextVariable(
                "SCORE",
                (int)OfficeRules.GetCandidateScore(application.Hero, application.OfficeType));
            if (OfficeRules.IsLocal(application.OfficeType))
            {
                description.SetTextVariable(
                    "SETTLEMENT",
                    application.Hero.GovernorOf?.Settlement?.Name ?? TextObject.GetEmpty());
            }

            InformationManager.ShowInquiry(
                new InquiryData(
                    new TextObject("{=MP_OfficeAiApplicationTitle}Office Application").ToString(),
                    description.ToString(),
                    true,
                    true,
                    new TextObject("{=MP_OfficeApplicationApprove}Approve").ToString(),
                    new TextObject("{=MP_OfficeApplicationReject}Reject").ToString(),
                    () => ApproveApplication(kingdom, application),
                    () => RejectApplication(kingdom, application),
                    string.Empty,
                    0f,
                    null,
                    null,
                    null),
                false,
                false);
        }

        private static void RejectApplication(
            Kingdom kingdom,
            ApplicationCandidate application)
        {
            OfficeApplicationService.RecordRejection(
                kingdom,
                application.Hero,
                application.OfficeType);
        }

        private static void ApproveApplication(
            Kingdom kingdom,
            ApplicationCandidate application)
        {
            if (!OfficeApplicationService.TryApprove(
                    kingdom,
                    application.Hero,
                    application.OfficeType,
                    out _,
                    out string message))
            {
                InformationManager.DisplayMessage(new InformationMessage(message));
            }
        }

        private bool IsCooldownComplete(Hero hero)
        {
            return !_lastApplicationTimes.TryGetValue(hero, out CampaignTime lastApplication)
                   || lastApplication == CampaignTime.Zero
                   || lastApplication.ElapsedDaysUntilNow >= ApplicationCooldownDays;
        }

        private void CleanupCooldowns()
        {
            foreach (Hero hero in _lastApplicationTimes.Keys
                         .Where(hero => hero == null
                                        || !hero.IsAlive
                                        || !hero.IsActive
                                        || hero.Clan?.Kingdom == null
                                        || hero.Clan.Kingdom.IsEliminated)
                         .ToList())
            {
                _lastApplicationTimes.Remove(hero);
            }
        }

        private static bool IsValidKingdom(Kingdom kingdom)
        {
            return kingdom != null && !kingdom.IsEliminated && kingdom.Leader != null;
        }

        private sealed class ApplicationCandidate
        {
            public ApplicationCandidate(Hero hero, OfficeType officeType, float score)
            {
                Hero = hero;
                OfficeType = officeType;
                Score = score;
            }

            public Hero Hero { get; }
            public OfficeType OfficeType { get; }
            public float Score { get; }
        }
    }
}
