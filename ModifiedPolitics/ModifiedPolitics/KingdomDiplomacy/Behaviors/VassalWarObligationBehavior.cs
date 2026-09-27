using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedPolitics.KingdomDiplomacy.Behaviors
{
    /// <summary>
    /// Observes native war and peace actions and applies the overlord's result
    /// to every direct vassal. It also repairs required wars after loading.
    /// </summary>
    public sealed class VassalWarObligationBehavior : CampaignBehaviorBase
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
            foreach (Kingdom kingdom in Kingdom.All)
            {
                VassalWarObligationSynchronizer
                    .JoinCurrentOverlordWars(kingdom);
            }
        }

        private static void OnWarDeclared(
            IFaction firstFaction,
            IFaction secondFaction,
            DeclareWarAction.DeclareWarDetail detail)
        {
            SynchronizeOverlordSide(firstFaction, secondFaction);
            SynchronizeOverlordSide(secondFaction, firstFaction);
        }

        private static void OnPeaceMade(
            IFaction firstFaction,
            IFaction secondFaction,
            MakePeaceAction.MakePeaceDetail detail)
        {
            SynchronizeOverlordSide(firstFaction, secondFaction);
            SynchronizeOverlordSide(secondFaction, firstFaction);

            // A vassal cannot make a separate peace while its overlord remains
            // at war. Reapplying the overlord state restores the required war.
            RestoreVassalObligation(firstFaction, secondFaction);
            RestoreVassalObligation(secondFaction, firstFaction);
        }

        private static void SynchronizeOverlordSide(
            IFaction possibleOverlord,
            IFaction otherFaction)
        {
            if (possibleOverlord is Kingdom overlord)
            {
                VassalWarObligationSynchronizer.SynchronizeVassalsAgainst(
                    overlord,
                    otherFaction);
            }
        }

        private static void RestoreVassalObligation(
            IFaction possibleVassal,
            IFaction otherFaction)
        {
            if (!(possibleVassal is Kingdom vassal))
            {
                return;
            }

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager?.GetSubjectType(vassal) != SubjectType.Vassal)
            {
                return;
            }

            Kingdom overlord = manager.GetOverlord(vassal);
            if (overlord != null
                && FactionManager.IsAtWarAgainstFaction(
                    overlord,
                    otherFaction))
            {
                VassalWarObligationSynchronizer.SynchronizeVassalsAgainst(
                    overlord,
                    otherFaction);
            }
        }
    }
}
