using HarmonyLib;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Patches
{
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
            if (troop == null)
                return FiefTroopType.Fief_Other;

            if (_typeMap.TryGetValue(troop, out var type))
                return type;

            // Cache miss: likely an unregistered troop (e.g., from another mod or custom unit)
            ModLogger.Warn($"[Fief] Cache miss for troop: '{troop.Name}' (ID: {troop.StringId ?? "null"}, Tier: {troop.Tier})");
            return FiefTroopType.Fief_Other;
        }

        /// <summary>
        /// Processes all base troops of a given culture and propagates their types through the upgrade tree.
        /// </summary>
        public static void ProcessCultureTroops(CultureObject culture, List<CharacterWeightPair> pairs)
        {
            foreach (var pair in pairs)
            {
                if (pair.Character == null) continue;

                FiefTroopType soldierType = pair.Type switch
                {
                    FiefTroopType.Fief_Militia => FiefTroopType.Fief_Militia,
                    FiefTroopType.Fief_Retinue => FiefTroopType.Fief_Retinue,
                    _ => FiefTroopType.Fief_Sergeant
                };

                PropagateTypeThroughUpgradeTree(pair.Character, soldierType);
            }
        }

        /// <summary>
        /// Initializes the classifier by processing all cultures registered in CultureBasicTroopManager.
        /// This should be called once after all modules are loaded.
        /// </summary>
        public static void InitializeAll()
        {
            ModLogger.Info("[Fief] Initializing SoldierTypeClassifier...");

            foreach (var kvp in CultureBasicTroopManager.Data)
            {
                var culture = kvp.Key;
                var entries = kvp.Value;
                var characterWeightPairList = new List<CharacterWeightPair>();
                var seenTroops = new HashSet<CharacterObject>();

                var allRelevantTypes = new List<string>
                {
                    "infantry", "twohanded", "ranged", "cavalry", "horsearcher", "basic", "elite"
                };

                foreach (var type in allRelevantTypes)
                {
                    if (entries.TryGetValue(type, out var entry) && seenTroops.Add(entry.Troop))
                    {
                        FiefTroopType troopType = type switch
                        {
                            "basic" => FiefTroopType.Fief_Militia,
                            "elite" => FiefTroopType.Fief_Retinue,
                            _ => FiefTroopType.Fief_Sergeant
                        };
                        characterWeightPairList.Add(new CharacterWeightPair(entry.Troop, entry.Weight, troopType));
                    }
                }

                // Fallback to vanilla basic/elite troops if no custom troops are defined
                if (characterWeightPairList.Count == 0)
                {
                    if (culture.EliteBasicTroop != null && seenTroops.Add(culture.EliteBasicTroop))
                    {
                        characterWeightPairList.Add(new CharacterWeightPair(culture.EliteBasicTroop, 1, FiefTroopType.Fief_Retinue));
                    }
                    else if (culture.BasicTroop != null && seenTroops.Add(culture.BasicTroop))
                    {
                        characterWeightPairList.Add(new CharacterWeightPair(culture.BasicTroop, 15, FiefTroopType.Fief_Militia));
                    }
                }

                ProcessCultureTroops(culture, characterWeightPairList);
            }

            ModLogger.Info($"[Fief] SoldierTypeClassifier initialized. Cached {TypeMap.Count} troops.");
        }

        /// <summary>
        /// Recursively assigns the given troop type to the entire upgrade tree starting from 'current'.
        /// </summary>
        private static void PropagateTypeThroughUpgradeTree(CharacterObject current, FiefTroopType type)
        {
            if (current == null) return;
            _typeMap[current] = type; // Overwrite if already present (shouldn't happen in practice)

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
    }

    /// <summary>
    /// Represents a weighted troop candidate with an assigned fief type.
    /// </summary>
    public class CharacterWeightPair
    {
        public CharacterObject Character { get; set; }
        public float Weight { get; set; }
        public FiefTroopType Type { get; set; }

        public CharacterWeightPair(CharacterObject character, float weight, FiefTroopType type)
        {
            Character = character;
            Weight = weight;
            Type = type;
        }
    }

    /// <summary>
    /// Caches volunteer candidate pools per culture, including weights and fief types.
    /// </summary>
    public class CultureVolunteerGroupsCache
    {
        public static CultureVolunteerGroupsCache Instance { get; } = new CultureVolunteerGroupsCache();

        public Dictionary<CultureObject, List<CharacterWeightPair>> CultureVolunteerData { get; private set; }

        private CultureVolunteerGroupsCache()
        {
            CultureVolunteerData = new Dictionary<CultureObject, List<CharacterWeightPair>>();
        }

        internal void AddOrReplaceVolunteerData(CultureObject culture, List<CharacterWeightPair> data)
        {
            CultureVolunteerData[culture] = data;
        }

        public List<CharacterWeightPair> GetVolunteerCandidateCache(CultureObject cultureObj)
        {
            if (cultureObj == null || !CultureVolunteerData.TryGetValue(cultureObj, out var list))
                return new List<CharacterWeightPair>();
            return list;
        }

        public List<CharacterWeightPair> GetVolunteerCandidateCache(Settlement settlement)
        {
            return settlement == null ? new List<CharacterWeightPair>() : GetVolunteerCandidateCache(settlement.Culture);
        }
    }

    /// <summary>
    /// Manages culture-specific base troop definitions loaded from XML during deserialization.
    /// </summary>
    public static class CultureBasicTroopManager
    {
        public static Dictionary<CultureObject, Dictionary<string, TroopEntry>> Data { get; } = new();

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
            BuildAndCacheUnifiedCandidates(__instance, entries);
        }

        private static void BuildAndCacheUnifiedCandidates(CultureObject culture, Dictionary<string, CultureBasicTroopManager.TroopEntry> entries)
        {
            var characterWeightPairList = new List<CharacterWeightPair>();
            var seenTroops = new HashSet<CharacterObject>();

            var allRelevantTypes = new List<string>
            {
                "infantry", "twohanded", "ranged", "cavalry", "horsearcher", "basic", "elite"
            };

            foreach (var type in allRelevantTypes)
            {
                if (entries.TryGetValue(type, out var entry) && seenTroops.Add(entry.Troop))
                {
                    FiefTroopType troopType = type switch
                    {
                        "basic" => FiefTroopType.Fief_Militia,
                        "elite" => FiefTroopType.Fief_Retinue,
                        _ => FiefTroopType.Fief_Sergeant
                    };
                    characterWeightPairList.Add(new CharacterWeightPair(entry.Troop, entry.Weight, troopType));
                }
            }

            // Fallback to vanilla troops if no valid custom troops were found
            if (characterWeightPairList.Count == 0)
            {
                if (culture.EliteBasicTroop != null && seenTroops.Add(culture.EliteBasicTroop))
                {
                    characterWeightPairList.Add(new CharacterWeightPair(culture.EliteBasicTroop, 1, FiefTroopType.Fief_Retinue));
                }
                else if (culture.BasicTroop != null && seenTroops.Add(culture.BasicTroop))
                {
                    characterWeightPairList.Add(new CharacterWeightPair(culture.BasicTroop, 15, FiefTroopType.Fief_Militia));
                }
            }

            CultureVolunteerGroupsCache.Instance.AddOrReplaceVolunteerData(culture, characterWeightPairList);
        }
    }
}