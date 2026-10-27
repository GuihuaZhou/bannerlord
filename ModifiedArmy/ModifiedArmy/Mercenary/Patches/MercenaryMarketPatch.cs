using HarmonyLib;
using ModifiedArmy.Mercenary.Services;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Mercenary.Patches
{
    /// <summary>
    /// Replaces tavern mercenary generation with the settlement culture's
    /// XML-configured probability and weighted troop pool. Each troop entry
    /// owns its exact generation range.
    /// Caravan guards retain their separate native refresh chance.
    /// </summary>
    [HarmonyPatch(
        typeof(RecruitmentCampaignBehavior),
        "UpdateCurrentMercenaryTroopAndCount")]
    public static class MercenaryMarketPatch
    {
        public static bool Prefix(
            RecruitmentCampaignBehavior __instance,
            Town town,
            bool forceUpdate = false)
        {
            MercenaryMarketService.Refresh(__instance, town, forceUpdate);
            return false;
        }
    }
}
