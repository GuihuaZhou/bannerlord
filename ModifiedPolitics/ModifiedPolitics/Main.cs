using Bannerlord.UIExtenderEx;
using ModifiedPolitics.Models;
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
            }
        }
    }
}
