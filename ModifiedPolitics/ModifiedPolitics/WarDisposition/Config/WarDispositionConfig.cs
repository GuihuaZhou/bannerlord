using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using ModifiedPolitics.Models.WarDisposition.Events;
using TaleWorlds.ObjectSystem;

namespace ModifiedPolitics.WarDisposition.Config
{
    public sealed class WarDispositionConfig : MBObjectBase
    {
        private readonly Dictionary<WarDispositionEventType, float> _eventBaseValues =
            new Dictionary<WarDispositionEventType, float>();

        public float WarWearinessRate { get; private set; }

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            _eventBaseValues.Clear();

            foreach (XmlNode eventNode in Required(node, "Events").SelectNodes("Event"))
            {
                string typeName = RequiredAttribute(eventNode, "type");
                if (!Enum.TryParse(typeName, false, out WarDispositionEventType eventType))
                    throw Error(eventNode, "unknown event type '" + typeName + "'");
                if (_eventBaseValues.ContainsKey(eventType))
                    throw Error(eventNode, "duplicate event type '" + typeName + "'");

                _eventBaseValues.Add(eventType, Number(eventNode, "baseValue"));
            }

            RequireConfiguredEvents();
            WarWearinessRate = NonNegative(
                Required(node, "DailyReturn/WarWeariness"),
                "rate");
            WarDispositionConfigManager.Instance.Register(this);
        }

        public float GetEventBaseValue(WarDispositionEventType eventType)
        {
            if (!_eventBaseValues.TryGetValue(eventType, out float value))
                throw new InvalidOperationException(
                    "War disposition configuration has no base value for event '" +
                    eventType + "'.");
            return value;
        }

        private void RequireConfiguredEvents()
        {
            WarDispositionEventType[] required =
            {
                WarDispositionEventType.PartyVictory,
                WarDispositionEventType.PartyDefeat,
                WarDispositionEventType.PartyDestroyed,
                WarDispositionEventType.ClanMemberCaptured,
                WarDispositionEventType.EnemyCaptured,
                WarDispositionEventType.VillageRaided,
                WarDispositionEventType.CastleLost,
                WarDispositionEventType.TownLost,
                WarDispositionEventType.CastleCaptured,
                WarDispositionEventType.TownCaptured,
                WarDispositionEventType.CastleGranted,
                WarDispositionEventType.TownGranted
            };

            foreach (WarDispositionEventType eventType in required)
                if (!_eventBaseValues.ContainsKey(eventType))
                    throw new InvalidOperationException(
                        "WarDispositionConfig is missing required event '" +
                        eventType + "'.");
        }

        private static XmlNode Required(XmlNode node, string path)
        {
            return node.SelectSingleNode(path)
                   ?? throw Error(node, "missing node '" + path + "'");
        }

        private static string RequiredAttribute(XmlNode node, string attribute)
        {
            string value = node.Attributes?[attribute]?.Value;
            if (string.IsNullOrWhiteSpace(value))
                throw Error(node, "missing attribute '" + attribute + "'");
            return value;
        }

        private static float Number(XmlNode node, string attribute)
        {
            string raw = RequiredAttribute(node, attribute);
            if (!float.TryParse(raw, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value)
                || float.IsNaN(value) || float.IsInfinity(value))
                throw Error(node, attribute + " must be a finite number");
            return value;
        }

        private static float NonNegative(XmlNode node, string attribute)
        {
            float value = Number(node, attribute);
            if (value < 0f)
                throw Error(node, attribute + " cannot be negative");
            return value;
        }

        private static InvalidOperationException Error(XmlNode node, string reason)
        {
            return new InvalidOperationException(
                "WarDispositionConfig/" + node.Name + ": " + reason);
        }
    }

    public sealed class WarDispositionConfigManager
    {
        public static readonly WarDispositionConfigManager Instance =
            new WarDispositionConfigManager();

        public WarDispositionConfig Active { get; private set; }

        private WarDispositionConfigManager() { }

        public void Register(WarDispositionConfig config)
        {
            Active = config ?? throw new ArgumentNullException(nameof(config));
        }

        public void Clear() { Active = null; }

        public WarDispositionConfig RequireActive()
        {
            return Active ?? throw new InvalidOperationException(
                "War disposition configuration is not loaded.");
        }
    }
}
