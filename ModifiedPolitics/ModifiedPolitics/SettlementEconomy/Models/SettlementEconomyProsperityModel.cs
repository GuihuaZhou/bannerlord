using ModifiedArmy.Models.Fief;
using ModifiedArmy.Population.Contracts;
using ModifiedPolitics.SettlementEconomy.Config;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.SettlementEconomy.Models
{
    public sealed class SettlementEconomyProsperityModel : DefaultSettlementProsperityModel
    {
        private static readonly TextObject PopulationSupportText = new TextObject("{=MP_PopulationSupportShortfall}Insufficient population support");
        private static readonly TextObject NaturalPopulationText = new TextObject("{=MP_NaturalPopulationGrowth}Natural population growth");

        public override ExplainedNumber CalculateHearthChange(Village village, bool includeDescriptions = false)
        {
            ExplainedNumber result = base.CalculateHearthChange(village, includeDescriptions);
            if (village == null || village.VillageState != Village.VillageStates.Normal) return result;
            SettlementEconomyConfig config = SettlementEconomyConfigManager.Instance.RequireActive();
            int mobilized = Campaign.Current?.GetCampaignBehavior<FiefPartyManager>()?.GetMobilizedHearth(village) ?? 0;
            float effective = Math.Max(0f, village.Hearth + mobilized);
            float natural = PopulationGrowthModel.Calculate(effective, config.VillageCapacity, config);
            float vanillaBase = village.Hearth < 300f ? 4f : village.Hearth < 600f ? 1.2f : 0.2f;
            result.Add(natural - vanillaBase, NaturalPopulationText, null);
            return result;
        }

        public override ExplainedNumber CalculateProsperityChange(Town fortification, bool includeDescriptions = false)
        {
            ExplainedNumber result = base.CalculateProsperityChange(fortification, includeDescriptions);
            Settlement settlement = fortification?.Settlement;
            ISettlementPopulationService service = SettlementPopulationServiceRegistry.Current;
            if (settlement == null || service == null) return result;
            SettlementEconomyConfig config = SettlementEconomyConfigManager.Instance.RequireActive();
            float perHearth = settlement.IsCastle ? config.CastleProsperityPerHearth : config.TownProsperityPerHearth;
            float supported = service.GetEffectiveHearth(settlement) *
                perHearth;
            float prosperity = fortification.Prosperity;
            float original = result.ResultNumber;
            float constrained = prosperity < supported
                ? Math.Min(original, supported - prosperity)
                : Math.Min(Math.Min(original, 0f), -(prosperity - supported) / config.ExcessProsperityDecayDays);
            float adjustment = constrained - original;
            if (adjustment < 0f) result.Add(adjustment, PopulationSupportText, null);
            return result;
        }
    }
}
