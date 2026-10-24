// Suppresses only vanilla random NPC matchmaking; romance conversations remain registered.
using System.Reflection;
using HarmonyLib;
using ModifiedFamily.Diagnostics;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using ModifiedFamily.Localization;

namespace ModifiedFamily.Marriage.Patches
{
    [HarmonyPatch]
    internal static class VanillaNpcMarriageSearchPatch
    {
        static MethodBase TargetMethod() { var method = AccessTools.Method(typeof(RomanceCampaignBehavior), "CheckNpcMarriages"); if (method == null) FamilyDiagnostics.DisableAi(FamilyLogText.Simple("MF_Log_NpcPatchMissing", "The vanilla NPC marriage entry point was not found; family AI is disabled.")); return method; }
        static bool Prefix() => false;
    }
}
