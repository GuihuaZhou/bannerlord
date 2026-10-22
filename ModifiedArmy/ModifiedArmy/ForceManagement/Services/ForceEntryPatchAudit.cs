using HarmonyLib;
using ModifiedArmy.Tool;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

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
                ? "无补丁"
                : string.Join(
                    ", ",
                    patches.Prefixes.Concat(patches.Postfixes)
                        .OrderBy(item => item.owner)
                        .ThenByDescending(item => item.priority)
                        .Select(item => string.Format(
                            "{0}(优先级{1})",
                            item.owner,
                            item.priority)));
            ModLogger.FileNotice(string.Format(
                "[兵力入口检查] {0}.{1} 当前补丁：{2}。",
                method?.DeclaringType?.Name ?? "未知类型",
                method?.Name ?? "未知方法",
                string.IsNullOrEmpty(owners) ? "无补丁" : owners));
        }
    }
}
