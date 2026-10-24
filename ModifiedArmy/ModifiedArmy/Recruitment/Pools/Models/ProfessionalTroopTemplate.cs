using ModifiedArmy.Models;
using ModifiedArmy.Tool;
using System.Collections.Generic;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Deserializes one culture and settlement-specific professional manpower
    /// template. Troop classification and barracks requirements are resolved
    /// from basic_troop_config.xml.
    /// </summary>
    public sealed class ProfessionalTroopTemplate : MBObjectBase,
        ITroopProductionTemplate
    {
        private readonly List<TroopProductionEntry> _troopEntries =
            new List<TroopProductionEntry>();

        public string TemplateId => StringId;

        public CultureObject Culture { get; private set; }

        public TroopProductionKind ProductionKind =>
            TroopProductionKind.Professional;

        public IReadOnlyList<TroopProductionEntry> TroopEntries =>
            _troopEntries;

        public override void Deserialize(
            MBObjectManager objectManager,
            XmlNode node)
        {
            base.Deserialize(objectManager, node);
            Culture = objectManager.ReadObjectReferenceFromXml<CultureObject>(
                "culture",
                node);
            XmlNode composition = node.SelectSingleNode("TroopComposition");
            if (composition != null)
            {
                // 正式配置只接受平铺 troop，不再读取旧五类包装。
                foreach (XmlNode troopNode in composition.SelectNodes("troop"))
                    ParseTroop(objectManager, troopNode);
            }

            TroopProductionTemplateRepository.Instance.Register(this);
        }

        private void ParseTroop(
            MBObjectManager objectManager,
            XmlNode troopNode)
        {
                CharacterObject troop =
                    objectManager.ReadObjectReferenceFromXml<CharacterObject>(
                        "id",
                        troopNode);
                BasicTroopEntry basicTroop = FindBasicTroop(troop);

                if (troop == null || basicTroop == null)
                {
                    string troopId = troopNode.Attributes?["id"]?.Value ??
                        "unknown";
                    TextObject message = new TextObject(
                        "{=ModifiedArmy_ProfessionalTemplateUnknownTroop}" +
                        "[Recruitment pool] Professional template " +
                        "'{TEMPLATE_ID}' cannot resolve troop '{TROOP_ID}'.");
                    message.SetTextVariable("TEMPLATE_ID", TemplateId);
                    message.SetTextVariable("TROOP_ID", troopId);
                    ModLogger.Warn(message.ToString());
                    return;
                }

                int weight = TroopProductionTemplateUtility.ReadOptionalInt(
                    troopNode,
                    "weight",
                    1);
                // Zero is a supported opt-out value. Negative values are also
                // normalized to zero by TroopProductionEntry.
                _troopEntries.Add(new TroopProductionEntry(
                    troop,
                    weight,
                    basicTroop.RequiredBarracksLevel));
        }

        private BasicTroopEntry FindBasicTroop(CharacterObject troop)
        {
            BasicTroopGroup group =
                BasicTroopGroupManager.GetGroupForCulture(Culture);
            if (group == null || troop == null)
            {
                return null;
            }

            foreach (BasicTroopEntry entry in group.Troops)
            {
                if (entry?.Troop == troop)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
