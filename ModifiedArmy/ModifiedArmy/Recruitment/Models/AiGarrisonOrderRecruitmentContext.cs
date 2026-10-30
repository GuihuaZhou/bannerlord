using System;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>Explicit, non-static authorization for one AI garrison plan.</summary>
    public sealed class AiGarrisonOrderRecruitmentContext
    {
        public AiGarrisonOrderRecruitmentContext(int remaining)
        {
            Remaining = Math.Max(0, remaining);
        }

        public int Remaining { get; }
    }
}
