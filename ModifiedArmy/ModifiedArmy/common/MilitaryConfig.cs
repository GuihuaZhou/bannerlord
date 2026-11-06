using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.common
{
    public struct AiSituationalModifier
    {
        public float Fief;
        public float GarrisonTransfer;
        public float Mercenary;
        public float Threshold;
    }

    public sealed class ProfessionalTrainingConfig
    {
        public float Base { get; internal set; }
        public float BarracksStep { get; internal set; }

        public float GetDailyProduction(int barracksLevel) =>
            Math.Max(0f, Base + Math.Max(0, barracksLevel) * BarracksStep);
    }

    public sealed class FiefProductionConfig
    {
        public float DailyRate { get; internal set; }
        public int ProsperityCostPerTroop { get; internal set; }
    }

    public sealed class HearthEconomyConfig
    {
        public IReadOnlyDictionary<int, int> ProfessionalRecruitment { get; internal set; }
        public float FeudalTaxReliefWageRatio { get; internal set; }

        public int GetProfessionalRecruitmentCost(CharacterObject troop) =>
            GetRequiredCost(ProfessionalRecruitment, troop,
                "ProfessionalRecruitment");

        internal static int GetRequiredCost(
            IReadOnlyDictionary<int, int> costs,
            CharacterObject troop, string group)
        {
            if (troop == null || costs == null ||
                !costs.TryGetValue(troop.Tier, out int cost) || cost <= 0)
                throw new InvalidOperationException(string.Format(
                    "HearthEconomy {0} has no positive cost for troop={1}, tier={2}.",
                    group, troop?.StringId ?? "null", troop?.Tier ?? -1));
            return cost;
        }
    }

    public sealed class FiefServiceConfig
    {
        public int FreeWageDays { get; internal set; }
        public int ReturnCooldownWeeks { get; internal set; }
    }

    public sealed class CulturalMilitaryDoctrine
    {
        public CultureObject Culture { get; internal set; }
        public int FiefWeight { get; internal set; }
        public int ProfessionalWeight { get; internal set; }
        public int MercenaryWeight { get; internal set; }
        public IReadOnlyDictionary<int, int> FeudalSupply { get; internal set; }
        public FiefServiceConfig Service { get; internal set; }
        public ProfessionalTrainingConfig TownTraining { get; internal set; }
        public ProfessionalTrainingConfig CastleTraining { get; internal set; }
        public ProfessionalTrainingConfig TownPortTraining { get; internal set; }
        public FiefProductionConfig TownProduction { get; internal set; }
        public FiefProductionConfig CastleProduction { get; internal set; }
        public FiefProductionConfig TownPortProduction { get; internal set; }

        public float ProfessionalShare =>
            (float)ProfessionalWeight / (FiefWeight + ProfessionalWeight);

        public bool TryGetFeudalSupplyCost(CharacterObject troop,
            out int cost)
        {
            cost = 0;
            return troop != null && FeudalSupply != null &&
                FeudalSupply.TryGetValue(troop.Tier, out cost) && cost > 0;
        }

        public int GetFeudalSupplyCost(CharacterObject troop)
        {
            if (TryGetFeudalSupplyCost(troop, out int cost)) return cost;
            return HearthEconomyConfig.GetRequiredCost(
                FeudalSupply, troop, "FeudalSupply");
        }

        public ProfessionalTrainingConfig GetTraining(Settlement settlement) =>
            settlement?.IsCastle == true
                ? CastleTraining
                : settlement?.HasPort == true && TownPortTraining != null
                    ? TownPortTraining
                    : TownTraining;

        public FiefProductionConfig GetProduction(Settlement settlement) =>
            settlement?.IsCastle == true
                ? CastleProduction
                : settlement?.HasPort == true && TownPortProduction != null
                    ? TownPortProduction
                    : TownProduction;
    }

    public sealed class AiRecruitmentConfig
    {
        public float ManpowerWeight { get; internal set; }
        public float WarUrgencyWeight { get; internal set; }
        public float EconomicWeight { get; internal set; }
        public float Threshold { get; internal set; }
        public float PeaceUrgency { get; internal set; }
        public float WarUrgency { get; internal set; }
        public float SiegeUrgency { get; internal set; }
        public float EconomicWeeks { get; internal set; }
        public float MatchFiefBonus { get; internal set; }
        public float MismatchGarrisonTransferPenalty { get; internal set; }
        public float EnemyDetectRadius { get; internal set; }
        public float EnemyStrengthRatio { get; internal set; }
        public float RandomChance { get; internal set; }
        public float MercenarySearchRadius { get; internal set; }
        public Dictionary<string, AiSituationalModifier> SituationalModifiers { get; } =
            new Dictionary<string, AiSituationalModifier>(StringComparer.OrdinalIgnoreCase);
        public ForceManagementConfig ForceManagement { get; } =
            new ForceManagementConfig();
    }

    public sealed class ForceManagementConfig
    {
        public bool EnableUnifiedAiForcePlanning { get; internal set; }
        public float PeaceTargetRatio { get; internal set; } = 0.35f;
        public int PeaceMinimumCount { get; internal set; } = 50;
        public float AggressiveWarDispositionThreshold { get; internal set; } = -50f;
        public float OwnFiefDefenseTargetRatio { get; internal set; } = 1f;
        public float InteriorGarrisonUpperRatio { get; internal set; } = 0.75f;
        public float BorderGarrisonUpperRatio { get; internal set; } = 0.85f;
        public float InteriorGarrisonLowerRatio { get; internal set; } = 0.50f;
        public float BorderGarrisonLowerRatio { get; internal set; } = 0.75f;
        public int MinimumOwnSurplusDelivery { get; internal set; } = 20;
        public int MinimumFiefRecruitBatch { get; internal set; } = 25;
        public int ReinforcementForecastDays { get; internal set; } = 7;
        public int TransportMissionDeadlineDays { get; internal set; } = 20;
        public bool EnableAutomaticReinforcement { get; internal set; }
        public DemobilizationConfig Demobilization { get; } =
            new DemobilizationConfig();
    }

    public sealed class DemobilizationConfig
    {
        public bool EnableAutomaticDemobilization { get; internal set; }
        public bool EnableDemobilizationRouting { get; internal set; }
        public int PeaceStabilityDays { get; internal set; } = 3;
        public float MaxDailyDemobilizationRatio { get; internal set; } = 0.20f;
        public int MaxDailyDemobilizationCount { get; internal set; } = 30;
    }

    public sealed class MilitaryConfig : MBObjectBase
    {
        private readonly Dictionary<string, CulturalMilitaryDoctrine> _doctrines =
            new Dictionary<string, CulturalMilitaryDoctrine>(StringComparer.OrdinalIgnoreCase);

        public string ConfigId => StringId;
        public AiRecruitmentConfig AiRecruitment { get; } = new AiRecruitmentConfig();
        public HearthEconomyConfig HearthEconomy { get; private set; }

        // Compatibility-shaped accessors keep decision code focused on behavior.
        public float AiNeedManpowerWeight => AiRecruitment.ManpowerWeight;
        public float AiNeedWarUrgencyWeight => AiRecruitment.WarUrgencyWeight;
        public float AiNeedEconomicWeight => AiRecruitment.EconomicWeight;
        public float AiNeedThreshold => AiRecruitment.Threshold;
        public float AiNeedPeaceUrgency => AiRecruitment.PeaceUrgency;
        public float AiNeedWarUrgency => AiRecruitment.WarUrgency;
        public float AiNeedSiegeUrgency => AiRecruitment.SiegeUrgency;
        public float AiNeedEconomicWeeks => AiRecruitment.EconomicWeeks;
        public float AiCultMatchFiefBonus => AiRecruitment.MatchFiefBonus;
        public float AiCultMismatchGarrisonTransferPenalty => AiRecruitment.MismatchGarrisonTransferPenalty;
        public float AiEnemyDetectRadius => AiRecruitment.EnemyDetectRadius;
        public float AiEnemyStrengthRatio => AiRecruitment.EnemyStrengthRatio;
        public float AiRecruitRandomChance => AiRecruitment.RandomChance;
        public float AiMercenarySearchRadius => AiRecruitment.MercenarySearchRadius;

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            ParseAi(node.SelectSingleNode("AiRecruitment"));
            HearthEconomy = ParseHearthEconomy(Required(node,
                "HearthEconomy"));
            XmlNode cultures = Required(node, "Cultures");
            foreach (XmlNode cultureNode in cultures.SelectNodes("Culture"))
            {
                CultureObject culture = objectManager.ReadObjectReferenceFromXml<CultureObject>("id", cultureNode);
                if (culture == null)
                    throw new InvalidOperationException("MilitaryConfig contains an invalid culture reference.");
                if (_doctrines.ContainsKey(culture.StringId))
                    throw new InvalidOperationException("MilitaryConfig contains duplicate culture: " + culture.StringId);
                CulturalMilitaryDoctrine doctrine = ParseDoctrine(cultureNode, culture);
                _doctrines.Add(culture.StringId, doctrine);
            }
            if (_doctrines.Count == 0)
                throw new InvalidOperationException("MilitaryConfig must contain at least one culture doctrine.");
            MilitaryConfigManager.Instance.Register(this);
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_config_loaded");
            message.SetTextVariable("CONFIG_ID", ConfigId);
            message.SetTextVariable("DOCTRINE_COUNT", _doctrines.Count);
            ModLogger.Info(message.ToString());
        }

        public CulturalMilitaryDoctrine GetDoctrine(CultureObject culture)
        {
            if (culture == null || !_doctrines.TryGetValue(culture.StringId, out var doctrine))
                throw new InvalidOperationException("No military doctrine exists for culture '" + (culture?.StringId ?? "null") + "'.");
            return doctrine;
        }

        public CulturalMilitaryDoctrine GetDoctrine(Settlement settlement) =>
            GetDoctrine(settlement?.Culture);

        public (float Fief, float GarrisonTransfer, float Mercenary) GetAiCulturalPreference(CultureObject culture)
        {
            CulturalMilitaryDoctrine doctrine = GetDoctrine(culture);
            return (doctrine.FiefWeight, doctrine.ProfessionalWeight, doctrine.MercenaryWeight);
        }

        public (float Fief, float GarrisonTransfer, float Mercenary) GetAiSituationalModifier(string id)
        {
            return AiRecruitment.SituationalModifiers.TryGetValue(id ?? string.Empty, out var value)
                ? (value.Fief, value.GarrisonTransfer, value.Mercenary)
                : (1f, 1f, 1f);
        }

        public float GetAiSituationalThreshold(string id) =>
            AiRecruitment.SituationalModifiers.TryGetValue(id ?? string.Empty, out var value)
                ? value.Threshold : 0f;

        private void ParseAi(XmlNode ai)
        {
            if (ai == null) throw new InvalidOperationException("MilitaryConfig requires AiRecruitment.");
            XmlNode need = Required(ai, "NeedScore");
            AiRecruitment.ManpowerWeight = Value(need, "ManpowerWeight");
            AiRecruitment.WarUrgencyWeight = Value(need, "WarUrgencyWeight");
            AiRecruitment.EconomicWeight = Value(need, "EconomicWeight");
            AiRecruitment.Threshold = Value(need, "Threshold");
            AiRecruitment.PeaceUrgency = Value(need, "PeaceUrgency");
            AiRecruitment.WarUrgency = Value(need, "WarUrgency");
            AiRecruitment.SiegeUrgency = Value(need, "SiegeUrgency");
            AiRecruitment.EconomicWeeks = Value(need, "EconomicWeeks");
            XmlNode identity = Required(ai, "CulturalIdentity");
            AiRecruitment.MatchFiefBonus = Value(identity, "MatchFiefBonus");
            AiRecruitment.MismatchGarrisonTransferPenalty = Value(identity, "MismatchGarrisonTransferPenalty");
            foreach (XmlNode item in Required(ai, "SituationalModifiers").SelectNodes("Situation"))
            {
                string id = Attr(item, "id");
                if (AiRecruitment.SituationalModifiers.ContainsKey(id))
                    throw new InvalidOperationException("Duplicate AI situation: " + id);
                AiRecruitment.SituationalModifiers[id] = new AiSituationalModifier
                {
                    Fief = Value(item, "Fief"),
                    GarrisonTransfer = Value(item, "GarrisonTransfer"),
                    Mercenary = Value(item, "Mercenary"),
                    Threshold = OptionalValue(item, "Threshold", 0f)
                };
            }
            foreach (string requiredSituation in new[] { "siege", "broke", "critical", "peace", "war" })
            {
                if (!AiRecruitment.SituationalModifiers.ContainsKey(requiredSituation))
                    throw new InvalidOperationException("MilitaryConfig is missing AI situation: " + requiredSituation);
            }
            XmlNode enemy = Required(ai, "EnemyDetection");
            AiRecruitment.EnemyDetectRadius = Value(enemy, "Radius");
            AiRecruitment.EnemyStrengthRatio = Value(enemy, "StrengthRatio");
            XmlNode limits = Required(ai, "RecruitmentLimits");
            AiRecruitment.RandomChance = Value(limits, "RandomChance");
            AiRecruitment.MercenarySearchRadius = Value(limits, "MercenarySearchRadius");
            ParseForceManagement(ai.SelectSingleNode("ForceManagement"));
        }

        private void ParseForceManagement(XmlNode node)
        {
            ForceManagementConfig value = AiRecruitment.ForceManagement;
            if (node == null)
            {
                return;
            }

            value.EnableUnifiedAiForcePlanning = BooleanValue(
                node, "EnableUnifiedAiForcePlanning", false);

            value.PeaceTargetRatio = RatioValue(
                node, "PeaceTargetRatio", value.PeaceTargetRatio);
            value.PeaceMinimumCount = NonNegativeValue(
                node, "PeaceMinimumCount", value.PeaceMinimumCount);
            // 战争倾向本身允许为负数，不能复用只接受非负数的
            // OptionalValue，否则 -50 这类合法配置会在读档时抛出异常。
            value.AggressiveWarDispositionThreshold = BoundedFloatValue(
                node,
                "AggressiveWarDispositionThreshold",
                value.AggressiveWarDispositionThreshold,
                -60f,
                60f);
            value.OwnFiefDefenseTargetRatio = RatioValue(
                node,
                "OwnFiefDefenseTargetRatio",
                value.OwnFiefDefenseTargetRatio);
            value.InteriorGarrisonUpperRatio = RatioValue(
                node,
                "InteriorGarrisonUpperRatio",
                value.InteriorGarrisonUpperRatio);
            value.BorderGarrisonUpperRatio = RatioValue(
                node,
                "BorderGarrisonUpperRatio",
                value.BorderGarrisonUpperRatio);
            value.InteriorGarrisonLowerRatio = RatioValue(
                node,
                "InteriorGarrisonLowerRatio",
                value.InteriorGarrisonLowerRatio);
            value.BorderGarrisonLowerRatio = RatioValue(
                node,
                "BorderGarrisonLowerRatio",
                value.BorderGarrisonLowerRatio);
            if (value.InteriorGarrisonLowerRatio >
                    value.InteriorGarrisonUpperRatio ||
                value.BorderGarrisonLowerRatio >
                    value.BorderGarrisonUpperRatio)
            {
                throw new InvalidOperationException(
                    "Garrison lower ratios cannot exceed upper ratios.");
            }
            value.MinimumOwnSurplusDelivery = NonNegativeValue(
                node,
                "MinimumOwnSurplusDelivery",
                value.MinimumOwnSurplusDelivery);
            value.MinimumFiefRecruitBatch = NonNegativeValue(
                node,
                "MinimumFiefRecruitBatch",
                value.MinimumFiefRecruitBatch);
            value.ReinforcementForecastDays = NonNegativeValue(
                node,
                "ReinforcementForecastDays",
                value.ReinforcementForecastDays);
            value.TransportMissionDeadlineDays = NonNegativeValue(
                node,
                "TransportMissionDeadlineDays",
                value.TransportMissionDeadlineDays);
            value.EnableAutomaticReinforcement = BooleanValue(
                node, "EnableAutomaticReinforcement", false);
            XmlNode demobilization = node.SelectSingleNode("Demobilization");
            if (demobilization != null)
            {
                value.Demobilization.EnableAutomaticDemobilization =
                    BooleanValue(
                        demobilization,
                        "EnableAutomaticDemobilization",
                        false);
                value.Demobilization.EnableDemobilizationRouting =
                    BooleanValue(
                        demobilization,
                        "EnableDemobilizationRouting",
                        false);
                value.Demobilization.PeaceStabilityDays = NonNegativeValue(
                    demobilization,
                    "PeaceStabilityDays",
                    value.Demobilization.PeaceStabilityDays);
                value.Demobilization.MaxDailyDemobilizationRatio = RatioValue(
                    demobilization,
                    "MaxDailyDemobilizationRatio",
                    value.Demobilization.MaxDailyDemobilizationRatio);
                value.Demobilization.MaxDailyDemobilizationCount =
                    NonNegativeValue(
                        demobilization,
                        "MaxDailyDemobilizationCount",
                        value.Demobilization.MaxDailyDemobilizationCount);
            }
        }

        private static float RatioValue(
            XmlNode parent,
            string name,
            float fallback)
        {
            return Math.Max(
                0f,
                Math.Min(1f, OptionalValue(parent, name, fallback)));
        }

        /// <summary>
        /// Reads an optional signed floating-point setting and rejects values
        /// outside the domain defined by that setting.
        /// </summary>
        private static float BoundedFloatValue(
            XmlNode parent,
            string name,
            float fallback,
            float minimum,
            float maximum)
        {
            XmlNode node = parent?.SelectSingleNode(name);
            if (node == null)
            {
                return fallback;
            }

            if (!float.TryParse(
                    Attr(node, "value"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value) ||
                value < minimum ||
                value > maximum)
            {
                throw new InvalidOperationException(
                    "MilitaryConfig requires " + name +
                    " to be between " +
                    minimum.ToString(CultureInfo.InvariantCulture) +
                    " and " +
                    maximum.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return value;
        }

        private static int NonNegativeValue(
            XmlNode parent,
            string name,
            int fallback)
        {
            XmlNode node = parent?.SelectSingleNode(name);
            if (node == null)
            {
                return fallback;
            }

            return Math.Max(
                0,
                int.Parse(Attr(node, "value"), CultureInfo.InvariantCulture));
        }

        private static bool BooleanValue(
            XmlNode parent,
            string name,
            bool fallback)
        {
            XmlNode node = parent?.SelectSingleNode(name);
            return node == null
                ? fallback
                : bool.Parse(Attr(node, "value"));
        }

        private static CulturalMilitaryDoctrine ParseDoctrine(XmlNode node, CultureObject culture)
        {
            XmlNode weights = Required(node, "MilitaryDoctrine");
            int fief = NonNegativeInt(weights, "fief");
            int professional = NonNegativeInt(weights, "professional");
            int mercenary = NonNegativeInt(weights, "mercenary");
            if (fief + professional + mercenary == 0 || fief + professional == 0)
                throw new InvalidOperationException("Invalid zero military doctrine for " + culture.StringId);
            XmlNode training = Required(Required(node, "Professional"), "Training");
            XmlNode fiefNode = Required(node, "Fief");
            XmlNode production = Required(fiefNode, "Production");
            XmlNode townPortTraining = training.SelectSingleNode("TownPort");
            XmlNode townPortProduction = production.SelectSingleNode("TownPort");
            return new CulturalMilitaryDoctrine
            {
                Culture = culture,
                FiefWeight = fief,
                ProfessionalWeight = professional,
                MercenaryWeight = mercenary,
                FeudalSupply = ParseTierCosts(
                    Required(fiefNode, "FeudalSupply"),
                    "FeudalSupply for " + culture.StringId,
                    requireStrictIncrease: true),
                Service = ParseService(Required(fiefNode, "Service")),
                TownTraining = ParseTraining(Required(training, "Town")),
                CastleTraining = ParseTraining(Required(training, "Castle")),
                TownPortTraining = townPortTraining == null ? null : ParseTraining(townPortTraining),
                TownProduction = ParseProduction(Required(production, "Town")),
                CastleProduction = ParseProduction(Required(production, "Castle")),
                TownPortProduction = townPortProduction == null ? null : ParseProduction(townPortProduction)
            };
        }

        private static FiefServiceConfig ParseService(XmlNode node) => new FiefServiceConfig
        {
            FreeWageDays = NonNegativeInt(node, "freeWageDays"),
            ReturnCooldownWeeks = NonNegativeInt(node, "returnCooldownWeeks")
        };

        private static ProfessionalTrainingConfig ParseTraining(XmlNode node) => new ProfessionalTrainingConfig
        {
            Base = NonNegativeFloat(node, "base"),
            BarracksStep = NonNegativeFloat(node, "barracksStep")
        };

        private static FiefProductionConfig ParseProduction(XmlNode node) => new FiefProductionConfig
        {
            DailyRate = NonNegativeFloat(node, "dailyRate"),
            ProsperityCostPerTroop = NonNegativeInt(node, "prosperityCostPerTroop")
        };

        private static HearthEconomyConfig ParseHearthEconomy(XmlNode node) =>
            new HearthEconomyConfig
            {
                ProfessionalRecruitment = ParseTierCosts(Required(node,
                    "ProfessionalRecruitment"), "ProfessionalRecruitment",
                    expectedValue: 1),
                FeudalTaxReliefWageRatio = RatioAttribute(Required(node,
                    "FeudalTaxRelief"), "wageRatio", "FeudalTaxRelief")
            };

        private static float RatioAttribute(XmlNode node, string attribute,
            string setting)
        {
            if (!float.TryParse(Attr(node, attribute), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value) ||
                value < 0f || value > 1f)
                throw new InvalidOperationException(
                    "MilitaryConfig " + setting + " requires " +
                    attribute + " between 0 and 1.");
            return value;
        }

        private static IReadOnlyDictionary<int, int> ParseTierCosts(
            XmlNode node, string group, bool requireStrictIncrease = false,
            int? expectedValue = null)
        {
            var result = new Dictionary<int, int>();
            foreach (XmlNode tier in node.SelectNodes("Tier"))
            {
                if (!int.TryParse(Attr(tier, "Level"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int level) ||
                    level < 1 || level > 6)
                    throw new InvalidOperationException(
                        "HearthEconomy " + group +
                        " contains invalid Level.");
                if (!int.TryParse(Attr(tier, "Hearth"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int hearth) || hearth <= 0)
                    throw new InvalidOperationException(string.Format(
                        "HearthEconomy {0} Level {1} requires a positive Hearth value.",
                        group, level));
                if (result.ContainsKey(level))
                    throw new InvalidOperationException(string.Format(
                        "HearthEconomy {0} contains duplicate Level {1}.",
                        group, level));
                result.Add(level, hearth);
            }
            if (result.Count != 6 || Enumerable.Range(1, 6)
                .Any(level => !result.ContainsKey(level)))
                throw new InvalidOperationException(
                    "HearthEconomy " + group +
                    " must define each Tier from 1 through 6 exactly once.");
            if (requireStrictIncrease && Enumerable.Range(2, 5)
                .Any(level => result[level] <= result[level - 1]))
                throw new InvalidOperationException(
                    "HearthEconomy " + group +
                    " must be strictly increasing from Tier 1 through Tier 6.");
            if (expectedValue.HasValue && result.Values
                .Any(value => value != expectedValue.Value))
                throw new InvalidOperationException(
                    "HearthEconomy " + group + " requires every Tier to equal " +
                    expectedValue.Value.ToString(CultureInfo.InvariantCulture) + ".");
            return result;
        }

        private static XmlNode Required(XmlNode node, string name) =>
            node?.SelectSingleNode(name) ?? throw new InvalidOperationException("MilitaryConfig is missing required node: " + name);
        private static string Attr(XmlNode node, string name) =>
            node?.Attributes?[name]?.Value ?? throw new InvalidOperationException("MilitaryConfig is missing attribute: " + name);
        private static float Value(XmlNode node, string child) => NonNegativeFloat(Required(node, child), "value");
        private static float OptionalValue(XmlNode node, string child, float fallback) =>
            node.SelectSingleNode(child) is XmlNode value ? NonNegativeFloat(value, "value") : fallback;
        private static int NonNegativeInt(XmlNode node, string name)
        {
            if (!int.TryParse(Attr(node, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
                throw new InvalidOperationException("MilitaryConfig requires a non-negative integer: " + name);
            return value;
        }
        private static float NonNegativeFloat(XmlNode node, string name)
        {
            if (!float.TryParse(Attr(node, name), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || value < 0f)
                throw new InvalidOperationException("MilitaryConfig requires a non-negative number: " + name);
            return value;
        }
    }

    public sealed class MilitaryConfigManager
    {
        public static MilitaryConfigManager Instance { get; } = new MilitaryConfigManager();
        private MilitaryConfig _active;
        private MilitaryConfigManager() { }
        public void Clear() => _active = null;
        public void Register(MilitaryConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (_active != null)
                throw new InvalidOperationException("Only one MilitaryConfig may be active.");
            _active = config;
        }
        public MilitaryConfig GetActiveConfig() => _active;
        public MilitaryConfig RequireActiveConfig() => _active ?? throw new InvalidOperationException("MilitaryConfig has not been loaded.");
    }
}
