using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Exposes allied garrison rosters from the town keep and castle menus.
    /// The native manage-troops screen is reused for complete roster details,
    /// while Harmony guards make the screen read-only for foreign clans.
    /// </summary>
    public class GarrisonInspectionMenuBehavior : CampaignBehaviorBase
    {
        // Town military information belongs under "Go to the keep".
        private const string TownMenuId = "town_keep";
        private const string CastleMenuId = "castle";
        private const string OptionId =
            "modified_army_view_allied_garrison";

        public static bool IsReadOnlyGarrisonOpen { get; private set; }

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
                OptionId,
                "{=ModifiedArmy_ViewGarrison}View Garrison",
                CanViewGarrison,
                OpenGarrison,
                false,
                -1,
                false);

            starter.AddGameMenuOption(
                CastleMenuId,
                OptionId,
                "{=ModifiedArmy_ViewGarrison}View Garrison",
                CanViewGarrison,
                OpenGarrison,
                false,
                -1,
                false);
        }

        private static bool CanViewGarrison(MenuCallbackArgs args)
        {
            // Returning to either settlement menu closes any previous party
            // screen, including a screen cancelled with the escape key.
            CloseGarrisonView();

            Settlement settlement = Settlement.CurrentSettlement;
            bool canView =
                settlement?.IsFortification == true &&
                settlement.OwnerClan != Clan.PlayerClan &&
                settlement.MapFaction == Hero.MainHero.MapFaction &&
                settlement.Town?.GarrisonParty != null;

            if (canView)
            {
                args.optionLeaveType =
                    GameMenuOption.LeaveType.ManageGarrison;
            }

            return canView;
        }

        private static void OpenGarrison(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;

            if (settlement?.Town?.GarrisonParty == null)
            {
                return;
            }

            IsReadOnlyGarrisonOpen = true;
            PartyScreenHelper.OpenScreenAsManageTroops(
                settlement.Town.GarrisonParty);
        }

        public static void CloseGarrisonView()
        {
            IsReadOnlyGarrisonOpen = false;
        }
    }
}
