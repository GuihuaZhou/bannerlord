using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Models
{
    /// <summary>
    /// Persistent and symmetric long-term relation between two kingdoms.
    /// </summary>
    [SaveableRootClass(2)]
    public sealed class KingdomRelationData
    {
        [SaveableProperty(1)]
        public Kingdom KingdomA { get; set; }

        [SaveableProperty(2)]
        public Kingdom KingdomB { get; set; }

        [SaveableProperty(3)]
        public int Relation { get; set; }

        public KingdomRelationData()
        {
        }

        public KingdomRelationData(Kingdom kingdomA, Kingdom kingdomB, int relation)
        {
            KingdomA = kingdomA;
            KingdomB = kingdomB;
            Relation = relation;
        }

        public bool Matches(Kingdom first, Kingdom second)
        {
            return (KingdomA == first && KingdomB == second)
                || (KingdomA == second && KingdomB == first);
        }
    }
}
