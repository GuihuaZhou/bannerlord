using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedPolitics.Models;
using ModifiedPolitics.Models.WarDisposition;
using ModifiedPolitics.Models.WarDisposition.Listeners.Battle;
using ModifiedPolitics.Models.WarDisposition.Listeners.Economy;
using ModifiedPolitics.Models.WarDisposition.Listeners.Hero;
using ModifiedPolitics.Models.WarDisposition.Listeners.Territory;
using ModifiedPolitics.Governor.Behaviors;
using ModifiedPolitics.Governor.Config;
using ModifiedPolitics.HeroOffices.Behaviors;
using ModifiedPolitics.HeroOffices.Config;
using ModifiedPolitics.Tool;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

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
                // Register and load policy objects through Bannerlord's standard XML object pipeline.
                GovernorPolicyManager.Instance.Clear();
                game.ObjectManager.RegisterType<CultureGovernorPolicy>(
                    "CultureGovernorPolicy",
                    "CultureGovernorPolicies",
                    100U,
                    true,
                    false);
                MBObjectManager.Instance.LoadXML("CultureGovernorPolicies", true);

                // Isolation test 1: load only the XML-backed office configuration.
                OfficeConfigManager.Instance.Clear();
                try
                {
                    ModLogger.Info("[Hero Offices XML Test] Starting hero_offices.xml registration and load.");
                    game.ObjectManager.RegisterType<OfficeCultureConfig>(
                        "HeroOfficeCultureConfig",
                        "HeroOfficeCultureConfigs",
                        100U,
                        true,
                        false);
                    MBObjectManager.Instance.LoadXML("HeroOfficeCultureConfigs", true);
                    ModLogger.Info("[Hero Offices XML Test] hero_offices.xml loaded successfully.");
                }
                catch (Exception exception)
                {
                    OfficeConfigManager.Instance.Clear();
                    ModLogger.Error(
                        "[Hero Offices XML Test] hero_offices.xml failed to load. " +
                        exception);
                }

                campaignStarter.AddModel(new WarPotentialModel());
                campaignStarter.AddModel(new NewBuildingConstructionModel());

                campaignStarter.AddBehavior(new AIBuildingAutoBoostBehavior());
                // Fill governor vacancies by kingdom policy and allow cross-clan appointments.
                campaignStarter.AddBehavior(new KingdomGovernorAssignmentBehavior());
                campaignStarter.AddBehavior(new HeroOfficeBehavior());
                campaignStarter.AddBehavior(new OfficeApplicationAiBehavior());
                campaignStarter.AddBehavior(new LocalOfficeAiBehavior());
                campaignStarter.AddBehavior(new MarshalOfficeAiBehavior());
                campaignStarter.AddBehavior(new WarDispositionManager());
                campaignStarter.AddBehavior(new PartyBattleEventBehavior());
                campaignStarter.AddBehavior(new HeroWarEventBehavior());
                campaignStarter.AddBehavior(new SettlementWarEventBehavior());
                campaignStarter.AddBehavior(new WeeklyWealthEventBehavior());
            }
        }
    }
}
