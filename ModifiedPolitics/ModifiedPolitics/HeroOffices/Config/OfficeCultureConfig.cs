using System;
using System.Collections.Generic;
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

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            Culture = objectManager.ReadObjectReferenceFromXml<CultureObject>("culture", node);
            LocalBaseLimit = ReadNonNegativeInt(node, "baseLimit", 0);
            LocalMaxLimit = Math.Max(LocalBaseLimit, ReadNonNegativeInt(node, "maxLimit", 3));
            SettlementsPerSlot = Math.Max(1, ReadNonNegativeInt(node, "settlementsPerSlot", 3));
            MarshalAppointmentNormalVote = ReadVoteMode(node, "marshalAppointmentVote");
            MarshalDismissalNormalVote = ReadVoteMode(node, "marshalDismissalVote");

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
