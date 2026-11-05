using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// Preserves the native mercenary contract calculation and doubles only
    /// the player's per-influence award, including the displayed offer rate.
    /// </summary>
    public sealed class NewMinorFactionsModel : DefaultMinorFactionsModel
    {
        private const int PlayerMercenaryAwardMultiplier = 2;

        public override int GetMercenaryAwardFactorToJoinKingdom(
            Clan mercenaryClan,
            Kingdom kingdom,
            bool neededAmountForClanToJoinCalculation = false)
        {
            int nativeAward = base.GetMercenaryAwardFactorToJoinKingdom(
                mercenaryClan,
                kingdom,
                neededAmountForClanToJoinCalculation);
            return mercenaryClan == Clan.PlayerClan
                ? checked(nativeAward * PlayerMercenaryAwardMultiplier)
                : nativeAward;
        }
    }
}
