using HarmonyLib;
using ModifiedArmy.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Patches
{
    /// <summary>
    /// Disables native notable-slot recruitment. The mod's garrison behavior
    /// now consumes daily professional production through the unified
    /// composition and clan-budget model.
    /// </summary>
    [HarmonyPatch(
        typeof(GarrisonRecruitmentCampaignBehavior),
        "TickAutoRecruitmentGarrisonChange")]
    public static class GarrisonRecruitmentPatch
    {
        private static bool Prefix()
        {
            return false;
        }
    }

    /// <summary>
    /// Replaces the native notable-slot forecast with professional production and
    /// prisoner forecast used by the actual daily recruitment behavior.
    /// </summary>
    [HarmonyPatch(
        typeof(GarrisonRecruitmentCampaignBehavior),
        "GetGarrisonChangeExplainedNumber")]
    public static class GarrisonRecruitmentPredictionPatch
    {
        private static void Postfix(Town town, ref ExplainedNumber __result)
        {
            if (town?.Settlement == null)
            {
                return;
            }

            ExplainedNumber replacement = Campaign.Current.Models
                .SettlementGarrisonModel.CalculateBaseGarrisonChange(
                    town.Settlement,
                    true);
            int recruitment = GarrisonRecruitFromPrisonersBehavior
                .GetPredictedDailyRecruitmentCount(town);
            if (recruitment > 0)
            {
                replacement.Add(
                    recruitment,
                    new TextObject("{=Uzsnek6O}Auto Recruitment"));
            }

            __result = replacement;
        }
    }
}
