// Builds a detached, read-only manpower presentation snapshot. Authoritative
// population, production, fief roster, and Hearth state stays in existing
// domain owners; this adapter performs no refresh, production, or persistence.
using ModifiedArmy.Models.Fief;
using ModifiedArmy.common;
using ModifiedArmy.Population.Contracts;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Presentation
{
    internal static class ManpowerOverviewDataProvider
    {
        /// <summary>
        /// Reads the same forecast and completed-settlement records used by the
        /// former text menu. Failure is isolated to the affected rows/section.
        /// </summary>
        internal static MBBindingList<ManpowerSectionVM> CreateSections(
            Settlement settlement)
        {
            var sections = new MBBindingList<ManpowerSectionVM>();
            SettlementManpowerProductionBehavior pools = Campaign.Current?
                .GetCampaignBehavior<SettlementManpowerProductionBehavior>();
            RecruitmentProductionForecast forecast = null;
            SettlementManpowerProductionData productionData = null;
            try
            {
                forecast = pools?.GetProductionForecast(settlement);
                productionData = pools?.GetProductionData(settlement);
            }
            catch (Exception exception)
            {
                LogUnavailable("forecast", settlement, exception);
            }

            sections.Add(CreateProfessionalSection(
                settlement, forecast, productionData));
            sections.Add(CreateFiefSection(forecast, productionData));
            sections.Add(CreateVillageSection(settlement));
            return sections;
        }

        private static ManpowerSectionVM CreateProfessionalSection(
            Settlement settlement, RecruitmentProductionForecast forecast,
            SettlementManpowerProductionData productionData)
        {
            var section = new ManpowerSectionVM(Text(
                "ModifiedArmy_Manpower_Professional", "Professional troops"));
            section.Rows.Add(new ManpowerRowVM(Text(
                "ModifiedArmy_Manpower_Available", "Available manpower"),
                CreateAvailableManpowerText(settlement)));
            if (forecast == null)
            {
                AddUnavailable(section, Text("ModifiedArmy_Manpower_Forecast",
                    "Production forecast"));
                return section;
            }

            section.Rows.Add(Row("ModifiedArmy_Manpower_TrainingBase",
                "Base training", Rate(forecast.ProfessionalTrainingBase)));
            TextObject barracks = new TextObject(
                "{=ModifiedArmy_Manpower_BarracksBonus}" +
                "Barracks bonus (level {LEVEL})");
            barracks.SetTextVariable("LEVEL", forecast.BarracksLevel);
            section.Rows.Add(new ManpowerRowVM(barracks.ToString(),
                "+" + Rate(forecast.ProfessionalTrainingStep *
                    forecast.BarracksLevel)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_CultureEfficiency",
                "Culture efficiency", Percent(forecast.CultureEfficiency)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_OtherBonus",
                "Other bonuses", Percent(forecast.ExternalTrainingBonusRate)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_TrainingCapacity",
                "Training capacity",
                Rate(forecast.ProfessionalTrainingCapacity)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_Expected",
                "Expected replenishment",
                Rate(forecast.ProfessionalAllocation)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_Actual",
                "Actual replenishment (last settlement)", Actual(
                    productionData?.HasProfessionalRecord == true,
                    productionData?.LastProfessionalRecruited ?? 0)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_AutoRecruit",
                "Automatic recruitment", Text(
                    forecast.ProfessionalRecruitmentEnabled
                        ? "ModifiedArmy_Common_Enabled"
                        : "ModifiedArmy_Common_Disabled",
                    forecast.ProfessionalRecruitmentEnabled
                        ? "Enabled" : "Disabled")));
            return section;
        }

        private static ManpowerSectionVM CreateFiefSection(
            RecruitmentProductionForecast forecast,
            SettlementManpowerProductionData productionData)
        {
            var section = new ManpowerSectionVM(Text(
                "ModifiedArmy_Manpower_Fief", "Fief troops"));
            if (forecast == null)
            {
                AddUnavailable(section, Text("ModifiedArmy_Manpower_Forecast",
                    "Production forecast"));
                return section;
            }
            section.Rows.Add(Row("ModifiedArmy_Manpower_FiefCurrent",
                "Current fief troops (all states)",
                People(forecast.FiefCurrent)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_FiefBase",
                "Base replenishment", Rate(forecast.FiefBaseDailyRate)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_Expected",
                "Expected replenishment", Rate(forecast.FiefAllocation)));
            section.Rows.Add(Row("ModifiedArmy_Manpower_Actual",
                "Actual replenishment (last settlement)", Actual(
                    productionData?.HasFiefRecord == true,
                    productionData?.LastFiefReplenished ?? 0)));
            return section;
        }

        private static ManpowerSectionVM CreateVillageSection(
            Settlement settlement)
        {
            var section = new ManpowerSectionVM(Text(
                "ModifiedArmy_Manpower_Villages", "Village support"));
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null || !manager.TryGetReadyFiefData(
                    settlement, out FiefPartyData fiefData))
            {
                AddUnavailable(section, Text("ModifiedArmy_Manpower_Supply",
                    "Support Hearth"));
                return section;
            }

            try
            {
                CulturalMilitaryDoctrine doctrine = MilitaryConfigManager
                    .Instance.RequireActiveConfig().GetDoctrine(settlement);
                FiefPartyTemplate template = fiefData.GetFiefPartyTemplate();
                foreach (Village village in (settlement.BoundVillages ??
                    new List<Village>()).Where(x => x?.Settlement != null)
                    .OrderBy(x => x.Settlement.StringId))
                {
                    string name = village.Settlement.Name.ToString();
                    if (fiefData.Villages == null ||
                        !fiefData.Villages.TryGetValue(village.Settlement,
                            out VillageFiefData data) || data == null)
                    {
                        section.Rows.Add(new ManpowerRowVM(name, Unavailable(),
                            true));
                        continue;
                    }
                    int limit = data.GetFeudalBudgetHearth(doctrine);
                    int occupied = data.GetOccupiedSupplyHearth(
                        template, doctrine);
                    section.Rows.Add(new ManpowerRowVM(name,
                        occupied + " / " + limit));
                }
                if (section.Rows.Count == 0)
                    AddUnavailable(section, Text("ModifiedArmy_Manpower_Supply",
                        "Support Hearth"));
            }
            catch (Exception exception)
            {
                LogUnavailable("villageSupply", settlement, exception);
                section.Rows.Clear();
                AddUnavailable(section, Text("ModifiedArmy_Manpower_Supply",
                    "Support Hearth"));
            }
            return section;
        }

        private static string CreateAvailableManpowerText(Settlement settlement)
        {
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            if (population == null) return Unavailable();
            try
            {
                float capacity = Math.Max(0f, population.GetHearth(settlement) -
                    population.GetNaturalCapacity(settlement) *
                    MilitaryConfigManager.Instance.RequireActiveConfig()
                        .HearthEconomy.ProfessionalReserveRatio);
                if (float.IsNaN(capacity) || float.IsInfinity(capacity))
                    throw new InvalidOperationException(
                        "NonFiniteRecruitmentCapacity");
                return Math.Max(0, (int)Math.Floor(capacity)).ToString();
            }
            catch (Exception exception)
            {
                LogUnavailable("availableManpower", settlement, exception);
                return Unavailable();
            }
        }

        private static ManpowerRowVM Row(string id, string fallback,
            string value) => new ManpowerRowVM(Text(id, fallback), value);

        private static void AddUnavailable(ManpowerSectionVM section,
            string label) => section.Rows.Add(new ManpowerRowVM(
                label, Unavailable(), true));

        private static string Rate(float value) =>
            Math.Max(0f, value).ToString("0.00");

        private static string Percent(float value) =>
            ((int)Math.Round(Math.Max(0f, value) * 100f)) + "%";

        private static string People(int value)
            => Math.Max(0, value).ToString();

        private static string Actual(bool hasRecord, int value) => hasRecord
            ? People(value)
            : Text("ModifiedArmy_ReinforcementRate_NoRecord", "No record");

        private static string Unavailable() => Text(
            "ModifiedArmy_Common_Unavailable", "Unavailable");

        private static string Text(string id, string fallback) =>
            new TextObject("{=" + id + "}" + fallback).ToString();

        private static void LogUnavailable(string component,
            Settlement settlement, Exception exception)
        {
            ModLogger.FileWarn(string.Format(
                "Manpower overview component unavailable: component={0}, " +
                "settlement={1}, settlementId={2}, reason={3}.", component,
                settlement?.Name, settlement?.StringId, exception.Message));
        }
    }
}
