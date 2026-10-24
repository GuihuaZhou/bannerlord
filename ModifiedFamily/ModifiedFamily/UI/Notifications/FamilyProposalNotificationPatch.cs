// Registers the custom proposal notification with the native map notification view model.
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map;

namespace ModifiedFamily.UI.Notifications
{
    [HarmonyPatch(typeof(MapNotificationVM), "PopulateTypeDictionary")]
    internal static class FamilyProposalNotificationPatch
    {
        [HarmonyPostfix]
        private static void Register(MapNotificationVM __instance)
        {
            __instance.RegisterMapNotificationType(typeof(FamilyProposalMapNotification), typeof(FamilyProposalMapNotificationVM));
        }
    }
}
