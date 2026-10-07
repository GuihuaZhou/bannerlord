using System;
using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.HeroOffices.Domain;
using ModifiedPolitics.HeroOffices.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.HeroOffices.Behaviors
{
    /// <summary>
    /// Opens an appointment decision immediately for an AI kingdom with a marshal vacancy.
    /// </summary>
    public sealed class MarshalOfficeAiBehavior : CampaignBehaviorBase
    {
        private const string SaveKey = "_modifiedPoliticsMarshalReviewTimes";

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

            foreach (Kingdom kingdom in Kingdom.All
                         .Where(item => item != null
                                        && !item.IsEliminated
                                        && item.Leader != null
                                        && item.Leader != Hero.MainHero)
                         .ToList())
            {
                OfficeAssignment marshal = behavior
                    .GetAssignments(kingdom, OfficeType.Marshal)
                    .SingleOrDefault();
                if (marshal == null)
                {
                    MarshalDecisionService.TryProposeAppointment(kingdom);
                    continue;
                }

                bool reviewDue = !_lastReviewTimes.TryGetValue(kingdom, out CampaignTime lastReview)
                                 || lastReview == CampaignTime.Zero
                                 || lastReview.ElapsedDaysUntilNow >= 28f;
                if (!reviewDue)
                    continue;

                ReviewMarshal(behavior, kingdom, marshal);
                _lastReviewTimes[kingdom] = CampaignTime.Now;
            }

            foreach (Kingdom staleKingdom in _lastReviewTimes.Keys
                         .Where(kingdom => kingdom == null || kingdom.IsEliminated)
                         .ToList())
                _lastReviewTimes.Remove(staleKingdom);
        }

        private static void ReviewMarshal(
            HeroOfficeBehavior behavior,
            Kingdom kingdom,
            OfficeAssignment marshal)
        {
            float currentScore = Math.Max(
                0f,
                OfficeRules.GetCandidateScore(marshal.Hero, OfficeType.Marshal));
            Hero bestCandidate = kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => behavior.GetAssignment(hero) == null)
                .Where(hero => OfficeRules.IsEligible(hero, kingdom, OfficeType.Marshal))
                .OrderByDescending(hero => OfficeRules.GetCandidateScore(hero, OfficeType.Marshal))
                .ThenBy(hero => hero.StringId)
                .FirstOrDefault();
            if (bestCandidate == null)
                return;

            float bestScore = Math.Max(
                0f,
                OfficeRules.GetCandidateScore(bestCandidate, OfficeType.Marshal));
            bool clearsThreshold = currentScore > 0f
                ? bestScore >= currentScore * 1.25f
                : bestScore > 0f;
            if (clearsThreshold)
                MarshalDecisionService.TryProposeDismissal(kingdom);
        }
    }
}
