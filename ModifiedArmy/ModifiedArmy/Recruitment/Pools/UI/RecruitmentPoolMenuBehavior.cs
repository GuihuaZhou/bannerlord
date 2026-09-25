using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Recruitment.Pools.UI
{
    /// <summary>
    /// Adds a read-only development view for the two manpower pools in every
    /// town and castle. Recruitment controls are intentionally deferred until
    /// pool production has been verified in game.
    /// </summary>
    public sealed class RecruitmentPoolMenuBehavior : CampaignBehaviorBase
    {
        private const string TownMenuId = "town";
        private const string CastleMenuId = "castle";
        private const string PoolMenuId =
            "modified_army_recruitment_pool_menu";
        private const string PoolOptionId =
            "modified_army_view_recruitment_pools";
        private const string ReturnOptionId =
            "modified_army_recruitment_pool_return";

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
                PoolOptionId,
                "{=ModifiedArmy_ViewRecruitmentPools}View Manpower Pools",
                CanViewPools,
                OpenPoolMenu,
                false,
                -1,
                false);
            starter.AddGameMenuOption(
                CastleMenuId,
                PoolOptionId,
                "{=ModifiedArmy_ViewRecruitmentPools}View Manpower Pools",
                CanViewPools,
                OpenPoolMenu,
                false,
                -1,
                false);

            starter.AddGameMenu(
                PoolMenuId,
                "{=!}{RECRUITMENT_POOL_TEXT}",
                InitializePoolMenu,
                GameMenu.MenuOverlayType.SettlementWithBoth);
            starter.AddGameMenuOption(
                PoolMenuId,
                ReturnOptionId,
                "{=ModifiedArmy_RecruitmentPoolsReturn}Return",
                args => true,
                args => GameMenu.ExitToLast(),
                true,
                -1,
                false);
        }

        private static bool CanViewPools(MenuCallbackArgs args)
        {
            bool canView =
                Settlement.CurrentSettlement?.IsFortification == true;
            args.IsEnabled = canView;
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return canView;
        }

        private static void OpenPoolMenu(MenuCallbackArgs args)
        {
            GameMenu.SwitchToMenu(PoolMenuId);
        }

        private static void InitializePoolMenu(MenuCallbackArgs args)
        {
            Settlement settlement = Settlement.CurrentSettlement;
            SettlementRecruitmentPoolBehavior behavior = Campaign.Current
                .GetCampaignBehavior<SettlementRecruitmentPoolBehavior>();

            if (settlement == null || behavior == null)
            {
                MBTextManager.SetTextVariable(
                    "RECRUITMENT_POOL_TEXT",
                    new TextObject(
                        "{=ModifiedArmy_RecruitmentPoolsUnavailable}" +
                        "Manpower pool data is unavailable."),
                    false);
                return;
            }

            SettlementRecruitmentPoolData data =
                behavior.GetOrCreatePool(settlement);
            TextObject description = new TextObject(
                "{=ModifiedArmy_RecruitmentPoolsDescription}" +
                "{SETTLEMENT_NAME} manpower pools" +
                "{NEWLINE}Barracks level: {BARRACKS_LEVEL}" +
                "{NEWLINE}{NEWLINE}Professional troops: " +
                "{PROFESSIONAL_COUNT}/{PROFESSIONAL_CAPACITY}" +
                "{NEWLINE}Daily production: {PROFESSIONAL_DAILY}" +
                "{NEWLINE}Production progress: {PROFESSIONAL_PROGRESS}" +
                "{NEWLINE}{PROFESSIONAL_TROOPS}" +
                "{NEWLINE}{NEWLINE}Fief troops: " +
                "{FIEF_COUNT}/{FIEF_CAPACITY}" +
                "{NEWLINE}Daily production: {FIEF_DAILY}" +
                "{NEWLINE}Production progress: {FIEF_PROGRESS}" +
                "{NEWLINE}{FIEF_TROOPS}");

            Dictionary<CharacterObject, int> professionalTroops =
                data.GetTroops(RecruitmentPoolKind.Professional);
            Dictionary<CharacterObject, int> fiefTroops =
                data.GetTroops(RecruitmentPoolKind.Fief);
            description.SetTextVariable("SETTLEMENT_NAME", settlement.Name);
            description.SetTextVariable("NEWLINE", Environment.NewLine);
            description.SetTextVariable(
                "BARRACKS_LEVEL",
                SettlementRecruitmentPoolBehavior.GetBarracksLevel(
                    settlement));
            SetPoolVariables(
                description,
                "PROFESSIONAL",
                settlement,
                behavior,
                data,
                RecruitmentPoolKind.Professional,
                professionalTroops);
            SetPoolVariables(
                description,
                "FIEF",
                settlement,
                behavior,
                data,
                RecruitmentPoolKind.Fief,
                fiefTroops);

            MBTextManager.SetTextVariable(
                "RECRUITMENT_POOL_TEXT",
                description,
                false);
        }

        private static void SetPoolVariables(
            TextObject description,
            string prefix,
            Settlement settlement,
            SettlementRecruitmentPoolBehavior behavior,
            SettlementRecruitmentPoolData data,
            RecruitmentPoolKind kind,
            IReadOnlyDictionary<CharacterObject, int> troops)
        {
            description.SetTextVariable(
                prefix + "_COUNT",
                troops.Values.Sum(count => Math.Max(0, count)));
            description.SetTextVariable(
                prefix + "_CAPACITY",
                behavior.GetCapacity(settlement, kind));
            description.SetTextVariable(
                prefix + "_DAILY",
                FormatNumber(behavior.GetDailyProduction(settlement, kind)));
            description.SetTextVariable(
                prefix + "_PROGRESS",
                FormatNumber(data.GetProductionProgress(kind)));
            description.SetTextVariable(
                prefix + "_TROOPS",
                FormatTroops(troops));
        }

        private static string FormatTroops(
            IReadOnlyDictionary<CharacterObject, int> troops)
        {
            List<string> lines = troops
                .Where(entry => entry.Key != null && entry.Value > 0)
                .OrderBy(entry => entry.Key.Name.ToString())
                .Select(entry =>
                    "- " + entry.Key.Name + ": " + entry.Value)
                .ToList();

            return lines.Count > 0
                ? string.Join(Environment.NewLine, lines)
                : new TextObject(
                    "{=ModifiedArmy_RecruitmentPoolsEmpty}None")
                    .ToString();
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
