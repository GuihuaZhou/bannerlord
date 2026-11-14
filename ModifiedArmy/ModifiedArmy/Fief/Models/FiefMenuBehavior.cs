using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Models.Fief.Presentation;
using ModifiedArmy.Tool;
using ModifiedArmy.Population.Contracts;
using SandBox.View.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ScreenSystem;

namespace ModifiedArmy.Models.Fief
{
    /// <summary>
    /// Campaign behavior responsible for dynamically injecting a "Fief"
    /// submenu into town, castle, and village settlement menus.
    /// 
    /// The submenu provides options to:
    /// - Recruit fief squads into the player's party
    /// - Disband fief squads back to the settlement
    /// - Manage (view) current fief troop composition
    /// - Return to the parent settlement menu
    /// 
    /// Foreign fiefs may be inspected, but modification commands are enabled
    /// only when the player's clan owns the current settlement.
    /// </summary>
    public class FiefMenuBehavior : CampaignBehaviorBase
    {
        // Only register in these settlement types
        // Town fief administration belongs in the keep submenu. Castles use
        // their outer menu as the equivalent military-administration panel.
        private const string TOWN_MENU_ID = "town_keep";
        private const string CASTLE_MENU_ID = "castle";
        private const string VILLAGE_MENU_ID = "village";

        // Custom fief menu identifiers
        private const string FIEF_MENU_ID = "modified_army_fief_menu";
        private const string FIEF_OPTION_ID = "modified_army_settlement_fief";

        private const string RECRUIT_FIEF_OPTION_ID = "fief_manage";
        private const string DISBAND_FIEF_OPTION_ID = "fief_disband";
        private const string LEAVE_FIEF_OPTION_ID = "fief_return";
        private const string REINFORCEMENT_RATE_OPTION_ID =
            "modified_army_view_reinforcement_rate";
        private const string REINFORCEMENT_RATE_MENU_ID =
            "modified_army_reinforcement_rate_menu";
        private const string LEAVE_REINFORCEMENT_RATE_OPTION_ID =
            "modified_army_reinforcement_rate_return";

        private static void SetFiefIntroductionText(Settlement settlement)
        {
            if (settlement == null)
            {
                MBTextManager.SetTextVariable("FIEF_INTRODUCTION_TEXT", new TextObject("{=!}You are not in a valid settlement."), false);
                return;
            }

            var fiefManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (fiefManager == null)
            {
                MBTextManager.SetTextVariable("FIEF_INTRODUCTION_TEXT", new TextObject("{=!}Fief squad manager is not available."), false);
                return;
            }

            string introKey, descKey;

            CultureObject rulingCulture = settlement.OwnerClan?.Kingdom?.Culture;
            string rulingCultureId = rulingCulture?.StringId ?? "default";

            switch (rulingCultureId)
            {
                case "empire":
                    introKey = "{=Fief_Intro_Line_Empire}a Pronoia of the Empire.";
                    break;
                case "vlandia":
                    introKey = "{=Fief_Intro_Line_Vlandia}a Fief of Vlandia.";
                    break;
                case "aserai":
                    introKey = "{=Fief_Intro_Line_Aserai}an Iqta of the Aserai.";
                    break;
                case "khuzait":
                    introKey = "{=Fief_Intro_Line_Khuzait}an Ulus of the Khuzait.";
                    break;
                case "sturgia":
                    introKey = "{=Fief_Intro_Line_Sturgia}a Princely Domain of Sturgia.";
                    break;
                case "battania":
                    introKey = "{=Fief_Intro_Line_Battania}a Tribal Holding of Battania.";
                    break;
                case "nord":
                    introKey = "{=Fief_Intro_Line_Northern}a Viking Holding of the Northern clans.";
                    break;
                default:
                    introKey = "{=Fief_Intro_Line_Default}a fiefdom.";
                    break;
            }


            CultureObject nativeCulture = settlement.Culture;
            string nativeCultureId = nativeCulture?.StringId ?? "default";

            switch (nativeCultureId)
            {
                case "empire":
                    descKey = "{=Fief_Desc_Empire}The land here is fertile and abundant, yet the Empire’s martial spirit has waned—its defenses rely heavily on mercenaries.";
                    break;
                case "vlandia":
                    descKey = "{=Fief_Desc_Vlandia}The Vlandians field the continent’s finest heavy cavalry and deadliest crossbowmen.";
                    break;
                case "aserai":
                    descKey = "{=Fief_Desc_Aserai}Oasis-fed lands sustain the Aserai, whose wealth stems from trade and law, and whose elite Mamluk slave-soldiers are famed for discipline and valor.";
                    break;
                case "khuzait":
                    descKey = "{=Fief_Desc_Khuzait}Though some Khuzait now settle and raise infantry, their horse archers remain unmatched across the continent.";
                    break;
                case "sturgia":
                    descKey = "{=Fief_Desc_Sturgia}Sturgian princes guard vast forests, rivers, and frontier strongholds with loyal druzhina and levied militias.";
                    break;
                case "battania":
                    descKey = "{=Fief_Desc_Battania}Amid mist-shrouded highlands, Battanian nobles train the continent’s finest archers—silent, precise, and deadly.";
                    break;
                case "nord":
                    descKey = "{=Fief_Desc_Northern}Fjords and longships define the Northerners, whose warriors wield axe and shield-wall to conquer coast and sea alike.";
                    break;
                default:
                    descKey = "{=Fief_Desc_Default}This land is rugged and its people fierce—a vital source of levy troops.";
                    break;
            }

            var text = new TextObject(
                "{=ModifiedArmy_Fief_Intro}" +
                "{INTRO_LINE} {DESCRIPTION}"
            );

            text.SetTextVariable("INTRO_LINE", introKey.ToString());
            text.SetTextVariable("DESCRIPTION", descKey.ToString());
            MBTextManager.SetTextVariable("FIEF_INTRODUCTION_TEXT", text, false);
        }

        /// <inheritdoc/>
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        /// <inheritdoc/>
        public override void SyncData(IDataStore dataStore)
        {
            // No persistent state to sync.
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddGameMenus(starter);
        }

        /// <summary>
        /// Registers one shared Fief entry point in town, castle, and village
        /// menus and defines the common submenu.
        /// </summary>
        private void AddGameMenus(CampaignGameStarter starter)
        {
            // All fortifications can be inspected. Mutation options inside
            // the submenu remain restricted to the owning player clan.
            GameMenuOption.OnConditionDelegate fiefCondition = (args) =>
            {
                Settlement currentSettlement = Settlement.CurrentSettlement;
                bool canView = currentSettlement != null &&
                    (currentSettlement.IsFortification ||
                     currentSettlement.IsVillage);

                args.IsEnabled = canView;
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return canView;
            };

            GameMenuOption.OnConsequenceDelegate fiefConsequence = (args) =>
            {
                GameMenu.SwitchToMenu(FIEF_MENU_ID);
            };

            // Add "Entry Fief" to TOWN menu
            starter.AddGameMenuOption(
                TOWN_MENU_ID,
                FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Entry}View Fief",
                fiefCondition,
                fiefConsequence,
                isLeave: false,
                index: -1,
                isRepeatable: false
            );

            // Add "Entry Fief" to CASTLE menu
            starter.AddGameMenuOption(
                CASTLE_MENU_ID,
                FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Entry}View Fief",
                fiefCondition,
                fiefConsequence,
                isLeave: false,
                index: -1,
                isRepeatable: false
            );

            starter.AddGameMenuOption(
                VILLAGE_MENU_ID,
                FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_VillageEntry}View village fief troops",
                fiefCondition,
                fiefConsequence,
                isLeave: false,
                index: -1,
                isRepeatable: false);

            // Keep the rate report directly accessible from both settlement
            // screens; it is informational and never changes campaign state.
            starter.AddGameMenuOption(
                TOWN_MENU_ID,
                REINFORCEMENT_RATE_OPTION_ID,
                "{=ModifiedArmy_ReinforcementRate_Entry}View manpower",
                fiefCondition,
                args => GameMenu.SwitchToMenu(REINFORCEMENT_RATE_MENU_ID),
                isLeave: false,
                index: -1,
                isRepeatable: false);
            starter.AddGameMenuOption(
                CASTLE_MENU_ID,
                REINFORCEMENT_RATE_OPTION_ID,
                "{=ModifiedArmy_ReinforcementRate_Entry}View manpower",
                fiefCondition,
                args => GameMenu.SwitchToMenu(REINFORCEMENT_RATE_MENU_ID),
                isLeave: false,
                index: -1,
                isRepeatable: false);

            starter.AddGameMenu(
                REINFORCEMENT_RATE_MENU_ID,
                "{=!}{REINFORCEMENT_RATE_TEXT}",
                InitializeReinforcementRateMenu,
                GameMenu.MenuOverlayType.SettlementWithBoth);
            starter.AddGameMenuOption(
                REINFORCEMENT_RATE_MENU_ID,
                LEAVE_REINFORCEMENT_RATE_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Return}Return",
                args => true,
                ReturnToSettlementAdministration,
                isLeave: true,
                index: -1,
                isRepeatable: false);

            // Create the "Your Fief" submenu with dynamic description
            // Register the fief menu with dynamic title
            starter.AddGameMenu(
                FIEF_MENU_ID,
                "{=!}{FIEF_INTRODUCTION_TEXT}",
                (args) =>
                {
                    SetFiefIntroductionText(Settlement.CurrentSettlement);
                },
                GameMenu.MenuOverlayType.SettlementWithBoth
            );


            // --- Submenu Options ---

            // Source and lifecycle filters are hosted inside the troop window.
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                RECRUIT_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Recruit}Recruit soldiers",
                ConfigureFiefPoolViewOption,
                OpenFiefManagement,
                isLeave: false, index: 0, isRepeatable: false
            );

            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                DISBAND_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Disband}Disband soldiers",
                ConfigureFiefDisbandOption,
                DisbandFiefTroops,
                isLeave: false, index: 1, isRepeatable: false
            );

            // Return
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                LEAVE_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Return}Return",
                (args) => true,
                ReturnToSettlementAdministration,
                isLeave: true, index: -1, isRepeatable: false
            );
        }

        /// <summary>
        /// Returns to the exact administration menu that owns the Fief entry.
        /// GameMenu.ExitToLast does not reliably retain town_keep when a
        /// custom submenu is opened from a foreign settlement.
        /// </summary>
        private static void ReturnToSettlementAdministration(
            MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            GameMenu.SwitchToMenu(
                settlement?.IsTown == true
                    ? TOWN_MENU_ID
                    : settlement?.IsVillage == true
                        ? VILLAGE_MENU_ID
                        : CASTLE_MENU_ID);
        }

        /// <summary>
        /// Keeps modification commands visible for inspection while disabling
        /// them in settlements owned by another clan.
        /// </summary>
        private static bool ConfigureFiefDisbandOption(
            MenuCallbackArgs args)
        {
            args.IsEnabled = CanModifyCurrentFief();
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            return true;
        }

        private static bool ConfigureFiefPoolViewOption(
            MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            bool canView = settlement != null &&
                (settlement.IsFortification || settlement.IsVillage);
            args.IsEnabled = canView;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return canView;
        }

        private void OpenFiefManagement(MenuCallbackArgs args)
        {
            Settlement interaction = Settlement.CurrentSettlement;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            MobileParty playerParty = MobileParty.MainParty;
            if (interaction == null || manager == null || playerParty == null)
                return;

            FiefManagementPresentationContext context =
                FiefManagementPresentationContext.Begin(
                    interaction, manager, playerParty,
                    canModify: CanModifyCurrentFief());
            args.MenuContext.OpenTroopSelection(
                fullRoster: context.GetFullRoster(),
                initialSelections: TroopRoster.CreateDummyTroopRoster(),
                canChangeStatusOfTroop: context.CanSelect,
                onDone: context.Commit,
                maxSelectableTroopCount: context.GetMaximumSelection(),
                minSelectableTroopCount: 0);
            args.MenuContext.SetPanelSound(
                "event:/ui/panels/panel_settlement_enter_recruit");
        }

        private static void DisbandFiefTroops(MenuCallbackArgs args)
        {
            Settlement interaction = Settlement.CurrentSettlement;
            MobileParty playerParty = MobileParty.MainParty;
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (interaction == null || playerParty == null || manager == null ||
                !CanModifyCurrentFief())
                return;

            manager.ReturnTroopsToSettlement(interaction, playerParty);
        }

        /// <summary>
        /// Displays the exact forecast used by the next daily production tick.
        /// </summary>
        private static void InitializeReinforcementRateMenu(
            MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            SettlementManpowerProductionBehavior pools = Campaign.Current
                ?.GetCampaignBehavior<SettlementManpowerProductionBehavior>();
            RecruitmentProductionForecast forecast = pools
                ?.GetProductionForecast(settlement);
            if (forecast == null)
            {
                MBTextManager.SetTextVariable(
                    "REINFORCEMENT_RATE_TEXT",
                    new TextObject(
                        "{=ModifiedArmy_ReinforcementRate_Unavailable}" +
                        "Reinforcement data is unavailable."),
                    false);
                return;
            }

            TextObject text = new TextObject(
                "{=ModifiedArmy_ReinforcementRate_Description}" +
                "Professional troops" +
                "{NEWLINE}Available manpower: {AVAILABLE_MANPOWER}" +
                "{NEWLINE}Base training: {TRAINING_BASE}/day" +
                "{NEWLINE}Barracks bonus (level {BARRACKS_LEVEL}): +{BARRACKS_BONUS}/day" +
                "{NEWLINE}Culture efficiency: {CULTURE_EFFICIENCY}%" +
                "{NEWLINE}Other bonuses: {EXTERNAL_BONUS}%" +
                "{NEWLINE}Training capacity: {TRAINING}/day" +
                "{NEWLINE}Expected replenishment: {PROFESSIONAL}/day" +
                "{NEWLINE}Actual replenishment: {PROFESSIONAL_ACTUAL}" +
                "{NEWLINE}Automatic recruitment: {AUTO_RECRUIT}" +
                "{NEWLINE}{NEWLINE}Fief troops" +
                "{NEWLINE}{VILLAGE_SUPPLY}" +
                "{NEWLINE}Base replenishment: {FIEF_BASE}/day" +
                "{NEWLINE}Expected replenishment: {FIEF}/day" +
                "{NEWLINE}Actual replenishment: {FIEF_ACTUAL}");
            text.SetTextVariable("NEWLINE", Environment.NewLine);
            text.SetTextVariable(
                "PROFESSIONAL",
                FormatRate(forecast.ProfessionalAllocation));
            text.SetTextVariable(
                "TRAINING",
                FormatRate(forecast.ProfessionalTrainingCapacity));
            text.SetTextVariable(
                "TRAINING_BASE",
                FormatRate(forecast.ProfessionalTrainingBase));
            text.SetTextVariable(
                "BARRACKS_BONUS",
                FormatRate(
                    forecast.ProfessionalTrainingStep *
                    forecast.BarracksLevel));
            text.SetTextVariable(
                "BARRACKS_LEVEL",
                forecast.BarracksLevel);
            text.SetTextVariable(
                "CULTURE_EFFICIENCY",
                (int)Math.Round(forecast.CultureEfficiency * 100f));
            text.SetTextVariable(
                "EXTERNAL_BONUS",
                (int)Math.Round(forecast.ExternalTrainingBonusRate * 100f));
            text.SetTextVariable(
                "AUTO_RECRUIT",
                new TextObject(
                    forecast.ProfessionalRecruitmentEnabled
                        ? "{=ModifiedArmy_Common_Enabled}Enabled"
                        : "{=ModifiedArmy_Common_Disabled}Disabled"));
            text.SetTextVariable("FIEF", FormatRate(forecast.FiefAllocation));
            text.SetTextVariable(
                "FIEF_BASE",
                FormatRate(forecast.FiefBaseDailyRate));
            SettlementManpowerProductionData productionData = pools
                .GetProductionData(settlement);
            text.SetTextVariable(
                "PROFESSIONAL_ACTUAL",
                CreateActualReplenishmentText(
                    productionData?.HasProfessionalRecord == true,
                    productionData?.LastProfessionalRecruited ?? 0));
            text.SetTextVariable(
                "FIEF_ACTUAL",
                CreateActualReplenishmentText(
                    productionData?.HasFiefRecord == true,
                    productionData?.LastFiefReplenished ?? 0));
            ISettlementPopulationService population =
                SettlementPopulationServiceRegistry.Current;
            text.SetTextVariable("AVAILABLE_MANPOWER",
                CreateAvailableManpowerText(settlement, population));
            text.SetTextVariable("VILLAGE_SUPPLY",
                CreateVillageSupplyText(settlement));
            MBTextManager.SetTextVariable(
                "REINFORCEMENT_RATE_TEXT",
                text,
                false);
        }

        private static string FormatRate(float value)
        {
            return Math.Max(0f, value).ToString("0.00");
        }

        private static TextObject CreateAvailableManpowerText(
            Settlement settlement,
            ISettlementPopulationService population)
        {
            if (population == null)
                return new TextObject(
                    "{=ModifiedArmy_Common_Unavailable}Unavailable");
            try
            {
                float capacity = Math.Max(0f, population.GetHearth(settlement) -
                    population.GetNaturalCapacity(settlement) *
                    MilitaryConfigManager.Instance.RequireActiveConfig()
                        .HearthEconomy.ProfessionalReserveRatio);
                if (float.IsNaN(capacity) || float.IsInfinity(capacity))
                    throw new InvalidOperationException(
                        "NonFiniteRecruitmentCapacity");
                return new TextObject(Math.Max(0,
                    (int)Math.Floor(capacity)).ToString());
            }
            catch (Exception exception)
            {
                ModLogger.FileWarn(string.Format(
                    "Available manpower UI unavailable: settlement={0}, " +
                    "settlementId={1}, reason={2}.", settlement?.Name,
                    settlement?.StringId, exception.Message));
                return new TextObject(
                    "{=ModifiedArmy_Common_Unavailable}Unavailable");
            }
        }

        private static TextObject CreateVillageSupplyText(
            Settlement settlement)
        {
            FiefPartyManager manager = Campaign.Current?
                .GetCampaignBehavior<FiefPartyManager>();
            if (manager == null || !manager.TryGetReadyFiefData(
                    settlement, out FiefPartyData fiefData))
                return new TextObject(
                    "{=ModifiedArmy_Common_Unavailable}Unavailable");
            try
            {
                CulturalMilitaryDoctrine doctrine =
                    MilitaryConfigManager.Instance.RequireActiveConfig()
                        .GetDoctrine(settlement);
                FiefPartyTemplate template = fiefData.GetFiefPartyTemplate();
                var lines = new List<string>();
                foreach (Village village in (settlement.BoundVillages ??
                    new List<Village>()).Where(item =>
                        item?.Settlement != null).OrderBy(item =>
                        item.Settlement.StringId))
                {
                    if (fiefData.Villages == null ||
                        !fiefData.Villages.TryGetValue(village.Settlement,
                            out VillageFiefData villageData) ||
                        villageData == null)
                    {
                        TextObject unavailable = new TextObject(
                            "{=ModifiedArmy_VillageSupplyUnavailable}" +
                            "{VILLAGE_NAME}: unavailable");
                        unavailable.SetTextVariable("VILLAGE_NAME",
                            village.Settlement.Name);
                        lines.Add(unavailable.ToString());
                        continue;
                    }
                    int limit = villageData.GetFeudalBudgetHearth(doctrine);
                    int occupied = villageData.GetOccupiedSupplyHearth(
                        template, doctrine);
                    TextObject line = new TextObject(
                        "{=ModifiedArmy_VillageSupply}" +
                        "{VILLAGE_NAME} support Hearth: " +
                        "{OCCUPIED}/{LIMIT}");
                    line.SetTextVariable("VILLAGE_NAME",
                        village.Settlement.Name);
                    line.SetTextVariable("OCCUPIED", occupied);
                    line.SetTextVariable("LIMIT", limit);
                    lines.Add(line.ToString());
                }
                return lines.Count > 0
                    ? new TextObject(string.Join(Environment.NewLine, lines))
                    : new TextObject(
                        "{=ModifiedArmy_Common_Unavailable}Unavailable");
            }
            catch (Exception exception)
            {
                ModLogger.FileWarn(string.Format(
                    "Village supply UI unavailable: settlement={0}, " +
                    "settlementId={1}, reason={2}.", settlement?.Name,
                    settlement?.StringId, exception.Message));
                return new TextObject(
                    "{=ModifiedArmy_Common_Unavailable}Unavailable");
            }
        }

        private static TextObject CreateActualReplenishmentText(
            bool hasRecord,
            int count)
        {
            if (!hasRecord)
            {
                return new TextObject(
                    "{=ModifiedArmy_ReinforcementRate_NoRecord}" +
                    "No record");
            }

            TextObject result = new TextObject(
                "{=ModifiedArmy_ReinforcementRate_ActualRecorded}" +
                "{COUNT}");
            result.SetTextVariable("COUNT", Math.Max(0, count));
            return result;
        }

        private static bool CanModifyCurrentFief()
        {
            Settlement settlement = Settlement.CurrentSettlement;

            return settlement != null &&
                (settlement.IsFortification || settlement.IsVillage) &&
                settlement.OwnerClan == Clan.PlayerClan &&
                MobileParty.MainParty?.CurrentSettlement == settlement;
        }
    }
}
