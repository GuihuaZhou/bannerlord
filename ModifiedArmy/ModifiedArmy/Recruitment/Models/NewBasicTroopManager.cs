using ModifiedArmy.common;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models
{
    public class BasicTroopEntry
    {
        public CharacterObject Troop { get; }
        public int Weight { get; set; }
        public SoldierType Type { get; }

        /// <summary>
        /// 招募此兵种所需的最低兵营等级。
        /// 0 表示无要求。
        /// </summary>
        public int RequiredBarracksLevel { get; }

        public BasicTroopEntry(CharacterObject troop, int weight, SoldierType type, int requiredBarracksLevel = 0)
        {
            Troop = troop;
            Weight = weight;
            Type = type;
            RequiredBarracksLevel = requiredBarracksLevel;
        }

        /// <summary>
        /// 设置此兵种条目的权重。
        /// </summary>
        /// <param name="newWeight">新的权重值。必须大于0。</param>
        public void SetWeight(int newWeight)
        {
            if (newWeight <= 0)
            {
                Weight = 1;
            }
            else
            {
                Weight = newWeight;
            }
        }
    }

    public class BasicTroopGroup : MBObjectBase
    {
        public CultureObject Culture { get; private set; }
        public Dictionary<SoldierType, List<BasicTroopEntry>> TroopsByType { get; } = new()
        {
            [SoldierType.Retinue] = new List<BasicTroopEntry>(),
            [SoldierType.Sergeant] = new List<BasicTroopEntry>(),
            [SoldierType.Slave] = new List<BasicTroopEntry>(),
            [SoldierType.Marine] = new List<BasicTroopEntry>(),
            [SoldierType.Militia] = new List<BasicTroopEntry>()
        };

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            string groupId = XmlHelper.ReadString(node, "id");
            string cultureId = XmlHelper.ReadString(node, "culture");

            //ModLogger.Debug($"[BasicTroopGroup] Starting deserialization: id='{groupId}' for {cultureId}");

            // 读取 culture="xxx"
            Culture = MBObjectManager.Instance.ReadObjectReferenceFromXml<CultureObject>("culture", node);
            if (Culture == null)
            {
                ModLogger.Warn($"[BasicTroopGroup] Failed to resolve culture='{cultureId}', skipping group (id='{groupId}')");
                return;
            }

            //ModLogger.Info($"[BasicTroopGroup] Successfully loaded culture: {Culture.StringId} (id='{groupId}')");

            foreach (XmlNode child in node.ChildNodes)
            {
                switch (child.Name)
                {
                    case "RetinueTroops":
                        ParseTroopNodes(objectManager, child, SoldierType.Retinue);
                        break;
                    case "SergeantTroops":
                        ParseTroopNodes(objectManager, child, SoldierType.Sergeant);
                        break;
                    case "MilitiaTroops":
                        ParseTroopNodes(objectManager, child, SoldierType.Militia);
                        break;
                    case "SlaveTroops":
                        ParseTroopNodes(objectManager, child, SoldierType.Slave);
                        break;
                    case "MarineTroops":
                        ParseTroopNodes(objectManager, child, SoldierType.Marine);
                        break;
                }
            }
            int total = TroopsByType[SoldierType.Retinue].Count +
                        TroopsByType[SoldierType.Sergeant].Count +
                        TroopsByType[SoldierType.Slave].Count +
                        TroopsByType[SoldierType.Marine].Count +
                        TroopsByType[SoldierType.Militia].Count;

            ModLogger.Debug($"[BasicTroopGroup] Finished loading troop config for '{Culture.Name}', total entries: {total}");
            BasicTroopGroupManager.Instance.RegisterGroup(this);
        }

        private void ParseTroopNodes(MBObjectManager objectManager, XmlNode parent, SoldierType type)
        {
            var list = TroopsByType[type];
            int count = 0;

            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node.Name == "troop")
                {
                    CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("id", node);

                    int barracksLevel = 0;
                    if (node.Attributes?["barracksLevel"] != null)
                    {
                        barracksLevel = XmlHelper.ReadInt(node, "barracksLevel");
                    }

                    if (troop != null)
                    {
                        list.Add(new BasicTroopEntry(troop, 1, type, barracksLevel));
                        count++;
                        ModLogger.Debug($"  ├─ [{type}] Added troop: {troop.Name}");
                    }
                    else
                    {
                        string rawId = XmlHelper.ReadString(node, "id");
                        ModLogger.Warn($"  ├─ [{type}] Failed to load troop: id='{rawId}' (invalid or undefined ID)");
                    }
                }
            }
            ModLogger.Debug($"  └─ {type}: Successfully loaded {count} troops");
        }
    }

    public class BasicTroopGroupManager
    {
        public static readonly BasicTroopGroupManager Instance = new();
        private static readonly Dictionary<CultureObject, BasicTroopGroup> _groups = new();

        public void RegisterGroup(BasicTroopGroup group)
        {
            if (group?.Culture != null)
            {
                _groups[group.Culture] = group;
                ModLogger.Debug($"[BasicTroopGroupManager] Registered culture group: {group.Culture.Name}");
            }
        }

        public static BasicTroopGroup GetGroupForCulture(CultureObject culture)
        {
            if (culture == null)
            {
                ModLogger.Warn("[BasicTroopGroupManager] GetGroupForCulture called with null culture");
                return null;
            }

            if (_groups.TryGetValue(culture, out var group))
            {
                //ModLogger.Debug($"[BasicTroopGroupManager] Cache hit: {culture.Name}");
                return group;
            }

            ModLogger.Debug($"[BasicTroopGroupManager] No troop configuration found for culture: {culture.Name}");
            return null;
        }


        public static void Clear()
        {
            int count = _groups.Count;
            _groups.Clear();
            ModLogger.Debug($"[BasicTroopGroupManager] Cleared cache ({count} entries)");
        }

        public static BasicTroopEntry FindBasicTroop(CultureObject culture, SoldierType type, CharacterObject troop)
        {
            if (culture == null || troop == null)
            {
                return null;
            }

            if (!_groups.TryGetValue(culture, out BasicTroopGroup group))
            {
                ModLogger.Debug($"[BasicTroopGroupManager] No group found for culture: {culture.StringId}");
                return null;
            }

            if (!group.TroopsByType.TryGetValue(type, out List<BasicTroopEntry> entries))
            {
                ModLogger.Debug($"[BasicTroopGroupManager] No list found for SoldierType: {type} in culture: {culture.StringId}");
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry?.Troop == troop)
                {
                    return entry;
                }
            }

            ModLogger.Debug($"[BasicTroopGroupManager] No entry found for troop '{troop.StringId}' of type {type} in culture: {culture.StringId}");
            return null;
        }
    }

    /// <summary>
    /// Global classifier that maps CharacterObject to SoldierType.
    /// Supports propagation through the entire upgrade tree based on culture-defined base troops.
    /// </summary>
    public static class SoldierTypeClassifier
    {
        private static readonly Dictionary<CharacterObject, SoldierType> _typeMap = new();

        /// <summary>
        /// [DEBUG] Prints all entries in the _typeMap for debugging purposes.
        /// </summary>
        public static void DebugPrintAllMappings()
        {
            ModLogger.Debug($"[SoldierTypeClassifier DEBUG] Printing all {_typeMap.Count} mappings:");
            foreach (var kvp in _typeMap)
            {
                string troopName = kvp.Key.Name.ToString() ?? "NULL_TROOP";
                string typeName = kvp.Value.ToString();
                ModLogger.Debug($" ├─ {troopName} -> {typeName}");
            }
            ModLogger.Debug($"[SoldierTypeClassifier DEBUG] End of mappings.");
        }

        /// <summary>
        /// Read-only view of the troop type mapping for external access.
        /// </summary>
        public static IReadOnlyDictionary<CharacterObject, SoldierType> TypeMap =>
            new ReadOnlyDictionary<CharacterObject, SoldierType>(_typeMap);

        /// <summary>
        /// Retrieves the fief troop type for a given character.
        /// Returns Other if not found or if the input is null.
        /// </summary>
        public static SoldierType GetSoldierType(CharacterObject troop)
        {
            //DebugPrintAllMappings();
            if (troop == null) return SoldierType.Other;
            if (_typeMap.TryGetValue(troop, out var type)) return type;
            return SoldierType.Other;
        }

        /// <summary>
        /// Recursively assigns the given troop type to the entire upgrade tree starting from 'current'.
        /// </summary>
        private static void PropagateTypeThroughUpgradeTree(CharacterObject current, SoldierType type)
        {
            if (current == null) return;
            _typeMap[current] = type;

            if (current.UpgradeTargets?.Length > 0)
            {
                foreach (var upgrade in current.UpgradeTargets)
                {
                    if (upgrade != null)
                    {
                        PropagateTypeThroughUpgradeTree(upgrade, type);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes the classifier by processing all cultures via BasicTroopGroupManager.
        /// This should be called once after all modules are loaded.
        /// </summary>
        public static void InitializeAll()
        {
            ModLogger.Debug("[Fief] Initializing SoldierTypeClassifier...");

            // Clear previous data (in case of reload)
            _typeMap.Clear();

            foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
            {
                var group = BasicTroopGroupManager.GetGroupForCulture(culture);

                if (group != null)
                {
                    // Process pre-classified entries from XML config
                    ProcessTroopEntries(group.TroopsByType[SoldierType.Retinue]);
                    ProcessTroopEntries(group.TroopsByType[SoldierType.Sergeant]);
                    ProcessTroopEntries(group.TroopsByType[SoldierType.Militia]);
                    ProcessTroopEntries(group.TroopsByType[SoldierType.Slave]);
                    ProcessTroopEntries(group.TroopsByType[SoldierType.Marine]);
                }
            }

            ModLogger.Debug($"[Fief] SoldierTypeClassifier initialized. Cached {_typeMap.Count} troops.");
        }

        private static void ProcessTroopEntries(List<BasicTroopEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry?.Troop != null)
                {
                    PropagateTypeThroughUpgradeTree(entry.Troop, entry.Type);
                }
            }
        }

        /// <summary>
        /// 判断指定兵种是否为三类封邑士兵之一（民兵、军士、扈从）。
        /// </summary>
        /// <param name="troop">要检查的兵种</param>
        /// <returns>如果是封邑士兵，返回 true；否则 false</returns>
        public static bool IsFiefTroop(CharacterObject troop)
        {
            if (troop == null)
                return false;

            var type = GetSoldierType(troop);
            return type == SoldierType.Militia ||
                   type == SoldierType.Sergeant ||
                   type == SoldierType.Marine ||
                   type == SoldierType.Slave ||
                   type == SoldierType.Retinue;
        }
    }

}
