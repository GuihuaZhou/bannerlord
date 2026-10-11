using System;
using System.Linq;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Scopes the settlement override to native lord-party spawning so other
    /// callers of SettlementHelper retain their original behavior.
    /// </summary>
    [HarmonyPatch]
    public static class HeroSpawnSettlementPatch
    {
        [ThreadStatic]
        private static Hero _spawningHero;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty")]
        private static void BeginSpawn(Hero hero)
        {
            _spawningHero = hero;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HeroSpawnCampaignBehavior), "SpawnLordParty")]
        private static void EndSpawn()
        {
            _spawningHero = null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SettlementHelper), "GetBestSettlementToSpawnAround")]
        private static void PreferClanFortification(
            Hero hero,
            ref Settlement __result)
        {
            if (hero == null || hero != _spawningHero || hero.Clan == null)
            {
                return;
            }

            Settlement origin = hero.LastKnownClosestSettlement ?? __result;
            Settlement preferred = hero.Clan.Settlements
                .Where(settlement =>
                    settlement?.IsFortification == true &&
                    !settlement.IsUnderSiege &&
                    settlement.Town?.GarrisonParty != null &&
                    (origin == null ||
                        GetDistance(origin, settlement) < float.MaxValue))
                .OrderByDescending(settlement =>
                    settlement.Town.GarrisonParty.MemberRoster
                        .TotalHealthyCount)
                .ThenByDescending(settlement => GetTransferableCount(settlement))
                .ThenBy(settlement => GetDistance(origin, settlement))
                .FirstOrDefault();

            if (preferred != null)
            {
                __result = preferred;
            }
        }

        private static int GetTransferableCount(Settlement settlement)
        {
            int minimum = settlement.IsTown ? 125 : 75;
            return Math.Max(
                0,
                settlement.Town.GarrisonParty.Party.NumberOfRegularMembers -
                    minimum);
        }

        private static float GetDistance(
            Settlement origin,
            Settlement destination)
        {
            if (origin == null || destination == null)
            {
                return float.MaxValue;
            }

            return Campaign.Current.Models.MapDistanceModel.GetDistance(
                origin,
                destination,
                false,
                false,
                MobileParty.NavigationType.Default);
        }
    }
}
