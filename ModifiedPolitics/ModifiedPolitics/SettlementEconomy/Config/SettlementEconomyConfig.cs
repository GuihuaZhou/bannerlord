using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TaleWorlds.ObjectSystem;

namespace ModifiedPolitics.SettlementEconomy.Config
{
    public sealed class PopulationWallConfig { public float Initial { get; set; } public float Capacity { get; set; } }
    public sealed class SettlementEconomyConfig : MBObjectBase
    {
        public int ProsperityConsumptionDivisor { get; private set; }
        public float NaturalGrowthRate { get; private set; }
        public float NaturalGrowthSeed { get; private set; }
        public float LowPopulationMultiplier { get; private set; }
        public float LowPopulationThreshold { get; private set; }
        public float VillageCapacity { get; private set; }
        public float TownProsperityPerHearth { get; private set; }
        public float CastleProsperityPerHearth { get; private set; }
        public float ExcessProsperityDecayDays { get; private set; }
        public Dictionary<int, PopulationWallConfig> TownWalls { get; private set; }
        public Dictionary<int, PopulationWallConfig> CastleWalls { get; private set; }

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            ProsperityConsumptionDivisor = PositiveInt(Required(node, "Food/ProsperityConsumptionDivisor"), "value");
            XmlNode growth = Required(node, "Population/NaturalGrowth");
            NaturalGrowthRate = NonNegative(growth, "rate"); NaturalGrowthSeed = NonNegative(growth, "seed");
            LowPopulationMultiplier = Number(growth, "lowPopulationMultiplier"); LowPopulationThreshold = Number(growth, "lowPopulationThreshold");
            if (LowPopulationMultiplier < 1f) throw Error(growth, "lowPopulationMultiplier must be at least 1");
            if (LowPopulationThreshold <= 0f || LowPopulationThreshold > 1f) throw Error(growth, "lowPopulationThreshold must be in (0,1]");
            VillageCapacity = Positive(Required(node, "Population/Village"), "capacity");
            TownWalls = ParseWalls(Required(node, "Population/Town")); CastleWalls = ParseWalls(Required(node, "Population/Castle"));
            XmlNode support = Required(node, "Population/ProsperitySupport");
            TownProsperityPerHearth = Positive(support, "townPerHearth"); CastleProsperityPerHearth = Positive(support, "castlePerHearth");
            ExcessProsperityDecayDays = Positive(support, "excessDecayDays");
            SettlementEconomyConfigManager.Instance.Register(this);
        }
        private static Dictionary<int, PopulationWallConfig> ParseWalls(XmlNode parent)
        {
            var result = new Dictionary<int, PopulationWallConfig>();
            foreach (XmlNode wall in parent.SelectNodes("Wall"))
            {
                int level = PositiveInt(wall, "level");
                if (level > 3 || result.ContainsKey(level)) throw Error(wall, "wall level must be unique and in 1..3");
                float initial = NonNegative(wall, "initial"), capacity = Positive(wall, "capacity");
                if (initial > capacity) throw Error(wall, "initial cannot exceed capacity");
                result.Add(level, new PopulationWallConfig { Initial = initial, Capacity = capacity });
            }
            for (int i = 1; i <= 3; i++) if (!result.ContainsKey(i)) throw Error(parent, "missing Wall level " + i);
            return result;
        }
        private static XmlNode Required(XmlNode node, string path) => node.SelectSingleNode(path) ?? throw Error(node, "missing node " + path);
        private static int PositiveInt(XmlNode node, string attribute)
        {
            string raw = node.Attributes?[attribute]?.Value;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value <= 0) throw Error(node, attribute + " must be a positive integer");
            return value;
        }
        private static float Number(XmlNode node, string attribute)
        {
            string raw = node.Attributes?[attribute]?.Value;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value)) throw Error(node, attribute + " must be a finite number");
            return value;
        }
        private static float Positive(XmlNode node, string attribute) { float v = Number(node, attribute); if (v <= 0f) throw Error(node, attribute + " must be greater than 0"); return v; }
        private static float NonNegative(XmlNode node, string attribute) { float v = Number(node, attribute); if (v < 0f) throw Error(node, attribute + " cannot be negative"); return v; }
        private static InvalidOperationException Error(XmlNode node, string reason) => new InvalidOperationException("SettlementEconomyConfig/" + node.Name + ": " + reason);
    }
    public sealed class SettlementEconomyConfigManager
    {
        public static readonly SettlementEconomyConfigManager Instance = new SettlementEconomyConfigManager();
        public SettlementEconomyConfig Active { get; private set; }
        public int ActiveProsperityConsumptionDivisor => Active?.ProsperityConsumptionDivisor ?? 34;
        public void Register(SettlementEconomyConfig config) { Active = config; }
        public void Clear() { Active = null; }
        public SettlementEconomyConfig RequireActive() => Active ?? throw new InvalidOperationException("Settlement economy configuration is not loaded.");
    }
}
