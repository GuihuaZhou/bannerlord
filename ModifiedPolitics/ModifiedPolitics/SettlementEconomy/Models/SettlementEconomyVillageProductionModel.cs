using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedPolitics.SettlementEconomy.Models
{
    /// <summary>
    /// Preserves vanilla modifiers while replacing its discrete hearth tiers
    /// with a continuous, linear population multiplier.
    /// </summary>
    public sealed class SettlementEconomyVillageProductionModel :
        DefaultVillageProductionCalculatorModel
    {
        public override ExplainedNumber CalculateDailyProductionAmount(
            Village village,
            ItemObject item)
        {
            ExplainedNumber result = base.CalculateDailyProductionAmount(
                village,
                item);
            if (village == null ||
                village.VillageState != Village.VillageStates.Normal)
            {
                return result;
            }

            float vanillaMultiplier =
                (village.GetHearthLevel() + 1f) * 0.5f;
            if (vanillaMultiplier <= 0f) return result;
            float desiredMultiplier = Math.Max(0f, village.Hearth) / 300f;
            float current = result.ResultNumber;
            result.Add(current * desiredMultiplier / vanillaMultiplier - current);
            return result;
        }

        public override float CalculateDailyFoodProductionAmount(Village village)
        {
            float result = base.CalculateDailyFoodProductionAmount(village);
            if (village == null ||
                village.VillageState != Village.VillageStates.Normal)
            {
                return result;
            }

            float vanillaMultiplier = village.GetHearthLevel() + 1f;
            if (vanillaMultiplier <= 0f) return result;
            float desiredMultiplier = Math.Max(0f, village.Hearth) / 300f;
            return result * desiredMultiplier / vanillaMultiplier;
        }
    }
}
