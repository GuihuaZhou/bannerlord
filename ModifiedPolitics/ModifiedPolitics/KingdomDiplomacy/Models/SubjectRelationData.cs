using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Models
{
    /// <summary>
    /// Persistent directed relation from a subject kingdom to its overlord.
    /// </summary>
    [SaveableRootClass(3)]
    public sealed class SubjectRelationData
    {
        [SaveableProperty(1)]
        public Kingdom SubjectKingdom { get; set; }

        [SaveableProperty(2)]
        public Kingdom OverlordKingdom { get; set; }

        [SaveableProperty(3)]
        public SubjectType Type { get; set; }

        [SaveableProperty(4)]
        public CampaignTime EstablishedTime { get; set; }

        [SaveableProperty(5)]
        public int DailyTribute { get; set; }

        public SubjectRelationData()
        {
        }

        public SubjectRelationData(
            Kingdom subjectKingdom,
            Kingdom overlordKingdom,
            SubjectType type,
            int dailyTribute)
        {
            SubjectKingdom = subjectKingdom;
            OverlordKingdom = overlordKingdom;
            Type = type;
            EstablishedTime = CampaignTime.Now;
            DailyTribute = dailyTribute < 0 ? 0 : dailyTribute;
        }
    }
}
