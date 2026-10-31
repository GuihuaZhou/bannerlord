using HarmonyLib;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.PartyManagement.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Adapts the native commander/score-selected creation call to the managed
    /// creation coordinator. All non-managed callers retain the native method.
    /// </summary>
    [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty")]
    public static class HeroSpawnSettlementPatch
    {
        [HarmonyPrefix]
        private static bool CreateAtBoundClanBase(
            Hero hero, bool isNewGame, ref MobileParty __result)
        {
            if (!ManagedPartyPolicy.IsModManagedAiLeader(hero)) return true;
            __result = new PartyCreationCoordinator()
                .TryCreateManagedLordParty(hero, isNewGame);
            return false;
        }
    }
}
