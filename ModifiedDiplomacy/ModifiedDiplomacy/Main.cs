using Bannerlord.UIExtenderEx;
using HarmonyLib;
using ModifiedDiplomacy.AI;
using ModifiedDiplomacy.Clans;
using ModifiedDiplomacy.Subjects.Behaviors;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ModifiedDiplomacy
{
    /// <summary>
    /// Entry point of the standalone diplomacy module.
    ///
    /// During the migration this module only patches types that have already
    /// been moved here. Existing diplomacy behaviors remain registered by
    /// ModifiedPolitics until their migration batch is complete, preventing
    /// duplicate campaign behaviors and duplicate event subscriptions.
    /// </summary>
    public sealed class Main : MBSubModuleBase
    {
        private UIExtender _uiExtender;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Assembly assembly = Assembly.GetExecutingAssembly();
            new Harmony("com.mod.ModifiedDiplomacy").PatchAll(assembly);

            _uiExtender = new UIExtender("ModifiedDiplomacy");
            _uiExtender.Register(assembly);
            _uiExtender.Enable();
        }

        protected override void OnGameStart(
            Game game,
            IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign
                && gameStarterObject is CampaignGameStarter campaignStarter)
            {
                // The diplomacy manager owns all subject, treaty, proposal and
                // execution state. New campaigns now create it in this module.
                campaignStarter.AddBehavior(new KingdomDiplomacyManager());
                campaignStarter.AddModel(new ExiledClanDiplomacyModel());
                campaignStarter.AddBehavior(
                    new ExiledClanRealignmentBehavior());

                // These stateless repair behaviors are the first live feature
                // batch moved out of ModifiedPolitics. Their old registrations
                // are removed in the same change to prevent duplicate events.
                campaignStarter.AddBehavior(
                    new SubjectAllianceRestrictionBehavior());
                campaignStarter.AddBehavior(
                    new SubjectTradeRestrictionBehavior());
                campaignStarter.AddBehavior(
                    new PuppetDiplomacyBehavior());
                campaignStarter.AddBehavior(
                    new VassalWarObligationBehavior());
                campaignStarter.AddBehavior(
                    new SubjectProposalAiBehavior());
                campaignStarter.AddBehavior(
                    new SubjectRelationAiBehavior());
                campaignStarter.AddBehavior(
                    new KingdomNegotiationAiBehavior());
            }
        }
    }
}
