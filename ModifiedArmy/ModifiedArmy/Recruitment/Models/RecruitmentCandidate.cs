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
            RecruitmentSource source,
            object sourceContext = null)
        {
            Troop = troop;
            AvailableCount = availableCount;
            Source = source;
            SourceContext = sourceContext;
        }

        public CharacterObject Troop { get; }

        public int AvailableCount { get; }

        public RecruitmentSource Source { get; }

        /// <summary>
        /// Optional integration-owned data needed to execute an approved
        /// candidate, such as the notable and volunteer slot index.
        /// </summary>
        public object SourceContext { get; }
    }
}
