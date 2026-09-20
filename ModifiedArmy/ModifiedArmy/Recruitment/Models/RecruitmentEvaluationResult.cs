using ModifiedArmy.Recruitment.Classification;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Contains the approved quantity and every intermediate limit for logs
    /// and future UI diagnostics.
    /// </summary>
    public sealed class RecruitmentEvaluationResult
    {
        public CharacterObject Troop { get; set; }

        public RecruitmentSource Source { get; set; }

        public CombatRole CombatRole { get; set; }

        public TroopQuality Quality { get; set; }

        public int RequestedCount { get; set; }

        public int RecruitableCount { get; set; }

        public int AllowedByPartySize { get; set; }

        public int AllowedByCombatRole { get; set; }

        public int AllowedByQuality { get; set; }

        public int AllowedByWageLimit { get; set; }

        public int AllowedByRecruitmentCost { get; set; }

        public int AllowedByMaintenance { get; set; }

        public int UnitRecruitmentCost { get; set; }

        public float UnitDailyWage { get; set; }

        public int SustainableDays { get; set; }

        public float Priority { get; set; }

        public RecruitmentLimitReason PrimaryLimit { get; set; }
    }
}
