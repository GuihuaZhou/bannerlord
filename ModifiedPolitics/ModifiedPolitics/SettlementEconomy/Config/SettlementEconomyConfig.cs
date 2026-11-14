// XML-backed settlement economy and tax-AI balance configuration. This file
// validates inputs during campaign startup so runtime models can assume finite,
// normalized weights and hysteresis parameters.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace ModifiedPolitics.SettlementEconomy.Config
{
    public sealed class CulturePopulationCapacityConfig
    {
        public string CultureId { get; set; }
        public float VillageCapacity { get; set; }
        public float TownCapacity { get; set; }
        public float CastleCapacity { get; set; }
    }

    public sealed class SettlementEconomyConfig : MBObjectBase
    {
        private static readonly string[] RequiredCultureIds = { "empire", "aserai", "vlandia", "sturgia", "khuzait", "battania", "nord" };
        private readonly Dictionary<string, CulturePopulationCapacityConfig> _cultureCapacities = new Dictionary<string, CulturePopulationCapacityConfig>(StringComparer.OrdinalIgnoreCase);

        public int ProsperityConsumptionDivisor { get; private set; }
        public float VillageGrowthRate { get; private set; }
        public float TownGrowthRate { get; private set; }
        public float CastleGrowthRate { get; private set; }
        public float NaturalGrowthSeed { get; private set; }
        public float LowPopulationMultiplier { get; private set; }
        public float LowPopulationThreshold { get; private set; }
        public float FoodPerChange { get; private set; }
        public float FoodMinimum { get; private set; }
        public float FoodMaximum { get; private set; }
        public float ProsperityTriggerSpan { get; private set; }
        public float ProsperityMaximum { get; private set; }
        public float WallLevel1Factor { get; private set; }
        public float WallLevel2Factor { get; private set; }
        public float WallLevel3Factor { get; private set; }
        public float InitialToCapacity { get; private set; }
        public float TownProsperityPerHearth { get; private set; }
        public float CastleProsperityPerHearth { get; private set; }
        public float ExcessProsperityDecayDays { get; private set; }
        public int TaxProtectionSafeDays { get; private set; }
        public float TaxSwitchThreshold { get; private set; }
        public float TaxFiscalWeight { get; private set; }
        public float TaxEconomyWeight { get; private set; }
        public float TaxRecruitmentWeight { get; private set; }
        public float TaxRebellionWeight { get; private set; }
        public IReadOnlyDictionary<string, CulturePopulationCapacityConfig> CultureCapacities => _cultureCapacities;

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            ProsperityConsumptionDivisor = PositiveInt(Required(node, "Food/ProsperityConsumptionDivisor"), "value");
            XmlNode growth = Required(node, "Population/NaturalGrowth");
            NaturalGrowthSeed = NonNegative(growth, "seed");
            LowPopulationMultiplier = Number(growth, "lowPopulationMultiplier");
            LowPopulationThreshold = Number(growth, "lowPopulationThreshold");
            if (LowPopulationMultiplier < 1f) throw Error(growth, "lowPopulationMultiplier must be at least 1");
            if (LowPopulationThreshold <= 0f || LowPopulationThreshold > 1f) throw Error(growth, "lowPopulationThreshold must be in (0,1]");

            XmlNode rates = Required(node, "Population/PopulationRates");
            VillageGrowthRate = NonNegative(rates, "village");
            TownGrowthRate = NonNegative(rates, "town");
            CastleGrowthRate = NonNegative(rates, "castle");
            XmlNode factors = Required(node, "Population/DailyFactors");
            FoodPerChange = NonNegative(factors, "foodPerChange");
            FoodMinimum = Number(factors, "foodMin");
            FoodMaximum = Number(factors, "foodMax");
            ProsperityTriggerSpan = Positive(factors, "prosperityTriggerSpan");
            ProsperityMaximum = NonNegative(factors, "prosperityMax");
            if (FoodMinimum > FoodMaximum) throw Error(factors, "foodMin cannot exceed foodMax");

            XmlNode walls = Required(node, "Population/WallCapacityFactors");
            WallLevel1Factor = Positive(walls, "level1");
            WallLevel2Factor = Positive(walls, "level2");
            WallLevel3Factor = Positive(walls, "level3");
            InitialToCapacity = Positive(walls, "initialToCapacity");
            if (WallLevel1Factor > WallLevel2Factor || WallLevel2Factor > WallLevel3Factor) throw Error(walls, "wall factors must be monotonic");
            if (InitialToCapacity > 1f) throw Error(walls, "initialToCapacity cannot exceed 1");

            _cultureCapacities.Clear();
            foreach (XmlNode culture in Required(node, "Population/Cultures").SelectNodes("Culture"))
            {
                string id = NormalizeCultureId(RequiredText(culture, "id"));
                if (_cultureCapacities.ContainsKey(id)) throw Error(culture, "duplicate culture id " + id);
                _cultureCapacities.Add(id, new CulturePopulationCapacityConfig
                {
                    CultureId = id,
                    VillageCapacity = Positive(culture, "villageCapacity"),
                    TownCapacity = Positive(culture, "townCapacity"),
                    CastleCapacity = Positive(culture, "castleCapacity")
                });
            }
            foreach (string id in RequiredCultureIds) if (!_cultureCapacities.ContainsKey(id)) throw Error(node, "missing culture capacity " + id);
            if (_cultureCapacities.Count != RequiredCultureIds.Length) throw Error(node, "unexpected culture capacity entry");

            XmlNode support = Required(node, "Population/ProsperitySupport");
            TownProsperityPerHearth = Positive(support, "townPerHearth");
            CastleProsperityPerHearth = Positive(support, "castlePerHearth");
            ExcessProsperityDecayDays = Positive(support, "excessDecayDays");
            XmlNode tax = Required(node, "TaxPolicy/Ai");
            TaxProtectionSafeDays = PositiveInt(tax, "safeDays");
            TaxSwitchThreshold = NonNegative(tax, "switchThreshold");
            TaxFiscalWeight = NonNegative(tax, "fiscalWeight");
            TaxEconomyWeight = NonNegative(tax, "economyWeight");
            TaxRecruitmentWeight = NonNegative(tax, "recruitmentWeight");
            TaxRebellionWeight = NonNegative(tax, "rebellionWeight");
            SettlementEconomyConfigManager.Instance.Register(this);
        }

        public float GetCapacity(Settlement settlement)
        {
            CulturePopulationCapacityConfig culture = GetCulture(settlement);
            if (settlement?.Village != null) return culture.VillageCapacity;
            if (settlement?.Town == null) return 0f;
            float wall3 = settlement.IsCastle ? culture.CastleCapacity : culture.TownCapacity;
            return wall3 * GetWallFactor(settlement.Town.GetWallLevel());
        }

        public float GetInitialHearth(Settlement settlement) => (float)Math.Round(GetCapacity(settlement) * InitialToCapacity, MidpointRounding.AwayFromZero);
        public float GetGrowthRate(Settlement settlement) => settlement?.Village != null ? VillageGrowthRate : settlement?.IsCastle == true ? CastleGrowthRate : TownGrowthRate;

        private CulturePopulationCapacityConfig GetCulture(Settlement settlement)
        {
            string id = NormalizeCultureId(settlement?.Culture?.StringId);
            if (string.IsNullOrWhiteSpace(id) || !_cultureCapacities.TryGetValue(id, out var value))
                throw new InvalidOperationException("Missing population capacity for settlement culture '" + (id ?? "<null>") + "'.");
            return value;
        }

        private float GetWallFactor(int rawLevel)
        {
            int level = Math.Max(1, Math.Min(3, rawLevel));
            return level == 1 ? WallLevel1Factor : level == 2 ? WallLevel2Factor : WallLevel3Factor;
        }

        private static string NormalizeCultureId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            const string prefix = "Culture.";
            return id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? id.Substring(prefix.Length) : id;
        }

        private static XmlNode Required(XmlNode node, string path) => node.SelectSingleNode(path) ?? throw Error(node, "missing node " + path);
        private static string RequiredText(XmlNode node, string attribute)
        {
            string value = node.Attributes?[attribute]?.Value;
            if (string.IsNullOrWhiteSpace(value)) throw Error(node, attribute + " is required");
            return value;
        }
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
        private static float Positive(XmlNode node, string attribute) { float value = Number(node, attribute); if (value <= 0f) throw Error(node, attribute + " must be greater than 0"); return value; }
        private static float NonNegative(XmlNode node, string attribute) { float value = Number(node, attribute); if (value < 0f) throw Error(node, attribute + " cannot be negative"); return value; }
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
