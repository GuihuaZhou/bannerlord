using ModifiedArmy.Models.Fief;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedPolitics.SettlementEconomy.Models
{
    /// <summary>
    /// Applies continuous natural village growth and a soft carrying capacity.
    /// </summary>
    public sealed class SettlementEconomyProsperityModel :
        DefaultSettlementProsperityModel
    {
        private const float NaturalCapacity = 1200f;
        private static readonly TextObject NaturalDecayText = new TextObject(
            "{=MP_VillageNaturalDecay}Natural decay");

        public override ExplainedNumber CalculateHearthChange(
            Village village,
            bool includeDescriptions = false)
        {
            ExplainedNumber result = base.CalculateHearthChange(
                village,
                includeDescriptions);
            if (village == null ||
                village.VillageState != Village.VillageStates.Normal)
            {
                return result;
            }

            int mobilized = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>()?
                .GetMobilizedHearth(village) ?? 0;
            float effective = Math.Max(0f, village.Hearth + mobilized);
            float replacementBase = 4f /
                (1f + (float)Math.Pow(effective / 225f, 3d));
            float vanillaBase = village.Hearth < 300f
                ? 4f
                : village.Hearth < 600f ? 1.2f : 0.2f;
            result.Add(replacementBase - vanillaBase);

            float growth = result.ResultNumber;
            if (growth <= 0f) return result;
            float ratio = Math.Min(effective, NaturalCapacity) /
                NaturalCapacity;
            float retention = Math.Max(
                0f,
                1f - (float)Math.Pow(ratio, 4d));
            float expected = Math.Min(
                growth * retention,
                Math.Max(0f, NaturalCapacity - effective));
            float decay = Math.Min(0f, expected - growth);
            if (decay < 0f)
            {
                result.Add(decay, NaturalDecayText, null);
            }

            return result;
        }
    }
}
