using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// Defines one settlement culture's tavern mercenary market, including
    /// refresh probability and weighted troop pool.
    /// Loaded through TaleWorlds MBObjectManager.
    /// </summary>
    public class MercenaryTemplate : MBObjectBase
    {
        /// <summary>
        /// Stable XML template identifier.
        /// </summary>
        public string TemplateId => StringId;

        /// <summary>
        /// Settlement culture whose taverns use this market template.
        /// </summary>
        public CultureObject Culture { get; private set; }

        /// <summary>
        /// Refresh probability from zero to one.
        /// </summary>
        public float SpawnChance { get; private set; } = 0.3f;

        /// <summary>
        /// Troops that this settlement culture can place in its taverns.
        /// Empire can list every foreign mercenary here, while an exporting
        /// culture normally lists only its own mercenaries.
        /// </summary>
        public IReadOnlyList<MercenaryTroopEntry> MercenaryTroops =>
            _mercenaryTroops;

        /// <summary>
        /// Distinguishes an intentionally empty culture pool from an older
        /// configuration that has not yet adopted the child-node format.
        /// </summary>
        public bool HasConfiguredTroopPool { get; private set; }

        private readonly List<MercenaryTroopEntry> _mercenaryTroops =
            new List<MercenaryTroopEntry>();

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            Culture = objectManager
                .ReadObjectReferenceFromXml<CultureObject>("culture", node);

            var spawnChanceStr = node.Attributes?["spawnChance"]?.Value;
            if (spawnChanceStr != null
                && float.TryParse(
                    spawnChanceStr,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedSpawnChance))
            {
                SpawnChance = Math.Max(0f, Math.Min(1f, parsedSpawnChance));
            }

            ReadMercenaryTroops(objectManager, node);

            MercenaryTemplateManager.Instance.RegisterTemplate(this);
        }

        /// <summary>
        /// Selects one troop entry with probability proportional to its XML
        /// weight. The returned entry also owns its generation range.
        /// Returns null when the template deliberately has no configured pool.
        /// </summary>
        public MercenaryTroopEntry SelectWeightedTroop()
        {
            int totalWeight = 0;

            foreach (MercenaryTroopEntry entry in _mercenaryTroops)
            {
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0)
            {
                return null;
            }

            int roll = MBRandom.RandomInt(totalWeight);

            foreach (MercenaryTroopEntry entry in _mercenaryTroops)
            {
                if (roll < entry.Weight)
                {
                    return entry;
                }

                roll -= entry.Weight;
            }

            return _mercenaryTroops[_mercenaryTroops.Count - 1];
        }

        /// <summary>
        /// Reads the nested MercenaryTroops/troop nodes. Invalid references or
        /// non-positive weights are ignored so one optional entry cannot make
        /// the entire market unavailable.
        /// </summary>
        private void ReadMercenaryTroops(
            MBObjectManager objectManager,
            XmlNode templateNode)
        {
            _mercenaryTroops.Clear();
            XmlNode troopPoolNode = templateNode.SelectSingleNode(
                "MercenaryTroops");
            HasConfiguredTroopPool = troopPoolNode != null;

            if (troopPoolNode == null)
            {
                return;
            }

            foreach (XmlNode troopNode in troopPoolNode.SelectNodes("troop"))
            {
                CharacterObject troop = objectManager
                    .ReadObjectReferenceFromXml<CharacterObject>(
                        "id",
                        troopNode);
                int weight = troopNode.Attributes?["weight"] != null
                    ? XmlHelper.ReadInt(troopNode, "weight")
                    : 1;
                int minCount = troopNode.Attributes?["minCount"] != null
                    ? XmlHelper.ReadInt(troopNode, "minCount")
                    : 0;
                int maxCount = troopNode.Attributes?["maxCount"] != null
                    ? XmlHelper.ReadInt(troopNode, "maxCount")
                    : 0;

                if (troop != null && weight > 0 && minCount > 0 &&
                    maxCount >= minCount)
                {
                    _mercenaryTroops.Add(
                        new MercenaryTroopEntry(
                            troop,
                            weight,
                            minCount,
                            maxCount));
                }
            }
        }
    }

    /// <summary>
    /// One weighted troop option in a culture's tavern mercenary pool.
    /// </summary>
    public sealed class MercenaryTroopEntry
    {
        public MercenaryTroopEntry(
            CharacterObject troop,
            int weight,
            int minCount,
            int maxCount)
        {
            Troop = troop;
            Weight = weight;
            MinCount = minCount;
            MaxCount = maxCount;
        }

        public CharacterObject Troop { get; }

        public int Weight { get; }

        public int MinCount { get; }

        public int MaxCount { get; }
    }

    /// <summary>
    /// Stores loaded market templates and resolves them by settlement culture.
    /// </summary>
    public class MercenaryTemplateManager
    {
        public static readonly MercenaryTemplateManager Instance = new();

        private readonly Dictionary<string, MercenaryTemplate> _templates = new();

        private MercenaryTemplateManager() { }

        /// <summary>
        /// Registers or replaces one XML template.
        /// </summary>
        public void RegisterTemplate(MercenaryTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.TemplateId))
            {
                return;
            }

            _templates[template.TemplateId] = template;
        }

        /// <summary>
        /// Gets the tavern market template for a settlement culture.
        /// </summary>
        public MercenaryTemplate GetTemplateByCulture(CultureObject culture)
        {
            if (culture == null) return null;
            foreach (var kvp in _templates)
            {
                if (kvp.Value.Culture == culture)
                    return kvp.Value;
            }
            return null;
        }
    }
}
