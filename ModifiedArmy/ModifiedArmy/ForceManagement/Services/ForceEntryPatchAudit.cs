using HarmonyLib;
using ModifiedArmy.Tool;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>Writes the active owners and priorities of contested force entry patches.</summary>
    public static class ForceEntryPatchAudit
    {
        /// <summary>Audits the two native methods blocked by the atomic switch.</summary>
        public static void LogActivePatches()
        {
            bool enabled = ManagedPartyPolicy.IsEnabled();
            // One startup line makes the rollback boundary explicit without
            // producing per-party noise throughout the campaign.
            TextObject mode = GameTexts.FindText(
                "str_modifiedarmy_force_entry_mode");
            mode.SetTextVariable(
                "MODE",
                GameTexts.FindText(enabled
                    ? "str_modifiedarmy_force_entry_mode_unified"
                    : "str_modifiedarmy_force_entry_mode_native"));
            ModLogger.FileNotice(mode.ToString());
            if (!enabled) return;
            LogMethod(AccessTools.Method(
                typeof(GarrisonTroopsCampaignBehavior),
                "OnSettlementEntered"));
            LogMethod(AccessTools.Method(
                typeof(RecruitmentCampaignBehavior),
                "CheckRecruiting"));
        }

        /// <summary>Logs every prefix/postfix owner without executing any patch.</summary>
        private static void LogMethod(MethodBase method)
        {
            Patches patches = method == null ? null : Harmony.GetPatchInfo(method);
            string owners = patches == null
                ? string.Empty
                : string.Join(
                    ", ",
                    patches.Prefixes
                        .Select(item => FormatPatch("Prefix", item))
                        .Concat(patches.Postfixes.Select(item =>
                            FormatPatch("Postfix", item)))
                        .OrderBy(item => item));
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_force_patch_audit");
            message.SetTextVariable(
                "TYPE_NAME", method?.DeclaringType?.Name ?? "Unknown type");
            message.SetTextVariable("METHOD_NAME", method?.Name ?? "Unknown method");
            message.SetTextVariable(
                "PATCHES",
                string.IsNullOrEmpty(owners)
                    ? GameTexts.FindText("str_modifiedarmy_force_patch_none")
                    : new TextObject(owners));
            ModLogger.FileNotice(message.ToString());
        }

        /// <summary>Formats one patch owner and priority through localization.</summary>
        private static string FormatPatch(string type, Patch patch)
        {
            MethodInfo method = patch?.PatchMethod;
            string methodName = method == null
                ? "Unknown method"
                : $"{method.DeclaringType?.FullName ?? "Unknown type"}.{method.Name}";
            return $"{type}:{patch?.owner ?? "Unknown owner"} " +
                $"(priority {patch?.priority ?? 0}, method {methodName})";
        }
    }
}
