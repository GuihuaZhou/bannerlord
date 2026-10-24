using System;
using TaleWorlds.Library;
using ModifiedFamily.Tool;

namespace ModifiedFamily.Diagnostics
{
    internal static class FamilyDiagnostics
    {
        internal static bool IsSafeToRunAi { get; private set; } = true;
        internal static void DebugLog(string message) { Debug.Print("[ModifiedFamily] " + message, 0, Debug.DebugColor.White); ModLogger.Debug(message); }
        internal static void Info(string message) { Debug.Print("[ModifiedFamily] " + message, 0, Debug.DebugColor.Green); ModLogger.Info(message); }
        internal static void Notice(string message) { Debug.Print("[ModifiedFamily] " + message, 0, Debug.DebugColor.Green); ModLogger.Notice(message); }
        internal static void Warn(string message) { Debug.Print("[ModifiedFamily] WARNING: " + message, 0, Debug.DebugColor.Yellow); ModLogger.Warn(message); }
        internal static void Error(string message, Exception exception = null) { string detail = message + (exception == null ? "" : " " + exception); Debug.Print("[ModifiedFamily] ERROR: " + detail, 0, Debug.DebugColor.Red); ModLogger.Error(detail); }
        internal static void DisableAi(string reason) { IsSafeToRunAi = false; Error(reason); }
    }
}
