// Computes tax-policy targets from the authoritative clan wealth band and
// read-only settlement recovery evidence. It owns no policy or campaign state;
// persistence and mutations remain in SettlementTaxPolicyBehavior.
using System;
using ModifiedArmy.PartyManagement.Finance.Models;
using ModifiedPolitics.SettlementEconomy.Behaviors;
using ModifiedPolitics.SettlementEconomy.Config;

namespace ModifiedPolitics.SettlementEconomy.Models
{
    internal enum CivilRecoveryNeed { None, Moderate, Severe }

    internal sealed class TaxPolicyDecisionSnapshot
    {
        internal ClanFiscalState FiscalState { get; set; }
        internal CivilRecoveryNeed CivilNeed { get; set; }
        internal bool RebellionDanger { get; set; }
        internal float CurrentLoyalty { get; set; }
        internal float NormalLoyaltyDailyChange { get; set; }
        internal float CurrentProsperity { get; set; }
        internal float SupportedProsperity { get; set; }
        internal float NormalProsperityDailyChange { get; set; }
        internal float LowProsperityDailyChange { get; set; }
        internal float VeryLowProsperityDailyChange { get; set; }
        internal string CivilReasonCode { get; set; }
    }

    internal static class SettlementTaxDecisionModel
    {
        /// <summary>Maps the two independent needs to the audited five-level policy table.</summary>
        internal static SettlementTaxLevel ResolveTarget(
            ClanFiscalState fiscal,
            CivilRecoveryNeed civil,
            bool rebellionDanger)
        {
            if (rebellionDanger)
                return SettlementTaxLevel.VeryLow;

            if (fiscal == ClanFiscalState.Normal)
                return civil == CivilRecoveryNeed.Severe ? SettlementTaxLevel.VeryLow
                    : civil == CivilRecoveryNeed.Moderate ? SettlementTaxLevel.Low
                    : SettlementTaxLevel.Normal;
            if (fiscal == ClanFiscalState.Tight)
                return civil == CivilRecoveryNeed.Severe ? SettlementTaxLevel.Low
                    : civil == CivilRecoveryNeed.Moderate ? SettlementTaxLevel.Normal
                    : SettlementTaxLevel.High;
            return civil == CivilRecoveryNeed.Severe ? SettlementTaxLevel.Normal
                : civil == CivilRecoveryNeed.Moderate ? SettlementTaxLevel.High
                : SettlementTaxLevel.VeryHigh;
        }

        /// <summary>Classifies loyalty need against the active model's dynamic thresholds.</summary>
        internal static CivilRecoveryNeed EvaluateLoyalty(
            float loyalty, float rebelliousThreshold, float driftMedium,
            float normalDaily, bool rebellionDanger,
            SettlementEconomyConfig config, out string reason)
        {
            if (rebellionDanger)
            { reason = "LoyaltyRebellionDanger"; return CivilRecoveryNeed.Severe; }
            if (driftMedium <= rebelliousThreshold)
            { reason = "InvalidLoyaltyThresholds"; return CivilRecoveryNeed.None; }
            float span = driftMedium - rebelliousThreshold;
            if (loyalty < rebelliousThreshold + 0.5f * span &&
                normalDaily < config.TaxLoyaltySevereRecovery)
            { reason = "LoyaltySevereSlowRecovery"; return CivilRecoveryNeed.Severe; }
            if (loyalty < driftMedium &&
                (normalDaily < config.TaxLoyaltySlowRecovery || normalDaily < 0f))
            { reason = "LoyaltyModerateSlowRecovery"; return CivilRecoveryNeed.Moderate; }
            reason = "LoyaltyNoRecoveryNeed";
            return CivilRecoveryNeed.None;
        }

        /// <summary>
        /// Requires an actual constrained tax benefit before prosperity can
        /// motivate a reduction; theoretical tax bonuses alone are ignored.
        /// </summary>
        internal static CivilRecoveryNeed EvaluateProsperity(
            float prosperity, float supported, float normalDaily,
            float lowDaily, float veryLowDaily,
            SettlementEconomyConfig config, out string reason)
        {
            if (supported <= 0f)
            { reason = "ProsperitySupportUnavailable"; return CivilRecoveryNeed.None; }
            float lowBenefit = lowDaily - normalDaily;
            float veryLowBenefit = veryLowDaily - normalDaily;
            if (lowBenefit <= config.TaxProsperityMinBenefit &&
                veryLowBenefit <= config.TaxProsperityMinBenefit)
            { reason = "ProsperityNoMarginalTaxBenefit"; return CivilRecoveryNeed.None; }
            if (normalDaily > config.TaxProsperityHealthyGrowth)
            { reason = "ProsperityHealthyGrowth"; return CivilRecoveryNeed.None; }
            if (normalDaily <= config.TaxProsperitySevereDecline &&
                veryLowBenefit > config.TaxProsperityMinBenefit)
            { reason = "ProsperitySevereDecline"; return CivilRecoveryNeed.Severe; }
            float gapRatio = Math.Max(0f, supported - prosperity) / supported;
            bool declining = normalDaily < config.TaxProsperityDecline;
            bool stalledBelowCapacity = gapRatio >= config.TaxProsperityCapacityGapRatio &&
                normalDaily <= config.TaxProsperityStalledGrowth;
            if ((declining || stalledBelowCapacity) &&
                lowBenefit > config.TaxProsperityMinBenefit)
            { reason = declining ? "ProsperityDecline" : "ProsperityStalledBelowCapacity"; return CivilRecoveryNeed.Moderate; }
            reason = "ProsperityNoRecoveryNeed";
            return CivilRecoveryNeed.None;
        }
    }
}
