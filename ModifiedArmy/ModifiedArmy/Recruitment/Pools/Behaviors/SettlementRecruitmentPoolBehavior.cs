using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Pools.Behaviors
{
    /// <summary>
    /// Owns the saved professional and fief manpower pools for every town and
    /// castle. Daily production fills the largest weighted shortage first,
    /// while AI recruitment and Fief Party reinforcement consume saved stock
    /// through explicit transactions.
    /// </summary>
    public sealed class SettlementRecruitmentPoolBehavior :
        CampaignBehaviorBase
    {
        private const float HighProfessionalProductionRate = 1f;
        private const float MediumProfessionalProductionRate = 0.5f;
        private const float LowProfessionalProductionRate = 0.25f;

        private sealed class ProfessionalProductionRule
        {
            public ProfessionalProductionRule(
                CultureObject culture,
                float productionRate,
                int maximumBarracksLevel)
            {
                Culture = culture;
                ProductionRate = productionRate;
                MaximumBarracksLevel = maximumBarracksLevel;
            }

            public CultureObject Culture { get; }

            public float ProductionRate { get; }

            public int MaximumBarracksLevel { get; }
        }

        private Dictionary<Settlement, SettlementRecruitmentPoolData>
            _settlementPools =
                new Dictionary<Settlement, SettlementRecruitmentPoolData>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(
                this,
                OnDailySettlementTick);
        }

        /// <summary>
        /// Defers upgrade-tree traversal until all character objects and
        /// their upgrade targets have finished loading.
        /// </summary>
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            RecruitmentPoolTemplateRepository.Instance
                .InitializeTroopSources();
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "_settlementRecruitmentPools",
                ref _settlementPools);
            _settlementPools ??=
                new Dictionary<Settlement, SettlementRecruitmentPoolData>();

            foreach (SettlementRecruitmentPoolData data in
                _settlementPools.Values)
            {
                data?.EnsureInitialized();
            }
        }

        public SettlementRecruitmentPoolData GetOrCreatePool(
            Settlement settlement)
        {
            if (settlement?.IsFortification != true)
            {
                return null;
            }

            if (!_settlementPools.TryGetValue(settlement, out var data) ||
                data == null)
            {
                data = new SettlementRecruitmentPoolData();
                _settlementPools[settlement] = data;
            }

            data.EnsureInitialized();
            return data;
        }

        public int GetCapacity(
            Settlement settlement,
            RecruitmentPoolKind kind)
        {
            ProfessionalProductionRule professionalRule = kind ==
                RecruitmentPoolKind.Professional
                    ? ResolveProfessionalProductionRule(settlement)
                    : null;
            if (kind == RecruitmentPoolKind.Professional &&
                professionalRule == null)
            {
                return 0;
            }

            IRecruitmentPoolTemplate template =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    kind,
                    professionalRule?.Culture ?? settlement?.Culture);
            return template?.GetCapacity() ?? 0;
        }

        public float GetDailyProduction(
            Settlement settlement,
            RecruitmentPoolKind kind)
        {
            int barracksLevel = GetBarracksLevel(settlement);
            ProfessionalProductionRule professionalRule = kind ==
                RecruitmentPoolKind.Professional
                    ? ResolveProfessionalProductionRule(settlement)
                    : null;
            if (kind == RecruitmentPoolKind.Professional &&
                professionalRule == null)
            {
                return 0f;
            }

            IRecruitmentPoolTemplate template =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    kind,
                    professionalRule?.Culture ?? settlement?.Culture);
            if (professionalRule != null)
            {
                barracksLevel = Math.Min(
                    barracksLevel,
                    professionalRule.MaximumBarracksLevel);
            }

            return template?.GetDailyProduction(
                barracksLevel) *
                (professionalRule?.ProductionRate ?? 1f) ?? 0f;
        }

        /// <summary>
        /// Returns a detached snapshot so recruitment callers cannot mutate
        /// saved pool state without using the transaction method below.
        /// </summary>
        public IReadOnlyDictionary<CharacterObject, int> GetAvailableTroops(
            Settlement settlement,
            RecruitmentPoolKind kind)
        {
            SettlementRecruitmentPoolData data = GetOrCreatePool(settlement);
            return data == null
                ? new Dictionary<CharacterObject, int>()
                : new Dictionary<CharacterObject, int>(
                    data.GetTroops(kind));
        }

        /// <summary>
        /// Atomically removes an exact troop quantity from a settlement pool.
        /// No partial withdrawal occurs when the requested stock is missing.
        /// </summary>
        public bool TryConsume(
            Settlement settlement,
            RecruitmentPoolKind kind,
            CharacterObject troop,
            int count)
        {
            if (troop == null || count <= 0)
            {
                return false;
            }

            SettlementRecruitmentPoolData data = GetOrCreatePool(settlement);
            if (data == null)
            {
                return false;
            }

            Dictionary<CharacterObject, int> troops = data.GetTroops(kind);
            if (!troops.TryGetValue(troop, out int available) ||
                available < count)
            {
                return false;
            }

            int remaining = available - count;
            if (remaining > 0)
            {
                troops[troop] = remaining;
            }
            else
            {
                troops.Remove(troop);
            }

            return true;
        }

        private void OnDailySettlementTick(Settlement settlement)
        {
            if (settlement?.IsFortification != true ||
                settlement.Town == null)
            {
                return;
            }

            SettlementRecruitmentPoolData data = GetOrCreatePool(settlement);
            int barracksLevel = GetBarracksLevel(settlement);
            ReplenishPool(
                settlement,
                data,
                RecruitmentPoolKind.Professional,
                barracksLevel);
            ReplenishPool(
                settlement,
                data,
                RecruitmentPoolKind.Fief,
                barracksLevel);
        }

        private static void ReplenishPool(
            Settlement settlement,
            SettlementRecruitmentPoolData data,
            RecruitmentPoolKind kind,
            int barracksLevel)
        {
            ProfessionalProductionRule professionalRule = kind ==
                RecruitmentPoolKind.Professional
                    ? ResolveProfessionalProductionRule(settlement)
                    : null;
            if (kind == RecruitmentPoolKind.Professional &&
                professionalRule == null)
            {
                return;
            }

            IRecruitmentPoolTemplate template =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    kind,
                    professionalRule?.Culture ?? settlement?.Culture);
            if (template == null)
            {
                return;
            }

            int effectiveBarracksLevel = professionalRule == null
                ? barracksLevel
                : Math.Min(
                    barracksLevel,
                    professionalRule.MaximumBarracksLevel);

            Dictionary<CharacterObject, int> troops = data.GetTroops(kind);
            List<RecruitmentPoolTroopEntry> eligible = template.PoolTroops
                .Where(entry =>
                    entry?.Troop != null &&
                    entry.RequiredBarracksLevel <= effectiveBarracksLevel)
                .ToList();
            int capacity = template.GetCapacity();
            int currentCount = troops.Values.Sum(value => Math.Max(0, value));

            if (eligible.Count == 0 || currentCount >= capacity)
            {
                return;
            }

            float progress = data.AddProductionProgress(
                kind,
                template.GetDailyProduction(effectiveBarracksLevel) *
                (professionalRule?.ProductionRate ?? 1f));
            int produced = Math.Min(
                (int)Math.Floor(progress),
                capacity - currentCount);

            for (int index = 0; index < produced; index++)
            {
                RecruitmentPoolTroopEntry selected = SelectLargestShortage(
                    eligible,
                    troops,
                    capacity);
                if (selected == null)
                {
                    break;
                }

                troops.TryGetValue(selected.Troop, out int count);
                troops[selected.Troop] = Math.Max(0, count) + 1;
            }

            data.ConsumeProductionProgress(kind, produced);
        }

        private static ProfessionalProductionRule
            ResolveProfessionalProductionRule(Settlement settlement)
        {
            CultureObject settlementCulture = settlement?.Culture;
            Clan ownerClan = settlement?.OwnerClan;
            CultureObject clanCulture = ownerClan?.Culture;
            if (settlementCulture == null || ownerClan == null ||
                ownerClan.IsEliminated || clanCulture == null)
            {
                return null;
            }

            Kingdom kingdom = ownerClan.Kingdom;
            if (kingdom == null || kingdom.IsEliminated ||
                kingdom.Culture == null)
            {
                return null;
            }

            CultureObject kingdomCulture = kingdom.Culture;
            bool settlementMatchesClan = CulturesMatch(
                settlementCulture,
                clanCulture);
            bool settlementMatchesKingdom = CulturesMatch(
                settlementCulture,
                kingdomCulture);
            bool clanMatchesKingdom = CulturesMatch(
                clanCulture,
                kingdomCulture);

            if (settlementMatchesClan)
            {
                return new ProfessionalProductionRule(
                    settlementCulture,
                    HighProfessionalProductionRate,
                    clanMatchesKingdom ? 3 : 2);
            }

            if (settlementMatchesKingdom)
            {
                return new ProfessionalProductionRule(
                    settlementCulture,
                    MediumProfessionalProductionRate,
                    2);
            }

            if (clanMatchesKingdom)
            {
                return new ProfessionalProductionRule(
                    clanCulture,
                    MediumProfessionalProductionRate,
                    2);
            }

            return new ProfessionalProductionRule(
                kingdomCulture,
                LowProfessionalProductionRate,
                1);
        }

        private static bool CulturesMatch(
            CultureObject first,
            CultureObject second)
        {
            return first != null && second != null &&
                string.Equals(
                    first.StringId,
                    second.StringId,
                    StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Chooses the troop furthest below its weighted capacity target.
        /// This produces stable long-term ratios without random streaks.
        /// </summary>
        private static RecruitmentPoolTroopEntry SelectLargestShortage(
            IReadOnlyList<RecruitmentPoolTroopEntry> entries,
            IReadOnlyDictionary<CharacterObject, int> troops,
            int capacity)
        {
            int totalWeight = entries.Sum(entry => entry.Weight);
            RecruitmentPoolTroopEntry selected = null;
            float largestShortage = float.MinValue;

            foreach (RecruitmentPoolTroopEntry entry in entries)
            {
                troops.TryGetValue(entry.Troop, out int currentCount);
                float target = capacity * entry.Weight /
                    (float)Math.Max(1, totalWeight);
                float shortage = target - Math.Max(0, currentCount);

                if (shortage > largestShortage)
                {
                    largestShortage = shortage;
                    selected = entry;
                }
            }

            return selected;
        }

        public static int GetBarracksLevel(Settlement settlement)
        {
            if (settlement?.Town == null)
            {
                return 0;
            }

            BuildingType buildingType = settlement.IsCastle
                ? DefaultBuildingTypes.CastleBarracks
                : DefaultBuildingTypes.SettlementBarracks;
            Building barracks = settlement.Town.Buildings.FirstOrDefault(
                building => building.BuildingType == buildingType);
            return Math.Max(0, Math.Min(3, barracks?.CurrentLevel ?? 0));
        }
    }
}
