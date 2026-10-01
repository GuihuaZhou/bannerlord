using ModifiedDiplomacy.KingdomDiplomacy.Models;
using ModifiedDiplomacy.KingdomDiplomacy.Persistence;
using ModifiedDiplomacy.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedDiplomacy.Subjects.Behaviors
{
    /// <summary>
    /// Applies an overlord's war and peace results to every direct vassal and
    /// repairs mandatory wars when a campaign session is loaded.
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
