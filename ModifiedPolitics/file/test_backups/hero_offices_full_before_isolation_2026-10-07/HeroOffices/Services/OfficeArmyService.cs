using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedPolitics.HeroOffices.Services
{
    /// <summary>
    /// Disbands an army led by a marshal whose office has ended without depending on one game-version overload.
    /// </summary>
    internal static class OfficeArmyService
    {
        public static void DisbandLedArmy(Hero hero)
        {
            MobileParty party = hero?.PartyBelongedTo;
            Army army = party?.Army;
            if (army == null || army.LeaderParty != party)
                return;

            // Action overload names vary by Bannerlord version. Select a public one that accepts the army.
            MethodInfo action = typeof(DisbandArmyAction)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name.StartsWith("Apply", StringComparison.Ordinal))
                .FirstOrDefault(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length > 0 && parameters[0].ParameterType == typeof(Army);
                });

            if (action == null)
                return;

            ParameterInfo[] actionParameters = action.GetParameters();
            object[] arguments = new object[actionParameters.Length];
            arguments[0] = army;
            for (int i = 1; i < actionParameters.Length; i++)
            {
                arguments[i] = actionParameters[i].HasDefaultValue
                    ? actionParameters[i].DefaultValue
                    : actionParameters[i].ParameterType.IsValueType
                        ? Activator.CreateInstance(actionParameters[i].ParameterType)
                        : null;
            }

            action.Invoke(null, arguments);
        }
    }
}
