using System;
using System.Linq;
using System.Reflection;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using CampaignArmy = TaleWorlds.CampaignSystem.Army;

namespace ModifiedArmy.Army.Services
{
    public static class ArmyDisbandService
    {
        public static void DisbandLedArmy(Hero hero)
        {
            MobileParty party = hero?.PartyBelongedTo;
            CampaignArmy army = party?.Army;
            if (army == null || army.LeaderParty != party) return;

            MethodInfo action = typeof(DisbandArmyAction)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name.StartsWith("Apply", StringComparison.Ordinal))
                .FirstOrDefault(method => method.GetParameters().Length > 0 &&
                    method.GetParameters()[0].ParameterType == typeof(CampaignArmy));
            if (action == null)
            {
                ModLogger.FileError("[ModifiedArmy][Army] phase=DISBAND_REJECTED " +
                    "reasonCode=CompatibleActionMissing leader=" +
                    (hero?.Name?.ToString() ?? "none") + " leaderId=" +
                    (hero?.StringId ?? "none") + ".");
                return;
            }

            ParameterInfo[] parameters = action.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = army;
            for (int i = 1; i < parameters.Length; i++)
                arguments[i] = parameters[i].HasDefaultValue
                    ? parameters[i].DefaultValue
                    : parameters[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(parameters[i].ParameterType)
                        : null;
            action.Invoke(null, arguments);
            ModLogger.FileInfo("[ModifiedArmy][Army] phase=DISBAND_APPLIED leader=" +
                (hero?.Name?.ToString() ?? "none") + " leaderId=" +
                (hero?.StringId ?? "none") + " action=" + action.Name + ".");
        }
    }
}
