using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using ModifiedArmy.Models.Fief;

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

        private readonly Dictionary<Settlement, Dictionary<CharacterObject, int>>
            _dailyProfessionalOffers =
                new Dictionary<Settlement, Dictionary<CharacterObject, int>>();

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
            foreach (SettlementRecruitmentPoolData data in _settlementPools.Values)
            {
                data?.EnsureInitialized();
                data?.ProfessionalTroops.Clear();
                data?.FiefTroops.Clear();
            }
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
            if (kind != RecruitmentPoolKind.Professional || settlement == null ||
                !_dailyProfessionalOffers.TryGetValue(settlement, out var offers))
                return new Dictionary<CharacterObject, int>();
            return new Dictionary<CharacterObject, int>(offers);
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

            if (kind != RecruitmentPoolKind.Professional ||
                !_dailyProfessionalOffers.TryGetValue(settlement, out var troops))
            {
                return false;
            }

            if (!troops.TryGetValue(troop, out int available) ||
                available < count)
            {
                return false;
            }

            FiefPartyManager manager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null ||
                !manager.TryConsumeProfessionalHearth(settlement, count))
                return false;

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

            _dailyProfessionalOffers.Remove(settlement);
            SettlementRecruitmentPoolData data = GetOrCreatePool(settlement);
            FiefPartyManager fiefManager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            FiefPartyData fiefData = fiefManager?.GetFiefData(settlement);
            if (data == null || fiefData == null) return;

            int weekKey = (int)CampaignTime.Now.ToDays / 7;
            data.ResetWeekIfNeeded(weekKey);
            RecruitmentProductionForecast forecast = GetProductionForecast(settlement);
            if (forecast == null || forecast.SharedSupply <= 0f) return;

            int barracksLevel = GetBarracksLevel(settlement);
            ProfessionalTroopTemplate professionalTemplate =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    RecruitmentPoolKind.Professional) as ProfessionalTroopTemplate;
            int professionalCount = ConvertProgressToCount(
                data,
                RecruitmentPoolKind.Professional,
                forecast.ProfessionalAllocation);
            int weeklyRemaining = Math.Max(
                0,
                forecast.FiefWeeklyLimit - forecast.FiefProducedThisWeek);
            int fiefCount = Math.Min(
                weeklyRemaining,
                ConvertProgressToCount(
                    data,
                    RecruitmentPoolKind.Fief,
                    forecast.FiefAllocation));
            if (professionalCount > 0 && professionalTemplate != null)
                _dailyProfessionalOffers[settlement] = BuildVirtualOffers(
                    professionalTemplate,
                    barracksLevel,
                    professionalCount);
            if (fiefCount > 0)
            {
                int produced = fiefData.ProduceDailyFiefTroops(fiefCount);
                data.RecordFiefProduction(produced);
            }
        }

        /// <summary>
        /// Calculates today's allocation without consuming manpower or
        /// producing troops. The daily tick and UI share this method.
        /// </summary>
        public RecruitmentProductionForecast GetProductionForecast(
            Settlement settlement)
        {
            if (settlement?.IsFortification != true) return null;
            FiefPartyManager manager = Campaign.Current
                ?.GetCampaignBehavior<FiefPartyManager>();
            FiefPartyData fiefData = manager?.GetFiefData(settlement);
            if (fiefData == null) return null;

            SettlementRecruitmentPoolData data = GetOrCreatePool(settlement);
            int weekKey = (int)CampaignTime.Now.ToDays / 7;
            data.ResetWeekIfNeeded(weekKey);
            ProfessionalTroopTemplate professionalTemplate =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    RecruitmentPoolKind.Professional) as ProfessionalTroopTemplate;
            FiefPartyTemplate fiefTemplate = fiefData.GetFiefPartyTemplate();
            float supply = CalculateSettlementSupply(settlement, fiefData, manager);
            bool professionalEnabled = professionalTemplate != null &&
                settlement.Town?.GarrisonAutoRecruitmentIsEnabled == true;
            float professionalDemand = professionalEnabled
                ? GetDailyProduction(settlement, RecruitmentPoolKind.Professional)
                : 0f;
            int weeklyLimit = fiefTemplate?.MaxWeeklySupplement ?? 0;
            int weeklyRemaining = Math.Max(
                0,
                weeklyLimit - data.FiefProducedThisWeek);
            int current = fiefData.GetTroopCounts().Values.Sum();
            int capacity = fiefData.GetFiefTroopLimit();
            float fiefDemand = Math.Min(
                weeklyRemaining,
                Math.Max(0, capacity - current));
            float ratio = professionalTemplate?.ProfessionalRatio ?? 0.5f;
            float professionalAllocation = Math.Min(
                supply * ratio,
                professionalDemand);
            float fiefAllocation = Math.Min(
                supply * (1f - ratio),
                fiefDemand);
            float unused = Math.Max(
                0f,
                supply - professionalAllocation - fiefAllocation);
            float extra = Math.Min(
                unused,
                Math.Max(0f, professionalDemand - professionalAllocation));
            professionalAllocation += extra;
            unused -= extra;
            fiefAllocation += Math.Min(
                unused,
                Math.Max(0f, fiefDemand - fiefAllocation));

            return new RecruitmentProductionForecast
            {
                SharedSupply = supply,
                ProfessionalRatio = ratio,
                ProfessionalTrainingCapacity = professionalDemand,
                ProfessionalAllocation = professionalAllocation,
                FiefDemand = fiefDemand,
                FiefAllocation = fiefAllocation,
                FiefCurrent = current,
                FiefCapacity = capacity,
                FiefProducedThisWeek = data.FiefProducedThisWeek,
                FiefWeeklyLimit = weeklyLimit,
                ProfessionalRecruitmentEnabled = professionalEnabled
            };
        }

        private static int ConvertProgressToCount(
            SettlementRecruitmentPoolData data,
            RecruitmentPoolKind kind,
            float allocation)
        {
            if (allocation <= 0f) return 0;
            float progress = data.AddProductionProgress(kind, allocation);
            int count = (int)Math.Floor(progress);
            data.ConsumeProductionProgress(kind, count);
            data.SetProductionProgress(kind, progress - count);
            return count;
        }

        private static Dictionary<CharacterObject, int> BuildVirtualOffers(
            ProfessionalTroopTemplate template,
            int barracksLevel,
            int count)
        {
            Dictionary<CharacterObject, int> result = new();
            List<RecruitmentPoolTroopEntry> entries = template.PoolTroops
                .Where(e => e?.Troop != null &&
                    e.RequiredBarracksLevel <= barracksLevel)
                .ToList();
            for (int i = 0; i < count && entries.Count > 0; i++)
            {
                RecruitmentPoolTroopEntry selected = entries
                    .OrderBy(e =>
                        (result.TryGetValue(e.Troop, out int current)
                            ? current
                            : 0) / (float)Math.Max(1, e.Weight))
                    .ThenBy(e => e.Troop.StringId)
                    .First();
                result[selected.Troop] = result.TryGetValue(
                    selected.Troop,
                    out int value) ? value + 1 : 1;
            }
            return result;
        }

        public static float CalculateVillageSupply(Village village, int assignedFiefCount)
        {
            if (village?.VillageState != Village.VillageStates.Normal)
                return 0f;
            FiefPartyManager manager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            float effective = Math.Max(
                0f,
                village.Hearth + (manager?.GetMobilizedHearth(village) ?? 0) -
                    Math.Max(0, assignedFiefCount));
            return effective <= 100f
                ? 0f
                : 0.03f * effective * (effective - 100f) /
                    (effective + 50f);
        }

        private static float CalculateSettlementSupply(
            Settlement settlement,
            FiefPartyData fiefData,
            FiefPartyManager manager)
        {
            List<Village> villages = settlement.BoundVillages
                .Where(v => v?.Settlement != null)
                .OrderBy(v => v.Settlement.StringId)
                .ToList();
            if (villages.Count == 0) return 0f;
            int totalFief = fiefData.GetTroopCounts().Values.Sum();
            int totalMobilized = villages.Sum(v => manager.GetMobilizedHearth(v));
            int remaining = Math.Max(0, totalFief - totalMobilized);
            int quotient = remaining / villages.Count;
            int remainder = remaining % villages.Count;
            float total = 0f;
            for (int i = 0; i < villages.Count; i++)
            {
                int assigned = manager.GetMobilizedHearth(villages[i]) +
                    quotient + (i < remainder ? 1 : 0);
                total += CalculateVillageSupply(villages[i], assigned);
            }
            return total;
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
                    settlementCulture,
                    MediumProfessionalProductionRate,
                    1);
            }

            return new ProfessionalProductionRule(
                settlementCulture,
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
