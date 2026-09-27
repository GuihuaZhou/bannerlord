using System.Collections.Generic;
using System.Linq;
using ModifiedPolitics.KingdomDiplomacy.Models;
using ModifiedPolitics.KingdomDiplomacy.Persistence;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace ModifiedPolitics.KingdomDiplomacy.Services
{
    /// <summary>
    /// Enforces wars and peace settlements initiated by an overlord on its
    /// direct vassals. Unlike puppets, vassals keep wars that they started
    /// independently unless the overlord makes peace with the same kingdom.
    /// </summary>
    public static class VassalWarObligationSynchronizer
    {
        private static readonly HashSet<Kingdom> ActiveSynchronizations =
            new HashSet<Kingdom>();

        public static void SynchronizeVassalsAgainst(
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

                bool overlordAtWar = FactionManager.IsAtWarAgainstFaction(
                    overlord,
                    otherFaction);
                List<Kingdom> vassals = manager.GetSubjects(overlord)
                    .Where(relation =>
                        relation.Type == SubjectType.Vassal
                        && IsUsable(relation.SubjectKingdom))
                    .Select(relation => relation.SubjectKingdom)
                    .ToList();

                foreach (Kingdom vassal in vassals)
                {
                    if (otherFaction == vassal)
                    {
                        continue;
                    }

                    bool vassalAtWar = FactionManager.IsAtWarAgainstFaction(
                        vassal,
                        otherFaction);
                    if (overlordAtWar && !vassalAtWar)
                    {
                        DeclareWarAction.ApplyByDefault(
                            vassal,
                            otherFaction);
                    }
                    else if (!overlordAtWar && vassalAtWar)
                    {
                        MakePeaceAction.Apply(vassal, otherFaction);
                    }
                }
            }
            finally
            {
                ActiveSynchronizations.Remove(overlord);
            }
        }

        /// <summary>
        /// Adds only wars required by the overlord. It deliberately does not
        /// remove the vassal's independent wars when a relation is established
        /// or a save is loaded.
        /// </summary>
        public static void JoinCurrentOverlordWars(Kingdom vassal)
        {
            KingdomDiplomacyManager manager = KingdomDiplomacyManager.Current;
            SubjectRelationData relation = manager?.GetSubjectRelation(vassal);
            if (relation?.Type != SubjectType.Vassal
                || !IsUsable(relation.OverlordKingdom))
            {
                return;
            }

            Kingdom overlord = relation.OverlordKingdom;
            foreach (Kingdom other in Kingdom.All.ToList())
            {
                if (!IsUsable(other)
                    || other == overlord
                    || other == vassal
                    || !FactionManager.IsAtWarAgainstFaction(overlord, other))
                {
                    continue;
                }

                SynchronizeVassalsAgainst(overlord, other);
            }
        }

        private static bool IsUsable(Kingdom kingdom)
        {
            return kingdom != null && !kingdom.IsEliminated;
        }
    }
}
