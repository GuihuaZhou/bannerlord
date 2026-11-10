using HarmonyLib;
using ModifiedArmy.Army.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Army.Patches
{
    /// <summary>
    /// Read-only observation points for native marshal army scoring. These patches
    /// never alter arguments, collections, random state, or return values.
    /// </summary>
    [HarmonyPatch]
    internal static class MarshalArmyDebugPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(CampaignEventDispatcher),
            nameof(CampaignEventDispatcher.AiHourlyTick),
            new[] { typeof(MobileParty), typeof(PartyThinkParams) })]
        private static void TraceFinalBehaviorPool(MobileParty party, PartyThinkParams partyThinkParams)
        {
            MarshalArmyDebugTrace.TraceFinalPool(party, partyThinkParams);
        }
    }
}
