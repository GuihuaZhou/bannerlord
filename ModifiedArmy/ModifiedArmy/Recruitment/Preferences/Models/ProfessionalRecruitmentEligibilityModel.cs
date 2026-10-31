using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Preferences.Models
{
    public sealed class ProfessionalRecruitmentEligibilitySnapshot
    {
        public ProfessionalTroopTemplate Template { get; internal set; }
        public CultureObject Culture { get; internal set; }
        public int BarracksLevel { get; internal set; }
        public int AllowedRecruitmentLevel { get; internal set; }
        public float ProductionRate { get; internal set; }
        public IReadOnlyList<TroopProductionEntry> EligibleEntries { get; internal set; }
            = new List<TroopProductionEntry>();
    }

    public static class ProfessionalRecruitmentEligibilityModel
    {
        public static ProfessionalRecruitmentEligibilitySnapshot Evaluate(
            Settlement settlement)
        {
            if (settlement?.IsFortification != true) return null;
            ProfessionalTroopTemplate template =
                TroopProductionTemplateRepository.Instance.Resolve(
                    settlement, TroopProductionKind.Professional) as
                    ProfessionalTroopTemplate;
            Clan owner = settlement.OwnerClan;
            CultureObject local = settlement.Culture;
            CultureObject clan = owner?.Culture;
            if (template == null || owner == null || owner.IsEliminated ||
                local == null || clan == null) return null;
            CultureObject kingdom = owner.Kingdom?.Culture ?? clan;
            if (owner.Kingdom != null && owner.Kingdom.Culture == null) return null;
            bool localClan = Match(local, clan);
            bool localKingdom = Match(local, kingdom);
            bool clanKingdom = Match(clan, kingdom);
            int maximum;
            float rate;
            if (localClan) { maximum = clanKingdom ? 3 : 2; rate = 1f; }
            else if (localKingdom || clanKingdom) { maximum = 2; rate = 0.5f; }
            else { maximum = 1; rate = 0.25f; }
            int barracks = GetBarracksLevel(settlement);
            int allowed = Math.Min(barracks, maximum);
            var eligible = template.TroopEntries
                .Where(x => x?.Troop != null &&
                    x.RequiredBarracksLevel <= allowed)
                .GroupBy(x => x.Troop.StringId, StringComparer.Ordinal)
                .Select(x => x.First())
                .OrderBy(x => x.Troop.StringId)
                .ToList();
            return new ProfessionalRecruitmentEligibilitySnapshot
            {
                Template = template,
                Culture = local,
                BarracksLevel = barracks,
                AllowedRecruitmentLevel = allowed,
                ProductionRate = rate,
                EligibleEntries = eligible
            };
        }

        public static int GetBarracksLevel(Settlement settlement)
        {
            if (settlement?.Town == null) return 0;
            BuildingType type = settlement.IsCastle
                ? DefaultBuildingTypes.CastleBarracks
                : DefaultBuildingTypes.SettlementBarracks;
            Building building = settlement.Town.Buildings.FirstOrDefault(
                x => x.BuildingType == type);
            return Math.Max(0, Math.Min(3, building?.CurrentLevel ?? 0));
        }

        private static bool Match(CultureObject a, CultureObject b) =>
            a != null && b != null && string.Equals(
                a.StringId, b.StringId, StringComparison.OrdinalIgnoreCase);
    }
}
