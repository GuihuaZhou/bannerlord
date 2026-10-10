using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedPolitics.Governor.Models
{
    public enum GovernorProfile
    {
        Civil,
        Frontier
    }

    /// <summary>
    /// Immutable settlement facts used by one governor planning pass.
    /// Candidate count is recalculated as heroes are consumed by the plan.
    /// </summary>
    public sealed class GovernorSettlementContext
    {
        public GovernorSettlementContext(
            Town town,
            bool isBorder,
            GovernorProfile profile,
            float requiredAbility)
        {
            Town = town;
            IsTown = town?.IsTown == true;
            IsBorder = isBorder;
            Prosperity = town?.Prosperity ?? 0f;
            ProsperityBand = (int)(Prosperity < 0f ? 0f : Prosperity) / 1000;
            Profile = profile;
            RequiredAbility = requiredAbility;
        }

        public Town Town { get; }
        public bool IsTown { get; }
        public bool IsBorder { get; }
        public float Prosperity { get; }
        public int ProsperityBand { get; }
        public GovernorProfile Profile { get; }
        public float RequiredAbility { get; }
        public int EligibleCandidateCount { get; set; }
    }
}
