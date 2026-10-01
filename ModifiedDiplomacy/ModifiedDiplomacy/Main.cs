using HarmonyLib;
using ModifiedDiplomacy.AI;
using ModifiedDiplomacy.Clans;
using ModifiedDiplomacy.Subjects.Behaviors;
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
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Assembly assembly = Assembly.GetExecutingAssembly();
            new Harmony("com.mod.ModifiedDiplomacy").PatchAll(assembly);
        }

        protected override void OnGameStart(
            Game game,
            IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            if (game.GameType is Campaign
                && gameStarterObject is CampaignGameStarter campaignStarter)
            {
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
