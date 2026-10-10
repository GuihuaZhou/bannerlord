using System.Collections.Generic;
using ModifiedPolitics.Governor.Notifications;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Services
{
    public sealed class GovernorNotificationService
    {
        public void Publish(GovernorAssignmentPlan plan, GovernorAssignmentResult result)
        {
            if (result.FailedTowns.Count > 0 || Campaign.Current?.CampaignInformationManager == null)
                return;

            var notified = new HashSet<Hero>();
            foreach (Town destination in plan.Targets.Keys)
            {
                Hero hero = result.After[destination];
                if (hero?.Clan != Clan.PlayerClan || result.Before[destination] == hero || !notified.Add(hero))
                    continue;

                Town origin = FindOldPost(result.Before, hero);
                Campaign.Current.CampaignInformationManager.NewMapNoticeAdded(
                    new GovernorAppointmentMapNotification(
                        hero,
                        origin?.Settlement,
                        destination.Settlement));
            }
        }

        private static Town FindOldPost(IReadOnlyDictionary<Town, Hero> before, Hero hero)
        {
            foreach (KeyValuePair<Town, Hero> pair in before)
            {
                if (pair.Value == hero)
                    return pair.Key;
            }

            return null;
        }
    }
}
