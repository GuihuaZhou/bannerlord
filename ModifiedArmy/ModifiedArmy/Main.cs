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
                campaignStarter.AddModel(new NewSettlementMilitiaModel());
                //campaignStarter.AddModel(new NewDiplomacyModel());
                campaignStarter.AddModel(new NewBuildingConstructionModel());
                campaignStarter.AddModel(new NewPrisonerRecruitmentCalculationModel());
                campaignStarter.AddModel(new NewMinorFactionsModel());


                //campaignStarter.AddModel(new DebugGarrisonMoraleModel());
                //campaignStarter.AddModel(new DebugDefaultPartyDesertionModel());

                // //campaignStarter.AddModel(new FiefPartyFoodConsumptionModel());

                campaignStarter.AddModel(new FiefSettlementTaxModel());
                campaignStarter.AddBehavior(new FiefMenuBehavior());
                campaignStarter.AddBehavior(new AiRecruitFiefTroopsBehavior());
                campaignStarter.AddBehavior(new FiefWageExemptionManager());
                campaignStarter.AddBehavior(new FiefPartyManager());
                campaignStarter.AddBehavior(new AIBuildingAutoBoostBehavior());
                campaignStarter.AddBehavior(new GarrisonRecruitFromPrisonersBehavior());

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

                var item = MBObjectManager.Instance.GetObject<ItemObject>("sturgian_lamellar_fortified");
                if (item != null)
                    MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);

                item = MBObjectManager.Instance.GetObject<ItemObject>("brass_lamellar_shoulder_white");
                if (item != null)
                    MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);

                item = MBObjectManager.Instance.GetObject<ItemObject>("sturgian_helmet_closed");
                if (item != null)
                    MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);

                item = MBObjectManager.Instance.GetObject<ItemObject>("mail_chausses");
                if (item != null)
                    MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);

                item = MBObjectManager.Instance.GetObject<ItemObject>("northern_brass_bracers");
                if (item != null)
                    MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);

                item = MBObjectManager.Instance.GetObject<ItemObject>("t3_khuzait_horse");
                if (item != null)
                    MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);

                item = MBObjectManager.Instance.GetObject<ItemObject>("steppe_half_barding");
                if (item != null)
                    MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);

                CampaignState.IsNewGame = false;
            }
        }
    }
}