using System.Reflection;
using HarmonyLib;
using ModifiedFamily.Diagnostics;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace ModifiedFamily.Patches
{
    [HarmonyPatch]
    internal static class VanillaNpcMarriageSearchPatch
    {
        static MethodBase TargetMethod() { var method = AccessTools.Method(typeof(RomanceCampaignBehavior), "CheckNpcMarriages"); if (method == null) FamilyDiagnostics.DisableAi("CheckNpcMarriages was not found; refusing partial AI takeover."); return method; }
        static bool Prefix() => false;
    }
}
