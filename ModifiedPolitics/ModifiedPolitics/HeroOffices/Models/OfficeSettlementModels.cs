using ModifiedPolitics.HeroOffices.Services;
using ModifiedArmy.Models.Fief;
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

        public override ExplainedNumber CalculateLoyaltyChange(
            Town town,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = base.CalculateLoyaltyChange(
                town,
                includeDescriptions);
            float value = OfficeEffectService.GetDailyLoyalty(town);
            if (value > 0f)
            {
                result.Add(value, SecurityOfficeText, null);
            }

            return result;
        }
    }
}
