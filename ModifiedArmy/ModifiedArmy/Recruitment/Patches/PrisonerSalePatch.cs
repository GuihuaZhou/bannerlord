using HarmonyLib;
using ModifiedArmy.Recruitment.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Patches
{
    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior),
        "OnSettlementEntered")]
    public static class PrisonerSaleOnSettlementEnteredPatch
    {
        public static bool Prefix(MobileParty mobileParty,
            Settlement settlement, Hero hero)
        {
            PrisonerSaleService.HandleSettlementEntry(
                mobileParty, settlement);
            return false;
        }
    }

    [HarmonyPatch(typeof(PartiesSellPrisonerCampaignBehavior),
        "DailyTickSettlement")]
    public static class PrisonerSaleDailySettlementPatch
    {
        public static bool Prefix(Settlement settlement) => false;
    }
}
