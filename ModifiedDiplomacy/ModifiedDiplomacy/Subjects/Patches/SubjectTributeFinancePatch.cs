using HarmonyLib;
using ModifiedPolitics.KingdomDiplomacy.Finance;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedDiplomacy.Subjects.Patches
{
    /// <summary>
    /// Adds subject tribute to native clan finance calculations. The
    /// calculator remains in ModifiedPolitics until subject persistence moves;
    /// this final integration point is now owned by the diplomacy module.
    /// </summary>
    [HarmonyPatch]
    public static class SubjectTributeFinancePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(DefaultClanFinanceModel),
            nameof(DefaultClanFinanceModel.CalculateClanGoldChange))]
        private static void CalculateClanGoldChangePostfix(
            Clan clan,
            ref ExplainedNumber __result)
        {
            AddIncome(clan, ref __result);
            AddExpense(clan, ref __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(DefaultClanFinanceModel),
            nameof(DefaultClanFinanceModel.CalculateClanIncome))]
        private static void CalculateClanIncomePostfix(
            Clan clan,
            ref ExplainedNumber __result)
        {
            AddIncome(clan, ref __result);
        }

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(DefaultClanFinanceModel),
            nameof(DefaultClanFinanceModel.CalculateClanExpenses))]
        private static void CalculateClanExpensesPostfix(
            Clan clan,
            ref ExplainedNumber __result)
        {
            AddExpense(clan, ref __result);
        }

        private static void AddIncome(
            Clan clan,
            ref ExplainedNumber result)
        {
            if (SubjectTributeCalculator.IsBuildingAssessment)
            {
                return;
            }

            int income = SubjectTributeCalculator.GetIncomeForClan(clan);
            if (income > 0)
            {
                result.Add(
                    income,
                    new TextObject(
                        "{=ModifiedPolitics_SubjectTributeIncome}" +
                        "Subject tribute"),
                    null);
            }
        }

        private static void AddExpense(
            Clan clan,
            ref ExplainedNumber result)
        {
            if (SubjectTributeCalculator.IsBuildingAssessment)
            {
                return;
            }

            int expense = SubjectTributeCalculator.GetExpenseForClan(clan);
            if (expense > 0)
            {
                result.Add(
                    -expense,
                    new TextObject(
                        "{=ModifiedPolitics_SubjectTributeExpense}" +
                        "Subject tribute payment"),
                    null);
            }
        }
    }
}
