using ModifiedPolitics.KingdomDiplomacy.Actions;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using ModifiedPolitics.KingdomDiplomacy.Services;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedDiplomacy.Subjects.Behaviors
{
    /// <summary>
    /// Mirrors completed war and peace actions to direct puppet kingdoms.
    /// The relation store and synchronizer remain compatibility dependencies
    /// until the persistence migration is performed.
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
            HandleWarAgainstOverlord(firstFaction, secondFaction);
            HandleUnauthorizedPuppetChange(firstFaction, secondFaction);
            SynchronizeBothSides(firstFaction, secondFaction);
        }

        private static void OnPeaceMade(
            IFaction firstFaction,
            IFaction secondFaction,
            MakePeaceAction.MakePeaceDetail detail)
        {
            HandleUnauthorizedPuppetChange(firstFaction, secondFaction);
            SynchronizeBothSides(firstFaction, secondFaction);
        }

        private static void HandleWarAgainstOverlord(
            IFaction firstFaction,
            IFaction secondFaction)
        {
            if (!(firstFaction is Kingdom firstKingdom)
                || !(secondFaction is Kingdom secondKingdom))
            {
                return;
            }

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager?.GetOverlord(firstKingdom) == secondKingdom)
            {
                SubjectIndependenceAction.TryApply(
                    firstKingdom,
                    SubjectIndependenceReason.WarWithOverlord);
            }
            else if (manager?.GetOverlord(secondKingdom) == firstKingdom)
            {
                SubjectIndependenceAction.TryApply(
                    secondKingdom,
                    SubjectIndependenceReason.OverlordAggression);
            }
        }

        private static void HandleUnauthorizedPuppetChange(
            IFaction actingFaction,
            IFaction otherFaction)
        {
            if (!(actingFaction is Kingdom actingKingdom)
                || otherFaction == null
                || SubjectFormerWarResolutionService
                    .IsResolutionInProgressFor(actingKingdom)
                || PuppetDiplomacySynchronizer
                    .IsSynchronizationInProgressFor(actingKingdom))
            {
                return;
            }

            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager?.GetSubjectType(actingKingdom) != SubjectType.Puppet)
            {
                return;
            }

            Kingdom overlord = manager.GetOverlord(actingKingdom);
            if (overlord == null || otherFaction == overlord)
            {
                return;
            }

            bool puppetAtWar = FactionManager.IsAtWarAgainstFaction(
                actingKingdom,
                otherFaction);
            bool overlordAtWar = FactionManager.IsAtWarAgainstFaction(
                overlord,
                otherFaction);
            if (puppetAtWar != overlordAtWar)
            {
                SubjectIndependenceAction.TryApply(
                    actingKingdom,
                    SubjectIndependenceReason.PuppetDefiance);
            }
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
                SynchronizePuppetSide(firstKingdom, secondFaction);
            }

            if (secondFaction is Kingdom secondKingdom)
            {
                PuppetDiplomacySynchronizer.SynchronizePuppetsAgainst(
                    secondKingdom,
                    firstFaction);
                SynchronizePuppetSide(secondKingdom, firstFaction);
            }
        }

        private static void SynchronizePuppetSide(
            Kingdom possiblePuppet,
            IFaction otherFaction)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            if (manager?.GetSubjectType(possiblePuppet) != SubjectType.Puppet)
            {
                return;
            }

            Kingdom overlord = manager.GetOverlord(possiblePuppet);
            PuppetDiplomacySynchronizer.SynchronizePuppetsAgainst(
                overlord,
                otherFaction);
        }
    }
}
