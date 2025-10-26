using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ModifiedArmy
{
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
            }
        }
    }
}