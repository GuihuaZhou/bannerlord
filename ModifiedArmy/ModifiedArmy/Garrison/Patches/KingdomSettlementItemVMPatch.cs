using HarmonyLib;
using ModifiedArmy.Garrison.Models;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;

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

            TextObject settlementTypeText =
                new TextObject(
                    "{=ModifiedArmy_GarrisonSettlementTypeValue}" +
                    "{LOCATION} {TYPE}");

            settlementTypeText.SetTextVariable(
                "LOCATION",
                new TextObject(
                    isBorder
                        ? "{=ModifiedArmy_GarrisonBorder}Border"
                        : "{=ModifiedArmy_GarrisonInterior}Interior"));

            settlementTypeText.SetTextVariable(
                "TYPE",
                new TextObject(
                    __instance.Settlement.IsCastle
                        ? "{=ModifiedArmy_GarrisonCastle}Castle"
                        : "{=ModifiedArmy_GarrisonTown}Town"));

            string settlementType = settlementTypeText.ToString();

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
                            GetLocalizedText(
                                "{=ModifiedArmy_GarrisonWageLimit}Wage Limit"),
                            wageLimit.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.Title),
                        new TooltipProperty(
                            GetLocalizedText(
                                "{=ModifiedArmy_GarrisonSettlementType}Settlement Type"),
                            settlementType,
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            GetLocalizedText(
                                "{=ModifiedArmy_GarrisonMaximumTroops}Maximum Troops"),
                            desiredSize.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            GetLocalizedText(
                                "{=ModifiedArmy_GarrisonAverageWage}Average Wage"),
                            expectedAverageWage.ToString("F2"),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            GetLocalizedText(
                                "{=ModifiedArmy_GarrisonLeaderWealth}Leader Wealth"),
                            leaderWealth.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            GetLocalizedText(
                                "{=ModifiedArmy_GarrisonWealthLevel}Wealth Level"),
                            wealthLevel,
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None),
                        new TooltipProperty(
                            GetLocalizedText(
                                "{=ModifiedArmy_GarrisonCurrentWage}Current Wage"),
                            currentGarrisonWage.ToString(),
                            0,
                            false,
                            TooltipProperty.TooltipPropertyFlags.None)
                    });

            __instance.ItemProperties.Add(
                new SelectableFiefItemPropertyVM(
                    GetLocalizedText(
                        "{=ModifiedArmy_GarrisonWageLimit}Wage Limit"),
                    wageLimit.ToString(),
                    0,
                    SelectableItemPropertyVM.PropertyType.Garrison,
                    hint,
                    false));
        }

        private static string GetLocalizedText(string taggedText)
        {
            return new TextObject(taggedText).ToString();
        }
    }
}
