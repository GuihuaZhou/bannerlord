using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Mirrors an overlord's current war and peace states to its puppets.
    /// State checks make repeated and recursively raised campaign events safe.
    /// </summary>
    public static class PuppetDiplomacySynchronizer
    {
        private static readonly HashSet<Kingdom> ActiveSynchronizations =
            new HashSet<Kingdom>();

        /// <summary>
        /// Returns true while the given puppet's overlord is intentionally
        /// mirroring a diplomatic state. Event listeners use this distinction
        /// to avoid treating legitimate synchronization as puppet defiance.
        /// </summary>
        public static bool IsSynchronizationInProgressFor(Kingdom puppet)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            Kingdom overlord = manager?.GetOverlord(puppet);
            return overlord != null && ActiveSynchronizations.Contains(overlord);
        }

        public static void SynchronizePuppetsAgainst(
            Kingdom overlord,
            IFaction otherFaction)
        {
            if (overlord == null
                || otherFaction == null
                || otherFaction == overlord
                || !ActiveSynchronizations.Add(overlord))
            {
                return;
            }

            try
            {
                KingdomDiplomacyManager manager =
                    KingdomDiplomacyManager.Current;
                if (manager == null)
                {
                    return;
                }

                bool overlordAtWar =
                    FactionManager.IsAtWarAgainstFaction(
                        overlord,
                        otherFaction);
                List<Kingdom> puppets = manager.GetSubjects(overlord)
                    .Where(relation =>
                        relation.Type == SubjectType.Puppet
                        && IsUsable(relation.SubjectKingdom))
                    .Select(relation => relation.SubjectKingdom)
                    .ToList();

                foreach (Kingdom puppet in puppets)
                {
                    if (otherFaction == puppet)
                    {
                        continue;
                    }

                    bool puppetAtWar =
                        FactionManager.IsAtWarAgainstFaction(
                            puppet,
                            otherFaction);
                    if (overlordAtWar && !puppetAtWar)
                    {
                        DeclareWarAction.ApplyByDefault(
                            puppet,
                            otherFaction);
                    }
                    else if (!overlordAtWar && puppetAtWar)
                    {
                        MakePeaceAction.Apply(puppet, otherFaction);
                    }
                }
            }
            finally
            {
                ActiveSynchronizations.Remove(overlord);
            }
        }

        public static void SynchronizeAllWars(Kingdom puppet)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectRelationData relation = manager?.GetSubjectRelation(puppet);
            if (relation?.Type != SubjectType.Puppet
                || !IsUsable(relation.OverlordKingdom))
            {
                return;
            }

            foreach (Kingdom other in Kingdom.All.ToList())
            {
                if (other == null
                    || other == puppet
                    || other == relation.OverlordKingdom
                    || other.IsEliminated)
                {
                    continue;
                }

                SynchronizePuppetsAgainst(relation.OverlordKingdom, other);
            }
        }

        /// <summary>
        /// Adds the overlord's wars without erasing wars inherited by a newly
        /// established puppet. Former enemies resolve those wars separately.
        /// </summary>
        public static void JoinCurrentOverlordWars(Kingdom puppet)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectRelationData relation = manager?.GetSubjectRelation(puppet);
            if (relation?.Type != SubjectType.Puppet
                || !IsUsable(relation.OverlordKingdom))
            {
                return;
            }

            Kingdom overlord = relation.OverlordKingdom;
            foreach (Kingdom other in Kingdom.All.ToList())
            {
                if (!IsUsable(other)
                    || other == overlord
                    || other == puppet
                    || !FactionManager.IsAtWarAgainstFaction(overlord, other))
                {
                    continue;
                }

                SynchronizePuppetsAgainst(overlord, other);
            }
        }

        private static bool IsUsable(Kingdom kingdom)
        {
            return kingdom != null && !kingdom.IsEliminated;
        }
    }
}
