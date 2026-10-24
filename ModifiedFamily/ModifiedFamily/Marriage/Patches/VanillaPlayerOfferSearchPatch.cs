// Prevents the vanilla AI from creating marriage offers for the player clan.
using System.Reflection;
using HarmonyLib;
using ModifiedFamily.Diagnostics;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using ModifiedFamily.Localization;

namespace ModifiedFamily.Marriage.Patches
{
    [HarmonyPatch]
    internal static class VanillaPlayerOfferSearchPatch
    {
        static MethodBase TargetMethod() { var method = AccessTools.Method(typeof(MarriageOfferCampaignBehavior), "DailyTickClan"); if (method == null) FamilyDiagnostics.DisableAi(FamilyLogText.Simple("MF_Log_PlayerPatchMissing", "The vanilla player-clan marriage offer entry point was not found; family AI is disabled.")); return method; }
        static bool Prefix() => false;
    }
}
