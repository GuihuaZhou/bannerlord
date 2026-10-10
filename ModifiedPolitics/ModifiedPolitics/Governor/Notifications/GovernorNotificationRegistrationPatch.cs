using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;

namespace ModifiedPolitics.Governor.Notifications
{
    [HarmonyPatch(typeof(MapNotificationVM), "PopulateTypeDictionary")]
    internal static class GovernorNotificationRegistrationPatch
    {
        [HarmonyPostfix]
        private static void RegisterGovernorNotification(MapNotificationVM __instance)
        {
            __instance.RegisterMapNotificationType(
                typeof(GovernorAppointmentMapNotification),
                typeof(GovernorAppointmentMapNotificationVM));
        }
    }
}
