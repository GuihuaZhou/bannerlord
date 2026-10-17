using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.common;

namespace ModifiedArmy.Recruitment.Pools.Behaviors
{
    /// <summary>
    /// Converts settlement manpower into daily professional offers and direct
    /// fief-party reinforcement. No generated troop inventory is persisted.
    /// </summary>
    public sealed class SettlementManpowerProductionBehavior :
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
                int maximumRecruitableBarracksLevel)
            {
                Culture = culture;
                ProductionRate = productionRate;
                MaximumRecruitableBarracksLevel =
                    maximumRecruitableBarracksLevel;
            }

            public CultureObject Culture { get; }

            public float ProductionRate { get; }

            public int MaximumRecruitableBarracksLevel { get; }
        }

        private Dictionary<Settlement, SettlementManpowerProductionData>
            _settlementProductionData =
                new Dictionary<Settlement, SettlementManpowerProductionData>();

        private readonly Dictionary<Settlement, Dictionary<CharacterObject, int>>
            _dailyProfessionalOffers =
                new Dictionary<Settlement, Dictionary<CharacterObject, int>>();

        private readonly Dictionary<Settlement, int> _professionalOfferDays =
            new Dictionary<Settlement, int>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(
                this,
                OnDailySettlementTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "_settlementRecruitmentPools",
                ref _settlementProductionData);
            _settlementProductionData ??=
                new Dictionary<Settlement, SettlementManpowerProductionData>();

        }

        public SettlementManpowerProductionData GetOrCreateProductionData(
            Settlement settlement)
        {
            if (settlement?.IsFortification != true)
            {
                return null;
            }

            if (!_settlementProductionData.TryGetValue(settlement, out var data) ||
                data == null)
            {
                data = new SettlementManpowerProductionData();
                _settlementProductionData[settlement] = data;
            }

            return data;
        }

        public SettlementManpowerProductionData GetProductionData(
            Settlement settlement)
        {
            if (settlement == null)
            {
                return null;
            }

            _settlementProductionData.TryGetValue(settlement, out var data);
            return data;
        }

        public float GetDailyProduction(
            Settlement settlement,
            TroopProductionKind kind)
        {
            if (kind != TroopProductionKind.Professional)
            {
                return 0f;
            }
            int barracksLevel = GetBarracksLevel(settlement);
            ProfessionalProductionRule professionalRule =
                ResolveProfessionalProductionRule(settlement);
            if (professionalRule == null)
            {
                return 0f;
            }

            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(
                    professionalRule.Culture);
            return doctrine.GetTraining(settlement).GetDailyProduction(
                barracksLevel) * professionalRule.ProductionRate;
        }

        /// <summary>
        /// Returns a detached snapshot of today's generated professional
        /// offers so callers cannot bypass the manpower transaction.
        /// </summary>
        public IReadOnlyDictionary<CharacterObject, int> GetAvailableProfessionalOffers(
            Settlement settlement)
        {
            if (settlement == null ||
                !_professionalOfferDays.TryGetValue(
                    settlement,
                    out int offerDay) ||
                offerDay != (int)CampaignTime.Now.ToDays ||
                !_dailyProfessionalOffers.TryGetValue(settlement, out var offers))
                return new Dictionary<CharacterObject, int>();
            return new Dictionary<CharacterObject, int>(offers);
        }

        /// <summary>
        /// Consumes an exact quantity from today's offers and charges the
        /// corresponding village hearth manpower.
        /// </summary>
        public bool TryConsumeProfessionalOffer(
            Settlement settlement,
            CharacterObject troop,
            int count)
        {
            if (troop == null || count <= 0)
            {
                return false;
            }

            if (!_dailyProfessionalOffers.TryGetValue(
                    settlement,
                    out var troops))
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
            _professionalOfferDays[settlement] =
                (int)CampaignTime.Now.ToDays;
            SettlementManpowerProductionData data = GetOrCreateProductionData(settlement);
            FiefPartyManager fiefManager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            FiefPartyData fiefData = fiefManager?.GetFiefData(settlement);
            if (data == null || fiefData == null) return;

            int day = (int)CampaignTime.Now.ToDays;
            data.RecordFiefReplenishment(day, 0);
            RecruitmentProductionForecast forecast = GetProductionForecast(settlement);
            if (forecast == null || forecast.SharedSupply <= 0f) return;

            ProfessionalTroopTemplate professionalTemplate =
                TroopProductionTemplateRepository.Instance.Resolve(
                    settlement,
                    TroopProductionKind.Professional) as ProfessionalTroopTemplate;
            int professionalCount = ConvertProgressToCount(
                data,
                TroopProductionKind.Professional,
                forecast.ProfessionalAllocation);
            int fiefCount = ConvertProgressToCount(
                data,
                TroopProductionKind.Fief,
                forecast.FiefAllocation);
            if (professionalCount > 0 && professionalTemplate != null)
            {
                Dictionary<CharacterObject, int> offers =
                    BuildDailyProfessionalOffers(
                    professionalTemplate,
                    forecast.AllowedRecruitmentLevel,
                    professionalCount,
                    day);
                int offeredCount = offers.Values.Sum();
                if (offeredCount > 0)
                {
                    _dailyProfessionalOffers[settlement] = offers;
                }
                if (offeredCount < professionalCount)
                {
                    data.AddProductionProgress(
                        TroopProductionKind.Professional,
                        professionalCount - offeredCount);
                }
            }
            if (fiefCount > 0)
            {
                int produced = fiefData.ProduceDailyFiefTroops(fiefCount);
                data.RecordFiefReplenishment(day, produced);
                if (produced < fiefCount)
                {
                    data.AddProductionProgress(
                        TroopProductionKind.Fief,
                        fiefCount - produced);
                }
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

            ProfessionalTroopTemplate professionalTemplate =
                TroopProductionTemplateRepository.Instance.Resolve(
                    settlement,
                    TroopProductionKind.Professional) as ProfessionalTroopTemplate;
            float supply = CalculateSettlementSupply(settlement, fiefData, manager);
            bool professionalEnabled = professionalTemplate != null &&
                settlement.Town?.GarrisonAutoRecruitmentIsEnabled == true;
            float professionalTrainingCapacity = professionalTemplate != null
                ? GetDailyProduction(
                    settlement,
                    TroopProductionKind.Professional)
                : 0f;
            float professionalDemand = professionalEnabled
                ? professionalTrainingCapacity
                : 0f;
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(settlement);
            FiefProductionConfig fiefProduction =
                doctrine.GetProduction(settlement);
            int current = fiefData.GetTroopCounts().Values.Sum();
            int capacity = fiefData.GetFiefTroopLimit();
            float fiefDemand = Math.Min(
                fiefProduction.DailyRate,
                Math.Max(0, capacity - current));
            float ratio = doctrine.ProfessionalShare;
            ProfessionalProductionRule productionRule =
                ResolveProfessionalProductionRule(settlement);
            ProfessionalTrainingConfig training = doctrine.GetTraining(settlement);
            int barracksLevel = GetBarracksLevel(settlement);
            int allowedRecruitmentLevel = productionRule == null
                ? 0
                : Math.Min(
                    barracksLevel,
                    productionRule.MaximumRecruitableBarracksLevel);
            float rawTraining = training.GetDailyProduction(
                barracksLevel) *
                (productionRule?.ProductionRate ?? 0f);
            float externalBonusRate = rawTraining > 0.0001f
                ? Math.Max(
                    0f,
                    professionalTrainingCapacity / rawTraining - 1f)
                : 0f;
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
                FiefDoctrineWeight = doctrine.FiefWeight,
                ProfessionalDoctrineWeight = doctrine.ProfessionalWeight,
                ProfessionalTrainingBase = training.Base,
                ProfessionalTrainingStep = training.BarracksStep,
                BarracksLevel = barracksLevel,
                AllowedRecruitmentLevel = allowedRecruitmentLevel,
                CultureEfficiency = productionRule?.ProductionRate ?? 0f,
                RawProfessionalTrainingBeforeExternalEffects = rawTraining,
                ExternalTrainingBonusRate = externalBonusRate,
                ProfessionalTrainingCapacity = professionalTrainingCapacity,
                ProfessionalAllocation = professionalAllocation,
                FiefDemand = fiefDemand,
                FiefAllocation = fiefAllocation,
                FiefCurrent = current,
                FiefCapacity = capacity,
                FiefBaseDailyRate = fiefProduction.DailyRate,
                ProfessionalRecruitmentEnabled = professionalEnabled
            };
        }

        private static int ConvertProgressToCount(
            SettlementManpowerProductionData data,
            TroopProductionKind kind,
            float allocation)
        {
            if (allocation <= 0f) return 0;
            float progress = data.AddProductionProgress(kind, allocation);
            int count = (int)Math.Floor(progress);
            data.ConsumeProductionProgress(kind, count);
            data.SetProductionProgress(kind, progress - count);
            return count;
        }

        private static Dictionary<CharacterObject, int> BuildDailyProfessionalOffers(
            ProfessionalTroopTemplate template,
            int allowedRecruitmentLevel,
            int count,
            int day)
        {
            Dictionary<CharacterObject, int> result = new();
            List<TroopProductionEntry> entries = template.TroopEntries
                .Where(e => e?.Troop != null &&
                    e.RequiredBarracksLevel <= allowedRecruitmentLevel)
                .OrderBy(e => e.Troop.StringId)
                .ToList();
            List<TroopProductionEntry> weightedCycle = new();
            foreach (TroopProductionEntry entry in entries)
            {
                for (int i = 0; i < Math.Max(1, entry.Weight); i++)
                {
                    weightedCycle.Add(entry);
                }
            }

            for (int i = 0; i < count && weightedCycle.Count > 0; i++)
            {
                TroopProductionEntry selected = weightedCycle[
                    Math.Abs(day + i) % weightedCycle.Count];
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
            if (kingdom != null && kingdom.Culture == null)
            {
                return null;
            }

            CultureObject kingdomCulture = kingdom?.Culture ?? clanCulture;
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
                    2);
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
