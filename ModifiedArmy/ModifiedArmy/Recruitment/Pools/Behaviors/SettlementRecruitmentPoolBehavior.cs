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
    /// castle. Daily production fills the largest weighted shortage first.
    /// Recruitment transactions will consume these pools in a later batch.
    /// </summary>
    public sealed class SettlementRecruitmentPoolBehavior :
        CampaignBehaviorBase
    {
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
            IRecruitmentPoolTemplate template =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    kind);
            return template?.GetCapacity() ?? 0;
        }

        public float GetDailyProduction(
            Settlement settlement,
            RecruitmentPoolKind kind)
        {
            IRecruitmentPoolTemplate template =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    kind);
            return template?.GetDailyProduction(
                GetBarracksLevel(settlement)) ?? 0f;
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
            IRecruitmentPoolTemplate template =
                RecruitmentPoolTemplateRepository.Instance.Resolve(
                    settlement,
                    kind);
            if (template == null)
            {
                return;
            }

            Dictionary<CharacterObject, int> troops = data.GetTroops(kind);
            List<RecruitmentPoolTroopEntry> eligible = template.PoolTroops
                .Where(entry =>
                    entry?.Troop != null &&
                    entry.RequiredBarracksLevel <= barracksLevel)
                .ToList();
            int capacity = template.GetCapacity();
            int currentCount = troops.Values.Sum(value => Math.Max(0, value));

            if (eligible.Count == 0 || currentCount >= capacity)
            {
                return;
            }

            float progress = data.AddProductionProgress(
                kind,
                template.GetDailyProduction(barracksLevel));
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
