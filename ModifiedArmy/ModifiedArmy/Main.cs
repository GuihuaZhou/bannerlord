using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedArmy.common;
using ModifiedArmy.Garrison.Behaviors;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Garrison.Supply;
using ModifiedArmy.Logistics.Models;
using ModifiedArmy.Models;
using ModifiedArmy.Models.Fief;
using ModifiedArmy.PartyManagement.Behaviors;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.SettlementInspection.Behaviors;
using ModifiedArmy.ForceManagement.Behaviors;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Transport;
using ModifiedArmy.Tool;
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
            CampaignState.IsNewGame = false;
            CampaignState.IsReady = true;
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            CampaignState.IsReady = false;
            CampaignState.IsNewGame = true;
        }
    }

    public class Main : MBSubModuleBase
    {
        private UIExtender _uiExtender;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Assembly assembly = Assembly.GetExecutingAssembly();

            // Harmony
            new Harmony("com.mod.ModifiedArmy").PatchAll(assembly);

            // UIExtenderEx
            _uiExtender = new UIExtender("ModifiedArmy");
            _uiExtender.Register(assembly);
            _uiExtender.Enable();
        }

        // protected override void OnSubModuleLoad()
        // {
        //     new Harmony("com.mod.ModifiedArmy").PatchAll(Assembly.GetExecutingAssembly());
        // }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            if (game.GameType is Campaign)
            {
                var campaignStarter = (CampaignGameStarter)gameStarterObject;

                game.ObjectManager.RegisterType<BasicTroopGroup>(
                    "BasicTroopGroup",
                    "BasicTroopGroups",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("BasicTroopGroups", true);

                TroopProductionTemplateRepository.Instance.Clear();


                game.ObjectManager.RegisterType<FiefPartyTemplate>(
                    "FiefPartyTemplate",
                    "FiefPartyTemplates",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("FiefPartyTemplates", true);

                game.ObjectManager.RegisterType<ProfessionalTroopTemplate>(
                    "ProfessionalTroopTemplate",
                    "ProfessionalTroopTemplates",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML(
                    "ProfessionalTroopTemplates",
                    true);


                MilitaryConfigManager.Instance.Clear();
                game.ObjectManager.RegisterType<MilitaryConfig>(
                    "MilitaryConfig",
                    "MilitaryConfigs",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("MilitaryConfigs", true);
                MilitaryConfigManager.Instance.RequireActiveConfig();


                game.ObjectManager.RegisterType<MercenaryTemplate>(
                    "MercenaryTemplate",
                    "MercenaryTemplates",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("MercenaryTemplates", true);

                campaignStarter.AddBehavior(new CampaignReadyBehavior());
                campaignStarter.AddBehavior(new GarrisonWageLimitBehavior());
                campaignStarter.AddBehavior(new GarrisonGranaryMenuBehavior());
                campaignStarter.AddBehavior(
                    new GarrisonInspectionMenuBehavior());
                campaignStarter.AddBehavior(
                    new GarrisonLocalProductionSupplyBehavior());
                campaignStarter.AddBehavior(
                    new GarrisonLocalMarketPurchaseBehavior());
                campaignStarter.AddBehavior(
                    new PartyPersonnelLossLogBehavior());
                campaignStarter.AddBehavior(
                    new SupplyPartyBehavior());
                campaignStarter.AddBehavior(
                    new GarrisonCaptureSupplyBehavior());
                campaignStarter.AddBehavior(
                    new SettlementManagementInspectionBehavior());

                campaignStarter.AddModel(
                    new NewMobilePartyFoodConsumptionModel());
                campaignStarter.AddModel(
                    new NewSettlementFoodModel());
                campaignStarter.AddModel(new NewPartyWageModel());
                campaignStarter.AddModel(new NewPartyTroopUpgradeModel());
                campaignStarter.AddModel(new NewPartySizeLimitModel());
                campaignStarter.AddModel(new NewClanTierModel());
                campaignStarter.AddModel(new NewSettlementMilitiaModel());
                campaignStarter.AddModel(new NewSettlementGarrisonModel());
                campaignStarter.AddModel(new NewPrisonerRecruitmentCalculationModel());
                //campaignStarter.AddModel(new FiefSettlementTaxModel());、
                ////campaignStarter.AddModel(new DebugGarrisonMoraleModel());
                ////campaignStarter.AddModel(new DebugDefaultPartyDesertionModel());

                //// //campaignStarter.AddModel(new FiefPartyFoodConsumptionModel());

                campaignStarter.AddBehavior(new FiefPartyManager());
                campaignStarter.AddBehavior(new FiefMenuBehavior());

                campaignStarter.AddBehavior(new AiForcePlanningBehavior());
                campaignStarter.AddBehavior(new ForceManagementBehavior());
                campaignStarter.AddBehavior(new DemobilizationBehavior());
                campaignStarter.AddBehavior(
                    new PartyExchangeBehavior());
                campaignStarter.AddBehavior(
                    new TransferOperationJournalBehavior());
                campaignStarter.AddBehavior(
                    new ReinforcementMissionBehavior());
                campaignStarter.AddBehavior(
                    new ReinforcementTransportBehavior());
                campaignStarter.AddBehavior(
                    new SettlementManpowerProductionBehavior());
                campaignStarter.AddBehavior(new GarrisonRecruitFromPrisonersBehavior());
                // Registered last so its recovery barrier runs after the
                // specialist load handlers registered above.
                campaignStarter.AddBehavior(
                    new CampaignRecoveryCoordinator());

                ForceEntryPatchAudit.LogActivePatches();

                CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched);

                var msgText = GameTexts.FindText("str_modifiedarmy_initialization_complete");
                ModLogger.Info(msgText.ToString());
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
