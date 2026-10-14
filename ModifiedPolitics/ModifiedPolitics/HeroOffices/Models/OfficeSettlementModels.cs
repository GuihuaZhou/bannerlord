using ModifiedPolitics.HeroOffices.Services;
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
