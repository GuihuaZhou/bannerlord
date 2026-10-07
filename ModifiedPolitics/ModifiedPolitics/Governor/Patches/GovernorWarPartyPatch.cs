using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedPolitics.Governor.Patches
{
    /// <summary>
    /// Keeps governorship mutually exclusive with command of a lord party.
    /// </summary>
    [HarmonyPatch]
    public static class GovernorWarPartyPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "GetHeroPartyCommandScore")]
        private static bool GetHeroPartyCommandScorePrefix(Hero hero, ref float __result)
        {
            if (hero?.GovernorOf == null)
                return true;

            // Exclude governors during scoring so the normal AI path cannot select them.
            __result = float.MinValue;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty")]
        private static bool SpawnLordPartyPrefix(Hero hero, ref MobileParty __result)
        {
            if (hero?.GovernorOf == null)
                return true;

            // Reject direct spawn attempts that bypass the command-score selection path.
            __result = null;
            return false;
        }
    }
}
