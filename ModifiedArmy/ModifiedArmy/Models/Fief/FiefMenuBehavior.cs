using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Tool;
using SandBox.View.Menu;
using System;
using System.Collections.Generic;
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
    /// Campaign behavior responsible for dynamically injecting a "Fief" submenu into the settlement menus
    /// of towns and castles when the player's clan owns the settlement.
    /// 
    /// ⚠️ Note: This behavior intentionally EXCLUDES villages — the "Fief" button will NOT appear in village menus.
    /// 
    /// The submenu provides options to:
    /// - Recruit fief squads into the player's party
    /// - Disband fief squads back to the settlement
    /// - Manage (view) current fief troop composition
    /// - Return to the parent settlement menu
    /// 
    /// Activation condition: <see cref="Hero.MainHero"/>'s <see cref="Clan"/> must equal the settlement's <see cref="Settlement.OwnerClan"/>.
    /// </summary>
    public class FiefMenuBehavior : CampaignBehaviorBase
    {
        // Only register in these settlement types
        private const string TOWN_MENU_ID = "town";
        private const string CASTLE_MENU_ID = "castle";

        // Custom fief menu identifiers
        private const string FIEF_MENU_ID = "modified_army_fief_menu";
        private const string FIEF_OPTION_ID = "modified_army_settlement_fief";

        private const string RECRUIT_FIEF_OPTION_ID = "fief_recruit";
        private const string DISBAND_FIEF_OPTION_ID = "fief_disband";
        private const string MANAGE_FIEF_OPTION_ID = "fief_manage";
        private const string LEAVE_FIEF_OPTION_ID = "fief_return";

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

            var text = new TextObject(
                "{=ModifiedArmy_Fief_Intro}" +
                "You are at your fief. You can currently recruit {AVAILABLE} troops, {RECRUITED} are already serving with you, " +
                "and {WAITCYCLE} are on their way back to their homes, and up to {WEEKLY_REINFORCEMENT} will be replenished each week.\n\n" +
                "Current troop composition:\n" +
                "- Retinue: {RETINUE_COUNT}/{RETINUE_MAX}\n" +
                "- Sergeants: {SERGEANT_COUNT}/{SERGEANT_MAX}\n" +
                "- Marine: {MARINE_COUNT}/{MARINE_MAX}\n" +
                "- Slave: {SLAVE_COUNT}/{SLAVE_MAX}\n" +
                "- Militia: {MILITIA_COUNT}/{MILITIA_MAX}"
            );

            Dictionary<SoldierType, int> tmpSoldierTypeMaxCounts = fiefManager.GetFiefMaxTroopCounts(settlement);
            Dictionary<SoldierType, int> tmpSoldierTypeCounts = fiefManager.GetFiefTroopCounts(settlement);

            text.SetTextVariable("AVAILABLE", fiefManager.GetAvailableTroopCount(settlement).ToString());
            text.SetTextVariable("RECRUITED", fiefManager.GetRecruitedTroopCount(settlement).ToString());
            text.SetTextVariable("WAITCYCLE", fiefManager.GetWaitCycleTroopCount(settlement).ToString());
            text.SetTextVariable("WEEKLY_REINFORCEMENT", fiefManager.GetWeeklyUpdateCount(settlement).ToString());
            text.SetTextVariable("RETINUE_COUNT", tmpSoldierTypeCounts[SoldierType.Retinue].ToString());
            text.SetTextVariable("RETINUE_MAX", tmpSoldierTypeMaxCounts[SoldierType.Retinue].ToString());
            text.SetTextVariable("SERGEANT_COUNT", tmpSoldierTypeCounts[SoldierType.Sergeant].ToString());
            text.SetTextVariable("SERGEANT_MAX", tmpSoldierTypeMaxCounts[SoldierType.Sergeant].ToString());
            text.SetTextVariable("MARINE_COUNT", tmpSoldierTypeCounts[SoldierType.Marine].ToString());
            text.SetTextVariable("MARINE_MAX", tmpSoldierTypeMaxCounts[SoldierType.Marine].ToString());
            text.SetTextVariable("SLAVE_COUNT", tmpSoldierTypeCounts[SoldierType.Slave].ToString());
            text.SetTextVariable("SLAVE_MAX", tmpSoldierTypeMaxCounts[SoldierType.Slave].ToString());
            text.SetTextVariable("MILITIA_COUNT", tmpSoldierTypeCounts[SoldierType.Militia].ToString());
            text.SetTextVariable("MILITIA_MAX", tmpSoldierTypeMaxCounts[SoldierType.Militia].ToString());
            

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
            if (selectedRoster == null || selectedRoster.TotalManCount <= 0)
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
            // Condition: Show "Fief" option only if main hero's clan owns the current settlement
            GameMenuOption.OnConditionDelegate fiefCondition = (args) =>
            {
                Settlement currentSettlement = Settlement.CurrentSettlement;
                Hero visitingHero = Hero.MainHero;

                if (currentSettlement != null && visitingHero != null)
                {
                    bool isEnabled = visitingHero.Clan == currentSettlement.OwnerClan;
                    args.IsEnabled = isEnabled;
                    if (isEnabled)
                    {
                        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                        return true;
                    }
                }

                args.IsEnabled = false;
                return false;
            };

            GameMenuOption.OnConsequenceDelegate fiefConsequence = (args) =>
            {
                GameMenu.SwitchToMenu(FIEF_MENU_ID);
            };

            // Add "Entry Fief" to TOWN menu
            starter.AddGameMenuOption(
                TOWN_MENU_ID,
                FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Entry}Entry Fief",
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
                "{=ModifiedArmy_FiefMenu_Entry}Entry Fief",
                fiefCondition,
                fiefConsequence,
                isLeave: false,
                index: -1,
                isRepeatable: false
            );

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
                "{=ModifiedArmy_FiefMenu_Recruit}Recruit Fief Troops",
                (args) => { args.optionLeaveType = GameMenuOption.LeaveType.Submenu; return true; },
                (args) =>
                {
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
                isLeave: false, index: -1, isRepeatable: false
            );

            // Disband
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                DISBAND_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Disband}Disband Fief Troops",
                (args) => { args.optionLeaveType = GameMenuOption.LeaveType.Submenu; return true; },
                (args) =>
                {
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
                isLeave: false, index: -1, isRepeatable: false
            );

            // Recruit Partial 
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                MANAGE_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_RecruitPartial}Recruit Partial Fief Troops",
                (args) => { args.optionLeaveType = GameMenuOption.LeaveType.Submenu; return true; },
                (args) =>
                {
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
                isLeave: false, index: -1, isRepeatable: false
            );

            // Return
            starter.AddGameMenuOption(
                FIEF_MENU_ID,
                LEAVE_FIEF_OPTION_ID,
                "{=ModifiedArmy_FiefMenu_Return}Return",
                (args) => true,
                (args) =>
                {
                    GameMenu.ExitToLast();
                },
                isLeave: true, index: -1, isRepeatable: false
            );
        }
    }
}