using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedPolitics.Models;
using ModifiedPolitics.Models.WarDisposition;
using ModifiedPolitics.Models.WarDisposition.Listeners.Battle;
using ModifiedPolitics.Models.WarDisposition.Listeners.Hero;
using ModifiedPolitics.Models.WarDisposition.Listeners.Territory;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ModifiedPolitics
{
    public sealed class Main : MBSubModuleBase
    {
        private UIExtender _uiExtender;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Assembly assembly = Assembly.GetExecutingAssembly();

            new Harmony("com.mod.ModifiedPolitics").PatchAll(assembly);

            _uiExtender = new UIExtender("ModifiedPolitics");
            _uiExtender.Register(assembly);
            _uiExtender.Enable();
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign
                && gameStarterObject is CampaignGameStarter campaignStarter)
            {
                campaignStarter.AddModel(new WarPotentialModel());
                campaignStarter.AddModel(new NewBuildingConstructionModel());
                campaignStarter.AddModel(new NewSettlementLoyaltyModel());

                campaignStarter.AddBehavior(new AIBuildingAutoBoostBehavior());
                campaignStarter.AddBehavior(new WarDispositionManager());
                campaignStarter.AddBehavior(new PartyBattleEventBehavior());
                campaignStarter.AddBehavior(new HeroWarEventBehavior());
                campaignStarter.AddBehavior(new SettlementWarEventBehavior());
            }
        }
    }
}
