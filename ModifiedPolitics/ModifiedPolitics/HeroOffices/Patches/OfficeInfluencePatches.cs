using HarmonyLib;
using ModifiedPolitics.HeroOffices.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace ModifiedPolitics.HeroOffices.Patches
{
    /// <summary>
    /// Adds office income to the native influence model so daily settlement and UI use the same value.
    /// </summary>
    [HarmonyPatch(typeof(DefaultClanPoliticsModel), nameof(DefaultClanPoliticsModel.CalculateInfluenceChange))]
    internal static class OfficeInfluencePatches
    {
        private static readonly TextObject OfficeInfluenceText =
            new TextObject("{=MP_OfficeInfluenceEffect}Kingdom offices");

        [HarmonyPostfix]
        private static void AddOfficeInfluence(Clan clan, ref ExplainedNumber __result)
        {
            float influence = OfficeInfluenceModel.GetDailyInfluence(clan);
            if (influence > 0f)
                __result.Add(influence, OfficeInfluenceText);
        }
    }
}
