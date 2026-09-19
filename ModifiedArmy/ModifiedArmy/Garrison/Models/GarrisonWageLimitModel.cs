using Helpers;
using ModifiedArmy.Models;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ModifiedArmy.Garrison.Models
{
    /// <summary>
    /// Calculates the strategic troop target and wage limit for a garrison.
    /// Prosperity and food are deliberately excluded. They are civilian and
    /// logistical constraints, not measures of the settlement's military need.
    /// </summary>
    public static class GarrisonWageLimitModel
    {
        public const int CastleBaseGarrison = 100;
        public const int CastleBorderBonus = 150;
        public const int TownBaseGarrison = 200;
        public const int TownBorderBonus = 200;
        public const int BorderNeighborCount = 5;

        /// <summary>
        /// Uses the shared average of the custom T4 and T5 troop wages.
        /// NewPartyWageModel calculates this value once at type initialization.
        /// </summary>
        public static float ExpectedAverageTroopWageFloor =>
            NewPartyWageModel.Tier4AndTier5AverageWage;

        /// <summary>
        /// Calculates the wage ceiling from strategic size, expected average
        /// troop wage and the vanilla clan-leader gold factor.
        /// </summary>
        public static int CalculateWagePaymentLimit(
            Town town,
            out int desiredGarrisonSize,
            out float clanLeaderGoldFactor,
            out float expectedAverageTroopWage,
            out float rawWagePaymentLimit,
            out bool isBorder)
        {
            desiredGarrisonSize = 0;
            clanLeaderGoldFactor = 1f;
            expectedAverageTroopWage =
                ExpectedAverageTroopWageFloor;
            rawWagePaymentLimit = 0f;
            isBorder = false;

            if (town?.Settlement == null || Campaign.Current == null)
            {
                return 0;
            }

            isBorder = IsBorderSettlement(town.Settlement);
            desiredGarrisonSize = GetDesiredGarrisonSize(
                town.Settlement,
                isBorder);

            clanLeaderGoldFactor =
                FactionHelper.OwnerClanEconomyEffectOnGarrisonSizeConstant(
                    town.OwnerClan);

            expectedAverageTroopWage =
                CalculateExpectedAverageTroopWage(town);

            rawWagePaymentLimit =
                desiredGarrisonSize
                * expectedAverageTroopWage
                * clanLeaderGoldFactor;

            return (int)MathF.Clamp(
                rawWagePaymentLimit,
                0f,
                Campaign.Current.Models.PartyWageModel.MaxWagePaymentLimit);
        }

        /// <summary>
        /// Uses the professional troop wage as a stable minimum. If the
        /// current garrison is more expensive, its real average wage is used
        /// so elite troops and mercenaries are not valued as low-tier troops.
        /// </summary>
        private static float CalculateExpectedAverageTroopWage(Town town)
        {
            int regularCount =
                town.GarrisonParty?.MemberRoster?.TotalRegulars ?? 0;

            if (regularCount <= 0)
            {
                return ExpectedAverageTroopWageFloor;
            }

            float actualAverageWage =
                town.GarrisonParty.TotalWage / (float)regularCount;

            return MathF.Max(
                ExpectedAverageTroopWageFloor,
                actualAverageWage);
        }

        private static int GetDesiredGarrisonSize(
            Settlement settlement,
            bool isBorder)
        {
            if (settlement.IsCastle)
            {
                return CastleBaseGarrison
                    + (isBorder ? CastleBorderBonus : 0);
            }

            return TownBaseGarrison
                + (isBorder ? TownBorderBonus : 0);
        }

        /// <summary>
        /// A fortification is considered a border position when any of its
        /// nearest local neighbors belongs to another political faction.
        /// Border status is intentionally independent of current wars so a
        /// peace agreement cannot suddenly reduce the wage limit and dismiss
        /// the frontier garrison. Checking a local group rather than only the single nearest
        /// fortification prevents a nearby friendly center from hiding an
        /// otherwise obvious frontline position.
        /// </summary>
        private static bool IsBorderSettlement(Settlement settlement)
        {
            IFaction ownerFaction = settlement?.MapFaction;

            if (ownerFaction == null)
            {
                return false;
            }

            return Town.AllFiefs
                .Where(candidate =>
                    candidate?.Settlement != null
                    && candidate.Settlement != settlement)
                .OrderBy(candidate =>
                    candidate.Settlement.GetPosition2D.DistanceSquared(
                        settlement.GetPosition2D))
                .Take(BorderNeighborCount)
                .Any(candidate =>
                {
                    IFaction candidateFaction =
                        candidate.Settlement.MapFaction;

                    return candidateFaction != null
                        && candidateFaction != ownerFaction;
                });
        }
    }
}
