using HarmonyLib;
using ModifiedArmy.Garrison.Models;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;

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
            if (__instance?.Settlement == null
                || !__instance.Settlement.IsFortification
                || __instance.ItemProperties == null)
            {
                return;
            }

            int wageLimit =
                __instance.Settlement.GarrisonWagePaymentLimit;

            GarrisonWageLimitModel.CalculateWagePaymentLimit(
                __instance.Settlement.Town,
                out int desiredSize,
                out _,
                out float expectedAverageWage,
                out _,
                out bool isBorder);

            int currentGarrisonWage =
                __instance.Settlement.Town.GarrisonParty?.TotalWage ?? 0;

            string settlementType =
                $"{(isBorder ? "边境" : "腹地")}" +
                $"{(__instance.Settlement.IsCastle ? "城堡" : "城镇")}";

            string wealthLevel =
                CampaignUIHelper.GetClanWealthStatusText(
                    __instance.Settlement.OwnerClan);

            int leaderWealth =
                __instance.Settlement.OwnerClan?.Leader?.Gold ?? 0;

            BasicTooltipViewModel hint =
                new BasicTooltipViewModel(
                    () => new List<TooltipProperty>
                    {
                        new TooltipProperty(
                            "工资上限",
                            wageLimit.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.Title),
                        new TooltipProperty(
                            "据点类型",
                            settlementType,
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            "预期人数",
                            desiredSize.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            "平均工资",
                            expectedAverageWage.ToString("F2"),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            "领袖财富",
                            leaderWealth.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            "财富等级",
                            wealthLevel,
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            "当前工资",
                            currentGarrisonWage.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None)
                    });

            __instance.ItemProperties.Add(
                new SelectableFiefItemPropertyVM(
                    "工资上限",
                    wageLimit.ToString(),
                    0,
                    SelectableItemPropertyVM.PropertyType.Garrison,
                    hint,
                    false));
        }
    }
}
