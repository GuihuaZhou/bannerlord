using ModifiedArmy.Garrison.Models;
using ModifiedPolitics.HeroOffices.Services;
using ModifiedPolitics.SettlementEconomy.Config;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.SettlementEconomy.Models
{
    /// <summary>
    /// Extends the military-aware food model with civilian economy rules.
    /// </summary>
    public sealed class SettlementEconomyFoodModel : NewSettlementFoodModel
    {
        private static readonly TextObject AgricultureOfficeText =
            new TextObject("{=MP_AgricultureOfficerEffect}Agriculture officer");

        public override int NumberOfProsperityToEatOneFood => Math.Max(
            1,
            SettlementEconomyConfigManager.Instance
                .ActiveProsperityConsumptionDivisor);

        protected override float CalculateVillageDirectFood(Village village)
        {
            return Math.Max(0f, village?.Hearth ?? 0f) / 25f;
        }

        public override ExplainedNumber CalculateTownFoodStocksChange(
            Town town,
            bool includeMarketStocks = true,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = base.CalculateTownFoodStocksChange(
                town,
                includeMarketStocks,
                includeDescriptions);
            float factor = OfficeEffectService.GetFoodProductionFactor(town);
            if (factor > 0f && result.ResultNumber > 0f)
            {
                result.AddFactor(factor, AgricultureOfficeText);
            }

            return result;
        }
    }
}
