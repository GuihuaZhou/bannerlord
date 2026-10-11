using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using ModifiedPolitics.HeroOffices.Domain;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace ModifiedPolitics.HeroOffices.Config
{
    /// <summary>
    /// XML-backed culture policy. Effect values remain code-owned by design.
    /// </summary>
    public sealed class OfficeCultureConfig : MBObjectBase
    {
        private readonly HashSet<OfficeType> _enabledOffices = new HashSet<OfficeType>();

        public CultureObject Culture { get; private set; }
        public int LocalBaseLimit { get; private set; }
        public int LocalMaxLimit { get; private set; }
        public int SettlementsPerSlot { get; private set; }
        public bool MarshalAppointmentNormalVote { get; private set; }
        public bool MarshalDismissalNormalVote { get; private set; }
        public MarshalScoreConfig MarshalScore { get; private set; } =
            new MarshalScoreConfig();

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            Culture = objectManager.ReadObjectReferenceFromXml<CultureObject>("culture", node);
            LocalBaseLimit = ReadNonNegativeInt(node, "baseLimit", 0);
            LocalMaxLimit = Math.Max(LocalBaseLimit, ReadNonNegativeInt(node, "maxLimit", 3));
            SettlementsPerSlot = Math.Max(1, ReadNonNegativeInt(node, "settlementsPerSlot", 3));
            MarshalAppointmentNormalVote = ReadVoteMode(node, "marshalAppointmentVote");
            MarshalDismissalNormalVote = ReadVoteMode(node, "marshalDismissalVote");
            MarshalScore = MarshalScoreConfig.FromXml(
                node.SelectSingleNode("MarshalScore"));

            string enabled = node.Attributes?["enabledOffices"]?.Value ?? string.Empty;
            foreach (string value in enabled.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse(value, true, out OfficeType officeType))
                    _enabledOffices.Add(officeType);
            }

            OfficeConfigManager.Instance.Register(this);
        }

        public bool IsEnabled(OfficeType officeType)
        {
            return _enabledOffices.Contains(officeType);
        }

        private static int ReadNonNegativeInt(XmlNode node, string name, int fallback)
        {
            return int.TryParse(node.Attributes?[name]?.Value, out int value)
                ? Math.Max(0, value)
                : fallback;
        }

        private static bool ReadVoteMode(XmlNode node, string name)
        {
            return string.Equals(node.Attributes?[name]?.Value, "NormalVote", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// XML-backed marshal seat count, political bands and competence weights.
    /// </summary>
    public sealed class MarshalScoreConfig
    {
        public int OfficeLimit { get; private set; } = 3;
        public float InfluenceTarget { get; private set; } = 1000f;
        public float PoliticalPriorityBand { get; private set; } = 1000000f;
        public float RelationBand { get; private set; } = 1000f;
        public float ArmyReadyBonus { get; private set; } = 500f;
        public float MaximumLowInfluenceBonus { get; private set; } = 400f;
        public float ClanTierWeight { get; private set; } = 10f;
        public float MaximumOfficeDistributionBonus { get; private set; } = 50f;
        public float MaximumCompetenceTieBreaker { get; private set; } = 9.9f;
        public float CompetenceDivisor { get; private set; } = 500f;
        public float TacticsWeight { get; private set; } = 3f;
        public float LeadershipWeight { get; private set; } = 3f;
        public float ScoutingWeight { get; private set; } = 2f;
        public float StewardWeight { get; private set; } = 2f;
        public float EngineeringWeight { get; private set; } = 2f;
        public float CharmWeight { get; private set; } = 1f;
        public float TradeWeight { get; private set; } = 1f;
        public float CalculatingWeight { get; private set; } = 50f;
        public float ValorWeight { get; private set; } = 40f;
        public float HonorWeight { get; private set; } = 30f;

        public static MarshalScoreConfig FromXml(XmlNode node)
        {
            MarshalScoreConfig config = new MarshalScoreConfig();
            if (node == null)
            {
                return config;
            }

            config.OfficeLimit = ReadInt(node, "officeLimit", config.OfficeLimit, 0);
            config.InfluenceTarget = ReadFloat(node, "influenceTarget", config.InfluenceTarget, 50f);
            config.PoliticalPriorityBand = ReadFloat(node, "politicalPriorityBand", config.PoliticalPriorityBand, 0f);
            config.RelationBand = ReadFloat(node, "relationBand", config.RelationBand, 0f);
            config.ArmyReadyBonus = ReadFloat(node, "armyReadyBonus", config.ArmyReadyBonus, 0f);
            config.MaximumLowInfluenceBonus = ReadFloat(node, "lowInfluenceBonus", config.MaximumLowInfluenceBonus, 0f);
            config.ClanTierWeight = ReadFloat(node, "clanTierWeight", config.ClanTierWeight, 0f);
            config.MaximumOfficeDistributionBonus = ReadFloat(node, "officeDistributionBonus", config.MaximumOfficeDistributionBonus, 0f);
            config.MaximumCompetenceTieBreaker = ReadFloat(node, "competenceTieBreakerMax", config.MaximumCompetenceTieBreaker, 0f);
            config.CompetenceDivisor = ReadFloat(node, "competenceDivisor", config.CompetenceDivisor, 0.01f);
            config.TacticsWeight = ReadFloat(node, "tacticsWeight", config.TacticsWeight, 0f);
            config.LeadershipWeight = ReadFloat(node, "leadershipWeight", config.LeadershipWeight, 0f);
            config.ScoutingWeight = ReadFloat(node, "scoutingWeight", config.ScoutingWeight, 0f);
            config.StewardWeight = ReadFloat(node, "stewardWeight", config.StewardWeight, 0f);
            config.EngineeringWeight = ReadFloat(node, "engineeringWeight", config.EngineeringWeight, 0f);
            config.CharmWeight = ReadFloat(node, "charmWeight", config.CharmWeight, 0f);
            config.TradeWeight = ReadFloat(node, "tradeWeight", config.TradeWeight, 0f);
            config.CalculatingWeight = ReadFloat(node, "calculatingWeight", config.CalculatingWeight, 0f);
            config.ValorWeight = ReadFloat(node, "valorWeight", config.ValorWeight, 0f);
            config.HonorWeight = ReadFloat(node, "honorWeight", config.HonorWeight, 0f);
            return config;
        }

        private static int ReadInt(XmlNode node, string name, int fallback, int minimum)
        {
            return int.TryParse(node.Attributes?[name]?.Value, out int value)
                ? Math.Max(minimum, value)
                : fallback;
        }

        private static float ReadFloat(
            XmlNode node,
            string name,
            float fallback,
            float minimum)
        {
            return float.TryParse(
                    node.Attributes?[name]?.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float value)
                ? Math.Max(minimum, value)
                : fallback;
        }
    }

    /// <summary>
    /// Resolves office policy exclusively from Kingdom.Culture.
    /// </summary>
    public sealed class OfficeConfigManager
    {
        public static readonly OfficeConfigManager Instance = new OfficeConfigManager();

        private readonly Dictionary<string, OfficeCultureConfig> _configs =
            new Dictionary<string, OfficeCultureConfig>(StringComparer.OrdinalIgnoreCase);

        private OfficeConfigManager()
        {
        }

        public void Register(OfficeCultureConfig config)
        {
            if (config?.Culture == null || string.IsNullOrWhiteSpace(config.Culture.StringId))
                return;

            _configs[config.Culture.StringId] = config;
        }

        public bool TryGet(Kingdom kingdom, out OfficeCultureConfig config)
        {
            config = null;
            return kingdom?.Culture != null
                   && !kingdom.IsEliminated
                   && _configs.TryGetValue(kingdom.Culture.StringId, out config);
        }

        public void Clear()
        {
            _configs.Clear();
        }
    }
}
