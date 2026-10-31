using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Identifies the settlement manpower pool that owns a template entry.
    /// Flags allow one troop tree to be supplied by both systems.
    /// </summary>
    [Flags]
    public enum TroopProductionKind
    {
        None = 0,
        Professional = 1,
        Fief = 2
    }

    /// <summary>
    /// Immutable, template-local configuration for one base troop.
    /// Weight and barracks requirements belong to the concrete pool template.
    /// </summary>
    public sealed class TroopProductionEntry
    {
        public TroopProductionEntry(
            CharacterObject troop,
            int weight,
            int requiredBarracksLevel = 0)
        {
            Troop = troop;
            // A zero weight explicitly disables production. Do not silently
            // turn it into one, because that would contradict the XML author.
            Weight = Math.Max(0, weight);
            RequiredBarracksLevel = Math.Max(0, requiredBarracksLevel);
        }

        public CharacterObject Troop { get; }

        public int Weight { get; }

        public int RequiredBarracksLevel { get; }
    }

    /// <summary>
    /// Shared contract used by professional and fief manpower production.
    /// Production uses a base value plus one step per barracks level.
    /// </summary>
    public interface ITroopProductionTemplate
    {
        string TemplateId { get; }

        CultureObject Culture { get; }

        TroopProductionKind ProductionKind { get; }

        IReadOnlyList<TroopProductionEntry> TroopEntries { get; }
    }

    /// <summary>
    /// Common parsing and formula helpers shared by both XML template types.
    /// </summary>
    public static class TroopProductionTemplateUtility
    {
        public static int ReadOptionalInt(
            XmlNode node,
            string attribute,
            int fallback)
        {
            string raw = node?.Attributes?[attribute]?.Value;
            return int.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : fallback;
        }

        public static float ReadOptionalFloat(
            XmlNode node,
            string attribute,
            float fallback)
        {
            string raw = node?.Attributes?[attribute]?.Value;
            return float.TryParse(
                raw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : fallback;
        }

        public static float CalculateDailyProduction(
            float baseProduction,
            float productionStep,
            int barracksLevel)
        {
            int level = Math.Max(0, Math.Min(3, barracksLevel));
            return Math.Max(0f, baseProduction + productionStep * level);
        }
    }
}
