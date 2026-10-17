using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
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
        public int BaseLimit { get; internal set; }
        public int VillageBonus { get; internal set; }
        public int WeeklyLimit { get; internal set; }
        public int ProsperityCostPerTroop { get; internal set; }
        public int HearthCostPerTroop { get; internal set; }
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
        public FiefServiceConfig Service { get; internal set; }
        public ProfessionalTrainingConfig TownTraining { get; internal set; }
        public ProfessionalTrainingConfig CastleTraining { get; internal set; }
        public ProfessionalTrainingConfig TownPortTraining { get; internal set; }
        public FiefProductionConfig TownProduction { get; internal set; }
        public FiefProductionConfig CastleProduction { get; internal set; }
        public FiefProductionConfig TownPortProduction { get; internal set; }

        public float ProfessionalShare =>
            (float)ProfessionalWeight / (FiefWeight + ProfessionalWeight);

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
    }

    public sealed class MilitaryConfig : MBObjectBase
    {
        private readonly Dictionary<string, CulturalMilitaryDoctrine> _doctrines =
            new Dictionary<string, CulturalMilitaryDoctrine>(StringComparer.OrdinalIgnoreCase);

        public string ConfigId => StringId;
        public AiRecruitmentConfig AiRecruitment { get; } = new AiRecruitmentConfig();

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
            ModLogger.Info($"[MilitaryConfig] Loaded '{ConfigId}' with {_doctrines.Count} culture doctrines.");
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
            BaseLimit = NonNegativeInt(node, "baseLimit"),
            VillageBonus = NonNegativeInt(node, "villageBonus"),
            WeeklyLimit = NonNegativeInt(node, "weeklyLimit"),
            ProsperityCostPerTroop = NonNegativeInt(node, "prosperityCostPerTroop"),
            HearthCostPerTroop = NonNegativeInt(node, "hearthCostPerTroop")
        };

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
