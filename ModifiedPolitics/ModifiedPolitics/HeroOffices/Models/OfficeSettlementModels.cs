// Integrates offices and per-fief tax policy into the already registered tax
// and loyalty models. No parallel model or manual daily resource write exists.
using ModifiedPolitics.HeroOffices.Services;
using ModifiedArmy.Models.Fief;
using ModifiedPolitics.SettlementEconomy.Behaviors;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.Models
{
    /// <summary>
    /// Adds tax-office effects without patching the native tax model.
    /// </summary>
    public sealed class OfficeSettlementTaxModel : DefaultSettlementTaxModel
    {
        private static readonly TextObject TaxOfficeText =
            new TextObject("{=MP_TaxOfficerEffect}Tax officer");
        private static readonly TextObject FeudalTaxReliefText =
            new TextObject(
                "{=MP_FeudalTaxRelief}Feudal troop tax relief");
        private static readonly TextObject TaxPolicyText =
            new TextObject("{=MP_SettlementTaxPolicy}Settlement tax policy");

        /// <summary>
        /// Applies office and tax-policy multipliers to the complete native tax
        /// result, then subtracts feudal relief as an exact amount so percentage
        /// factors cannot amplify that relief.
        /// </summary>
        public override ExplainedNumber CalculateTownTax(
            Town town,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = base.CalculateTownTax(
                town,
                includeDescriptions);
            float factor = OfficeEffectService.GetTaxFactor(town);
            if (factor > 0f)
            {
                result.AddFactor(factor, TaxOfficeText);
            }

            SettlementTaxEffects tax = SettlementTaxPolicyRegistry.Current?
                .GetEffects(town?.Settlement) ??
                SettlementTaxPolicyBehavior.Effects(SettlementTaxLevel.Normal);
            // ExplainedNumber factors are additive. Scale the policy factor by
            // the existing multiplier so the complete normal tax result is
            // multiplied by the configured tax rate rather than merely adding
            // percentage points to native and office factors.
            float existingMultiplier = 1f + result.SumOfFactors;
            result.AddFactor((tax.Multiplier - 1f) * existingMultiplier,
                TaxPolicyText);

            float taxBeforeRelief = Math.Max(0f, result.ResultNumber);
            float requestedRelief = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetFeudalTaxReliefAmount(town?.Settlement) ?? 0f;
            float actualRelief = Math.Min(taxBeforeRelief,
                Math.Max(0f, requestedRelief));
            if (actualRelief > 0f)
            {
                // ExplainedNumber applies the accumulated factors to values
                // added later as well. Normalize the line so its final effect
                // is exactly actualRelief after the office/native factors.
                float factorMultiplier = 1f + result.SumOfFactors;
                if (factorMultiplier > 0.0001f)
                    result.Add(-actualRelief / factorMultiplier,
                        FeudalTaxReliefText);
            }
            result.LimitMin(0f);

            return result;
        }
    }

    /// <summary>
    /// Adds security-office effects without patching the native loyalty model.
    /// </summary>
    public sealed class OfficeSettlementLoyaltyModel :
        DefaultSettlementLoyaltyModel
    {
        private static readonly TextObject SecurityOfficeText =
            new TextObject("{=MP_SecurityOfficerEffect}Security officer");
        private static readonly TextObject TaxPolicyText =
            new TextObject("{=MP_SettlementTaxPolicy}Settlement tax policy");
        private static readonly TextObject GovernorCultureNeutralText =
            new TextObject("{=MP_GovernorCultureNeutral}Inclusive administration");

        public override float SettlementOwnerDifferentCultureLoyaltyEffect => 0f;

        /// <summary>
        /// Preserves native loyalty contributors, neutralizes the loaded native
        /// cross-culture penalties, and adds office and tax-policy daily lines.
        /// </summary>
        public override ExplainedNumber CalculateLoyaltyChange(
            Town town,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = base.CalculateLoyaltyChange(
                town,
                includeDescriptions);
            // The loaded game API does not expose the governor culture property
            // as overridable. Neutralize the native -1 line in the same model.
            if (town?.Governor != null && town.Governor.Culture != town.Culture)
                result.Add(1f, GovernorCultureNeutralText, null);
            float value = OfficeEffectService.GetDailyLoyalty(town);
            if (value > 0f)
            {
                result.Add(value, SecurityOfficeText, null);
            }
            float tax = SettlementTaxPolicyRegistry.Current?
                .GetEffects(town?.Settlement).Loyalty ?? 0f;
            if (Math.Abs(tax) > 0.0001f)
                result.Add(tax, TaxPolicyText, null);

            return result;
        }
    }
}
