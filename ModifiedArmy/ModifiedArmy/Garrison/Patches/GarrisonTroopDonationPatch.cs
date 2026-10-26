using HarmonyLib;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Garrison.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Patches
{
    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior),
        "OnSettlementEntered")]
    public static class GarrisonTroopDonationPatch
    {
        public static bool Prefix(MobileParty mobileParty,
            Settlement settlement, Hero hero) =>
            GarrisonDonationAdapter.ShouldRunNativeEntry(
                mobileParty, settlement);
    }

    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior),
        "CalculateSettlementIdealPartySizeWithEffects")]
    public static class GarrisonExchangeIdealSizePatch
    {
        private static void Postfix(Settlement settlement, ref int __result)
        {
            __result = GarrisonTransferPolicyModel
                .GetExchangeIdealGarrisonSize(settlement, __result);
        }
    }

    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior),
        "LeaveTroopsToGarrison")]
    public static class GarrisonPartyPriorityPatch
    {
        private static bool Prefix(MobileParty mobileParty,
            ref int numberOfTroopsToLeave,
            out GarrisonDonationTransferState __state) =>
            GarrisonDonationAdapter.PrepareDonation(mobileParty,
                ref numberOfTroopsToLeave, out __state);

        private static void Postfix(GarrisonDonationTransferState __state)
        {
            GarrisonDonationAdapter.ReconcileDonation(__state);
        }
    }

    [HarmonyPatch(typeof(GarrisonTroopsCampaignBehavior),
        "TakeTroopsFromGarrison")]
    public static class PlayerKingdomGarrisonWithdrawalLogPatch
    {
        private static bool Prefix(MobileParty mobileParty,
            Settlement settlement, int numberOfTroopsToTake,
            out int __state) =>
            GarrisonDonationAdapter.ExecuteWithdrawal(mobileParty,
                settlement, numberOfTroopsToTake, out __state);

        private static void Postfix(MobileParty mobileParty,
            Settlement settlement, int __state)
        {
            GarrisonDonationAdapter.LogWithdrawal(
                mobileParty, settlement, __state);
        }
    }
}
