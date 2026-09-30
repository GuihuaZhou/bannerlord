using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.KingdomDiplomacy.Negotiation.Services
{
    /// <summary>
    /// Values international territorial concessions without changing or
    /// borrowing the native internal-fief redistribution decisions. Losing a
    /// settlement and accepting a foreign settlement are intentionally
    /// asymmetric political events.
    /// </summary>
    public static class KingdomTerritoryNegotiationEvaluator
    {
        /// <summary>
        /// Returns a negative score for a clan asked to cede one of its
        /// kingdom's settlements. The owner suffers the direct fief loss,
        /// while every other clan still opposes the permanent national loss.
        /// A genuine wartime crisis can reduce, but never erase, that cost.
        /// </summary>
        public static float EvaluateTerritorialCession(
            Kingdom provider,
            Kingdom recipient,
            Clan evaluatingClan,
            Settlement settlement)
        {
            if (provider == null
                || recipient == null
                || evaluatingClan == null
                || settlement?.Town == null
                || settlement.MapFaction != provider)
            {
                return 0f;
            }

            float loss = CalculateBaseTerritorialValue(provider, settlement)
                * CalculateScarcityMultiplier(provider)
                * CalculateWarCrisisMultiplier(provider, recipient, settlement);

            if (settlement.OwnerClan == evaluatingClan)
            {
                // The fief owner loses both personal power and the kingdom-wide
                // value. Losing its final fief creates an additional political
                // survival concern.
                loss *= 2.5f;
                if (evaluatingClan.Fiefs.Count <= 1)
                {
                    loss += 50f;
                }
            }
            else if (evaluatingClan == provider.RulingClan)
            {
                // The ruler is held responsible for surrendering sovereign
                // territory even when another clan owns the settlement.
                loss = loss * 1.35f + 25f;
            }

            return -loss;
        }

        /// <summary>
        /// Returns a positive national benefit for accepting foreign land.
        /// No clan receives an owner bonus here because the later internal
        /// allocation of the new fief has not yet been decided.
        /// </summary>
        public static float EvaluateTerritorialAcquisition(
            Kingdom recipient,
            Kingdom provider,
            Clan evaluatingClan,
            Settlement settlement)
        {
            if (recipient == null
                || provider == null
                || evaluatingClan == null
                || settlement?.Town == null
                || settlement.MapFaction != provider)
            {
                return 0f;
            }

            float gain = CalculateBaseTerritorialValue(recipient, settlement)
                * 0.75f;
            if (evaluatingClan == recipient.RulingClan)
            {
                gain *= 1.1f;
            }

            return gain;
        }

        private static float CalculateBaseTerritorialValue(
            Kingdom evaluatingKingdom,
            Settlement settlement)
        {
            float value = settlement.IsTown ? 110f : 75f;
            value += Clamp(settlement.Town.Prosperity / 150f, 0f, 60f);
            value += Math.Min(24f, settlement.BoundVillages.Count * 8f);
            if (settlement.Culture == evaluatingKingdom.Culture)
            {
                value *= 1.2f;
            }

            return value;
        }

        private static float CalculateScarcityMultiplier(Kingdom provider)
        {
            int fortificationCount = provider.Settlements.Count(x =>
                x != null && x.IsFortification);
            if (fortificationCount <= 3)
            {
                return 1.5f;
            }
            if (fortificationCount <= 6)
            {
                return 1.25f;
            }

            return 1f;
        }

        /// <summary>
        /// A crisis only discounts territorial resistance. It never turns a
        /// cession into a positive event, and at least forty percent of the
        /// original political loss always remains.
        /// </summary>
        private static float CalculateWarCrisisMultiplier(
            Kingdom provider,
            Kingdom recipient,
            Settlement settlement)
        {
            if (!provider.IsAtWarWith(recipient))
            {
                return 1f;
            }

            float providerProgress = Campaign.Current.Models.DiplomacyModel
                .GetWarProgressScore(provider, recipient, false).ResultNumber;
            float recipientProgress = Campaign.Current.Models.DiplomacyModel
                .GetWarProgressScore(recipient, provider, false).ResultNumber;
            float progressDefeat = Clamp(
                (recipientProgress - providerProgress) / 50f,
                0f,
                0.35f);

            float providerStrength = Math.Max(1f, provider.CurrentTotalStrength);
            float strengthRatio = Math.Max(1f, recipient.CurrentTotalStrength)
                / providerStrength;
            float strengthDefeat = Clamp(
                Log2(strengthRatio) * 0.12f,
                0f,
                0.2f);
            int otherWars = Kingdom.All.Count(x => x != null
                && !x.IsEliminated
                && x != provider
                && x != recipient
                && provider.IsAtWarWith(x));
            float multiFrontDefeat = Clamp(otherWars * 0.05f, 0f, 0.15f);
            float siegeDefeat = settlement.IsUnderSiege ? 0.1f : 0f;

            return Clamp(
                1f - progressDefeat - strengthDefeat
                    - multiFrontDefeat - siegeDefeat,
                0.4f,
                1f);
        }

        private static float Log2(float value)
        {
            return (float)(Math.Log(Math.Max(0.01f, value)) / Math.Log(2d));
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
