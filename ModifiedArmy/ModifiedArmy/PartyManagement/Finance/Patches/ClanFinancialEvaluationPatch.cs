using HarmonyLib;
using ModifiedArmy.PartyManagement.Finance.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedArmy.PartyManagement.Finance.Patches
{
    /// <summary>
    /// Replaces native AI war-party wage-limit evaluation with limits scaled
    /// to the mod's troop wages. Garrison limits remain independent.
    /// </summary>
    [HarmonyPatch(
        typeof(ClanVariablesCampaignBehavior),
        "MakeClanFinancialEvaluation")]
    public static class ClanFinancialEvaluationPatch
    {
        /// <summary>
        /// Replaces only the native wage-limit assignment pass. Actual wages
        /// remain owned by the registered PartyWageModel implementation.
        /// </summary>
        private static bool Prefix(Clan clan)
        {
            ClanFinancialEvaluationService.EvaluateWarPartyWageLimits(clan);
            return false;
        }
    }
}
