// Authoritative per-fief tax policy and AI hysteresis owner. Tax levels and AI
// protection state are persisted here, while the existing tax, loyalty, and
// prosperity models remain the only writers of their respective game effects.
using System;
using System.Collections.Generic;
using ModifiedArmy.PartyManagement.Finance.Models;
using ModifiedArmy.Population.Contracts;
using ModifiedPolitics.HeroOffices.Models;
using ModifiedPolitics.SettlementEconomy.Config;
using ModifiedPolitics.SettlementEconomy.Models;
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
            if (!ClanFiscalStateModel.TryGetState(settlement.OwnerClan, out ClanFiscalState fiscal))
            {
                LogDecisionDebug(settlement, "UnavailableFiscalState", day);
                return;
            }

            if (!(Campaign.Current.Models.SettlementLoyaltyModel is OfficeSettlementLoyaltyModel officeLoyalty) ||
                !(Campaign.Current.Models.SettlementProsperityModel is SettlementEconomyProsperityModel economyProsperity))
            {
                LogDecisionDebug(settlement, "CandidateModelUnavailable", day);
                return;
            }

            SettlementEconomyConfig config = SettlementEconomyConfigManager.Instance.RequireActive();
            float loyaltyNormal = officeLoyalty.CalculateLoyaltyChangeForTaxLevel(
                settlement.Town, SettlementTaxLevel.Normal).ResultNumber;
            ExplainedNumber prosperityNormalResult = economyProsperity.CalculateProsperityChangeForTaxLevel(
                settlement.Town, SettlementTaxLevel.Normal);
            ExplainedNumber prosperityLowResult = economyProsperity.CalculateProsperityChangeForTaxLevel(
                settlement.Town, SettlementTaxLevel.Low);
            ExplainedNumber prosperityVeryLowResult = economyProsperity.CalculateProsperityChangeForTaxLevel(
                settlement.Town, SettlementTaxLevel.VeryLow);
            ISettlementPopulationService population = SettlementPopulationServiceRegistry.Current;
            float supported = population == null ? 0f : population.GetHearth(settlement) *
                (settlement.IsCastle ? config.CastleProsperityPerHearth : config.TownProsperityPerHearth);
            CivilRecoveryNeed loyaltyNeed = SettlementTaxDecisionModel.EvaluateLoyalty(
                loyalty, r, m, loyaltyNormal, danger, config, out string loyaltyReason);
            CivilRecoveryNeed prosperityNeed = SettlementTaxDecisionModel.EvaluateProsperity(
                settlement.Town.Prosperity, supported, prosperityNormalResult.ResultNumber,
                prosperityLowResult.ResultNumber, prosperityVeryLowResult.ResultNumber,
                config, out string prosperityReason);
            CivilRecoveryNeed civil = loyaltyNeed > prosperityNeed ? loyaltyNeed : prosperityNeed;
            var snapshot = new TaxPolicyDecisionSnapshot
            {
                FiscalState = fiscal,
                CivilNeed = civil,
                RebellionDanger = danger,
                CurrentLoyalty = loyalty,
                NormalLoyaltyDailyChange = loyaltyNormal,
                CurrentProsperity = settlement.Town.Prosperity,
                SupportedProsperity = supported,
                NormalProsperityDailyChange = prosperityNormalResult.ResultNumber,
                LowProsperityDailyChange = prosperityLowResult.ResultNumber,
                VeryLowProsperityDailyChange = prosperityVeryLowResult.ResultNumber,
                CivilReasonCode = loyaltyReason + "+" + prosperityReason
            };
            SettlementTaxLevel current = GetLevel(settlement);
            SettlementTaxLevel target = SettlementTaxDecisionModel.ResolveTarget(fiscal, civil, danger);
            if (target == current)
            {
                LogDecisionDebug(settlement, civil == CivilRecoveryNeed.None
                    ? "AlreadyAtTarget" : "CivilRecoveryStillNeeded", day);
                return;
            }
            if (target > current && protection)
            {
                LogDecisionDebug(settlement, "RebellionProtection", day);
                return;
            }

            // Rebellion danger alone may cross bands immediately. Every other
            // correction moves one band so old saves normalize without jumps.
            SettlementTaxLevel next = current;
            if (target < current) next = danger ? target : (SettlementTaxLevel)((int)current - 1);
            else next = (SettlementTaxLevel)((int)current + 1);
            float nextStepAdvantage = 0.25f *
                (Math.Abs((int)current - (int)target) - Math.Abs((int)next - (int)target));
            if (!danger && nextStepAdvantage < config.TaxSwitchThreshold)
            {
                LogDecisionDebug(settlement, "SwitchThresholdNotMet", day);
                return;
            }
            if (next != current && TrySetLevel(settlement, next, false, out _))
                ModLogger.FileInfo(string.Format(
                    "[TaxPolicy] changed settlement={0}, settlementId={1}, ownerClan={2}, " +
                    "day={3}, gold={4}, fiscalState={5}, loyalty={6:F2}, loyaltyNormalDaily={7:F2}, " +
                    "prosperity={8:F2}, prosperitySupported={9:F2}, prosperityNormalDaily={10:F2}, " +
                    "prosperityLowDaily={11:F2}, prosperityVeryLowDaily={12:F2}, civilNeed={13}, " +
                    "civilReason={14}, danger={15}, protection={16}, from={17}, target={18}, to={19}, reason={20}.",
                    settlement.Name, settlement.StringId, ClanName(settlement),
                    day, Math.Max(0, settlement.OwnerClan.Leader.Gold), snapshot.FiscalState,
                    snapshot.CurrentLoyalty, snapshot.NormalLoyaltyDailyChange,
                    snapshot.CurrentProsperity, snapshot.SupportedProsperity,
                    snapshot.NormalProsperityDailyChange, snapshot.LowProsperityDailyChange,
                    snapshot.VeryLowProsperityDailyChange, snapshot.CivilNeed,
                    snapshot.CivilReasonCode, snapshot.RebellionDanger, protection,
                    current, target, next, ChangeReason(snapshot, target)));
        }

        /// <summary>
        /// Categorizes a successful change for stable, reconstructable logs.
        /// </summary>
        private static string ChangeReason(TaxPolicyDecisionSnapshot snapshot, SettlementTaxLevel target)
        {
            if (snapshot.RebellionDanger) return "RebellionEmergency";
            if (snapshot.FiscalState != ClanFiscalState.Normal && snapshot.CivilNeed != CivilRecoveryNeed.None)
                return "ConflictCompromise";
            if (snapshot.CivilNeed != CivilRecoveryNeed.None) return "CivilRecovery";
            if (target == SettlementTaxLevel.Normal) return "ReturnToNormal";
            return "FiscalNeed";
        }

        private static void LogDecisionDebug(Settlement settlement, string reason, int day)
        {
            if (settlement?.OwnerClan?.Kingdom != Clan.PlayerClan?.Kingdom) return;
            ModLogger.FileDebug(string.Format(
                "[TaxPolicy] unchanged settlement={0}, settlementId={1}, ownerClan={2}, day={3}, reason={4}.",
                settlement.Name, settlement.StringId, ClanName(settlement), day, reason));
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
