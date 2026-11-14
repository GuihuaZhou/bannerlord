// Registers the settlement-menu entry point for the recruitment-weight popup;
// all persistent mutations remain owned by SettlementProfessionalPreferenceBehavior.
using ModifiedArmy.Recruitment.Preferences.Behaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Preferences.Presentation
{
    public sealed class SettlementProfessionalPreferenceMenuBehavior : CampaignBehaviorBase
    {
        private const string OptionId = "modifiedarmy_professional_preferences";

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, AddMenus);
            CampaignEvents.TickEvent.AddNonSerializedListener(this,
                RecruitmentWeightScreenService.Tick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private static void AddMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town_keep", OptionId,
                "{=ModifiedArmy_ProfessionalPreferences}Recruitment management",
                CanOpen, Open, false, -1, false);
            starter.AddGameMenuOption("castle", OptionId,
                "{=ModifiedArmy_ProfessionalPreferences}Recruitment management",
                CanOpen, Open, false, -1, false);
        }

        private static bool CanOpen(MenuCallbackArgs args)
        {
            bool allowed = Settlement.CurrentSettlement?.IsFortification == true &&
                Settlement.CurrentSettlement.OwnerClan == Clan.PlayerClan;
            if (allowed) args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            return allowed;
        }

        private static void Open(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            SettlementProfessionalPreferenceBehavior behavior = Campaign.Current?
                .GetCampaignBehavior<SettlementProfessionalPreferenceBehavior>();
            if (behavior != null)
                RecruitmentWeightScreenService.Open(settlement, behavior);
        }
    }
}
