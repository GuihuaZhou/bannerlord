using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.KingdomFief.Models
{
    /// <summary>
    /// Contains the native daily income and expense components for one fief.
    /// Calculations never apply withdrawals or mutate accumulated revenue.
    /// </summary>
    public sealed class SettlementFinanceBreakdown
    {
        public int Taxes { get; set; }

        public int Tariffs { get; set; }

        public int GarrisonWages { get; set; }

        public int ProjectIncome { get; set; }

        public int Profit { get; set; }

        public List<VillageIncomeEntry> VillageIncomes { get; } =
            new List<VillageIncomeEntry>();

        public static SettlementFinanceBreakdown Calculate(
            Settlement settlement)
        {
            SettlementFinanceBreakdown result =
                new SettlementFinanceBreakdown();
            Town town = settlement?.Town;
            Clan ownerClan = settlement?.OwnerClan;

            if (town == null || ownerClan == null)
            {
                return result;
            }

            result.Taxes = (int)Campaign.Current.Models
                .SettlementTaxModel
                .CalculateTownTax(town, false)
                .ResultNumber;

            result.Tariffs = (int)Campaign.Current.Models
                .ClanFinanceModel
                .CalculateTownIncomeFromTariffs(
                    ownerClan,
                    town,
                    false)
                .ResultNumber;

            if (town.GarrisonParty?.IsActive == true)
            {
                result.GarrisonWages = town.GarrisonParty.TotalWage;
            }

            foreach (Village village in town.Villages)
            {
                int income = Campaign.Current.Models
                    .ClanFinanceModel
                    .CalculateVillageIncome(
                        ownerClan,
                        village,
                        false);

                result.VillageIncomes.Add(
                    new VillageIncomeEntry(village, income));
            }

            result.ProjectIncome = Campaign.Current.Models
                .ClanFinanceModel
                .CalculateTownIncomeFromProjects(town);

            result.Profit = result.Taxes
                + result.Tariffs
                - result.GarrisonWages
                + result.ProjectIncome;

            foreach (VillageIncomeEntry villageIncome in
                result.VillageIncomes)
            {
                result.Profit += villageIncome.Income;
            }

            return result;
        }
    }

    /// <summary>
    /// Associates a bound village with its calculated daily income.
    /// </summary>
    public sealed class VillageIncomeEntry
    {
        public VillageIncomeEntry(Village village, int income)
        {
            Village = village;
            Income = income;
        }

        public Village Village { get; }

        public int Income { get; }
    }
}
