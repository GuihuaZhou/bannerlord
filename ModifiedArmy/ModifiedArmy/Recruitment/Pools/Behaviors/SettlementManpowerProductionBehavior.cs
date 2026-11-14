using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Diagnostics;
// Professional and feudal daily production coordinator. It owns generated
// offer/progress state, but delegates authoritative Hearth mutation to the
// population service and preserves native/office training integration.
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using ModifiedArmy.Recruitment.Preferences.Behaviors;
using ModifiedArmy.Recruitment.Preferences.Models;
using ModifiedArmy.Population.Contracts;

namespace ModifiedArmy.Recruitment.Pools.Behaviors
{
    internal sealed class ProfessionalOfferConsumption
    {
        public Settlement Settlement { get; set; }
        public CharacterObject Troop { get; set; }
        public int Count { get; set; }
        public string PopulationOperationId { get; set; }
    }

    internal sealed class ProfessionalAffordabilityDiagnostic
    {
        public float HearthBefore { get; set; }
        public float ReserveHearth { get; set; }
        public float EffectiveCapacityBefore { get; set; }
        public float HearthCostPerTroop { get; set; }
        public int AffordableCount { get; set; }
        public string ReasonCode { get; set; }
    }

    /// <summary>
    /// Converts settlement manpower into daily professional offers and direct
    /// fief-party reinforcement. No generated troop inventory is persisted.
    /// </summary>
    /// <summary>
    /// Produces and expires daily manpower offers while maintaining fractional
    /// progress. Professional capacity is population- and loyalty-adjusted;
    /// feudal production is intentionally unaffected by those multipliers.
    /// </summary>
    public sealed class SettlementManpowerProductionBehavior :
        CampaignBehaviorBase
    {
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

        /// <summary>
        /// Returns final internal professional training before external Harmony
        /// Postfix multipliers. Population and loyalty are each applied once.
        /// </summary>
        public float GetDailyProduction(
            Settlement settlement,
            TroopProductionKind kind)
        {
            if (kind != TroopProductionKind.Professional)
            {
                return 0f;
            }
            ProfessionalRecruitmentEligibilitySnapshot professionalRule =
                ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            if (professionalRule == null)
            {
                return 0f;
            }

            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(
                    professionalRule.Culture);
            float original = doctrine.GetTraining(settlement).GetDailyProduction(
                professionalRule.BarracksLevel) * professionalRule.ProductionRate;
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (population == null) return 0f;
            HearthEconomyConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig().HearthEconomy;
            float populationEfficiency = ProfessionalPopulationMath
                .CalculatePopulationEfficiency(population.GetHearth(settlement),
                    population.GetNaturalCapacity(settlement),
                    config.ProfessionalReserveRatio,
                    config.ProfessionalFullEfficiencyRatio);
            return original * populationEfficiency *
                CalculateLoyaltyRecruitmentEfficiency(settlement);
        }

        /// <summary>
        /// Calculates continuous willingness from thresholds supplied by the
        /// active loyalty model. Invalid/reordered thresholds fail closed to
        /// avoid NaN production or recruitment during model conflicts.
        /// </summary>
        public static float CalculateLoyaltyRecruitmentEfficiency(
            Settlement settlement)
        {
            if (settlement?.Town == null || settlement.InRebelliousState)
                return 0f;
            var model = Campaign.Current?.Models?.SettlementLoyaltyModel;
            if (model == null) return 0f;
            float r = model.RebelliousStateStartLoyaltyThreshold;
            float m = model.LoyaltyDriftMedium;
            float u = model.ThresholdForTaxBoost;
            float loyalty = settlement.Town.Loyalty;
            if (float.IsNaN(r) || float.IsNaN(m) || float.IsNaN(u) ||
                float.IsInfinity(r) || float.IsInfinity(m) ||
                float.IsInfinity(u) || !(r < m && m < u)) return 0f;
            if (loyalty <= r) return 0f;
            if (loyalty < m) return (loyalty - r) / (m - r);
            if (loyalty < u)
                return 1f + 0.15f * (loyalty - m) / (u - m);
            return 1.15f;
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
            IReadOnlyDictionary<string, int> weights = GetEffectiveWeights(settlement);
            return offers.Where(x => x.Key != null && x.Value > 0 &&
                    weights.TryGetValue(x.Key.StringId, out int weight) && weight > 0)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        public int GetMaxAffordableProfessionalCount(Settlement settlement,
            CharacterObject troop, int requested)
        {
            return GetMaxAffordableProfessionalCount(
                settlement, troop, requested, out _);
        }

        /// <summary>
        /// Delegates affordability to the authoritative population service and
        /// captures diagnostic values without reserving or consuming Hearth.
        /// </summary>
        internal int GetMaxAffordableProfessionalCount(Settlement settlement,
            CharacterObject troop, int requested,
            out ProfessionalAffordabilityDiagnostic diagnostic)
        {
            diagnostic = new ProfessionalAffordabilityDiagnostic
            {
                ReasonCode = "InvalidRequest"
            };
            if (settlement?.IsFortification != true || troop == null ||
                requested <= 0) return 0;
            try
            {
                float hearthCost = MilitaryConfigManager.Instance
                    .RequireActiveConfig().HearthEconomy
                    .GetProfessionalHearthCost(troop);
                diagnostic.HearthCostPerTroop = hearthCost;
                ISettlementPopulationService population =
                    SettlementPopulationServiceRegistry.Current;
                if (population == null)
                {
                    diagnostic.ReasonCode = "PopulationProviderMissing";
                    return 0;
                }
                diagnostic.HearthBefore = population.GetHearth(settlement);
                diagnostic.ReserveHearth = population
                    .GetNaturalCapacity(settlement) * MilitaryConfigManager
                    .Instance.RequireActiveConfig().HearthEconomy
                    .ProfessionalReserveRatio;
                diagnostic.EffectiveCapacityBefore = Math.Max(0f,
                    diagnostic.HearthBefore - diagnostic.ReserveHearth);
                if (population.IsRecoveryRequired(settlement))
                {
                    diagnostic.ReasonCode = "SettlementRecoveryRequired";
                    return 0;
                }
                diagnostic.AffordableCount = population
                    .GetMaxAffordableProfessionalCount(
                        settlement, troop, requested);
                diagnostic.ReasonCode = diagnostic.AffordableCount > 0
                    ? "Affordable" : "ProfessionalHearthInsufficient";
                return diagnostic.AffordableCount;
            }
            catch (Exception exception)
            {
                diagnostic.ReasonCode = "PolicyException:" +
                    exception.GetType().Name;
                ModLogger.Error(string.Format(
                    "Professional affordability policy failed: " +
                    "settlement={0}, id={1}, culture={2}, troop={3}, " +
                    "tier={4}, requested={5}, reason={6}.", settlement.Name,
                    settlement.StringId, settlement.Culture?.StringId,
                    troop.Name, troop.Tier, requested, exception.Message));
                return 0;
            }
        }

        /// <summary>
        /// Aggregates mixed-Tier offers in stable troop-id order against one
        /// simulated Hearth balance, preventing the same available population
        /// from being counted independently for every troop type.
        /// </summary>
        public int GetTotalAffordableProfessionalOffers(Settlement settlement)
        {
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (population == null) return 0;
            float hearth = population.GetHearth(settlement);
            float capacity = population.GetNaturalCapacity(settlement);
            HearthEconomyConfig config = MilitaryConfigManager.Instance
                .RequireActiveConfig().HearthEconomy;
            int total = 0;
            foreach (var pair in GetAvailableProfessionalOffers(settlement)
                .Where(item => item.Key != null && item.Value > 0)
                .OrderBy(item => item.Key.StringId, StringComparer.Ordinal))
            {
                float cost;
                try
                {
                    cost = config.GetProfessionalHearthCost(pair.Key);
                }
                catch { continue; }
                for (int i = 0; i < pair.Value; i++)
                {
                    if (!ProfessionalPopulationMath.CanRecruitExact(
                        hearth, capacity, config.ProfessionalReserveRatio,
                        cost, 1)) break;
                    hearth -= cost;
                    total++;
                }
            }
            return total;
        }

        /// <summary>
        /// Returns current offers when recruitment is still pending, otherwise
        /// projects the next daily batch without changing production progress.
        /// </summary>
        public IReadOnlyDictionary<CharacterObject, int>
            GetPredictedProfessionalOffers(Settlement settlement)
        {
            IReadOnlyDictionary<CharacterObject, int> currentOffers =
                GetAvailableProfessionalOffers(settlement);
            if (currentOffers.Count > 0)
            {
                return currentOffers;
            }

            RecruitmentProductionForecast forecast =
                GetProductionForecast(settlement);
            ProfessionalTroopTemplate template =
                TroopProductionTemplateRepository.Instance.Resolve(
                    settlement,
                    TroopProductionKind.Professional) as
                    ProfessionalTroopTemplate;
            if (forecast == null || template == null ||
                !forecast.ProfessionalRecruitmentEnabled)
            {
                return new Dictionary<CharacterObject, int>();
            }

            SettlementManpowerProductionData data =
                GetProductionData(settlement);
            float progress = data?.ProfessionalProductionProgress ?? 0f;
            int predictedCount = (int)Math.Floor(
                progress + forecast.ProfessionalAllocation);
            if (predictedCount <= 0)
            {
                return new Dictionary<CharacterObject, int>();
            }

            return BuildDailyProfessionalOffers(
                template,
                settlement,
                forecast.AllowedRecruitmentLevel,
                GetEffectiveWeights(settlement),
                predictedCount,
                data,
                false);
        }

        internal IReadOnlyDictionary<CharacterObject, int>
            AllocateProfessionalConversionTroops(Settlement settlement,
                int count, bool commitLedger)
        {
            ProfessionalRecruitmentEligibilitySnapshot eligibility =
                ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            if (eligibility?.Template == null || count <= 0)
                return new Dictionary<CharacterObject, int>();
            return BuildDailyProfessionalOffers(
                eligibility.Template,
                settlement,
                eligibility.AllowedRecruitmentLevel,
                GetEffectiveWeights(settlement),
                count,
                GetOrCreateProductionData(settlement),
                commitLedger);
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
            return TryConsumeProfessionalOffer(
                settlement, troop, count, out ProfessionalOfferConsumption _);
        }

        internal bool TryConsumeProfessionalOffer(
            Settlement settlement,
            CharacterObject troop,
            int count,
            out ProfessionalOfferConsumption consumption)
        {
            consumption = null;
            if (troop == null || count <= 0)
            {
                LogOfferConsumption(
                    settlement,
                    troop,
                    count,
                    false,
                    GameTexts.FindText(
                        "str_modifiedarmy_manpower_reason_invalid_troop"));
                return false;
            }

            int day = (int)CampaignTime.Now.ToDays;
            if (!_professionalOfferDays.TryGetValue(
                    settlement,
                    out int offerDay) ||
                offerDay != day)
            {
                LogOfferConsumption(
                    settlement,
                    troop,
                    count,
                    false,
                    GameTexts.FindText(
                        "str_modifiedarmy_manpower_reason_offer_not_today"));
                return false;
            }

            if (!_dailyProfessionalOffers.TryGetValue(
                    settlement,
                    out var troops))
            {
                LogOfferConsumption(
                    settlement,
                    troop,
                    count,
                    false,
                    GameTexts.FindText(
                        "str_modifiedarmy_manpower_reason_offer_missing"));
                return false;
            }

            if (!troops.TryGetValue(troop, out int available) ||
                available < count || !GetEffectiveWeights(settlement)
                    .TryGetValue(troop.StringId, out int effectiveWeight) ||
                effectiveWeight <= 0)
            {
                LogOfferConsumption(
                    settlement,
                    troop,
                    count,
                    false,
                    CreateAvailableCountReason(available));
                return false;
            }

            FiefPartyManager manager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null)
            {
                LogOfferConsumption(
                    settlement,
                    troop,
                    count,
                    false,
                    GameTexts.FindText(
                        "str_modifiedarmy_manpower_reason_manager_unavailable"));
                return false;
            }
            if (!manager.TryConsumeProfessionalHearth(
                    settlement,
                    troop,
                    count,
                    out string populationOperationId))
            {
                LogOfferConsumption(
                    settlement,
                    troop,
                    count,
                    false,
                    GameTexts.FindText(
                        "str_modifiedarmy_manpower_reason_hearth"));
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
            consumption = new ProfessionalOfferConsumption
            {
                Settlement = settlement,
                Troop = troop,
                Count = count,
                PopulationOperationId = populationOperationId
            };
            return true;
        }

        internal void RollbackProfessionalOffer(
            ProfessionalOfferConsumption consumption)
        {
            if (consumption?.Settlement == null || consumption.Troop == null ||
                consumption.Count <= 0) return;
            int day = (int)CampaignTime.Now.ToDays;
            if (!_professionalOfferDays.TryGetValue(
                    consumption.Settlement, out int offerDay) ||
                offerDay != day) return;
            if (!_dailyProfessionalOffers.TryGetValue(
                    consumption.Settlement, out var offers))
            {
                offers = new Dictionary<CharacterObject, int>();
                _dailyProfessionalOffers[consumption.Settlement] = offers;
            }
            offers[consumption.Troop] = offers.TryGetValue(
                consumption.Troop, out int current)
                    ? current + consumption.Count
                    : consumption.Count;
            bool populationRestored = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .RollbackProfessionalHearth(
                    consumption.Settlement,
                    consumption.PopulationOperationId) == true;
            if (!populationRestored)
                throw new InvalidOperationException(
                    "ProfessionalPopulationRollbackFailed");
        }

        private static void LogOfferConsumption(
            Settlement settlement,
            CharacterObject troop,
            int count,
            bool succeeded,
            TextObject detail)
        {
            // Successful consumption is already represented by the daily production
            // manifest and the final garrison recruitment summary. Logging every
            // troop type separately produced several redundant lines per settlement.
            // Keep only failures because they identify a broken or stale offer.
            if (succeeded)
                return;
            if (!RecruitmentLogFilter.ShouldLog(settlement?.OwnerClan))
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_manpower_consume");
            message.SetTextVariable(
                "SETTLEMENT_NAME",
                settlement?.Name ?? new TextObject("-"));
            message.SetTextVariable(
                "TROOP_NAME",
                troop?.Name ?? new TextObject("-"));
            message.SetTextVariable("COUNT", count);
            message.SetTextVariable(
                "OUTCOME",
                GameTexts.FindText(
                    succeeded
                        ? "str_modifiedarmy_manpower_outcome_success"
                        : "str_modifiedarmy_manpower_outcome_failure"));
            message.SetTextVariable("DETAIL", detail);
            ModLogger.Debug(message.ToString());
        }

        private static TextObject CreateAvailableCountReason(int count)
        {
            TextObject reason = GameTexts.FindText(
                "str_modifiedarmy_manpower_reason_offer_insufficient");
            reason.SetTextVariable("AVAILABLE", count);
            return reason;
        }

        private void OnDailySettlementTick(Settlement settlement)
        {
            EnsureDailyProduction(settlement);
        }

        /// <summary>
        /// Produces today's offers and fief reinforcements at most once. The
        /// recruitment behavior calls this explicitly so correctness does not
        /// depend on campaign-event listener order.
        /// </summary>
        public void EnsureDailyProduction(Settlement settlement)
        {
            if (settlement?.IsFortification != true ||
                settlement.Town == null)
            {
                return;
            }

            int day = (int)CampaignTime.Now.ToDays;
            if (_professionalOfferDays.TryGetValue(
                    settlement,
                    out int producedDay) &&
                producedDay == day)
            {
                return;
            }

            FiefPartyManager fiefManager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            if (fiefManager?.IsSessionReady != true) return;
            if (!fiefManager.TryGetReadyFiefData(
                settlement, out FiefPartyData fiefData)) return;

            Campaign.Current?.GetCampaignBehavior<
                SettlementProfessionalPreferenceBehavior>()?.Reconcile(settlement);
            RecruitmentProductionForecast forecast =
                GetProductionForecast(settlement);
            if (forecast == null) return;
            SettlementManpowerProductionData data =
                GetOrCreateProductionData(settlement);
            if (data == null) return;

            // Commit the once-per-day boundary only after runtime state and
            // the complete production forecast are valid, but before the
            // first production mutation so a later exception cannot duplicate
            // progress or troops on retry.
            _dailyProfessionalOffers.Remove(settlement);
            _professionalOfferDays[settlement] = day;
            data.RecordFiefReplenishment(day, 0);
            if (forecast.ProfessionalAllocation <= 0f &&
                forecast.FiefAllocation <= 0f)
            {
                if (RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
                    ModLogger.FileDebug(string.Format(
                        "Daily recruitment production skipped: settlement={0}, " +
                        "settlementId={1}, day={2}, professionalAllocation={3:F2}, " +
                        "fiefAllocation={4:F2}, professionalEnabled={5}, " +
                        "reason=NoEligibleDailyAllocation.", settlement.Name,
                        settlement.StringId, day,
                        forecast.ProfessionalAllocation,
                        forecast.FiefAllocation,
                        forecast.ProfessionalRecruitmentEnabled));
                return;
            }

            ProfessionalTroopTemplate professionalTemplate =
                TroopProductionTemplateRepository.Instance.Resolve(
                    settlement,
                    TroopProductionKind.Professional) as ProfessionalTroopTemplate;
            IReadOnlyDictionary<string, int> effectiveWeights =
                GetEffectiveWeights(settlement);
            int professionalCount = effectiveWeights.Values.Any(x => x > 0)
                ? ConvertProgressToCount(data, TroopProductionKind.Professional,
                    forecast.ProfessionalAllocation)
                : 0;
            int fiefCount = ConvertProgressToCount(
                data,
                TroopProductionKind.Fief,
                forecast.FiefAllocation);
            int offeredCount = 0;
            if (professionalCount > 0 && professionalTemplate != null)
            {
                Dictionary<CharacterObject, int> offers =
                    BuildDailyProfessionalOffers(
                    professionalTemplate,
                    settlement,
                    forecast.AllowedRecruitmentLevel,
                    effectiveWeights,
                    professionalCount,
                    data,
                    true);
                offeredCount = offers.Values.Sum();
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
            int fiefProduced = 0;
            if (fiefCount > 0)
            {
                fiefProduced = fiefData.ProduceDailyFiefTroops(fiefCount);
                data.RecordFiefReplenishment(day, fiefProduced);
                if (fiefProduced < fiefCount)
                {
                    data.AddProductionProgress(
                        TroopProductionKind.Fief,
                        fiefCount - fiefProduced);
                }
            }

            // Zero-output days are routine for fractional production. Logging
            // only completed batches keeps player-kingdom diagnostics useful.
            if ((offeredCount > 0 || fiefProduced > 0) &&
                RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
            {
                TextObject productionMessage = GameTexts.FindText(
                    "str_modifiedarmy_manpower_produce");
                productionMessage.SetTextVariable("DAY", day);
                productionMessage.SetTextVariable(
                    "SETTLEMENT_NAME",
                    settlement.Name);
                productionMessage.SetTextVariable(
                    "TRAINING",
                    forecast.ProfessionalTrainingCapacity.ToString("F2"));
                productionMessage.SetTextVariable(
                    "ALLOCATION",
                    forecast.ProfessionalAllocation.ToString("F2"));
                productionMessage.SetTextVariable(
                    "REQUESTED",
                    professionalCount);
                productionMessage.SetTextVariable("OFFERED", offeredCount);
                productionMessage.SetTextVariable("FIEF_REQUESTED", fiefCount);
                productionMessage.SetTextVariable("FIEF_PRODUCED", fiefProduced);
                productionMessage.SetTextVariable(
                    "MANIFEST",
                    FormatOfferManifest(offers: _dailyProfessionalOffers
                        .TryGetValue(settlement, out var producedOffers)
                            ? producedOffers
                            : null));
                ModLogger.Debug(productionMessage.ToString());
            }
        }

        /// <summary>
        /// Calculates today's allocation without consuming manpower or
        /// producing troops. The daily tick and UI share this method.
        /// </summary>
        /// <summary>
        /// Builds a read-only forecast. RawProfessionalTrainingBeforeExternalEffects
        /// already includes population and loyalty, so the reported external
        /// bonus remains meaningful when either multiplier is below one.
        /// </summary>
        public RecruitmentProductionForecast GetProductionForecast(
            Settlement settlement)
        {
            if (settlement?.IsFortification != true) return null;
            FiefPartyManager manager = Campaign.Current
                ?.GetCampaignBehavior<FiefPartyManager>();
            if (manager == null || !manager.TryGetReadyFiefData(
                settlement, out FiefPartyData fiefData)) return null;

            ProfessionalRecruitmentEligibilitySnapshot eligibility =
                ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            ProfessionalTroopTemplate professionalTemplate = eligibility?.Template;
            IReadOnlyDictionary<string, int> effectiveWeights =
                GetEffectiveWeights(settlement);
            bool professionalEnabled = professionalTemplate != null &&
                settlement.Town?.GarrisonAutoRecruitmentIsEnabled == true &&
                effectiveWeights.Values.Any(x => x > 0);
            float professionalTrainingCapacity = professionalTemplate != null
                ? GetDailyProduction(
                    settlement,
                    TroopProductionKind.Professional)
                : 0f;
            float professionalAllocation = professionalEnabled
                ? professionalTrainingCapacity
                : 0f;
            CulturalMilitaryDoctrine doctrine = MilitaryConfigManager.Instance
                .RequireActiveConfig().GetDoctrine(settlement);
            FiefProductionConfig fiefProduction =
                doctrine.GetProduction(settlement);
            int current = fiefData.GetTotalTroopCount();
            float fiefDemand = fiefData.CanProduceAnyFiefTroop()
                ? fiefProduction.DailyRate
                : 0f;
            ProfessionalRecruitmentEligibilitySnapshot productionRule = eligibility;
            ProfessionalTrainingConfig training = doctrine.GetTraining(settlement);
            int barracksLevel = GetBarracksLevel(settlement);
            int allowedRecruitmentLevel = productionRule == null
                ? 0
                : Math.Min(
                    barracksLevel,
                    productionRule.AllowedRecruitmentLevel);
            float rawTraining = training.GetDailyProduction(
                barracksLevel) *
                (productionRule?.ProductionRate ?? 0f);
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            HearthEconomyConfig populationConfig = MilitaryConfigManager
                .Instance.RequireActiveConfig().HearthEconomy;
            float internalEfficiency = population == null ? 0f :
                ProfessionalPopulationMath.CalculatePopulationEfficiency(
                    population.GetHearth(settlement),
                    population.GetNaturalCapacity(settlement),
                    populationConfig.ProfessionalReserveRatio,
                    populationConfig.ProfessionalFullEfficiencyRatio) *
                CalculateLoyaltyRecruitmentEfficiency(settlement);
            float effectiveBeforeExternal = rawTraining * internalEfficiency;
            float externalBonusRate = effectiveBeforeExternal > 0.0001f
                ? Math.Max(
                    0f,
                    professionalTrainingCapacity / effectiveBeforeExternal - 1f)
                : 0f;
            float fiefAllocation = fiefDemand;

            return new RecruitmentProductionForecast
            {
                FiefDoctrineWeight = doctrine.FiefWeight,
                ProfessionalDoctrineWeight = doctrine.ProfessionalWeight,
                ProfessionalTrainingBase = training.Base,
                ProfessionalTrainingStep = training.BarracksStep,
                BarracksLevel = barracksLevel,
                AllowedRecruitmentLevel = allowedRecruitmentLevel,
                CultureEfficiency = productionRule?.ProductionRate ?? 0f,
                RawProfessionalTrainingBeforeExternalEffects =
                    effectiveBeforeExternal,
                ExternalTrainingBonusRate = externalBonusRate,
                ProfessionalTrainingCapacity = professionalTrainingCapacity,
                ProfessionalAllocation = professionalAllocation,
                FiefDemand = fiefDemand,
                FiefAllocation = fiefAllocation,
                FiefCurrent = current,
                FeudalBudgetHearth = fiefData.GetTotalFeudalBudgetHearth(),
                FeudalOccupiedHearth =
                    fiefData.GetTotalOccupiedSupplyHearth(),
                FeudalBudgetRemainingHearth =
                    fiefData.GetTotalFeudalBudgetRemainingHearth(),
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

        /// <summary>
        /// Allocates concrete troops by the largest weighted historical
        /// deficit. Repeating this calculation for batches of one still
        /// converges on XML weights, unlike independent daily rounding.
        /// Prediction receives a detached ledger and never commits it.
        /// </summary>
        private static Dictionary<CharacterObject, int> BuildDailyProfessionalOffers(
            ProfessionalTroopTemplate template,
            Settlement settlement,
            int allowedRecruitmentLevel,
            IReadOnlyDictionary<string, int> weights,
            int count,
            SettlementManpowerProductionData data,
            bool commitLedger)
        {
            Dictionary<CharacterObject, int> result = new();
            List<TroopProductionEntry> entries = template.TroopEntries
                .Where(e => e?.Troop != null &&
                    weights != null && weights.TryGetValue(
                        e.Troop.StringId, out int weight) && weight > 0 &&
                    e.RequiredBarracksLevel <= allowedRecruitmentLevel)
                .OrderBy(e => e.Troop.StringId)
                .ToList();

            if (count <= 0 || entries.Count == 0)
            {
                return result;
            }

            string signature = BuildProductionSignature(
                template,
                settlement,
                entries,
                weights);
            bool rebuiltLedger = data != null &&
                !data.IsProfessionalLedgerCurrent(signature);
            Dictionary<CharacterObject, int> ledger = data
                ?.CopyProfessionalLedger(signature)
                ?? new Dictionary<CharacterObject, int>();
            int totalWeight = entries.Sum(entry => weights[entry.Troop.StringId]);
            long historicalTotal = entries.Sum(entry =>
                (long)GetLedgerCount(ledger, entry.Troop));

            for (int i = 0; i < count; i++)
            {
                long nextTotal = historicalTotal + 1;
                TroopProductionEntry selected = entries
                    .OrderByDescending(entry =>
                        nextTotal * (double)weights[entry.Troop.StringId] / totalWeight -
                        GetLedgerCount(ledger, entry.Troop))
                    .ThenBy(entry => entry.Troop.StringId)
                    .First();

                result[selected.Troop] = result.TryGetValue(
                    selected.Troop,
                    out int value) ? value + 1 : 1;
                ledger[selected.Troop] =
                    GetLedgerCount(ledger, selected.Troop) + 1;
                historicalTotal = nextTotal;
            }

            if (commitLedger)
            {
                data?.CommitProfessionalLedger(signature, ledger);
                if (rebuiltLedger &&
                    RecruitmentLogFilter.ShouldLog(settlement?.OwnerClan))
                {
                    TextObject resetMessage = GameTexts.FindText(
                        "str_modifiedarmy_manpower_ledger_rebuilt");
                    resetMessage.SetTextVariable(
                        "SETTLEMENT_NAME",
                        settlement.Name);
                    resetMessage.SetTextVariable(
                        "TEMPLATE_ID",
                        template.TemplateId);
                    ModLogger.Debug(resetMessage.ToString());
                }
            }

            return result;
        }

        /// <summary>
        /// Includes all inputs that define the eligible weighted pool. A
        /// changed template, culture resolution or barracks eligibility set
        /// therefore rebuilds history safely on the next real production.
        /// </summary>
        private static string BuildProductionSignature(
            ProfessionalTroopTemplate template,
            Settlement settlement,
            IEnumerable<TroopProductionEntry> entries,
            IReadOnlyDictionary<string, int> weights)
        {
            return (template?.TemplateId ?? "unknown") + "|" +
                (settlement?.OwnerClan?.StringId ?? "no_owner") + "|" +
                (settlement?.OwnerClan?.Culture?.StringId ??
                    settlement?.Culture?.StringId ?? "no_culture") + "|" +
                string.Join(
                    ";",
                    entries.Select(entry =>
                        entry.Troop.StringId + ":" + weights[entry.Troop.StringId]));
        }

        /// <summary>
        /// Reads a non-negative historical count without exposing missing or
        /// corrupted save values to the weighted allocator.
        /// </summary>
        private static int GetLedgerCount(
            IReadOnlyDictionary<CharacterObject, int> ledger,
            CharacterObject troop)
        {
            return ledger != null && troop != null &&
                ledger.TryGetValue(troop, out int count)
                    ? Math.Max(0, count)
                    : 0;
        }

        /// <summary>
        /// Produces a compact localized-name manifest for player-kingdom debug
        /// logs without exposing internal character identifiers.
        /// </summary>
        private static string FormatOfferManifest(
            IReadOnlyDictionary<CharacterObject, int> offers)
        {
            if (offers == null || offers.Count == 0)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_manpower_manifest_empty").ToString();
            }

            return string.Join(
                "、",
                offers.Where(entry => entry.Key != null && entry.Value > 0)
                    .OrderBy(entry => entry.Key.StringId)
                    .Select(entry => FormatOfferManifestEntry(
                        entry.Key,
                        entry.Value)));
        }

        /// <summary>Formats one troop/count pair through localization XML.</summary>
        private static string FormatOfferManifestEntry(
            CharacterObject troop,
            int count)
        {
            TextObject entry = GameTexts.FindText(
                "str_modifiedarmy_manpower_manifest_entry");
            entry.SetTextVariable("TROOP_NAME", troop.Name);
            entry.SetTextVariable("COUNT", count);
            return entry.ToString();
        }

        public static int GetBarracksLevel(Settlement settlement)
        {
            return ProfessionalRecruitmentEligibilityModel
                .GetBarracksLevel(settlement);
        }

        private static IReadOnlyDictionary<string, int> GetEffectiveWeights(
            Settlement settlement)
        {
            SettlementProfessionalPreferenceBehavior behavior = Campaign.Current?
                .GetCampaignBehavior<SettlementProfessionalPreferenceBehavior>();
            if (behavior != null) return behavior.GetEffectiveWeights(settlement);
            ProfessionalRecruitmentEligibilitySnapshot snapshot =
                ProfessionalRecruitmentEligibilityModel.Evaluate(settlement);
            return snapshot?.EligibleEntries.ToDictionary(
                x => x.Troop.StringId, x => x.Weight) ??
                new Dictionary<string, int>();
        }

        public void FilterProfessionalOffers(Settlement settlement)
        {
            if (settlement == null || !_dailyProfessionalOffers.TryGetValue(
                    settlement, out var offers)) return;
            IReadOnlyDictionary<string, int> weights = GetEffectiveWeights(settlement);
            foreach (CharacterObject troop in offers.Keys.ToList())
                if (troop == null || !weights.TryGetValue(
                        troop.StringId, out int weight) || weight <= 0)
                    offers.Remove(troop);
        }

        public void InvalidateProfessionalOffersForOwnershipChange(
            Settlement settlement)
        {
            if (settlement == null) return;
            _dailyProfessionalOffers.Remove(settlement);
            // Deliberately retain _professionalOfferDays so the new owner cannot
            // redraw production or consume hearth twice on the capture day.
        }
    }
}
