using ModifiedArmy.Models;
using ModifiedArmy.Tool;
using System;
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

        public float BaseDailyProduction { get; private set; }

        public float DailyProductionStep { get; private set; }

        public float ProfessionalRatio { get; private set; } = 0.5f;

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
            BaseDailyProduction =
                TroopProductionTemplateUtility.ReadOptionalFloat(
                    node,
                    "baseDailyProduction",
                    0f);
            DailyProductionStep =
                TroopProductionTemplateUtility.ReadOptionalFloat(
                    node,
                    "dailyProductionStep",
                    0f);
            ProfessionalRatio = Math.Max(
                0f,
                Math.Min(
                    1f,
                    TroopProductionTemplateUtility.ReadOptionalFloat(
                        node,
                        "professionalRatio",
                        0.5f)));

            XmlNode composition = node.SelectSingleNode("TroopComposition");
            if (composition != null)
            {
                foreach (XmlNode groupNode in composition.ChildNodes)
                {
                    ParseTroops(objectManager, groupNode);
                }
            }

            TroopProductionTemplateRepository.Instance.Register(this);
        }

        public float GetDailyProduction(int barracksLevel)
        {
            return TroopProductionTemplateUtility.CalculateDailyProduction(
                BaseDailyProduction,
                DailyProductionStep,
                barracksLevel);
        }

        private void ParseTroops(
            MBObjectManager objectManager,
            XmlNode groupNode)
        {
            foreach (XmlNode troopNode in groupNode.SelectNodes("troop"))
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
                    continue;
                }

                int weight = TroopProductionTemplateUtility.ReadOptionalInt(
                    troopNode,
                    "weight",
                    1);
                _troopEntries.Add(new TroopProductionEntry(
                    troop,
                    weight,
                    basicTroop.RequiredBarracksLevel));
            }
        }

        private BasicTroopEntry FindBasicTroop(CharacterObject troop)
        {
            BasicTroopGroup group =
                BasicTroopGroupManager.GetGroupForCulture(Culture);
            if (group == null || troop == null)
            {
                return null;
            }

            foreach (List<BasicTroopEntry> entries in
                group.TroopsByType.Values)
            {
                foreach (BasicTroopEntry entry in entries)
                {
                    if (entry?.Troop == troop)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }
    }
}
