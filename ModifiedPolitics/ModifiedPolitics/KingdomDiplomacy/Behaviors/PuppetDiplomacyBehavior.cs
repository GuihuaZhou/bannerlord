using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedPolitics.KingdomDiplomacy.Behaviors
{
    /// <summary>
    /// Observes completed native diplomacy actions and mirrors war/peace to
    /// direct puppet kingdoms on both sides of the diplomatic change.
    /// </summary>
    public sealed class PuppetDiplomacyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(
                this,
                OnSessionLaunched);
            CampaignEvents.WarDeclared.AddNonSerializedListener(
                this,
                OnWarDeclared);
            CampaignEvents.MakePeace.AddNonSerializedListener(
                this,
                OnPeaceMade);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnSessionLaunched(CampaignGameStarter starter)
        {
            // A loaded game may have been saved while another mod was changing
            // diplomacy. Restore every puppet before normal campaign play.
            foreach (Kingdom kingdom in Kingdom.All)
            {
                PuppetDiplomacySynchronizer.SynchronizeAllWars(kingdom);
            }
        }

        private static void OnWarDeclared(
            IFaction firstFaction,
            IFaction secondFaction,
            DeclareWarAction.DeclareWarDetail detail)
        {
            SynchronizeBothSides(firstFaction, secondFaction);
        }

        private static void OnPeaceMade(
            IFaction firstFaction,
            IFaction secondFaction,
            MakePeaceAction.MakePeaceDetail detail)
        {
            SynchronizeBothSides(firstFaction, secondFaction);
        }

        private static void SynchronizeBothSides(
            IFaction firstFaction,
            IFaction secondFaction)
        {
            if (firstFaction is Kingdom firstKingdom)
            {
                PuppetDiplomacySynchronizer.SynchronizePuppetsAgainst(
                    firstKingdom,
                    secondFaction);
            }

            if (secondFaction is Kingdom secondKingdom)
            {
                PuppetDiplomacySynchronizer.SynchronizePuppetsAgainst(
                    secondKingdom,
                    firstFaction);
            }
        }
    }
}
