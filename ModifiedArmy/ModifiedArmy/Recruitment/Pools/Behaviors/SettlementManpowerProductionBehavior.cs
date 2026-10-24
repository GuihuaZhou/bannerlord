using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.common;
using ModifiedArmy.Tool;

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
                predictedCount,
                data,
                false);
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
                available < count)
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
            if (!manager.TryConsumeProfessionalHearth(settlement, count))
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

            LogOfferConsumption(
                settlement,
                troop,
                count,
                true,
                CreateRemainingCountReason(remaining));
            return true;
        }

        private static void LogOfferConsumption(
            Settlement settlement,
            CharacterObject troop,
            int count,
            bool succeeded,
            TextObject detail)
        {
            if (!RecruitmentLogFilter.ShouldLog(settlement?.OwnerClan))
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_manpower_consume");
            message.SetTextVariable("DAY", (int)CampaignTime.Now.ToDays);
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

        private static TextObject CreateRemainingCountReason(int count)
        {
            TextObject reason = GameTexts.FindText(
                "str_modifiedarmy_manpower_reason_remaining");
            reason.SetTextVariable("REMAINING", count);
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

            _dailyProfessionalOffers.Remove(settlement);
            _professionalOfferDays[settlement] = day;
            SettlementManpowerProductionData data = GetOrCreateProductionData(settlement);
            FiefPartyManager fiefManager = Campaign.Current
                .GetCampaignBehavior<FiefPartyManager>();
            FiefPartyData fiefData = fiefManager?.GetFiefData(settlement);
            if (data == null || fiefData == null) return;

            data.RecordFiefReplenishment(day, 0);
            RecruitmentProductionForecast forecast = GetProductionForecast(settlement);
            if (forecast == null || forecast.SharedSupply <= 0f)
            {
                if (RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
                {
                    TextObject noSupplyMessage = GameTexts.FindText(
                        "str_modifiedarmy_manpower_produce_no_supply");
                    noSupplyMessage.SetTextVariable("DAY", day);
                    noSupplyMessage.SetTextVariable(
                        "SETTLEMENT_NAME",
                        settlement.Name);
                    ModLogger.Debug(noSupplyMessage.ToString());
                }
                return;
            }

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
            int offeredCount = 0;
            if (professionalCount > 0 && professionalTemplate != null)
            {
                Dictionary<CharacterObject, int> offers =
                    BuildDailyProfessionalOffers(
                    professionalTemplate,
                    settlement,
                    forecast.AllowedRecruitmentLevel,
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

            // Zero-output days are routine for fractional production. Logging
            // only completed batches keeps player-kingdom diagnostics useful.
            if (offeredCount > 0 &&
                RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
            {
                TextObject productionMessage = GameTexts.FindText(
                    "str_modifiedarmy_manpower_produce");
                productionMessage.SetTextVariable("DAY", day);
                productionMessage.SetTextVariable(
                    "SETTLEMENT_NAME",
                    settlement.Name);
                productionMessage.SetTextVariable(
                    "SUPPLY",
                    forecast.SharedSupply.ToString("F2"));
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
            int current = fiefData.GetTotalTroopCount();
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
            int count,
            SettlementManpowerProductionData data,
            bool commitLedger)
        {
            Dictionary<CharacterObject, int> result = new();
            List<TroopProductionEntry> entries = template.TroopEntries
                .Where(e => e?.Troop != null &&
                    e.Weight > 0 &&
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
                entries);
            bool rebuiltLedger = data != null &&
                !data.IsProfessionalLedgerCurrent(signature);
            Dictionary<CharacterObject, int> ledger = data
                ?.CopyProfessionalLedger(signature)
                ?? new Dictionary<CharacterObject, int>();
            int totalWeight = entries.Sum(entry => entry.Weight);
            long historicalTotal = entries.Sum(entry =>
                (long)GetLedgerCount(ledger, entry.Troop));

            for (int i = 0; i < count; i++)
            {
                long nextTotal = historicalTotal + 1;
                TroopProductionEntry selected = entries
                    .OrderByDescending(entry =>
                        nextTotal * (double)entry.Weight / totalWeight -
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
            IEnumerable<TroopProductionEntry> entries)
        {
            return (template?.TemplateId ?? "unknown") + "|" +
                (settlement?.OwnerClan?.StringId ?? "no_owner") + "|" +
                (settlement?.OwnerClan?.Culture?.StringId ??
                    settlement?.Culture?.StringId ?? "no_culture") + "|" +
                string.Join(
                    ";",
                    entries.Select(entry =>
                        entry.Troop.StringId + ":" + entry.Weight));
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
                ", ",
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
            int totalFief = fiefData.GetTotalTroopCount();
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
