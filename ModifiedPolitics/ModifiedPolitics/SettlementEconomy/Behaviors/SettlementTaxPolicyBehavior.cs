// Authoritative per-fief tax policy and AI hysteresis owner. Tax levels and AI
// protection state are persisted here, while the existing tax, loyalty, and
// prosperity models remain the only writers of their respective game effects.
using System;
using System.Collections.Generic;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedPolitics.SettlementEconomy.Config;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using CampaignHero = TaleWorlds.CampaignSystem.Hero;

namespace ModifiedPolitics.SettlementEconomy.Behaviors
{
    /// <summary>Stable persisted ordering of the five supported tax levels.</summary>
    public enum SettlementTaxLevel { VeryLow, Low, Normal, High, VeryHigh }

    /// <summary>Immutable direct daily effects of one tax level.</summary>
    public readonly struct SettlementTaxEffects
    {
        public SettlementTaxEffects(float multiplier, float loyalty, float prosperity)
        { Multiplier = multiplier; Loyalty = loyalty; Prosperity = prosperity; }
        public float Multiplier { get; }
        public float Loyalty { get; }
        public float Prosperity { get; }
    }

    /// <summary>
    /// Read/write boundary shared by campaign models, AI, and the deferred UI.
    /// </summary>
    public interface ISettlementTaxPolicyService
    {
        SettlementTaxLevel GetLevel(Settlement settlement);
        SettlementTaxEffects GetEffects(Settlement settlement);
        bool TrySetLevel(Settlement settlement, SettlementTaxLevel level,
            bool playerAction, out string reason);
    }

    /// <summary>
    /// Campaign-scoped access point. The behavior owns state; the registry only
    /// exposes the active campaign instance.
    /// </summary>
    public static class SettlementTaxPolicyRegistry
    {
        public static ISettlementTaxPolicyService Current { get; private set; }
        public static void Register(ISettlementTaxPolicyService service) => Current = service;
        public static void Clear() => Current = null;
    }

    /// <summary>
    /// Persists one tax level and the minimum AI hysteresis state per
    /// fortification. Ownership changes reset both policy and hysteresis.
    /// </summary>
    public sealed class SettlementTaxPolicyBehavior : CampaignBehaviorBase,
        ISettlementTaxPolicyService
    {
        private const string LevelsKey = "ModifiedPolitics.SettlementTax.Levels.v1";
        private const string ProtectionKey = "ModifiedPolitics.SettlementTax.Protection.v1";
        private const string SafeDaysKey = "ModifiedPolitics.SettlementTax.SafeDays.v1";
        private const string EvaluationKey = "ModifiedPolitics.SettlementTax.LastEvaluation.v1";
        private Dictionary<Settlement, int> _levels = new();
        private Dictionary<Settlement, bool> _protection = new();
        private Dictionary<Settlement, int> _safeDays = new();
        private Dictionary<Settlement, int> _lastEvaluation = new();

        /// <summary>Registers daily evaluation, ownership reset, and load recovery.</summary>
        public override void RegisterEvents()
        {
            SettlementTaxPolicyRegistry.Register(this);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnOwnerChanged);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, _ => SettlementTaxPolicyRegistry.Register(this));
        }

        /// <summary>
        /// Preserves same-day evaluation identity across save/load so a reload
        /// cannot apply a second AI decision for the same fief and day.
        /// </summary>
        public override void SyncData(IDataStore store)
        {
            store.SyncData(LevelsKey, ref _levels);
            store.SyncData(ProtectionKey, ref _protection);
            store.SyncData(SafeDaysKey, ref _safeDays);
            store.SyncData(EvaluationKey, ref _lastEvaluation);
            _levels ??= new Dictionary<Settlement, int>();
            _protection ??= new Dictionary<Settlement, bool>();
            _safeDays ??= new Dictionary<Settlement, int>();
            _lastEvaluation ??= new Dictionary<Settlement, int>();
        }

        /// <summary>Returns Normal for old saves or invalid persisted values.</summary>
        public SettlementTaxLevel GetLevel(Settlement settlement) =>
            settlement != null && _levels.TryGetValue(settlement, out int raw) &&
            raw >= 0 && raw <= 4 ? (SettlementTaxLevel)raw : SettlementTaxLevel.Normal;

        /// <summary>Resolves the current level to its exact configured effects.</summary>
        public SettlementTaxEffects GetEffects(Settlement settlement) => Effects(GetLevel(settlement));

        /// <summary>
        /// Validates the fief and player authority before changing policy. AI
        /// calls use the same mutation path after their eligibility checks.
        /// </summary>
        public bool TrySetLevel(Settlement settlement, SettlementTaxLevel level,
            bool playerAction, out string reason)
        {
            reason = null;
            if (settlement?.IsFortification != true || (int)level < 0 || (int)level > 4)
            { reason = "InvalidTaxPolicyRequest"; return false; }
            if (playerAction && settlement.OwnerClan != Clan.PlayerClan)
            { reason = "PlayerDoesNotManageSettlement"; return false; }
            _levels[settlement] = (int)level;
            return true;
        }

        /// <summary>
        /// Defines the fixed V3.0 five-level balance contract used by every
        /// consumer; callers must not duplicate these values.
        /// </summary>
        public static SettlementTaxEffects Effects(SettlementTaxLevel level)
        {
            return level switch
            {
                SettlementTaxLevel.VeryLow => new SettlementTaxEffects(0.50f, 1f, 1f),
                SettlementTaxLevel.Low => new SettlementTaxEffects(0.75f, 0.5f, 0.5f),
                SettlementTaxLevel.High => new SettlementTaxEffects(1.25f, -1.5f, -1.5f),
                SettlementTaxLevel.VeryHigh => new SettlementTaxEffects(1.50f, -3f, -3f),
                _ => new SettlementTaxEffects(1f, 0f, 0f)
            };
        }

        private void OnDailyTick(Settlement settlement)
        {
            if (settlement?.IsFortification != true || settlement.OwnerClan == null ||
                settlement.OwnerClan == Clan.PlayerClan || settlement.Town == null) return;
            int day = (int)CampaignTime.Now.ToDays;
            if (_lastEvaluation.TryGetValue(settlement, out int previous) && previous == day) return;
            _lastEvaluation[settlement] = day;
            var loyaltyModel = Campaign.Current.Models.SettlementLoyaltyModel;
            float r = loyaltyModel.RebelliousStateStartLoyaltyThreshold;
            float m = loyaltyModel.LoyaltyDriftMedium;
            float loyalty = settlement.Town.Loyalty;
            // "Near rebellion" is relative to the loaded loyalty model rather
            // than a hard-coded loyalty number, so other model overrides remain
            // compatible and cannot create a divide-by-zero path.
            bool danger = settlement.InRebelliousState ||
                (m > r && loyalty <= r + (m - r) * 0.25f);
            bool protection = _protection.TryGetValue(settlement, out bool active) && active;
            if (danger) { protection = true; _safeDays[settlement] = 0; }
            else if (protection)
            {
                int safe = loyalty >= m ? (_safeDays.TryGetValue(settlement, out int value) ? value : 0) + 1 : 0;
                _safeDays[settlement] = safe;
                if (safe >= SettlementEconomyConfigManager.Instance.RequireActive().TaxProtectionSafeDays) protection = false;
            }
            _protection[settlement] = protection;
            SettlementTaxLevel current = GetLevel(settlement);
            SettlementTaxLevel best = current;
            float currentScore = Score(settlement, current, danger);
            float bestScore = currentScore;
            foreach (SettlementTaxLevel candidate in Enum.GetValues(typeof(SettlementTaxLevel)))
            {
                float score = Score(settlement, candidate, danger);
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            float threshold = SettlementEconomyConfigManager.Instance.RequireActive().TaxSwitchThreshold;
            bool emergencyReduction = danger && best < current;
            if (!emergencyReduction && bestScore - currentScore < threshold)
                return;
            // Reductions may be immediate during danger. Increases are always
            // one step, forbidden during protection, and require score margin.
            SettlementTaxLevel next = current;
            if (best < current) next = danger ? best : (SettlementTaxLevel)((int)current - 1);
            else if (!protection && best > current) next = (SettlementTaxLevel)((int)current + 1);
            if (next != current && TrySetLevel(settlement, next, false, out _))
                ModLogger.FileInfo(string.Format(
                    "[TaxPolicy] AI tax level changed: settlement={0}, id={1}, ownerClan={2}, " +
                    "from={3}, to={4}, fiscalScore={5:F3}, candidateScore={6:F3}, " +
                    "protection={7}, rebellionDanger={8}, day={9}.",
                    settlement.Name, settlement.StringId, ClanName(settlement),
                    current, next, currentScore, bestScore, protection, danger, day));
        }

        /// <summary>
        /// Scores one candidate from current, side-effect-free finance and
        /// settlement snapshots. Components are normalized before weighting;
        /// this method never changes the real tax level to probe a candidate.
        /// </summary>
        private static float Score(Settlement settlement, SettlementTaxLevel level, bool danger)
        {
            SettlementTaxEffects effects = Effects(level);
            Clan clan = settlement.OwnerClan;
            var finance = Campaign.Current.Models.ClanFinanceModel;
            float income = Math.Max(1f, finance.CalculateClanIncome(clan, false, false, true).ResultNumber);
            float expenses = Math.Max(1f, finance.CalculateClanExpenses(clan, false, false, true).ResultNumber);
            float deficit = Math.Max(0f, expenses - income);
            float lossRatio = Math.Min(1f, deficit / expenses);
            float runwayPressure = deficit <= 0f ? 0f : 1f - Math.Min(1f, clan.Gold / Math.Max(1f, deficit * 30f));
            float fiscalPressure = Math.Min(1f, 0.6f * lossRatio + 0.4f * runwayPressure);
            float currentMultiplier = Math.Max(0.01f,
                SettlementTaxPolicyRegistry.Current?.GetEffects(settlement).Multiplier ?? 1f);
            float currentTax = Math.Max(0f, Campaign.Current.Models
                .SettlementTaxModel.CalculateTownTax(settlement.Town, false).ResultNumber);
            float normalTaxBase = currentTax / currentMultiplier;
            float localTaxShare = Math.Min(1f, normalTaxBase / income);
            ISettlementPopulationService population = SettlementPopulationServiceRegistry.Current;
            float populationRatio = population == null ? 0f : Math.Min(1f,
                population.GetHearth(settlement) / Math.Max(1f, population.GetNaturalCapacity(settlement)));
            float garrisonNeed = settlement.Town.GarrisonParty == null ? 1f :
                Math.Max(0f, 1f - settlement.Town.GarrisonParty.Party.NumberOfAllMembers /
                    Math.Max(1f, settlement.Town.GarrisonParty.Party.PartySizeLimit));
            float loyaltyEfficiency = SettlementManpowerProductionBehavior
                .CalculateLoyaltyRecruitmentEfficiency(settlement) / 1.15f;
            SettlementEconomyConfig config = SettlementEconomyConfigManager.Instance.RequireActive();
            float fiscal = config.TaxFiscalWeight *
                ((effects.Multiplier - 0.5f) / 1f) * fiscalPressure *
                (0.25f + 0.75f * localTaxShare);
            float economy = config.TaxEconomyWeight * effects.Prosperity / 3f * (1f - fiscalPressure);
            float recruitment = config.TaxRecruitmentWeight * effects.Loyalty /
                3f * garrisonNeed * (1f - populationRatio) *
                (1f - Math.Min(1f, loyaltyEfficiency));
            float rebellion = config.TaxRebellionWeight * (danger ? Math.Max(0f, -effects.Loyalty) / 3f : 0f);
            return fiscal + economy + recruitment - rebellion;
        }

        private static string ClanName(Settlement settlement) =>
            settlement?.OwnerClan?.Name?.ToString() ?? "missing";

        /// <summary>
        /// A new owner starts at Normal and receives fresh AI hysteresis state,
        /// preventing inherited punitive taxation from surviving conquest.
        /// </summary>
        private void OnOwnerChanged(Settlement settlement, bool openToClaim,
            CampaignHero newOwner, CampaignHero oldOwner, CampaignHero capturer,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (settlement?.IsFortification != true) return;
            _levels.Remove(settlement); _protection.Remove(settlement);
            _safeDays.Remove(settlement); _lastEvaluation.Remove(settlement);
        }
    }
}
