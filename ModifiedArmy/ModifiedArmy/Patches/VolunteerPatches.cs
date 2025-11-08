

using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static System.Net.Mime.MediaTypeNames;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ScreenSystem; // 确保添加了这一行


namespace ModifiedArmy.Patches
{
    // =============== 全局缓存结构 ===============
    public static class VolunteerCandidateCache
    {
        public enum ContextType
        {
            TownOrVillageToTown,   // 城镇 / 村庄→城镇
            VillageToCastle        // 村庄→城堡
            // Wild/Camp 不缓存（直接返回 BasicTroop）
        }

        // 主缓存：Context → Culture → (troops, weights)
        public static readonly Dictionary<ContextType, Dictionary<CultureObject, (List<CharacterObject>, List<int>)>> Cache =
            new()
            {
                { ContextType.TownOrVillageToTown, new Dictionary<CultureObject, (List<CharacterObject>, List<int>)>() },
                { ContextType.VillageToCastle, new Dictionary<CultureObject, (List<CharacterObject>, List<int>)>() }
            };
    }

    // =============== 自定义兵种数据管理 ===============
    public static class CultureBasicTroopManager
    {
        public struct TroopEntry
        {
            public CharacterObject Troop;
            public int Weight;

            public TroopEntry(CharacterObject troop, int weight)
            {
                Troop = troop ?? throw new ArgumentNullException(nameof(troop));
                Weight = Math.Max(1, weight);
            }
        }

        // CultureObject → (type → TroopEntry)
        public static readonly Dictionary<CultureObject, Dictionary<string, TroopEntry>> Data =
            new Dictionary<CultureObject, Dictionary<string, TroopEntry>>();
    }

    // =============== 读取xlm的basic troop和权重 ===============
    [HarmonyPatch(typeof(CultureObject), "Deserialize")]
    public static class Patch_CultureObject_Deserialize
    {
        public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
        {
            // === 1. 读取自定义 troop 和权重 ===
            var entries = new Dictionary<string, CultureBasicTroopManager.TroopEntry>();
            var typeMap = new Dictionary<string, (string troopAttr, string weightAttr)>
            {
                { "basic", ("basic_troop", "basic_troop_weight") },
                { "elite", ("elite_basic_troop", "elite_basic_troop_weight") },
                { "infantry", ("basic_infantry_troop", "basic_infantry_troop_weight") },
                { "ranged", ("basic_ranged_troop", "basic_ranged_troop_weight") },
                { "cavalry", ("basic_cavalry_troop", "basic_cavalry_troop_weight") },
                { "horsearcher", ("basic_horsearcher_troop", "basic_horsearcher_troop_weight") },
                { "twohanded", ("basic_twohanded_troop", "basic_twohanded_troop_weight") }
            };

            foreach (var kvp in typeMap)
            {
                string type = kvp.Key;
                string troopAttr = kvp.Value.troopAttr;
                string weightAttr = kvp.Value.weightAttr;

                CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>(troopAttr, node);
                if (troop == null) continue;

                int weight = 20;
                if (int.TryParse(node.Attributes?[weightAttr]?.Value, out int w))
                    weight = Math.Max(1, w);

                entries[type] = new CultureBasicTroopManager.TroopEntry(troop, weight);
            }

            CultureBasicTroopManager.Data[__instance] = entries;

            // === 2. 预计算并缓存 candidates ===
            PrecomputeAndCacheCandidates(__instance, entries);
        }

        private static void PrecomputeAndCacheCandidates(CultureObject culture, Dictionary<string, CultureBasicTroopManager.TroopEntry> entries)
        {
            // Context 1: TownOrVillageToTown
            var list1 = BuildCandidateList(entries, culture, VolunteerCandidateCache.ContextType.TownOrVillageToTown);
            VolunteerCandidateCache.Cache[VolunteerCandidateCache.ContextType.TownOrVillageToTown][culture] = list1;

            // Context 2: VillageToCastle
            var list2 = BuildCandidateList(entries, culture, VolunteerCandidateCache.ContextType.VillageToCastle);
            VolunteerCandidateCache.Cache[VolunteerCandidateCache.ContextType.VillageToCastle][culture] = list2;
        }

        private static (List<CharacterObject>, List<int>) BuildCandidateList(
            Dictionary<string, CultureBasicTroopManager.TroopEntry> entries,
            CultureObject culture,
            VolunteerCandidateCache.ContextType context)
        {
            var troops = new List<CharacterObject>();
            var weights = new List<int>();

            void AddIfDefined(string type)
            {
                if (entries.TryGetValue(type, out var entry))
                {
                    troops.Add(entry.Troop);
                    weights.Add(entry.Weight);
                }
            }

            switch (context)
            {
                case VolunteerCandidateCache.ContextType.TownOrVillageToTown:
                    AddIfDefined("infantry");
                    AddIfDefined("twohanded");
                    AddIfDefined("ranged");
                    AddIfDefined("cavalry");
                    AddIfDefined("horsearcher");
                    AddIfDefined("basic"); // fallback

                    if (troops.Count == 0 && culture.BasicTroop != null)
                    {
                        troops.Add(culture.BasicTroop);
                        weights.Add(15);
                    }
                    break;

                case VolunteerCandidateCache.ContextType.VillageToCastle:
                    AddIfDefined("infantry");
                    AddIfDefined("twohanded");
                    AddIfDefined("ranged");
                    AddIfDefined("cavalry");
                    AddIfDefined("horsearcher");
                    AddIfDefined("elite");

                    // 补充原版 elite（如果自定义未定义）
                    if (!entries.ContainsKey("elite") && culture.EliteBasicTroop != null)
                    {
                        troops.Add(culture.EliteBasicTroop);
                        weights.Add(1);
                    }

                    AddIfDefined("basic"); // fallback

                    if (troops.Count == 0 && culture.BasicTroop != null)
                    {
                        troops.Add(culture.BasicTroop);
                        weights.Add(15);
                    }
                    break;
            }

            return (troops, weights);
        }
    }

    // =============== 按权重生成志愿兵 ===============
    [HarmonyPatch(typeof(DefaultVolunteerModel), "GetBasicVolunteer")]
    public class Patch_GetBasicVolunteer
    {
        private static CharacterObject WeightedRandomSelect(List<CharacterObject> troops, List<int> weights)
        {
            if (troops.Count == 0) return null;
            int total = 0;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0) return troops[0];
            int rand = MBRandom.RandomInt(total);
            int sum = 0;
            for (int i = 0; i < troops.Count; i++)
            {
                sum += weights[i];
                if (rand < sum)
                    return troops[i];
            }
            return troops[troops.Count - 1];
        }

        public static bool Prefix(Hero sellerHero, ref CharacterObject __result)
        {
            if (!CampaignState.IsReady || sellerHero?.Culture == null || sellerHero.CurrentSettlement == null)
                return true;

            var culture = sellerHero.Culture;
            var settlement = sellerHero.CurrentSettlement;

            // === Wild / Camp: 直接返回 BasicTroop ===
            if (!settlement.IsTown &&
                !(settlement.IsVillage && (settlement.Village.Bound.IsTown || settlement.Village.Bound.IsCastle)))
            {
                __result = culture.BasicTroop;
                return false;
            }

            // === 确定上下文类型 ===
            var context = settlement.IsTown || settlement.Village.Bound.IsTown
                ? VolunteerCandidateCache.ContextType.TownOrVillageToTown
                : VolunteerCandidateCache.ContextType.VillageToCastle;

            // === 查缓存并选择 ===
            if (VolunteerCandidateCache.Cache[context].TryGetValue(culture, out var candidateData))
            {
                var (troops, weights) = candidateData;
                __result = WeightedRandomSelect(troops, weights) ?? culture.BasicTroop;
            }
            else
            {
                // 安全 fallback（理论上不会触发）
                __result = culture.BasicTroop;
            }

            //InformationManager.DisplayMessage(new InformationMessage($"[MOD] FINAL SELECTED: {__result.Name}"));

            return false;
        }
    }
}