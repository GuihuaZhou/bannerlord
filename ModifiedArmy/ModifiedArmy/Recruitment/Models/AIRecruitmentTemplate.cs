using ModifiedArmy.Recruitment.Classification;
using System;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// XML-backed definition for one culture and one supported party type.
    /// Child nodes keep combat-role and quality dimensions readable and make
    /// it possible to tune templates without recompiling the assembly.
    /// </summary>
    public sealed class AIRecruitmentTemplate : MBObjectBase
    {
        public CultureObject Culture { get; private set; }

        public RecruitmentPartyType PartyType { get; private set; }

        public override void Deserialize(MBObjectManager objectManager, XmlNode node)
        {
            base.Deserialize(objectManager, node);

            Culture = objectManager
                .ReadObjectReferenceFromXml<CultureObject>("culture", node);
            PartyType = ParsePartyType(node.Attributes?["partyType"]?.Value);

            XmlNode combatRolesNode = node.SelectSingleNode("CombatRoles");
            XmlNode qualitiesNode = node.SelectSingleNode("Qualities");

            Dictionary<CombatRole, RatioRange> combatRoles =
                new Dictionary<CombatRole, RatioRange>
                {
                    [CombatRole.Infantry] = ReadRange(combatRolesNode, "Infantry"),
                    [CombatRole.Ranged] = ReadRange(combatRolesNode, "Ranged"),
                    [CombatRole.Cavalry] = ReadRange(combatRolesNode, "Cavalry"),
                    [CombatRole.HorseArcher] = ReadRange(combatRolesNode, "HorseArcher")
                };
            Dictionary<TroopQuality, RatioRange> qualities =
                new Dictionary<TroopQuality, RatioRange>
                {
                    [TroopQuality.LowTier] = ReadRange(qualitiesNode, "LowTier"),
                    [TroopQuality.MiddleTier] = ReadRange(qualitiesNode, "MiddleTier"),
                    [TroopQuality.TopTier] = ReadRange(qualitiesNode, "TopTier")
                };

            if (Culture == null)
            {
                throw new InvalidOperationException(
                    $"AI recruitment template '{StringId}' has no valid culture reference.");
            }

            RecruitmentModelManager.Templates.Register(
                Culture.StringId,
                PartyType,
                new ArmyCompositionTemplate(combatRoles, qualities));
        }

        /// <summary>
        /// Reads one required child node whose minimum and maximum values are
        /// percentages. Missing nodes fail early instead of silently creating
        /// a partial template with unpredictable recruitment behavior.
        /// </summary>
        private static RatioRange ReadRange(XmlNode parent, string childName)
        {
            XmlNode child = parent?.SelectSingleNode(childName);

            if (child?.Attributes?["min"] == null ||
                child.Attributes["max"] == null)
            {
                throw new InvalidOperationException(
                    $"AI recruitment template is missing {childName} min/max values.");
            }

            int minimumPercent = int.Parse(child.Attributes["min"].Value);
            int maximumPercent = int.Parse(child.Attributes["max"].Value);
            return new RatioRange(
                minimumPercent / 100f,
                maximumPercent / 100f);
        }

        private static RecruitmentPartyType ParsePartyType(string value)
        {
            if (string.Equals(
                value,
                "garrison",
                StringComparison.OrdinalIgnoreCase))
            {
                return RecruitmentPartyType.Garrison;
            }

            if (string.Equals(
                value,
                "mobileParty",
                StringComparison.OrdinalIgnoreCase))
            {
                return RecruitmentPartyType.MobileParty;
            }

            throw new InvalidOperationException(
                $"Unknown AI recruitment party type '{value}'.");
        }
    }
}
