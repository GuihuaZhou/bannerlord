using HarmonyLib;
using ModifiedArmy.ForceManagement.Services;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace ModifiedArmy.ForceManagement.Patches
{
    /// <summary>
    /// Filters only the native call-to-army candidate list. A protected
    /// carrier also receives an empty list when evaluating creation of its own
    /// Army, without suppressing the rest of AiMilitaryBehavior's decisions.
    /// </summary>
    [HarmonyPatch(
        typeof(DefaultArmyManagementCalculationModel),
        nameof(DefaultArmyManagementCalculationModel
            .CanLordCreateArmy))]
    public static class ArmyReinforcementProtectionPatch
    {
        private static void Postfix(
            MobileParty mobileParty,
            ref MBList<MobileParty> possibleArmyMembers,
            ref bool __result)
        {
            var protection = ForceManagementFacade.Current
                .ArmyMissionProtection;
            if (protection.IsProtected(mobileParty))
            {
                possibleArmyMembers?.Clear();
                __result = false;
                return;
            }
            if (possibleArmyMembers == null) return;
            for (int index = possibleArmyMembers.Count - 1;
                index >= 0;
                index--)
            {
                if (protection.IsProtected(possibleArmyMembers[index]))
                {
                    possibleArmyMembers.RemoveAt(index);
                }
            }
            if (possibleArmyMembers.Count == 0)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(
        typeof(DefaultArmyManagementCalculationModel),
        nameof(DefaultArmyManagementCalculationModel.CheckPartyEligibility))]
    public static class ArmyEligibilityReinforcementProtectionPatch
    {
        private static void Postfix(
            MobileParty mobileParty,
            ref bool __result)
        {
            if (ForceManagementFacade.Current.ArmyMissionProtection
                .IsProtected(mobileParty))
            {
                __result = false;
            }
        }
    }
}
