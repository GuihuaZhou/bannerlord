

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
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using System.Collections.ObjectModel;
using TaleWorlds.ScreenSystem; // 确保添加了这一行
using static System.Net.Mime.MediaTypeNames;


namespace ModifiedArmy.Patches
{
    // ====== 新增：士兵类型枚举 ======
    public enum SoldierType
    {
        Levy,          // 征召兵（basic）
        Noble,         // 贵族兵（elite）
        Professional,   // 职业兵（其他）
        Other
    }

    // ====== 新增：全局分类器（支持增量构建）======
    public static class SoldierTypeClassifier
    {
        private static readonly Dictionary<CharacterObject, SoldierType> _typeMap = new();

        // 提供只读视图（线程安全非必需，但 API 更干净）
        public static IReadOnlyDictionary<CharacterObject, SoldierType> TypeMap =>
            new ReadOnlyDictionary<CharacterObject, SoldierType>(_typeMap);

        public static SoldierType GetSoldierType(CharacterObject troop)
        {
            if (troop == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("troop is null", new Color(1f, 0.8f, 0.3f)));
                return SoldierType.Other;
            }

            if (_typeMap.TryGetValue(troop, out var type))
            {
                return type;
            }
            else
            {
                // 新增日志：当找不到缓存类型时，打印 troop 信息
                InformationManager.DisplayMessage(new InformationMessage(
                    $"[Fief Debug] SoldierTypeClassifier 缓存未命中: troop = '{troop.Name}' (ID: {troop.StringId ?? "null"}, IsHero: {troop.IsHero}, Tier: {troop.Tier})",
                    new Color(1f, 0.8f, 0.3f) // 橙黄色，表示警告
                ));
                return SoldierType.Other;
            }
        }

        // 处理单个文化的兵种列表（来自 BuildAndCacheUnifiedCandidates）
        public static void ProcessCultureTroops(CultureObject culture, List<CharacterWeightPair> pairs)
        {
            var processed = new List<(string name, SoldierType type)>();

            foreach (var pair in pairs)
            {
                if (pair.Character == null) continue;

                // 映射 TroopType → SoldierType
                SoldierType soldierType = pair.Type switch
                {
                    TroopType.Basic => SoldierType.Levy,
                    TroopType.EliteBasic => SoldierType.Noble,
                    _ => SoldierType.Professional
                };

                // 游戏保证：此 troop 尚未被其他文化处理过
                PropagateTypeThroughUpgradeTree(pair.Character, soldierType);

                // 记录用于日志
                processed.Add(($"{pair.Character.Name}", soldierType));
            }

            // 打印日志到游戏内消息栏
            var logLines = new List<string> { $"[Fief] 已处理文化 '{culture.Name}' 的兵种分类：" };
            foreach (var (name, type) in processed)
            {
                string typeName = type switch
                {
                    SoldierType.Levy => "征召兵",
                    SoldierType.Noble => "贵族兵",
                    SoldierType.Professional => "职业兵"
                };
                logLines.Add($"  • {name} → {typeName}");
            }
            InformationManager.DisplayMessage(new InformationMessage(string.Join("\n", logLines)));
        }

        public static void InitializeAll()
        {
            InformationManager.DisplayMessage(new InformationMessage(
                "[Fief] 开始初始化 SoldierTypeClassifier 分类器...",
                new Color(0.6f, 0.9f, 1f)));

            foreach (var kvp in CultureBasicTroopManager.Data)
            {
                var culture = kvp.Key;
                var entries = kvp.Value;

                var characterWeightPairList = new List<CharacterWeightPair>();
                var seenTroops = new HashSet<CharacterObject>();

                // 重建 pairs（复用 BuildAndCacheUnifiedCandidates 中的逻辑）
                var allRelevantTypes = new List<string> { "infantry", "twohanded", "ranged", "cavalry", "horsearcher", "basic", "elite" };
                foreach (var type in allRelevantTypes)
                {
                    if (entries.TryGetValue(type, out var entry) && seenTroops.Add(entry.Troop))
                    {
                        TroopType troopType = type switch
                        {
                            "basic" => TroopType.Basic,
                            "elite" => TroopType.EliteBasic,
                            _ => TroopType.Professional
                        };
                        characterWeightPairList.Add(new CharacterWeightPair(entry.Troop, entry.Weight, troopType));
                    }
                }

                // 处理该文化的所有兵种（含升级树）
                ProcessCultureTroops(culture, characterWeightPairList);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                $"[Fief] SoldierTypeClassifier 初始化完成，共处理 {TypeMap.Count} 个兵种。",
                new Color(0.6f, 0.9f, 1f)));
        }

        // DFS 遍历升级树并标记类型
        private static void PropagateTypeThroughUpgradeTree(CharacterObject current, SoldierType type)
        {
            if (current == null) return;

            // 即使已存在也不报错（理论上不会发生，因游戏保证唯一性）
            _typeMap[current] = type;

            if (current.UpgradeTargets == null || current.UpgradeTargets?.Length == 0)
                return;

            foreach (var upgrade in current.UpgradeTargets)
            {
                if (upgrade == null) continue;
                PropagateTypeThroughUpgradeTree(upgrade, type);
            }
        }
    }

    // 这个类代表一个包含 基础troop, Weight, Type 的单元
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

    // 记录一个culture的全部基础troop和weight
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