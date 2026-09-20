using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Describes one troop type offered by a recruitment source.
    /// </summary>
    public sealed class RecruitmentCandidate
    {
        public RecruitmentCandidate(
            CharacterObject troop,
            int availableCount,
            RecruitmentSource source)
        {
            Troop = troop;
            AvailableCount = availableCount;
            Source = source;
        }

        public CharacterObject Troop { get; }

        public int AvailableCount { get; }

        public RecruitmentSource Source { get; }
    }
}
