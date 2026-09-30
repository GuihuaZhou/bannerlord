using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Pools.Models
{
    /// <summary>
    /// Stores loaded pool templates and the derived source flags for every
    /// troop upgrade tree. Templates remain XML data and are never saved.
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
        private readonly Dictionary<CharacterObject, RecruitmentPoolKind>
            _troopSources =
                new Dictionary<CharacterObject, RecruitmentPoolKind>();

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

        /// <summary>
        /// Builds source flags only after the campaign session has launched.
        /// Character upgrade targets are not guaranteed to be safe to walk
        /// while custom MBObject XML is still being deserialized.
        /// </summary>
        public void InitializeTroopSources()
        {
            _troopSources.Clear();
            RegisterTemplateTroopTrees(_professionalTemplates.Values);
            RegisterTemplateTroopTrees(_fiefTemplates.Values);
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

        public RecruitmentPoolKind GetSources(CharacterObject troop)
        {
            return troop != null && _troopSources.TryGetValue(
                troop,
                out RecruitmentPoolKind sources)
                ? sources
                : RecruitmentPoolKind.None;
        }

        public void Clear()
        {
            _professionalTemplates.Clear();
            _fiefTemplates.Clear();
            _troopSources.Clear();
        }

        private void RegisterTemplateTroopTrees(
            IEnumerable<IRecruitmentPoolTemplate> templates)
        {
            foreach (IRecruitmentPoolTemplate template in templates)
            {
                foreach (RecruitmentPoolTroopEntry entry in
                    template.PoolTroops)
                {
                    RegisterTroopTree(entry.Troop, template.PoolKind);
                }
            }
        }

        private void RegisterTroopTree(
            CharacterObject troop,
            RecruitmentPoolKind source)
        {
            RegisterTroopTree(
                troop,
                source,
                new HashSet<CharacterObject>());
        }

        private void RegisterTroopTree(
            CharacterObject troop,
            RecruitmentPoolKind source,
            ISet<CharacterObject> visited)
        {
            if (troop == null || !visited.Add(troop))
            {
                return;
            }

            _troopSources.TryGetValue(troop, out var existing);
            _troopSources[troop] = existing | source;

            if (troop.UpgradeTargets == null)
            {
                return;
            }

            foreach (CharacterObject target in troop.UpgradeTargets)
            {
                RegisterTroopTree(target, source, visited);
            }
        }
    }
}
