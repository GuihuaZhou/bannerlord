using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Pools.UI
{
    /// <summary>
    /// Adds the native recruitment popup to castles. Towns already register
    /// the same entry in PlayerTownVisitCampaignBehavior. Manpower pool
    /// inspection was a development tool and is intentionally not exposed.
    /// </summary>
    public sealed class PlayerRecruitmentMenuBehavior : CampaignBehaviorBase
    {
        private const string CastleMenuId = "castle";
        private const string CastleRecruitOptionId =
            "modified_army_recruit_professional_troops";

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
                CastleMenuId,
                CastleRecruitOptionId,
                "{=E31IJyqs}Recruit troops",
                CanRecruitProfessionalTroops,
                OpenRecruitment,
                false,
                -1,
                false);
        }

        private static bool CanRecruitProfessionalTroops(
            MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            if (settlement?.IsCastle != true)
            {
                return false;
            }

            bool shouldBeDisabled;
            TextObject disabledText;
            bool canRecruit = Campaign.Current.Models.SettlementAccessModel
                .CanMainHeroDoSettlementAction(
                    settlement,
                    SettlementAccessModel.SettlementAction.RecruitTroops,
                    out shouldBeDisabled,
                    out disabledText);
            args.optionLeaveType = GameMenuOption.LeaveType.Recruit;
            return MenuHelper.SetOptionProperties(
                args,
                canRecruit,
                shouldBeDisabled,
                disabledText);
        }

        private static void OpenRecruitment(MenuCallbackArgs args)
        {
            args.MenuContext.OpenRecruitVolunteers();
            args.MenuContext.SetPanelSound(
                "event:/ui/panels/panel_settlement_enter_recruit");
        }
    }
}
