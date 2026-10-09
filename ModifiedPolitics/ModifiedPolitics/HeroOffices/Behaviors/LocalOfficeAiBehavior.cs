using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Behaviors
{
    /// <summary>
    /// Reviews occupied local offices every 28 days; vacancies are filled through applications.
    /// </summary>
    public sealed class LocalOfficeAiBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "_modifiedPoliticsLocalOfficeReviewTimes";
        private static readonly OfficeType[] LocalOffices =
        {
            OfficeType.TaxOfficer,
            OfficeType.AgricultureOfficer,
            OfficeType.MilitaryOfficer,
            OfficeType.SecurityOfficer
        };

        [SaveableField(1)]
        private Dictionary<Kingdom, CampaignTime> _lastReviewTimes =
            new Dictionary<Kingdom, CampaignTime>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(SaveKey, ref _lastReviewTimes);
            if (_lastReviewTimes == null)
                _lastReviewTimes = new Dictionary<Kingdom, CampaignTime>();
        }

        private void OnDailyTick()
        {
            HeroOfficeBehavior behavior = HeroOfficeBehavior.Current;
            if (behavior == null)
                return;

            foreach (Kingdom kingdom in Kingdom.All.Where(IsAiKingdom).ToList())
            {
                bool reviewDue = !_lastReviewTimes.TryGetValue(kingdom, out CampaignTime lastReview)
                                 || lastReview == CampaignTime.Zero
                                 || lastReview.ElapsedDaysUntilNow >= 28f;
                if (!reviewDue)
                    continue;

                ReviewOccupiedOffices(behavior, kingdom);
                _lastReviewTimes[kingdom] = CampaignTime.Now;
            }

            foreach (Kingdom staleKingdom in _lastReviewTimes.Keys
                         .Where(kingdom => kingdom == null || kingdom.IsEliminated)
                         .ToList())
            {
                _lastReviewTimes.Remove(staleKingdom);
            }
        }

        private static bool IsAiKingdom(Kingdom kingdom)
        {
            return kingdom != null
                   && !kingdom.IsEliminated
                   && kingdom.Leader != null
                   && kingdom.Leader != Hero.MainHero;
        }

        private static void ReviewOccupiedOffices(HeroOfficeBehavior behavior, Kingdom kingdom)
        {
            foreach (OfficeType officeType in LocalOffices)
            {
                List<OfficeAssignment> incumbents = behavior.GetAssignments(kingdom, officeType).ToList();
                if (incumbents.Count == 0)
                    continue;

                OfficeAssignment weakest = incumbents
                    .OrderBy(item => OfficeRules.GetCandidateScore(item.Hero, officeType))
                    .ThenByDescending(item => item.Hero.StringId)
                    .First();
                float incumbentScore = Math.Max(0f, OfficeRules.GetCandidateScore(weakest.Hero, officeType));

                Hero challenger = GetAvailableCandidates(behavior, kingdom, officeType)
                    .OrderByDescending(hero => OfficeRules.GetCandidateScore(hero, officeType))
                    .ThenBy(hero => hero.StringId)
                    .FirstOrDefault();
                if (challenger == null)
                    continue;

                float challengerScore = Math.Max(0f, OfficeRules.GetCandidateScore(challenger, officeType));
                bool clearsThreshold = incumbentScore > 0f
                    ? challengerScore >= incumbentScore * 1.25f
                    : challengerScore > 0f;
                if (!clearsThreshold || !ShouldReplace(kingdom, weakest.Hero, challenger, incumbentScore, challengerScore))
                    continue;

                OfficeCompensation compensation = ChooseCompensation(kingdom.Leader);
                if (OfficeAppointmentService.TryDismiss(
                        kingdom,
                        kingdom.Leader,
                        weakest.Hero,
                        compensation,
                        out _))
                {
                    OfficeAppointmentService.TryAppoint(
                        kingdom,
                        kingdom.Leader,
                        challenger,
                        officeType,
                        out _);
                }
            }
        }

        private static IEnumerable<Hero> GetAvailableCandidates(
            HeroOfficeBehavior behavior,
            Kingdom kingdom,
            OfficeType officeType)
        {
            return kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => behavior.GetAssignment(hero) == null)
                .Where(hero => OfficeRules.IsEligible(hero, kingdom, officeType));
        }

        private static bool ShouldReplace(
            Kingdom kingdom,
            Hero incumbent,
            Hero challenger,
            float incumbentScore,
            float challengerScore)
        {
            int incumbentRelation = kingdom.Leader.GetRelation(incumbent.Clan.Leader);
            int challengerRelation = kingdom.Leader.GetRelation(challenger.Clan.Leader);
            float skillGain = challengerScore - incumbentScore;
            float politicalCost = Math.Max(0, incumbentRelation - challengerRelation) * 2f;

            // A ruler accepts political damage only when the office improvement clearly outweighs it.
            return skillGain >= politicalCost;
        }

        private static OfficeCompensation ChooseCompensation(Hero ruler)
        {
            if (ruler.Gold >= 140000)
                return OfficeCompensation.High;
            if (ruler.Gold >= 75000)
                return OfficeCompensation.Medium;
            if (ruler.Gold >= 35000)
                return OfficeCompensation.Low;
            return OfficeCompensation.None;
        }
    }
}
