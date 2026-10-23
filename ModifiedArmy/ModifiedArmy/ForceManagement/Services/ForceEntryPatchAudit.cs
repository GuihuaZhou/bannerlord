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
            if (!ManagedPartyPolicy.IsEnabled()) return;
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
                    patches.Prefixes.Concat(patches.Postfixes)
                        .OrderBy(item => item.owner)
                        .ThenByDescending(item => item.priority)
                        .Select(item => FormatPatch(item.owner, item.priority)));
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
        private static string FormatPatch(string owner, int priority)
        {
            TextObject item = GameTexts.FindText(
                "str_modifiedarmy_force_patch_item");
            item.SetTextVariable("OWNER", owner ?? "Unknown owner");
            item.SetTextVariable("PRIORITY", priority);
            return item.ToString();
        }
    }
}
