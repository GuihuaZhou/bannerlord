using HarmonyLib;
using ModifiedPolitics.HeroOffices.Diagnostics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.HeroOffices.Patches
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
            typeof(DefaultTargetScoreCalculatingModel),
            nameof(DefaultTargetScoreCalculatingModel.GetTargetScoreForFaction),
            new[] { typeof(Settlement), typeof(Army.ArmyTypes), typeof(MobileParty), typeof(float) })]
        private static void TraceNativeTargetScore(
            Settlement targetSettlement,
            Army.ArmyTypes missionType,
            MobileParty mobileParty,
            float ourStrength,
            float __result)
        {
            MarshalArmyDebugTrace.RecordTargetScore(
                targetSettlement,
                missionType,
                mobileParty,
                ourStrength,
                __result);
        }

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
