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
    public sealed class RecruitmentPoolTemplateRepository
    {
        private readonly Dictionary<string, IRecruitmentPoolTemplate>
            _professionalTemplates =
                new Dictionary<string, IRecruitmentPoolTemplate>(
                    StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IRecruitmentPoolTemplate>
            _fiefTemplates =
                new Dictionary<string, IRecruitmentPoolTemplate>(
                    StringComparer.OrdinalIgnoreCase);
        public static RecruitmentPoolTemplateRepository Instance { get; } =
            new RecruitmentPoolTemplateRepository();

        private RecruitmentPoolTemplateRepository()
        {
        }

        public void Register(IRecruitmentPoolTemplate template)
        {
            if (template == null || string.IsNullOrWhiteSpace(
                    template.TemplateId))
            {
                return;
            }

            Dictionary<string, IRecruitmentPoolTemplate> templates =
                template.PoolKind == RecruitmentPoolKind.Professional
                    ? _professionalTemplates
                    : _fiefTemplates;
            templates[template.TemplateId] = template;
        }

        public IRecruitmentPoolTemplate Resolve(
            Settlement settlement,
            RecruitmentPoolKind kind)
        {
            return Resolve(settlement, kind, settlement?.Culture);
        }

        public IRecruitmentPoolTemplate Resolve(
            Settlement settlement,
            RecruitmentPoolKind kind,
            CultureObject culture)
        {
            if (settlement == null || culture == null)
            {
                return null;
            }

            Dictionary<string, IRecruitmentPoolTemplate> templates =
                kind == RecruitmentPoolKind.Professional
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
