using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Computes structural production sources from the two templates for
        /// this settlement. Barracks, weight, supply and capacity are runtime
        /// constraints and deliberately do not change these booleans.
        /// </summary>
        public TroopProductionEligibility GetEligibility(
            Settlement settlement,
            CharacterObject troop)
        {
            if (settlement == null || troop == null)
                return new TroopProductionEligibility(false, false);

            ITroopProductionTemplate fief = Resolve(
                settlement,
                TroopProductionKind.Fief);
            ITroopProductionTemplate professional = Resolve(
                settlement,
                TroopProductionKind.Professional);
            return new TroopProductionEligibility(
                IsTroopInTemplate(fief, troop),
                IsTroopInTemplate(professional, troop));
        }

        private static bool IsTroopInTemplate(
            ITroopProductionTemplate template,
            CharacterObject troop)
        {
            if (template?.TroopEntries == null || troop == null)
                return false;
            foreach (TroopProductionEntry entry in template.TroopEntries)
            {
                CharacterObject root = entry?.Troop;
                if (root == null) continue;
                var visited = new HashSet<CharacterObject>();
                var pending = new Stack<CharacterObject>();
                pending.Push(root);
                while (pending.Count > 0)
                {
                    CharacterObject current = pending.Pop();
                    if (current == null || !visited.Add(current)) continue;
                    if (current == troop) return true;
                    CharacterObject[] upgrades = current.UpgradeTargets;
                    if (upgrades == null) continue;
                    foreach (CharacterObject upgrade in upgrades
                        .Where(item => item != null)
                        .OrderByDescending(
                            item => item.StringId,
                            StringComparer.Ordinal))
                    {
                        pending.Push(upgrade);
                    }
                }
            }
            return false;
        }

        public void Clear()
        {
            _professionalTemplates.Clear();
            _fiefTemplates.Clear();
        }
    }
}
