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
        public int Weight { get; }
        public FiefTroopType Type { get; }

        public BasicTroopEntry(CharacterObject troop, int weight, FiefTroopType type)
        {
            Troop = troop;
            Weight = weight;
            Type = type;
        }
    }

    public class BasicTroopGroup : MBObjectBase
    {
        public CultureObject Culture { get; private set; }
        public Dictionary<FiefTroopType, List<BasicTroopEntry>> TroopsByType { get; } = new()
        {
            [FiefTroopType.Fief_Retinue] = new List<BasicTroopEntry>(),
            [FiefTroopType.Fief_Sergeant] = new List<BasicTroopEntry>(),
            [FiefTroopType.Fief_Militia] = new List<BasicTroopEntry>()
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
                        ParseTroopNodes(objectManager, child, FiefTroopType.Fief_Retinue);
                        break;
                    case "SergeantTroops":
                        ParseTroopNodes(objectManager, child, FiefTroopType.Fief_Sergeant);
                        break;
                    case "MilitiaTroops":
                        ParseTroopNodes(objectManager, child, FiefTroopType.Fief_Militia);
                        break;
                }
            }
            int total = TroopsByType[FiefTroopType.Fief_Retinue].Count +
                        TroopsByType[FiefTroopType.Fief_Sergeant].Count +
                        TroopsByType[FiefTroopType.Fief_Militia].Count;
            ModLogger.Info($"[BasicTroopGroup] Finished loading troop config for '{Culture.Name}', total entries: {total}");
            BasicTroopGroupManager.Instance.RegisterGroup(this);
        }

        private void ParseTroopNodes(MBObjectManager objectManager, XmlNode parent, FiefTroopType type)
        {
            var list = TroopsByType[type];
            int count = 0;

            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node.Name == "troop")
                {
                    CharacterObject troop = objectManager.ReadObjectReferenceFromXml<CharacterObject>("id", node);
                    int weight = XmlHelper.ReadInt(node, "weight");

                    if (troop != null)
                    {
                        list.Add(new BasicTroopEntry(troop, weight, type));
                        count++;
                        ModLogger.Debug($"  ├─ [{type}] Added troop: {troop.Name} (weight={weight})");
                    }
                    else
                    {
                        string rawId = XmlHelper.ReadString(node, "id");
                        ModLogger.Warn($"  ├─ [{type}] Failed to load troop: id='{rawId}' (invalid or undefined ID)");
                    }
                }
            }
            ModLogger.Info($"  └─ {type}: Successfully loaded {count} troops");
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

            ModLogger.Info($"[BasicTroopGroupManager] No troop configuration found for culture: {culture.Name}");
            return null;
        }


        public static void Clear()
        {
            int count = _groups.Count;
            _groups.Clear();
            ModLogger.Debug($"[BasicTroopGroupManager] Cleared cache ({count} entries)");
        }
    }

    /// <summary>
    /// Global classifier that maps CharacterObject to FiefTroopType.
    /// Supports propagation through the entire upgrade tree based on culture-defined base troops.
    /// </summary>
    public static class SoldierTypeClassifier
    {
        private static readonly Dictionary<CharacterObject, FiefTroopType> _typeMap = new();

        /// <summary>
        /// Read-only view of the troop type mapping for external access.
        /// </summary>
        public static IReadOnlyDictionary<CharacterObject, FiefTroopType> TypeMap =>
            new ReadOnlyDictionary<CharacterObject, FiefTroopType>(_typeMap);

        /// <summary>
        /// Retrieves the fief troop type for a given character.
        /// Returns Fief_Other if not found or if the input is null.
        /// </summary>
        public static FiefTroopType GetSoldierType(CharacterObject troop)
        {
            if (troop == null) return FiefTroopType.Fief_Other;
            if (_typeMap.TryGetValue(troop, out var type)) return type;
            return FiefTroopType.Fief_Other;
        }

        /// <summary>
        /// Recursively assigns the given troop type to the entire upgrade tree starting from 'current'.
        /// </summary>
        private static void PropagateTypeThroughUpgradeTree(CharacterObject current, FiefTroopType type)
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
            ModLogger.Info("[Fief] Initializing SoldierTypeClassifier...");

            // Clear previous data (in case of reload)
            _typeMap.Clear();

            foreach (var culture in MBObjectManager.Instance.GetObjectTypeList<CultureObject>())
            {
                var group = BasicTroopGroupManager.GetGroupForCulture(culture);

                if (group != null)
                {
                    // Process pre-classified entries from XML config
                    ProcessTroopEntries(group.TroopsByType[FiefTroopType.Fief_Retinue]);
                    ProcessTroopEntries(group.TroopsByType[FiefTroopType.Fief_Sergeant]);
                    ProcessTroopEntries(group.TroopsByType[FiefTroopType.Fief_Militia]);
                }
                else
                {
                    // Fallback to vanilla basic/elite troops if no custom config exists
                    if (culture.EliteBasicTroop != null)
                    {
                        PropagateTypeThroughUpgradeTree(culture.EliteBasicTroop, FiefTroopType.Fief_Retinue);
                    }
                    else if (culture.BasicTroop != null)
                    {
                        PropagateTypeThroughUpgradeTree(culture.BasicTroop, FiefTroopType.Fief_Militia);
                    }
                }
            }

            ModLogger.Info($"[Fief] SoldierTypeClassifier initialized. Cached {_typeMap.Count} troops.");
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
    }
}
