using HarmonyLib;
using ModifiedArmy.Garrison.Behaviors;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// Makes an allied-clan garrison inspection screen fully read-only. The
    /// transferability result locks normal UI controls, while AddCommand is a
    /// second guard against bulk actions and queued commands.
    /// </summary>
    internal static class GarrisonInspectionReadOnlyPatch
    {
        [HarmonyPatch(
            typeof(PartyScreenLogic),
            nameof(PartyScreenLogic.IsTroopTransferable))]
        private static class TransferabilityPatch
        {
            private static void Postfix(ref bool __result)
            {
                if (GarrisonInspectionMenuBehavior
                    .IsReadOnlyGarrisonOpen)
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(
            typeof(PartyScreenLogic),
            nameof(PartyScreenLogic.AddCommand))]
        private static class CommandPatch
        {
            private static bool Prefix(
                PartyScreenLogic.PartyCommand command)
            {
                if (!GarrisonInspectionMenuBehavior
                    .IsReadOnlyGarrisonOpen)
                {
                    return true;
                }

                // Sorting and changing the visual order do not alter either
                // party roster. Every other command is rejected.
                return command.Code ==
                        PartyScreenLogic.PartyCommandCode.SortTroops ||
                    command.Code ==
                        PartyScreenLogic.PartyCommandCode.ShiftTroop;
            }
        }

        [HarmonyPatch(
            typeof(PartyScreenLogic),
            nameof(PartyScreenLogic.DoneLogic))]
        private static class DonePatch
        {
            private static void Postfix()
            {
                GarrisonInspectionMenuBehavior.CloseGarrisonView();
            }
        }
    }
}
