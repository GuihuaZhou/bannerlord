using HarmonyLib;
using ModifiedArmy.SettlementInspection.Behaviors;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement;

namespace ModifiedArmy.SettlementInspection.Patches
{
    /// <summary>
    /// Rejects building, governor, and reserve changes while inspecting the
    /// management panel of a settlement owned by another clan.
    /// </summary>
    internal static class SettlementManagementReadOnlyPatch
    {
        [HarmonyPatch]
        private static class BuildingCommandPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                string[] commandNames =
                {
                    nameof(SettlementBuildingProjectVM.ExecuteAddRemoveToQueue),
                    nameof(SettlementBuildingProjectVM.ExecuteSetAsActiveDevelopment),
                    nameof(SettlementBuildingProjectVM.ExecuteSetAsCurrent),
                    nameof(SettlementBuildingProjectVM.ExecuteResetCurrent),
                    nameof(SettlementBuildingProjectVM.ExecuteToggleSelected)
                };

                foreach (string commandName in commandNames)
                {
                    yield return AccessTools.Method(
                        typeof(SettlementBuildingProjectVM),
                        commandName);

                    // Daily activities use a separate ViewModel subclass with
                    // its own overrides of the same project commands.
                    yield return AccessTools.Method(
                        typeof(SettlementDailyProjectVM),
                        commandName);
                }

                yield return AccessTools.Method(
                    typeof(SettlementProjectSelectionVM),
                    nameof(SettlementProjectSelectionVM.ExecuteChangeQueueOrder));
            }

            private static bool Prefix()
            {
                return !SettlementManagementInspectionBehavior
                    .IsReadOnlyPanelOpen;
            }
        }

        [HarmonyPatch(
            typeof(TownManagementVM),
            "OnProjectSelectionDone")]
        private static class ProjectCommitPatch
        {
            private static bool Prefix()
            {
                return !SettlementManagementInspectionBehavior
                    .IsReadOnlyPanelOpen;
            }
        }

        [HarmonyPatch(
            typeof(TownManagementVM),
            "OnGovernorSelectionDone")]
        private static class GovernorCommitPatch
        {
            private static bool Prefix()
            {
                return !SettlementManagementInspectionBehavior
                    .IsReadOnlyPanelOpen;
            }
        }

        [HarmonyPatch(
            typeof(TownManagementReserveControlVM),
            nameof(TownManagementReserveControlVM.ExecuteConfirm))]
        private static class ReserveCommitPatch
        {
            private static bool Prefix()
            {
                return !SettlementManagementInspectionBehavior
                    .IsReadOnlyPanelOpen;
            }
        }

        [HarmonyPatch(
            typeof(TownManagementVM),
            nameof(TownManagementVM.ExecuteDone))]
        private static class ClosePanelPatch
        {
            private static void Postfix()
            {
                SettlementManagementInspectionBehavior
                    .CloseReadOnlyPanel();
            }
        }
    }
}
