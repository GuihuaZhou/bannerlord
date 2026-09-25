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
    public enum RecruitmentPoolKind
    {
        None = 0,
        Professional = 1,
        Fief = 2
    }

    /// <summary>
    /// Immutable, template-local configuration for one base troop.
    /// Weight belongs to the pool template; barracks level belongs to the
    /// troop and is copied from basic_troop_config.xml.
    /// </summary>
    public sealed class RecruitmentPoolTroopEntry
    {
        public RecruitmentPoolTroopEntry(
            CharacterObject troop,
            int weight,
            int requiredBarracksLevel)
        {
            Troop = troop;
            Weight = Math.Max(1, weight);
            RequiredBarracksLevel = Math.Max(0, requiredBarracksLevel);
        }

        public CharacterObject Troop { get; }

        public int Weight { get; }

        public int RequiredBarracksLevel { get; }
    }

    /// <summary>
    /// Shared contract used by professional and fief manpower pools.
    /// Capacity is fixed by the template. Production uses a base value plus
    /// one step per barracks level, so culture and settlement type remain
    /// fully data-driven without allowing barracks to enlarge storage.
    /// </summary>
    public interface IRecruitmentPoolTemplate
    {
        string TemplateId { get; }

        CultureObject Culture { get; }

        RecruitmentPoolKind PoolKind { get; }

        int BaseCapacity { get; }

        float BaseDailyProduction { get; }

        float DailyProductionStep { get; }

        IReadOnlyList<RecruitmentPoolTroopEntry> PoolTroops { get; }

        int GetCapacity();

        float GetDailyProduction(int barracksLevel);
    }

    /// <summary>
    /// Common parsing and formula helpers shared by both XML template types.
    /// </summary>
    public static class RecruitmentPoolTemplateUtility
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
