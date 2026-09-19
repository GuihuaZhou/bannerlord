using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Adds access to the military granary in every town and castle. Granaries
    /// owned by another clan are exposed as read-only during development.
    /// The garrison party item roster is the real military food storage, so no
    /// additional save data or synchronization layer is required.
    /// </summary>
    public class GarrisonGranaryMenuBehavior : CampaignBehaviorBase
    {
        private const string TownMenuId = "town";
        private const string CastleMenuId = "castle";
        private const string GranaryOptionId =
            "modified_army_open_military_granary";

        /// <summary>
        /// Indicates that the active stash screen is showing another clan's
        /// granary. Inventory patches use this flag to reject item transfers.
        /// </summary>
        public static bool IsReadOnlyGranaryOpen { get; private set; }

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
                GranaryOptionId,
                "{=ModifiedArmy_ViewMilitaryGranary}View Military Granary",
                CanOpenGranary,
                OpenGranary,
                false,
                -1,
                false);

            starter.AddGameMenuOption(
                CastleMenuId,
                GranaryOptionId,
                "{=ModifiedArmy_ViewMilitaryGranary}View Military Granary",
                CanOpenGranary,
                OpenGranary,
                false,
                -1,
                false);
        }

        private static bool CanOpenGranary(MenuCallbackArgs args)
        {
            // Returning to the settlement menu means the previous inventory
            // screen has closed, including when it was cancelled.
            CloseGranaryView();

            Settlement settlement = Settlement.CurrentSettlement;

            bool canOpen =
                settlement?.IsFortification == true
                && settlement.Town?.GarrisonParty != null;

            args.IsEnabled = canOpen;

            if (canOpen)
            {
                args.optionLeaveType =
                    GameMenuOption.LeaveType.OpenStash;
            }

            return canOpen;
        }

        private static void OpenGranary(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;

            // Foreign granaries remain visible for development inspection, but
            // their contents must not be transferred to or from the player.
            IsReadOnlyGranaryOpen =
                settlement?.OwnerClan != Clan.PlayerClan;

            // Bannerlord guarantees a garrison party for active fortifications.
            // Its ItemRoster is saved as part of the party and serves as the
            // military granary required by the logistics design.
            InventoryScreenHelper.OpenScreenAsStash(
                settlement
                    .Town
                    .GarrisonParty
                    .ItemRoster);
        }

        /// <summary>
        /// Clears the read-only state after the inventory screen is accepted or
        /// cancelled. This prevents later inventory screens from being affected.
        /// </summary>
        public static void CloseGranaryView()
        {
            IsReadOnlyGranaryOpen = false;
        }
    }
}
