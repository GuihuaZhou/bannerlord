using HarmonyLib;
using ModifiedArmy.Garrison.Presentation;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// Adds the current garrison wage payment limit after the vanilla fief
    /// property list has been rebuilt. UpdateProperties clears the list first,
    /// so this postfix produces exactly one additional row on every refresh.
    /// </summary>
    [HarmonyPatch(
        typeof(KingdomSettlementItemVM),
        "UpdateProperties")]
    public static class KingdomSettlementItemVMPatch
    {
        private static void Postfix(KingdomSettlementItemVM __instance)
        {
            GarrisonSettlementVmPresenter.AppendProperties(__instance);
        }
    }
}
