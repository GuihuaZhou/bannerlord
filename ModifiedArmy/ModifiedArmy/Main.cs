using Bannerlord.UIExtenderEx;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using ModifiedArmy.common;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.Utils;
using System.Collections.Generic;
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

        //public static Settings ModSettings { get; private set; }

        //protected override void OnSubModuleLoad()
        //{
        //    new Harmony("com.mod.ModifiedArmy").PatchAll(Assembly.GetExecutingAssembly());

        //    ModSettings = GlobalSettings<Settings>.Instance;
        //}

        //protected override void OnBeforeInitialModuleScreenSetAsRoot()
        //{
        //    base.OnBeforeInitialModuleScreenSetAsRoot();
        //    if (Main.ModSettings == null)
        //    {
        //        Main.ModSettings = GlobalSettings<Settings>.Instance;
        //    }
        //}

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
                // //campaignStarter.AddModel(new NewDiplomacyModel());
                campaignStarter.AddModel(new NewBuildingConstructionModel());
                campaignStarter.AddModel(new NewPrisonerRecruitmentCalculationModel());
                //campaignStarter.AddModel(new NewMinorFactionsModel());
                //campaignStarter.AddModel(new FiefSettlementTaxModel());


                ////campaignStarter.AddModel(new DebugGarrisonMoraleModel());
                ////campaignStarter.AddModel(new DebugDefaultPartyDesertionModel());

                //// //campaignStarter.AddModel(new FiefPartyFoodConsumptionModel());

                campaignStarter.AddBehavior(new FiefPartyManager());
                campaignStarter.AddBehavior(new FiefMenuBehavior());
                campaignStarter.AddBehavior(new FiefWageExemptionManager());

                campaignStarter.AddBehavior(new AiRecruitmentBehavior());


                campaignStarter.AddBehavior(new AIBuildingAutoBoostBehavior());
                //campaignStarter.AddBehavior(new GarrisonRecruitFromPrisonersBehavior());

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


                game.ObjectManager.RegisterType<ModConfig>(
                    "ModConfig",
                    "ModConfigs",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("ModConfigs", true);


                game.ObjectManager.RegisterType<MercenaryTemplate>(
                    "MercenaryTemplate",
                    "MercenaryTemplates",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("MercenaryTemplates", true);


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

                var itemIds = new List<string>
                {
                    "sturgian_lamellar_fortified",
                    "brass_lamellar_shoulder_white",
                    "sturgian_helmet_closed",
                    "sturgian_helmet_b_close",
                    "mail_chausses",
                    "northern_brass_bracers",
                    "t3_khuzait_horse",
                    "chain_horse_harness",
                    "steel_druzhinnik_kite_shield",
                    "sturgia_axe_5_t5",
                    "sturgia_lance_1_t4"
                };

                var partyRoster = MobileParty.MainParty.ItemRoster;
                foreach (string itemId in itemIds)
                {
                    var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
                    if (item != null)
                    {
                        partyRoster.AddToCounts(item, 1);
                    }
                }

                CampaignState.IsNewGame = false;
            }
        }
    }
}