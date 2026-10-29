using HarmonyLib;
using ModifiedArmy.PartyManagement.Bindings.Diagnostics;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ModifiedArmy.PartyManagement.Patches
{
    /// <summary>
    /// Read-only probe for validating the native creation sequence before a
    /// real-troop mobilization transaction is allowed to intercept it.
    /// </summary>
    [HarmonyPatch(typeof(LordPartyComponent), nameof(LordPartyComponent.CreateLordParty))]
    internal static class LordPartyCreationSequenceProbe
    {
        private static void Prefix(
            string stringId,
            Hero hero,
            CampaignVec2 position,
            float spawnRadius,
            Settlement spawnSettlement)
        {
            PartyBindingDiagnostics.LogCreationProbe(
                "CreateLordParty.Prefix",
                hero,
                null,
                spawnSettlement,
                null);
        }

        private static void Postfix(Hero hero, MobileParty __result)
        {
            PartyBindingDiagnostics.LogCreationProbe(
                "CreateLordParty.Postfix",
                hero,
                __result,
                null,
                null);
        }
    }

    [HarmonyPatch]
    internal static class HeroSpawnLordPartySequenceProbe
    {
        private static MethodBase TargetMethod() => AccessTools.Method(
            typeof(HeroSpawnCampaignBehavior),
            "SpawnLordParty",
            new[] { typeof(Hero), typeof(bool) });

        private static void Prefix(Hero hero, bool isNewGame)
        {
            PartyBindingDiagnostics.LogCreationProbe(
                "HeroSpawn.Prefix", hero, null, null, isNewGame);
        }

        private static void Postfix(
            Hero hero,
            bool isNewGame,
            MobileParty __result)
        {
            PartyBindingDiagnostics.LogCreationProbe(
                "HeroSpawn.Postfix", hero, __result, null, isNewGame);
        }
    }
}
