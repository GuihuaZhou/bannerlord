using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedDiplomacy.KingdomDiplomacy.Models
{
    /// <summary>
    /// Persistent political attributes belonging to one kingdom.
    /// </summary>
    [SaveableRootClass(1)]
    public sealed class KingdomPoliticalData
    {
        [SaveableProperty(1)]
        public Kingdom Kingdom { get; set; }

        [SaveableProperty(2)]
        public int Rank { get; set; }

        public KingdomPoliticalData()
        {
        }

        public KingdomPoliticalData(Kingdom kingdom, int rank)
        {
            Kingdom = kingdom;
            Rank = rank < 1 ? 1 : rank;
        }
    }
}
