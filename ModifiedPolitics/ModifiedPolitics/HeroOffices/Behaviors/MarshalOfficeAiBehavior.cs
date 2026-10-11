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
    /// Reviews occupied marshal offices; vacancies are initiated through applications.
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
                List<OfficeAssignment> marshals = behavior
                    .GetAssignments(kingdom, OfficeType.Marshal)
                    .Where(item => item?.Hero != null)
                    .ToList();
                if (marshals.Count == 0)
                    continue;

                OfficeAssignment invalidMarshal = marshals.FirstOrDefault(
                    item => !OfficeRules.IsEligible(item.Hero, kingdom, OfficeType.Marshal));
                // Losing the commanded war party invalidates marshal eligibility, but dismissal
                // still goes through the existing kingdom decision instead of silent removal.
                if (invalidMarshal != null)
                {
                    MarshalDecisionService.TryProposeDismissal(kingdom, invalidMarshal.Hero);
                    continue;
                }

                bool reviewDue = !_lastReviewTimes.TryGetValue(kingdom, out CampaignTime lastReview)
                                 || lastReview == CampaignTime.Zero
                                 || lastReview.ElapsedDaysUntilNow >= 28f;
                if (!reviewDue)
                    continue;

                ReviewMarshals(behavior, kingdom, marshals);
                _lastReviewTimes[kingdom] = CampaignTime.Now;
            }

            foreach (Kingdom staleKingdom in _lastReviewTimes.Keys
                         .Where(kingdom => kingdom == null || kingdom.IsEliminated)
                         .ToList())
                _lastReviewTimes.Remove(staleKingdom);
        }

        private static void ReviewMarshals(
            HeroOfficeBehavior behavior,
            Kingdom kingdom,
            IReadOnlyCollection<OfficeAssignment> marshals)
        {
            OfficeAssignment marshal = marshals
                .OrderBy(item => OfficeRules.GetMarshalAppointmentScore(
                    item.Hero,
                    kingdom))
                .First();
            Hero bestCandidate = kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Where(hero => behavior.GetAssignment(hero) == null)
                .Where(hero => OfficeRules.IsEligible(hero, kingdom, OfficeType.Marshal))
                .Where(hero => OfficeRules.IsMarshalClanAvailable(hero, kingdom))
                .OrderByDescending(hero =>
                    OfficeRules.GetMarshalAppointmentScore(hero, kingdom))
                .ThenBy(hero => hero.StringId)
                .FirstOrDefault();
            if (bestCandidate == null)
                return;

            if (MarshalAppointmentScoreModel.ShouldReplace(
                    marshal.Hero,
                    bestCandidate,
                    kingdom))
                MarshalDecisionService.TryProposeDismissal(kingdom, marshal.Hero);
        }
    }
}
