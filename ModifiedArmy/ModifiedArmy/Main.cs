using Bannerlord.UIExtenderEx;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Utils;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;


namespace ModifiedArmy
{
    /// <summary>
    /// 封邑军队类型：封邑扈从、封邑军士、封邑民兵
    /// </summary>
    public enum FiefTroopType
    {
        Fief_Retinue,
        Fief_Sergeant,
        Fief_Militia,
        Fief_Other
    }

    public static class CampaignState
    {
        public static bool IsReady { get; set; } = false;
        public static bool IsNewGame { get; set; } = false;
    }

    public class CampaignReadyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreatedPartialFollowUpEnd);
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
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

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            CampaignState.IsNewGame = true;
        }
    }

    public class Main : MBSubModuleBase
    {

        public static Settings ModSettings { get; private set; }

        protected override void OnSubModuleLoad()
        {
            new Harmony("com.mod.ModifiedArmy").PatchAll(Assembly.GetExecutingAssembly());

            ModSettings = GlobalSettings<Settings>.Instance;

            UIExtender _UIextender = new UIExtender("com.mod.ModifiedArmy");
            _UIextender.Register(Assembly.GetExecutingAssembly());
            _UIextender.Enable();
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (Main.ModSettings == null)
            {
                Main.ModSettings = GlobalSettings<Settings>.Instance;
            }
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
                campaignStarter.AddModel(new NewPartySizeLimitModel());
                campaignStarter.AddModel(new NewSettlementLoyaltyModel());
                campaignStarter.AddModel(new NewClanTierModel());

                // //campaignStarter.AddModel(new FiefPartyFoodConsumptionModel());
                campaignStarter.AddModel(new FiefSettlementTaxModel());
                campaignStarter.AddBehavior(new FiefMenuBehavior());
                campaignStarter.AddBehavior(new AiRecruitFiefTroopsBehavior());
                campaignStarter.AddBehavior(new FiefWageExemptionManager());
                campaignStarter.AddBehavior(new FiefPartyManager());

                game.ObjectManager.RegisterType<BasicTroopGroup>(
                    "BasicTroopGroup", 
                    "BasicTroopGroups", 
                    100U, 
                    true, 
                    false);
                MBObjectManager.Instance.LoadXML("BasicTroopGroups", true);


                game.ObjectManager.RegisterType<FiefPartyTemplate>(
                    "FiefPartyTemplate",
                    "FiefPartyTemplates",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("FiefPartyTemplates", true);


                CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched);

                var msgText = GameTexts.FindText("str_modifiedarmy_initialization_complete");
                InformationManager.DisplayMessage(new InformationMessage(msgText.ToString()));
            }
        }

        private void OnAfterSessionLaunched(CampaignGameStarter starter)
        {
            if (CampaignState.IsNewGame)
            {
                Hero.MainHero.ChangeHeroGold(99000);

                var armor = MBObjectManager.Instance.GetObject<ItemObject>("southern_lamellar_armor");
                if (armor != null && armor.HasArmorComponent)
                    MobileParty.MainParty.ItemRoster.AddToCounts(armor, 1);

                CampaignState.IsNewGame = false;
            }
        }
    }
}