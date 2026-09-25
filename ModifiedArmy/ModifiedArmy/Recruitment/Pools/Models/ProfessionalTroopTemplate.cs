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
        IRecruitmentPoolTemplate
    {
        private readonly List<RecruitmentPoolTroopEntry> _poolTroops =
            new List<RecruitmentPoolTroopEntry>();

        public string TemplateId => StringId;

        public CultureObject Culture { get; private set; }

        public RecruitmentPoolKind PoolKind =>
            RecruitmentPoolKind.Professional;

        public int BaseCapacity { get; private set; }

        public float BaseDailyProduction { get; private set; }

        public float DailyProductionStep { get; private set; }

        public IReadOnlyList<RecruitmentPoolTroopEntry> PoolTroops =>
            _poolTroops;

        public override void Deserialize(
            MBObjectManager objectManager,
            XmlNode node)
        {
            base.Deserialize(objectManager, node);
            Culture = objectManager.ReadObjectReferenceFromXml<CultureObject>(
                "culture",
                node);
            BaseCapacity = RecruitmentPoolTemplateUtility.ReadOptionalInt(
                node,
                "baseCapacity",
                0);
            BaseDailyProduction =
                RecruitmentPoolTemplateUtility.ReadOptionalFloat(
                    node,
                    "baseDailyProduction",
                    0f);
            DailyProductionStep =
                RecruitmentPoolTemplateUtility.ReadOptionalFloat(
                    node,
                    "dailyProductionStep",
                    0f);

            XmlNode composition = node.SelectSingleNode("TroopComposition");
            if (composition != null)
            {
                foreach (XmlNode groupNode in composition.ChildNodes)
                {
                    ParseTroops(objectManager, groupNode);
                }
            }

            RecruitmentPoolTemplateRepository.Instance.Register(this);
        }

        public int GetCapacity()
        {
            return Math.Max(0, BaseCapacity);
        }

        public float GetDailyProduction(int barracksLevel)
        {
            return RecruitmentPoolTemplateUtility.CalculateDailyProduction(
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

                int weight = RecruitmentPoolTemplateUtility.ReadOptionalInt(
                    troopNode,
                    "weight",
                    1);
                _poolTroops.Add(new RecruitmentPoolTroopEntry(
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
