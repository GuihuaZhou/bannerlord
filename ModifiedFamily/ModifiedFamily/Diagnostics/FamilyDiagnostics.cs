using System;
using TaleWorlds.Library;

namespace ModifiedFamily.Diagnostics
{
    internal static class FamilyDiagnostics
    {
        internal static bool IsSafeToRunAi { get; private set; } = true;
        internal static void Info(string message) => Debug.Print("[ModifiedFamily] " + message, 0, Debug.DebugColor.Green);
        internal static void Warn(string message) => Debug.Print("[ModifiedFamily] WARNING: " + message, 0, Debug.DebugColor.Yellow);
        internal static void Error(string message, Exception exception = null) => Debug.Print("[ModifiedFamily] ERROR: " + message + (exception == null ? "" : " " + exception), 0, Debug.DebugColor.Red);
        internal static void DisableAi(string reason) { IsSafeToRunAi = false; Error(reason); }
    }
}
