using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.SettlementInspection.Behaviors
{
    /// <summary>
    /// Adds development-only access to the management panel of fortifications
    /// owned by other clans. Mutation patches keep these panels read-only.
    /// </summary>
    public class SettlementManagementInspectionBehavior : CampaignBehaviorBase
    {
        // Town administration is grouped under the keep/main-building menu.
        private const string TownMenuId = "town_keep";
        private const string CastleMenuId = "castle";
        private const string TownOptionId =
            "modified_army_view_town_management";
        private const string CastleOptionId =
            "modified_army_view_castle_management";

        /// <summary>
        /// Indicates that the active town management panel belongs to another
        /// clan and must reject all management commands.
        /// </summary>
        public static bool IsReadOnlyPanelOpen { get; private set; }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                AddGameMenus);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void AddGameMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption(
                TownMenuId,
                TownOptionId,
                "{=ModifiedArmy_ViewTownManagement}View Town",
                CanViewForeignTownManagement,
                OpenReadOnlyManagement,
                false,
                -1,
                false);

            starter.AddGameMenuOption(
                CastleMenuId,
                CastleOptionId,
                "{=ModifiedArmy_ViewCastleManagement}View Castle",
                CanViewForeignCastleManagement,
                OpenReadOnlyManagement,
                false,
                -1,
                false);
        }

        private static bool CanViewForeignTownManagement(
            MenuCallbackArgs args)
        {
            CloseReadOnlyPanel();

            Settlement settlement = Settlement.CurrentSettlement;
            bool canView =
                settlement?.IsTown == true
                && settlement.OwnerClan != Clan.PlayerClan;

            ConfigureOption(args, canView);
            return canView;
        }

        private static bool CanViewForeignCastleManagement(
            MenuCallbackArgs args)
        {
            CloseReadOnlyPanel();

            Settlement settlement = Settlement.CurrentSettlement;
            bool canView =
                settlement?.IsCastle == true
                && settlement.OwnerClan != Clan.PlayerClan;

            ConfigureOption(args, canView);
            return canView;
        }

        private static void ConfigureOption(
            MenuCallbackArgs args,
            bool canView)
        {
            args.IsEnabled = canView;

            if (canView)
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            }
        }

        private static void OpenReadOnlyManagement(MenuCallbackArgs args)
        {
            IsReadOnlyPanelOpen = true;
            args.MenuContext.OpenTownManagement();
        }

        /// <summary>
        /// Clears the inspection state after closing the panel or returning to
        /// the settlement menu.
        /// </summary>
        public static void CloseReadOnlyPanel()
        {
            IsReadOnlyPanelOpen = false;
        }
    }
}
