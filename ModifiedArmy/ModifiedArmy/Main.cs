using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ModifiedArmy
{

    // 定义 Troop 的类型枚举
    public enum TroopType
    {
        Basic,      // 征召兵 (basic_troop)
        EliteBasic, // 贵族兵 (elite_basic_troop)
        Professional // 职业军 (其他所有)
    }

    public static class CampaignState
    {
        public static bool IsReady { get; set; } = false;
    }

    public class CampaignReadyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUpEnd);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnNewGameCreatedPartialFollowUpEnd(CampaignGameStarter starter)
        {
            CampaignState.IsReady = true;
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            CampaignState.IsReady = true;
        }
    }

    public class Main : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            new Harmony("com.mod.ModifiedArmy").PatchAll(Assembly.GetExecutingAssembly());
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (game.GameType is Campaign)
            {
                var campaignStarter = (CampaignGameStarter)gameStarterObject;
                campaignStarter.AddBehavior(new CampaignReadyBehavior());
                campaignStarter.AddModel(new NewVolunteerModel());
                campaignStarter.AddModel(new NewPartyWageModel());
                campaignStarter.AddModel(new NewPartyTroopUpgradeModel());

                campaignStarter.AddBehavior(new FiefMenuBehavior());
                campaignStarter.AddBehavior(new FiefSquadManager()); 

                string msg = "[MOD] NewVolunteerModel: Initialization complete.";
                InformationManager.DisplayMessage(new InformationMessage(msg));
            }
        }
    }
}