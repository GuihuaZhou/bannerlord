using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Tool;
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
    /// submenu into town and castle settlement menus.
    /// 
    /// Note: This behavior intentionally EXCLUDES villages — the "Fief" button will NOT appear in village menus.
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

        // Custom fief menu identifiers
        private const string FIEF_MENU_ID = "modified_army_fief_menu";
        private const string FIEF_OPTION_ID = "modified_army_settlement_fief";

        private const string RECRUIT_FIEF_OPTION_ID = "fief_recruit";
        private const string DISBAND_FIEF_OPTION_ID = "fief_disband";
        private const string MANAGE_FIEF_OPTION_ID = "fief_manage";
        private const string LEAVE_FIEF_OPTION_ID = "fief_return";
        private const string VIEW_FIEF_TROOPS_OPTION_ID =
            "fief_view_troops";
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
                "{INTRO_LINE}" +
                "{DESCRIPTION}" +
                "You can currently recruit {AVAILABLE} troops, " +
                "{RECRUITED} are already deployed, and " +
                "{WAITCYCLE} are on their way back home."
            );

            text.SetTextVariable("INTRO_LINE", introKey.ToString());
            text.SetTextVariable("DESCRIPTION", descKey.ToString());

            text.SetTextVariable("AVAILABLE", fiefManager.GetAvailableTroopCount(settlement).ToString());
            text.SetTextVariable("RECRUITED", fiefManager.GetRecruitedTroopCount(settlement).ToString());
            text.SetTextVariable("WAITCYCLE", fiefManager.GetWaitCycleTroopCount(settlement).ToString());


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

        // 允许玩家选择任意采邑士兵
        bool CanSelectTroop(CharacterObject troop)
        {
            if (troop == null) return false;
            // var type = SoldierTypeClassifier.GetSoldierType(troop);
            // return type is SoldierType.Retinue or SoldierType.Sergeant or SoldierType.Militia;
            return SoldierTypeClassifier.IsFiefTroop(troop);
        }

        // 回调：处理玩家选择结果
        void OnRecruitDone(TroopRoster selectedRoster)
        {
            if (!CanModifyCurrentFief()
                || selectedRoster == null
                || selectedRoster.TotalManCount <= 0)
                return;

            var manager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
            if (manager == null) return;

            var playerParty = MobileParty.MainParty;

            manager.ManualRecruitFromFief(Settlement.CurrentSettlement, selectedRoster, playerParty);
        }

        /// <summary>
        /// Registers the "Fief" entry point in town and castle menus only,
        /// and defines the full "Your Fief" submenu.
        /// Villages are explicitly excluded by design.
        /// </summary>
        private void AddGameMenus(CampaignGameStarter starter)
        {
            // All fortifications can be inspected. Mutation options inside
            // the submenu remain restricted to the owning player clan.
            GameMenuOption.OnConditionDelegate fiefCondition = (args) =>
            {
                Settlement currentSettlement = Settlement.CurrentSettlement;
                bool canView = currentSettlement?.IsFortification == true;

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

            // Recruit
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                RECRUIT_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Recruit}Recruit All",
                ConfigureFiefModificationOption,
                (args) =>
                {
                    if (!CanModifyCurrentFief())
                    {
                        return;
                    }

                    var manager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
                    var playerParty = MobileParty.MainParty;
                    Settlement currentSettlement = Settlement.CurrentSettlement;

                    if (currentSettlement == null || manager == null)
                    {
                        ModLogger.Warn("[Fief] Recruit failed: settlement or manager is null.");
                        return;
                    }

                    manager.RecruitFiefTroopsFromSettlement(currentSettlement, playerParty);
                },
                isLeave: false, index: 0, isRepeatable: false
            );

            // Disband
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                DISBAND_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Disband}Disband Troops",
                ConfigureFiefModificationOption,
                (args) =>
                {
                    if (!CanModifyCurrentFief())
                    {
                        return;
                    }

                    var manager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
                    var playerParty = MobileParty.MainParty;
                    Settlement currentSettlement = Settlement.CurrentSettlement;

                    if (currentSettlement == null || manager == null)
                    {
                        ModLogger.Warn("[Fief] Disband failed: settlement or manager is null.");
                        return;
                    }

                    manager.ReturnTroopsToSettlement(currentSettlement, playerParty);
                },
                isLeave: false, index: 2, isRepeatable: false
            );

            // Recruit Partial 
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                MANAGE_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_RecruitPartial}Recruit Selected",
                ConfigureFiefModificationOption,
                (args) =>
                {
                    if (!CanModifyCurrentFief())
                    {
                        return;
                    }

                    Settlement currentSettlement = Settlement.CurrentSettlement;
                    if (currentSettlement == null)
                    {
                        ModLogger.Warn("[Fief] Manage failed: settlement is null.");
                        return;
                    }

                    var fiefManager = Campaign.Current.GetCampaignBehavior<FiefPartyManager>();
                    if (fiefManager == null)
                    {
                        ModLogger.Error("[Fief] FiefSquadManager not found.");
                        return;
                    }

                    var playerParty = MobileParty.MainParty;
                    TroopRoster fiefRoster = fiefManager.GetFiefTroopRoster(currentSettlement);
                    TroopRoster selectRoster = TroopRoster.CreateDummyTroopRoster();

                    // 计算最大可招募数量（受 party size 限制）
                    int maxSelectable = Math.Max(0,
                       playerParty.Party.PartySizeLimit - playerParty.Party.NumberOfAllMembers);

                    args.MenuContext.OpenTroopSelection(
                        fullRoster: fiefRoster,
                        initialSelections: selectRoster,
                        canChangeStatusOfTroop: CanSelectTroop,
                        onDone: OnRecruitDone,
                        maxSelectableTroopCount: maxSelectable,
                        minSelectableTroopCount: 0
                    );
                    args.MenuContext.SetPanelSound("event:/ui/panels/panel_settlement_enter_recruit");
                },
                isLeave: false, index: 1, isRepeatable: false
            );

            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                VIEW_FIEF_TROOPS_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_ViewTroops}View Troops",
                ConfigureFiefPoolViewOption,
                OpenReadOnlyFiefTroops,
                isLeave: false,
                index: 4,
                isRepeatable: false);

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
                    : CASTLE_MENU_ID);
        }

        /// <summary>
        /// Keeps modification commands visible for inspection while disabling
        /// them in settlements owned by another clan.
        /// </summary>
        private static bool ConfigureFiefModificationOption(
            MenuCallbackArgs args)
        {
            args.IsEnabled = CanModifyCurrentFief();
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return true;
        }

        private static bool ConfigureFiefPoolViewOption(
            MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            Clan ownerClan = settlement?.OwnerClan;
            Clan playerClan = Clan.PlayerClan;
            bool isPlayerOwned = ownerClan != null &&
                ownerClan == playerClan;
            bool isSameKingdom = ownerClan?.Kingdom != null &&
                ownerClan.Kingdom == playerClan?.Kingdom;
            bool canView = settlement?.IsFortification == true &&
                (isPlayerOwned || isSameKingdom);
            args.IsEnabled = canView;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return canView;
        }

        /// <summary>
        /// Opens the same troop-selection popup used by partial recruitment,
        /// but rejects every selection so foreign and owned fief forces can be
        /// inspected without changing either the roster or detachments.
        /// </summary>
        private static void OpenReadOnlyFiefTroops(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            FiefPartyManager manager = Campaign.Current
                ?.GetCampaignBehavior<FiefPartyManager>();
            if (settlement?.IsFortification != true || manager == null)
            {
                return;
            }

            TroopRoster roster = manager.GetFiefTroopRoster(settlement);
            TroopRoster selection = TroopRoster.CreateDummyTroopRoster();
            args.MenuContext.OpenTroopSelection(
                fullRoster: roster,
                initialSelections: selection,
                canChangeStatusOfTroop: troop => false,
                onDone: selected => { },
                maxSelectableTroopCount: 0,
                minSelectableTroopCount: 0);
            args.MenuContext.SetPanelSound(
                "event:/ui/panels/panel_settlement_enter_recruit");
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
                "Base manpower: {SUPPLY}/day" +
                "{NEWLINE}{NEWLINE}Professional troops" +
                "{NEWLINE}Base training: {TRAINING_BASE}/day" +
                "{NEWLINE}Barracks bonus (level {BARRACKS_LEVEL}): +{BARRACKS_BONUS}/day" +
                "{NEWLINE}Culture efficiency: {CULTURE_EFFICIENCY}%" +
                "{NEWLINE}Other bonuses: {EXTERNAL_BONUS}%" +
                "{NEWLINE}Training capacity: {TRAINING}/day" +
                "{NEWLINE}Expected replenishment: {PROFESSIONAL}/day" +
                "{NEWLINE}Actual replenishment: {PROFESSIONAL_ACTUAL}" +
                "{NEWLINE}Automatic recruitment: {AUTO_RECRUIT}" +
                "{NEWLINE}{NEWLINE}Fief troops" +
                "{NEWLINE}Base replenishment: {FIEF_BASE}/day" +
                "{NEWLINE}Expected replenishment: {FIEF}/day" +
                "{NEWLINE}Actual replenishment: {FIEF_ACTUAL}");
            text.SetTextVariable("NEWLINE", Environment.NewLine);
            text.SetTextVariable("SUPPLY", FormatRate(forecast.SharedSupply));
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
            MBTextManager.SetTextVariable(
                "REINFORCEMENT_RATE_TEXT",
                text,
                false);
        }

        private static string FormatRate(float value)
        {
            return Math.Max(0f, value).ToString("0.00");
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

            return settlement?.IsFortification == true
                && settlement.OwnerClan == Clan.PlayerClan;
        }
    }
}
