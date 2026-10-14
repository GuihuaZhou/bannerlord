using System;
using System.Globalization;
using System.Xml;
using TaleWorlds.ObjectSystem;

namespace ModifiedPolitics.SettlementEconomy.Config
{
    /// <summary>
    /// XML-backed civilian settlement economy settings.
    /// </summary>
    public sealed class SettlementEconomyConfig : MBObjectBase
    {
        public int ProsperityConsumptionDivisor { get; private set; } = 34;

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);
            XmlNode valueNode = node.SelectSingleNode(
                "Food/ProsperityConsumptionDivisor");
            string raw = valueNode?.Attributes?["value"]?.Value;
            if (!int.TryParse(
                    raw,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int divisor) || divisor <= 0)
            {
                divisor = 34;
            }

            ProsperityConsumptionDivisor = divisor;
            SettlementEconomyConfigManager.Instance.Register(this);
        }
    }

    /// <summary>
    /// Provides a safe default when XML is absent or malformed.
    /// </summary>
    public sealed class SettlementEconomyConfigManager
    {
        public static readonly SettlementEconomyConfigManager Instance =
            new SettlementEconomyConfigManager();

        private int _prosperityConsumptionDivisor = 34;

        private SettlementEconomyConfigManager()
        {
        }

        public int ActiveProsperityConsumptionDivisor =>
            _prosperityConsumptionDivisor > 0
                ? _prosperityConsumptionDivisor
                : 34;

        public void Register(SettlementEconomyConfig config)
        {
            _prosperityConsumptionDivisor =
                config?.ProsperityConsumptionDivisor > 0
                    ? config.ProsperityConsumptionDivisor
                    : 34;
        }

        public void Clear()
        {
            _prosperityConsumptionDivisor = 34;
        }
    }
}
