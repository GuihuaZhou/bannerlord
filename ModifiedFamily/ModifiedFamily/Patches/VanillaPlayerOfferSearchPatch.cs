using System.Reflection;
using HarmonyLib;
using ModifiedFamily.Diagnostics;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedFamily.Patches
{
    [HarmonyPatch]
    internal static class VanillaPlayerOfferSearchPatch
    {
        static MethodBase TargetMethod() { var method = AccessTools.Method(typeof(MarriageOfferCampaignBehavior), "DailyTickClan"); if (method == null) FamilyDiagnostics.DisableAi("MarriageOfferCampaignBehavior.DailyTickClan was not found; refusing partial AI takeover."); return method; }
        static bool Prefix() => false;
    }
}
