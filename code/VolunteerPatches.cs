using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static System.Net.Mime.MediaTypeNames;

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

    // =============== 补丁：CultureObject.Deserialize ===============
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

                int weight = 100;
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

    // =============== 补丁：DefaultVolunteerModel.GetBasicVolunteer ===============
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





    //public static class CultureBasicTroopManager
    //{
    //    public struct TroopEntry
    //    {
    //        public CharacterObject Troop;
    //        public int Weight;

    //        public TroopEntry(CharacterObject troop, int weight)
    //        {
    //            Troop = troop ?? throw new System.ArgumentNullException(nameof(troop));
    //            Weight = System.Math.Max(1, weight);
    //        }
    //    }

    //    /// <summary>
    //    /// CultureObject → (type → TroopEntry)
    //    /// type: "basic", "elite", "infantry", "ranged", "cavalry", "horsearcher", "twohanded"
    //    /// </summary>
    //    public static readonly Dictionary<CultureObject, Dictionary<string, TroopEntry>> Data =
    //        new Dictionary<CultureObject, Dictionary<string, TroopEntry>>();
    //}

    //[HarmonyPatch(typeof(CultureObject), "Deserialize")]
    //public static class Patch_CultureObject_Deserialize
    //{
    //    public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
    //    {
    //        InformationManager.DisplayMessage(new InformationMessage(
    //            $"[MOD] Deserialize patch START for culture: '{__instance.StringId}'"));

    //        var entries = new Dictionary<string, CultureBasicTroopManager.TroopEntry>();

    //        // 映射：类型 → (XML属性名, 权重属性名)
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

    //        int loadedCount = 0;

    //        foreach (var kvp in typeMap)
    //        {
    //            string type = kvp.Key;
    //            string troopAttr = kvp.Value.troopAttr;
    //            string weightAttr = kvp.Value.weightAttr;

    //            // === 1. 读取 troop 引用（使用 objectManager，和原版逻辑一致）===
    //            CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>(troopAttr, node);
    //            string troopIdValue = node.Attributes?[troopAttr]?.Value; // 用于日志

    //            InformationManager.DisplayMessage(new InformationMessage(
    //                $"[MOD] Checking type='{type}': attr='{troopAttr}' = '{troopIdValue ?? "NULL"}'"));

    //            if (troop == null)
    //            {
    //                if (!string.IsNullOrEmpty(troopIdValue))
    //                {
    //                    InformationManager.DisplayMessage(new InformationMessage(
    //                        $"[MOD] Troop ID '{troopIdValue}' found but NOT RESOLVED to CharacterObject!"));
    //                }
    //                continue;
    //            }

    //            // === 2. 读取权重 ===
    //            int weight = 100; // 默认权重
    //            string weightStr = node.Attributes?[weightAttr]?.Value;
    //            if (!string.IsNullOrEmpty(weightStr))
    //            {
    //                if (int.TryParse(weightStr, out int parsedWeight))
    //                {
    //                    weight = System.Math.Max(1, parsedWeight);
    //                }
    //                else
    //                {
    //                    InformationManager.DisplayMessage(new InformationMessage(
    //                        $"[MOD] Invalid weight '{weightStr}' for type='{type}', using default 1"));
    //                }
    //            }

    //            // === 3. 存储有效条目 ===
    //            entries[type] = new CultureBasicTroopManager.TroopEntry(troop, weight);
    //            loadedCount++;

    //            InformationManager.DisplayMessage(new InformationMessage(
    //                $"[MOD] SUCCESS: type='{type}', troop='{troop.Name}', weight={weight}"));
    //        }

    //        // === 4. 绑定数据 ===
    //        CultureBasicTroopManager.Data[__instance] = entries;

    //        InformationManager.DisplayMessage(new InformationMessage(
    //            $"[MOD] Deserialize patch END for '{__instance.StringId}': {loadedCount} custom troops loaded."));
    //    }
    //}

    //[HarmonyPatch(typeof(DefaultVolunteerModel), "GetBasicVolunteer")]
    //public class Patch_GetBasicVolunteer
    //{
    //    private static void TryAddTroopByType(
    //        CultureObject culture,
    //        string type,
    //        List<CharacterObject> troops,
    //        List<int> weights)
    //    {
    //        if (CultureBasicTroopManager.Data.TryGetValue(culture, out var typeDict) &&
    //            typeDict.TryGetValue(type, out var entry))
    //        {
    //            troops.Add(entry.Troop);
    //            weights.Add(entry.Weight);
    //            InformationManager.DisplayMessage(new InformationMessage(
    //                $"[MOD] Added {type}: {entry.Troop.Name} (weight={entry.Weight})"));
    //        }
    //    }

    //    private static CharacterObject WeightedRandomSelect(List<CharacterObject> troops, List<int> weights)
    //    {
    //        if (troops.Count == 0) return null;
    //        int total = 0;
    //        foreach (int w in weights) total += w;
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

    //        InformationManager.DisplayMessage(new InformationMessage(
    //            $"[MOD] Recruiting volunteers for culture: {culture.StringId} at {settlement.Name}"));

    //        List<CharacterObject> candidates = new List<CharacterObject>();
    //        List<int> weights = new List<int>();

    //        if (settlement.IsTown || (settlement.IsVillage && settlement.Village.Bound.IsTown))
    //        {
    //            InformationManager.DisplayMessage(new InformationMessage("[MOD] Context: Town or Village→Town"));
    //            TryAddTroopByType(culture, "infantry", candidates, weights);
    //            TryAddTroopByType(culture, "twohanded", candidates, weights);
    //            TryAddTroopByType(culture, "ranged", candidates, weights);
    //            TryAddTroopByType(culture, "cavalry", candidates, weights);
    //            TryAddTroopByType(culture, "horsearcher", candidates, weights);
    //            TryAddTroopByType(culture, "basic", candidates, weights); // fallback

    //            if (candidates.Count == 0 && culture.BasicTroop != null)
    //            {
    //                InformationManager.DisplayMessage(new InformationMessage("[MOD] NO CANDIDATES — using BasicTroop fallback"));
    //                candidates.Add(culture.BasicTroop);
    //                weights.Add(15);
    //            }
    //        }
    //        else if (settlement.IsVillage && settlement.Village.Bound.IsCastle)
    //        {
    //            InformationManager.DisplayMessage(new InformationMessage("[MOD] Context: Village→Castle"));
    //            TryAddTroopByType(culture, "infantry", candidates, weights);
    //            TryAddTroopByType(culture, "twohanded", candidates, weights);
    //            TryAddTroopByType(culture, "ranged", candidates, weights);
    //            TryAddTroopByType(culture, "cavalry", candidates, weights);
    //            TryAddTroopByType(culture, "horsearcher", candidates, weights);
    //            TryAddTroopByType(culture, "elite", candidates, weights);

    //            // 如果没有自定义 elite，但原版有，则补充
    //            if (!CultureBasicTroopManager.Data.TryGetValue(culture, out var dict) || !dict.ContainsKey("elite"))
    //            {
    //                if (culture.EliteBasicTroop != null)
    //                {
    //                    candidates.Add(culture.EliteBasicTroop);
    //                    weights.Add(1);
    //                    InformationManager.DisplayMessage(new InformationMessage("[MOD] Added original EliteBasicTroop"));
    //                }
    //            }

    //            TryAddTroopByType(culture, "basic", candidates, weights); // fallback

    //            if (candidates.Count == 0 && culture.BasicTroop != null)
    //            {
    //                InformationManager.DisplayMessage(new InformationMessage("[MOD] NO CANDIDATES — using BasicTroop fallback"));
    //                candidates.Add(culture.BasicTroop);
    //                weights.Add(15);
    //            }
    //        }
    //        else
    //        {
    //            InformationManager.DisplayMessage(new InformationMessage("[MOD] Context: Wild/Camp — only basic"));
    //            TryAddTroopByType(culture, "basic", candidates, weights);
    //            __result = candidates.Count > 0 ? candidates[0] : culture.BasicTroop;
    //            InformationManager.DisplayMessage(new InformationMessage($"[MOD] Selected (wild): {__result.Name}"));
    //            return false;
    //        }

    //        __result = WeightedRandomSelect(candidates, weights) ?? culture.BasicTroop;
    //        InformationManager.DisplayMessage(new InformationMessage($"[MOD] FINAL SELECTED: {__result.Name}"));
    //        return false;
    //    }
    //}

    //// =============== 扩展 CultureObject 读取 troop + 权重 ===============
    //[HarmonyPatch(typeof(CultureObject), "Deserialize")]
    //public class Patch_CultureObject_Deserialize
    //{
    //    // Troop 字段
    //    public static Dictionary<CultureObject, CharacterObject> InfantryTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> TwoHandedTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> RangedTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> CavalryTroop = new Dictionary<CultureObject, CharacterObject>();
    //    public static Dictionary<CultureObject, CharacterObject> HorseArcherTroop = new Dictionary<CultureObject, CharacterObject>();

    //    // 权重字段（默认 0）
    //    public static Dictionary<CultureObject, int> InfantryWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> TwoHandedWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> RangedWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> CavalryWeight = new Dictionary<CultureObject, int>();
    //    public static Dictionary<CultureObject, int> HorseArcherWeight = new Dictionary<CultureObject, int>();

    //    public static void Postfix(CultureObject __instance, MBObjectManager objectManager, XmlNode node)
    //    {
    //        var culture = __instance;

    //        // 清理旧值
    //        InfantryTroop.Remove(culture); TwoHandedTroop.Remove(culture);
    //        RangedTroop.Remove(culture); CavalryTroop.Remove(culture); HorseArcherTroop.Remove(culture);
    //        InfantryWeight.Remove(culture); TwoHandedWeight.Remove(culture);
    //        RangedWeight.Remove(culture); CavalryWeight.Remove(culture); HorseArcherWeight.Remove(culture);

    //        // 读取 troop
    //        var inf = objectManager.ReadObjectReferenceFromXml<CharacterObject>("basic_infantry_troop", node);
    //        var two = objectManager.ReadObjectReferenceFromXml<CharacterObject>("basic_twohanded_troop", node);
    //        var ran = objectManager.ReadObjectReferenceFromXml<CharacterObject>("basic_ranged_troop", node);
    //        var cav = objectManager.ReadObjectReferenceFromXml<CharacterObject>("basic_cavalry_troop", node);
    //        var har = objectManager.ReadObjectReferenceFromXml<CharacterObject>("basic_horsearcher_troop", node);

    //        if (inf != null) InfantryTroop[culture] = inf;
    //        if (two != null) TwoHandedTroop[culture] = two;
    //        if (ran != null) RangedTroop[culture] = ran;
    //        if (cav != null) CavalryTroop[culture] = cav;
    //        if (har != null) HorseArcherTroop[culture] = har;

    //        // 读取权重（默认 0）
    //        InfantryWeight[culture] = ReadIntAttr(node, "basic_infantry_troop_weight", 0);
    //        TwoHandedWeight[culture] = ReadIntAttr(node, "basic_twohanded_troop_weight", 0);
    //        RangedWeight[culture] = ReadIntAttr(node, "basic_ranged_troop_weight", 0);
    //        CavalryWeight[culture] = ReadIntAttr(node, "basic_cavalry_troop_weight", 0);
    //        HorseArcherWeight[culture] = ReadIntAttr(node, "basic_horsearcher_troop_weight", 0);
    //    }

    //    private static int ReadIntAttr(XmlNode node, string attrName, int defaultValue)
    //    {
    //        var attr = node.Attributes?[attrName];
    //        if (attr != null && int.TryParse(attr.Value, out int result))
    //            return Math.Max(0, result);
    //        return defaultValue;
    //    }
    //}

    //// =============== GetBasicVolunteer 按权重选择基础troop ===============
    //[HarmonyPatch(typeof(DefaultVolunteerModel), "GetBasicVolunteer")]
    //public class Patch_GetBasicVolunteer
    //{
    //    private static void TryAddTroop(
    //        Dictionary<CultureObject, CharacterObject> troopDict,
    //        Dictionary<CultureObject, int> weightDict,
    //        CultureObject culture,
    //        List<CharacterObject> troops,
    //        List<int> weights)
    //    {
    //        if (!troopDict.TryGetValue(culture, out CharacterObject troop) || troop == null)
    //            return;
    //        int weight = 0;
    //        weightDict.TryGetValue(culture, out weight);
    //        if (weight > 0)
    //        {
    //            troops.Add(troop);
    //            weights.Add(weight);
    //        }
    //    }

    //    private static CharacterObject WeightedRandomSelect(List<CharacterObject> troops, List<int> weights)
    //    {
    //        if (troops.Count == 0) return null;
    //        int total = 0;
    //        foreach (int w in weights) total += w;
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

    //        List<CharacterObject> candidates = new List<CharacterObject>();
    //        List<int> weights = new List<int>();

    //        if (settlement.IsTown || (settlement.IsVillage && settlement.Village.Bound.IsTown))
    //        {
    //            // Town / Village→Town: infantry, twohanded, ranged + basic_troop (weight=100)
    //            TryAddTroop(Patch_CultureObject_Deserialize.InfantryTroop,
    //                        Patch_CultureObject_Deserialize.InfantryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.TwoHandedTroop,
    //                        Patch_CultureObject_Deserialize.TwoHandedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.RangedTroop,
    //                        Patch_CultureObject_Deserialize.RangedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.CavalryTroop,
    //                        Patch_CultureObject_Deserialize.CavalryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.HorseArcherTroop,
    //                        Patch_CultureObject_Deserialize.HorseArcherWeight, culture, candidates, weights);

    //            if (culture.BasicTroop != null)
    //            {
    //                candidates.Add(culture.BasicTroop);
    //                weights.Add(20); // 
    //            }
    //        }
    //        else if (settlement.IsVillage && settlement.Village.Bound.IsCastle)
    //        {
    //            // Village→Castle: infantry, twohanded, ranged, cavalry, horseArcher + elite_basic_troop (weight=25)
    //            TryAddTroop(Patch_CultureObject_Deserialize.InfantryTroop,
    //                        Patch_CultureObject_Deserialize.InfantryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.TwoHandedTroop,
    //                        Patch_CultureObject_Deserialize.TwoHandedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.RangedTroop,
    //                        Patch_CultureObject_Deserialize.RangedWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.CavalryTroop,
    //                        Patch_CultureObject_Deserialize.CavalryWeight, culture, candidates, weights);
    //            TryAddTroop(Patch_CultureObject_Deserialize.HorseArcherTroop,
    //                        Patch_CultureObject_Deserialize.HorseArcherWeight, culture, candidates, weights);

    //            var elite = culture.EliteBasicTroop;
    //            if (elite != null)
    //            {
    //                candidates.Add(elite);
    //                weights.Add(1); // 
    //            }
    //            else if (culture.BasicTroop != null)
    //            {
    //                // fallback to basic_troop if elite is missing
    //                candidates.Add(culture.BasicTroop);
    //                weights.Add(20);
    //            }
    //        }
    //        else
    //        {
    //            __result = culture.BasicTroop;
    //            return false;
    //        }

    //        if (candidates.Count == 0)
    //        {
    //            __result = culture.BasicTroop;
    //        }
    //        else
    //        {
    //            __result = WeightedRandomSelect(candidates, weights);
    //            if (__result == null) __result = culture.BasicTroop;
    //        }

    //        InformationManager.DisplayMessage(new InformationMessage($"[DEBUG] Selected: {__result.Name}"));

    //        return false;
    //    }
    //}

    // =============== 限制志愿兵招募 ===============
    [HarmonyPatch(typeof(DefaultVolunteerModel), "MaximumIndexHeroCanRecruitFromHero")]
    public class Patch_MaximumIndexHeroCanRecruitFromHero
    {
        public static bool Prefix(
            DefaultVolunteerModel __instance,
            Hero buyerHero,
            Hero sellerHero,
            int useValueAsRelation,
            ref int __result)
        {
            if (!CampaignState.IsReady || buyerHero == null || sellerHero == null)
            {
                __result = -1;
                return false;
            }

            MobileParty buyerParty = buyerHero.PartyBelongedTo;
            Settlement settlement = sellerHero.CurrentSettlement;
            if (settlement == null)
            {
                __result = -1;
                return false;
            }

            Clan buyerClan = buyerParty?.ActualClan;
            Clan ownerClan = settlement.OwnerClan;

            if (buyerClan == null)
            {
                __result = -1;
                return false;
            }

            // ====== 地域限制 ======
            bool isPlayer = (buyerClan == Clan.PlayerClan);
            bool canRecruitFromSettlement = false;

            if (isPlayer)
            {
                bool playerHasSettlement = Clan.PlayerClan.Settlements.Count > 0;
                canRecruitFromSettlement = !playerHasSettlement || (ownerClan == Clan.PlayerClan);
            }
            else
            {
                canRecruitFromSettlement = (ownerClan == buyerClan);
            }

            if (!canRecruitFromSettlement)
            {
                __result = -1;
                return false;
            }

            // ====== 关系限制 ======
            // int relation = (useValueAsRelation < -100) ? buyerHero.GetRelation(sellerHero) : useValueAsRelation;
            int relation = buyerHero.GetRelation(sellerHero);
            if (relation < -10)
            {
                __result = -1;
                return false;
            }

            // ====== 基础招募格子数 ======
            int baseIndex = 2;

            // ====== 原版加成计算 ======
            int relationBonus = (relation >= 100) ? 7 :
                                (relation >= 80) ? 6 :
                                (relation >= 60) ? 5 :
                                (relation >= 40) ? 4 :
                                (relation >= 20) ? 3 :
                                (relation >= 10) ? 2 :
                                (relation >= 5) ? 1 :
                                (relation >= 0) ? 0 : -1;

            int factionBonus = (settlement.MapFaction == buyerHero.MapFaction) ? 1 : 0;
            int npcPenalty = (buyerHero != Hero.MainHero) ? 1 : 0;
            int warPenalty = (buyerHero.MapFaction.IsAtWarWith(settlement.MapFaction)) ? -(1 + npcPenalty) : 0;

            if (buyerHero.IsMinorFactionHero && settlement.IsVillage)
            {
                warPenalty = 0;
            }

            int perkBonus = 0;
            if (sellerHero.IsMerchant && buyerHero.GetPerkValue(DefaultPerks.Trade.ArtisanCommunity))
                perkBonus += (int)DefaultPerks.Trade.ArtisanCommunity.SecondaryBonus;
            if (sellerHero.Culture == buyerHero.Culture && buyerHero.GetPerkValue(DefaultPerks.Leadership.CombatTips))
                perkBonus += (int)DefaultPerks.Leadership.CombatTips.SecondaryBonus;
            if (sellerHero.IsRuralNotable && buyerHero.GetPerkValue(DefaultPerks.Charm.Firebrand))
                perkBonus += (int)DefaultPerks.Charm.Firebrand.SecondaryBonus;
            if (sellerHero.IsUrbanNotable && buyerHero.GetPerkValue(DefaultPerks.Charm.FlexibleEthics))
                perkBonus += (int)DefaultPerks.Charm.FlexibleEthics.SecondaryBonus;
            if (sellerHero.IsArtisan && buyerParty?.EffectiveEngineer?.GetPerkValue(DefaultPerks.Engineering.EngineeringGuilds) == true)
                perkBonus += (int)DefaultPerks.Engineering.EngineeringGuilds.PrimaryBonus;

            int maxIndex = MathF.Min(6, baseIndex + relationBonus + factionBonus + npcPenalty + warPenalty + perkBonus);
            if (maxIndex < 0) maxIndex = -1;

            // ====== 高阶兵限制 ======
            float influence = buyerClan.Influence;
            CharacterObject[] volunteerTypes = sellerHero.VolunteerTypes;

            int finalIndex = -1;
            //string debugLog = $"[RecruitDebug] Buyer: {buyerHero.Name}, Seller: {sellerHero.Name}\n" +
            //                  $"Relation: {relation}, BaseIndex: {baseIndex}, MaxIndex: {maxIndex}, Influence: {influence:F1}\n" +
            //                  $"Volunteer Tiers: ";

            for (int i = 0; i <= maxIndex && i < 6; i++)
            {
                CharacterObject troop = volunteerTypes[i];
                if (troop == null)
                {
                    //debugLog += $"[{i}: null], ";
                    continue;
                }

                int tier = troop.Tier;
                bool allowed = false;
                if (tier <= 3)
                {
                    allowed = true;
                }
                else if (tier == 4 && influence > 200f)
                {
                    allowed = true;
                }
                else if (tier >= 5 && influence > 500f)
                {
                    allowed = true;
                }
                else
                {
                    break;
                }

                if (allowed)
                {
                    finalIndex = i;
                    //debugLog += $"[{i}: T{tier} ✓], ";
                }
                else
                {
                    //debugLog += $"[{i}: T{tier} ✗], ";
                }
            }

            //debugLog += $"\nFinalIndex: {finalIndex}";
            //InformationManager.DisplayMessage(new InformationMessage(debugLog, new Color(1f, 1f, 0f)));

            __result = finalIndex;
            return false;
        }
    }
}