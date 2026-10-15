using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Stores loaded troop-production templates. Templates remain XML data
    /// and are never saved.
    /// </summary>
    public sealed class TroopProductionTemplateRepository
    {
        private readonly Dictionary<string, ITroopProductionTemplate>
            _professionalTemplates =
                new Dictionary<string, ITroopProductionTemplate>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ITroopProductionTemplate>
            _fiefTemplates =
                new Dictionary<string, ITroopProductionTemplate>(
                    StringComparer.OrdinalIgnoreCase);
        public static TroopProductionTemplateRepository Instance { get; } =
            new TroopProductionTemplateRepository();

        private TroopProductionTemplateRepository()
        {
        }

        public void Register(ITroopProductionTemplate template)
        {
            if (template == null || string.IsNullOrWhiteSpace(
                    template.TemplateId))
            {
                return;
            }

            Dictionary<string, ITroopProductionTemplate> templates =
                template.ProductionKind == TroopProductionKind.Professional
                    ? _professionalTemplates
                    : _fiefTemplates;
            templates[template.TemplateId] = template;
        }

        public ITroopProductionTemplate Resolve(
            Settlement settlement,
            TroopProductionKind kind)
        {
            return Resolve(settlement, kind, settlement?.Culture);
        }

        public ITroopProductionTemplate Resolve(
            Settlement settlement,
            TroopProductionKind kind,
            CultureObject culture)
        {
            if (settlement == null || culture == null)
            {
                return null;
            }

            Dictionary<string, ITroopProductionTemplate> templates =
                kind == TroopProductionKind.Professional
                    ? _professionalTemplates
                    : _fiefTemplates;
            string settlementType = settlement.IsCastle
                ? "castle"
                : settlement.HasPort ? "town_port" : "town";
            string templateId = culture.StringId + "_" +
                settlementType;

            if (templates.TryGetValue(templateId, out var template))
            {
                return template;
            }

            if (settlement.HasPort)
            {
                templates.TryGetValue(
                    culture.StringId + "_town",
                    out template);
            }

            return template;
        }

        public void Clear()
        {
            _professionalTemplates.Clear();
            _fiefTemplates.Clear();
        }
    }
}
