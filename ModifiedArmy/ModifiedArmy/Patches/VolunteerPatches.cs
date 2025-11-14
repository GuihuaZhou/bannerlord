

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
    //// =============== 全局缓存结构 ===============
    //public static class VolunteerCandidateCache
    //{
    //    public enum ContextType
    //    {
    //        TownOrVillageToTown,   // 城镇 / 村庄→城镇
    //        VillageToCastle        // 村庄→城堡
    //        // Wild/Camp 不缓存（直接返回 BasicTroop）
    //    }

    //    // 主缓存：Context → Culture → (troops, weights)
    //    public static readonly Dictionary<ContextType, Dictionary<CultureObject, (List<CharacterObject>, List<int>)>> Cache =
    //        new()
    //        {
    //            { ContextType.TownOrVillageToTown, new Dictionary<CultureObject, (List<CharacterObject>, List<int>)>() },
    //            { ContextType.VillageToCastle, new Dictionary<CultureObject, (List<CharacterObject>, List<int>)>() }
    //        };
    //}

    //// =============== 自定义兵种数据管理 ===============
    //public static class CultureBasicTroopManager
    //{
    //    public struct TroopEntry
    //    {
    //        public CharacterObject Troop;
    //        public int Weight;

    //        public TroopEntry(CharacterObject troop, int weight)
    //        {
    //            Troop = troop ?? throw new ArgumentNullException(nameof(troop));
    //            Weight = Math.Max(1, weight);
    //        }
    //    }

    //    // CultureObject → (type → TroopEntry)
    //    public static readonly Dictionary<CultureObject, Dictionary<string, TroopEntry>> Data =
    //        new Dictionary<CultureObject, Dictionary<string, TroopEntry>>();
    //}

    //// =============== 读取xlm的basic troop和权重 ===============
    //[HarmonyPatch(typeof(CultureObject), "Deserialize")]
    //public static class Patch_CultureObject_Deserialize
    //{
    //    public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
    //    {
    //        // === 1. 读取自定义 troop 和权重 ===
    //        var entries = new Dictionary<string, CultureBasicTroopManager.TroopEntry>();
    //        var typeMap = new Dictionary<string, (string troopAttr, string weightAttr)>
    //        {
    //            { "basic", ("basic_troop", "basic_troop_weight") },
    //            { "elite", ("elite_basic_troop", "elite_basic_troop_weight") },
    //            { "infantry", ("basic_infantry_troop", "basic_infantry_troop_weight") },
    //            { "ranged", ("basic_ranged_troop", "basic_ranged_troop_weight") },
    //            { "cavalry", ("basic_cavalry_troop", "basic_cavalry_troop_weight") },
    //            { "horsearcher", ("basic_horsearcher_troop", "basic_horsearcher_troop_weight") },
    //            { "twohanded", ("basic_twohanded_troop", "basic_twohanded_troop_weight") }
    //        };

    //        foreach (var kvp in typeMap)
    //        {
    //            string type = kvp.Key;
    //            string troopAttr = kvp.Value.troopAttr;
    //            string weightAttr = kvp.Value.weightAttr;

    //            CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>(troopAttr, node);
    //            if (troop == null) continue;

    //            int weight = 20;
    //            if (int.TryParse(node.Attributes?[weightAttr]?.Value, out int w))
    //                weight = Math.Max(1, w);

    //            entries[type] = new CultureBasicTroopManager.TroopEntry(troop, weight);
    //        }

    //        CultureBasicTroopManager.Data[__instance] = entries;

    //        // === 2. 预计算并缓存 candidates ===
    //        PrecomputeAndCacheCandidates(__instance, entries);
    //    }

    //    private static void PrecomputeAndCacheCandidates(CultureObject culture, Dictionary<string, CultureBasicTroopManager.TroopEntry> entries)
    //    {
    //        // Context 1: TownOrVillageToTown
    //        var list1 = BuildCandidateList(entries, culture, VolunteerCandidateCache.ContextType.TownOrVillageToTown);
    //        VolunteerCandidateCache.Cache[VolunteerCandidateCache.ContextType.TownOrVillageToTown][culture] = list1;

    //        // Context 2: VillageToCastle
    //        var list2 = BuildCandidateList(entries, culture, VolunteerCandidateCache.ContextType.VillageToCastle);
    //        VolunteerCandidateCache.Cache[VolunteerCandidateCache.ContextType.VillageToCastle][culture] = list2;
    //    }

    //    private static (List<CharacterObject>, List<int>) BuildCandidateList(
    //        Dictionary<string, CultureBasicTroopManager.TroopEntry> entries,
    //        CultureObject culture,
    //        VolunteerCandidateCache.ContextType context)
    //    {
    //        var troops = new List<CharacterObject>();
    //        var weights = new List<int>();

    //        void AddIfDefined(string type)
    //        {
    //            if (entries.TryGetValue(type, out var entry))
    //            {
    //                troops.Add(entry.Troop);
    //                weights.Add(entry.Weight);
    //            }
    //        }

    //        switch (context)
    //        {
    //            case VolunteerCandidateCache.ContextType.TownOrVillageToTown:
    //                AddIfDefined("infantry");
    //                AddIfDefined("twohanded");
    //                AddIfDefined("ranged");
    //                AddIfDefined("cavalry");
    //                AddIfDefined("horsearcher");
    //                AddIfDefined("basic"); // fallback

    //                if (troops.Count == 0 && culture.BasicTroop != null)
    //                {
    //                    troops.Add(culture.BasicTroop);
    //                    weights.Add(15);
    //                }
    //                break;

    //            case VolunteerCandidateCache.ContextType.VillageToCastle:
    //                AddIfDefined("infantry");
    //                AddIfDefined("twohanded");
    //                AddIfDefined("ranged");
    //                AddIfDefined("cavalry");
    //                AddIfDefined("horsearcher");
    //                AddIfDefined("elite");

    //                // 补充原版 elite（如果自定义未定义）
    //                if (!entries.ContainsKey("elite") && culture.EliteBasicTroop != null)
    //                {
    //                    troops.Add(culture.EliteBasicTroop);
    //                    weights.Add(1);
    //                }

    //                AddIfDefined("basic"); // fallback

    //                if (troops.Count == 0 && culture.BasicTroop != null)
    //                {
    //                    troops.Add(culture.BasicTroop);
    //                    weights.Add(15);
    //                }
    //                break;
    //        }

    //        return (troops, weights);
    //    }
    //}

    //// =============== 按权重生成志愿兵 ===============
    //[HarmonyPatch(typeof(DefaultVolunteerModel), "GetBasicVolunteer")]
    //public class Patch_GetBasicVolunteer
    //{
    //    private static CharacterObject WeightedRandomSelect(List<CharacterObject> troops, List<int> weights)
    //    {
    //        if (troops.Count == 0) return null;
    //        int total = 0;
    //        for (int i = 0; i < weights.Count; i++) total += weights[i];
    //        if (total <= 0) return troops[0];
    //        int rand = MBRandom.RandomInt(total);
    //        int sum = 0;
    //        for (int i = 0; i < troops.Count; i++)
    //        {
    //            sum += weights[i];
    //            if (rand < sum)
    //                return troops[i];
    //        }
    //        return troops[troops.Count - 1];
    //    }

    //    public static bool Prefix(Hero sellerHero, ref CharacterObject __result)
    //    {
    //        if (!CampaignState.IsReady || sellerHero?.Culture == null || sellerHero.CurrentSettlement == null)
    //            return true;

    //        var culture = sellerHero.Culture;
    //        var settlement = sellerHero.CurrentSettlement;

    //        // === Wild / Camp: 直接返回 BasicTroop ===
    //        if (!settlement.IsTown &&
    //            !(settlement.IsVillage && (settlement.Village.Bound.IsTown || settlement.Village.Bound.IsCastle)))
    //        {
    //            __result = culture.BasicTroop;
    //            return false;
    //        }

    //        // === 确定上下文类型 ===
    //        var context = settlement.IsTown || settlement.Village.Bound.IsTown
    //            ? VolunteerCandidateCache.ContextType.TownOrVillageToTown
    //            : VolunteerCandidateCache.ContextType.VillageToCastle;

    //        // === 查缓存并选择 ===
    //        if (VolunteerCandidateCache.Cache[context].TryGetValue(culture, out var candidateData))
    //        {
    //            var (troops, weights) = candidateData;
    //            __result = WeightedRandomSelect(troops, weights) ?? culture.BasicTroop;
    //        }
    //        else
    //        {
    //            // 安全 fallback（理论上不会触发）
    //            __result = culture.BasicTroop;
    //        }

    //        //InformationManager.DisplayMessage(new InformationMessage($"[MOD] FINAL SELECTED: {__result.Name}"));

    //        return false;
    //    }
    //}


    

    // 这个类代表一个包含 Character, Weight, Type 的单元
    public class CharacterWeightPair
    {
        public CharacterObject Character { get; set; } // 使用 CharacterObject
        public float Weight { get; set; }
        public TroopType Type { get; set; }

        public CharacterWeightPair(CharacterObject character, float weight, TroopType type)
        {
            Character = character;
            Weight = weight;
            Type = type;
        }
    }

    // 记录一个culture的全部troop和weight
    public class CultureVolunteerGroupsCache
    {
        public static CultureVolunteerGroupsCache Instance { get; private set; } = new CultureVolunteerGroupsCache();

        // 使用 CultureObject 作为 Key
        public Dictionary<CultureObject, List<CharacterWeightPair>> CultureVolunteerData { get; private set; }

        private CultureVolunteerGroupsCache()
        {
            CultureVolunteerData = new Dictionary<CultureObject, List<CharacterWeightPair>>();
            // LoadData(); // 不再需要手动加载，由 Patch_CultureObject_Deserialize 触发
        }

        // 内部方法，供 Patch_CultureObject_Deserialize 调用以填充缓存
        internal void AddOrReplaceVolunteerData(CultureObject culture, List<CharacterWeightPair> data)
        {
            CultureVolunteerData[culture] = data;
        }

        // 1. 添加 GetVolunteerCandidateCache 方法，供外部获取缓存数据 (修正位置)
        // 该方法现在基于 CultureObject 而非 Settlement 或 StringId
        public List<CharacterWeightPair> GetVolunteerCandidateCache(CultureObject cultureObj)
        {
            if (cultureObj == null || !CultureVolunteerData.TryGetValue(cultureObj, out List<CharacterWeightPair> volunteerList))
            {
                return new List<CharacterWeightPair>(); // 如果没有配置或 CultureObject 为 null，返回空列表
            }

            // 直接返回缓存的列表，或者可以返回一个副本以防止外部修改
            // return new List<CharacterWeightPair>(volunteerList); // 返回副本
            return volunteerList; // 返回原始列表 (如果外部保证不修改，这样更高效)
        }

        // 重载方法，接受 Settlement 参数，内部获取 CultureObject
        public List<CharacterWeightPair> GetVolunteerCandidateCache(Settlement settlement)
        {
            if (settlement == null) return new List<CharacterWeightPair>();
            return GetVolunteerCandidateCache(settlement.Culture); // 使用 CultureObject
        }
    }

    // 管理 CultureObject 反序列化时的数据读取
    public static class CultureBasicTroopManager
    {
        public static Dictionary<CultureObject, Dictionary<string, CultureBasicTroopManager.TroopEntry>> Data { get; } = new Dictionary<CultureObject, Dictionary<string, CultureBasicTroopManager.TroopEntry>>();

        public class TroopEntry
        {
            public CharacterObject Troop { get; }
            public int Weight { get; }

            public TroopEntry(CharacterObject troop, int weight)
            {
                Troop = troop;
                Weight = weight;
            }
        }
    }


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

            // === 2. 预计算并缓存 candidates (统一候选池，不再区分 ContextType) ===
            BuildAndCacheUnifiedCandidates(__instance, entries);
        }

        private static void BuildAndCacheUnifiedCandidates(CultureObject culture, Dictionary<string, CultureBasicTroopManager.TroopEntry> entries)
        {
            var characterWeightPairList = new List<CharacterWeightPair>();

            // 遍历所有预定义的类型，将它们添加到统一的候选池中
            var allRelevantTypes = new List<string> { "infantry", "twohanded", "ranged", "cavalry", "horsearcher", "basic", "elite" };

            var seenTroops = new HashSet<CharacterObject>();

            foreach (var type in allRelevantTypes)
            {
                if (entries.TryGetValue(type, out var entry) && seenTroops.Add(entry.Troop)) // 避免重复添加同一个单位
                {
                    // 确定 TroopType
                    TroopType troopType = type switch
                    {
                        "basic" => TroopType.Basic,
                        "elite" => TroopType.EliteBasic,
                        _ => TroopType.Professional // 其他类型都视为职业军
                    };

                    // 添加到列表
                    characterWeightPairList.Add(new CharacterWeightPair(entry.Troop, entry.Weight, troopType));
                }
            }

            // 如果列表为空，添加原版 fallback (basic_troop 或 elite_basic_troop)
            if (characterWeightPairList.Count == 0)
            {
                // 尝试 elite_basic_troop
                if (culture.EliteBasicTroop != null && seenTroops.Add(culture.EliteBasicTroop))
                {
                    characterWeightPairList.Add(new CharacterWeightPair(culture.EliteBasicTroop, 1, TroopType.EliteBasic));
                }
                // 如果 elite_basic_troop 也没有或不使用，则尝试 basic_troop
                else if (culture.BasicTroop != null && seenTroops.Add(culture.BasicTroop))
                {
                    characterWeightPairList.Add(new CharacterWeightPair(culture.BasicTroop, 15, TroopType.Basic));
                }
            }

            // 将构建好的统一候选池列表添加到全局缓存中
            CultureVolunteerGroupsCache.Instance.AddOrReplaceVolunteerData(culture, characterWeightPairList);
        }
    }
}