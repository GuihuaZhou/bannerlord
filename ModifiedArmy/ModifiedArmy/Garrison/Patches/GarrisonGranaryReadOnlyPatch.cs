using HarmonyLib;
using ModifiedArmy.Garrison.Behaviors;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Inventory;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// Prevents inventory commands while another clan's granary is open.
    /// Transfer validation blocks normal UI interaction, while command patches
    /// provide a second guard against alternate or queued transfer paths.
    /// </summary>
    internal static class GarrisonGranaryReadOnlyPatch
    {
        [HarmonyPatch(
            typeof(InventoryLogic),
            "TransferIsMovementValid")]
        private static class TransferValidationPatch
        {
            private static bool Prefix(ref bool __result)
            {
                if (!GarrisonGranaryMenuBehavior.IsReadOnlyGranaryOpen)
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(
            typeof(InventoryLogic),
            nameof(InventoryLogic.AddTransferCommand),
            typeof(TransferCommand))]
        private static class AddTransferCommandPatch
        {
            private static bool Prefix()
            {
                return !GarrisonGranaryMenuBehavior.IsReadOnlyGranaryOpen;
            }
        }

        [HarmonyPatch(
            typeof(InventoryLogic),
            nameof(InventoryLogic.AddTransferCommands),
            typeof(IEnumerable<TransferCommand>))]
        private static class AddTransferCommandsPatch
        {
            private static bool Prefix()
            {
                return !GarrisonGranaryMenuBehavior.IsReadOnlyGranaryOpen;
            }
        }

        [HarmonyPatch(
            typeof(InventoryLogic),
            nameof(InventoryLogic.TransferOne))]
        private static class TransferOnePatch
        {
            private static bool Prefix()
            {
                return !GarrisonGranaryMenuBehavior.IsReadOnlyGranaryOpen;
            }
        }

        [HarmonyPatch(
            typeof(InventoryLogic),
            nameof(InventoryLogic.DonateItem))]
        private static class DonateItemPatch
        {
            private static bool Prefix()
            {
                return !GarrisonGranaryMenuBehavior.IsReadOnlyGranaryOpen;
            }
        }

        [HarmonyPatch(
            typeof(InventoryLogic),
            nameof(InventoryLogic.SlaughterItem))]
        private static class SlaughterItemPatch
        {
            private static bool Prefix()
            {
                return !GarrisonGranaryMenuBehavior.IsReadOnlyGranaryOpen;
            }
        }

        [HarmonyPatch(
            typeof(InventoryLogic),
            nameof(InventoryLogic.DoneLogic))]
        private static class DoneLogicPatch
        {
            private static void Postfix()
            {
                GarrisonGranaryMenuBehavior.CloseGranaryView();
            }
        }

    }
}
