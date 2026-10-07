using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;

namespace ModifiedPolitics.Governor.Config
{
    /// <summary>
    /// An XML-backed policy object loaded by Bannerlord's MBObjectManager.
    /// </summary>
    public sealed class CultureGovernorPolicy : MBObjectBase
    {
        public CultureObject Culture { get; private set; }

        public bool CentralizedGovernorAssignment { get; private set; }

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            Culture = objectManager.ReadObjectReferenceFromXml<CultureObject>("culture", node);

            bool enabled;
            CentralizedGovernorAssignment = bool.TryParse(
                node.Attributes?["centralizedGovernorAssignment"]?.Value,
                out enabled) && enabled;

            GovernorPolicyManager.Instance.Register(this);
        }
    }

    /// <summary>
    /// Provides culture policy lookups after MBObjectManager has deserialized the XML objects.
    /// Missing cultures always fall back to vanilla governor assignment.
    /// </summary>
    public sealed class GovernorPolicyManager
    {
        public static readonly GovernorPolicyManager Instance = new GovernorPolicyManager();

        private readonly Dictionary<string, CultureGovernorPolicy> _policies =
            new Dictionary<string, CultureGovernorPolicy>(StringComparer.OrdinalIgnoreCase);

        private GovernorPolicyManager()
        {
        }

        public void Register(CultureGovernorPolicy policy)
        {
            if (policy?.Culture == null || string.IsNullOrWhiteSpace(policy.Culture.StringId))
                return;

            // Later entries replace earlier entries to make duplicate XML definitions deterministic.
            _policies[policy.Culture.StringId] = policy;
        }

        public bool IsCentralizedAssignment(CultureObject culture)
        {
            if (culture == null || string.IsNullOrWhiteSpace(culture.StringId))
                return false;

            CultureGovernorPolicy policy;
            return _policies.TryGetValue(culture.StringId, out policy)
                   && policy.CentralizedGovernorAssignment;
        }

        public void Clear()
        {
            _policies.Clear();
        }
    }
}
