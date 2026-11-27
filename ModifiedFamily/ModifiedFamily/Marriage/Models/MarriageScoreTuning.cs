using System;
using ModifiedFamily.Marriage.Configuration;

namespace ModifiedFamily.Marriage.Models
{
    public static class MarriageScoreTuning
    {
        public const float AcceptanceThreshold = 0f;
        public const float CommitmentThreshold = -20f;
        public const float CounterOtherClanFloor = -20f;
        public const float CandidateReceiverFloor = -40f;
        public const float AllianceBenefit = 55f;
        public const float SameCultureBenefit = 20f;
        public const float SameKingdomBenefit = 20f;
        public const float FemaleOutflowMultiplier = 1f;
        public const float MaleOutflowMultiplier = 1.5f;
        public const float SuccessionPenalty = 80f;
        public const float MaximumHeroInflow = 75f;
        public const float MaximumRelationBenefit = 30f;
        public const float MaximumGiftBenefit = 100f;
        public const float MaxPositiveScore = MaximumHeroInflow +
            AllianceBenefit + MaximumRelationBenefit + SameCultureBenefit +
            SameKingdomBenefit + MaximumGiftBenefit;

        /// <summary>Preserves small clan costs and eases the cost after the fourth adult noble.</summary>
        public static float CalculateScarcity(int adultHeroCount)
        {
            float n = Math.Max(0, adultHeroCount) + 1f;
            return adultHeroCount <= 3 ? 1f + 4f / n : 0.3f + 27.2f / (n * n);
        }

        public static float CalculateAcceptanceChance(float score)
        {
            return DecisionChances(score).Accept;
        }

        /// <summary>Interpolates one acceptance and counter probability from the authoritative curve.</summary>
        public static (float Accept, float Counter) DecisionChances(float score)
        {
            if (float.IsNaN(score) || float.IsInfinity(score)) return (0f, 0f);
            var points = MarriageAiConfig.Current.DecisionCurve;
            if (score <= points[0].Score) return (points[0].Accept, points[0].Counter);
            for (int index = 1; index < points.Count; index++)
                if (score <= points[index].Score)
                {
                    var lo = points[index - 1]; var hi = points[index];
                    float t = (score - lo.Score) / (hi.Score - lo.Score);
                    return (lo.Accept + (hi.Accept - lo.Accept) * t,
                        lo.Counter + (hi.Counter - lo.Counter) * t);
                }
            var last = points[points.Count - 1];
            return (last.Accept, last.Counter);
        }
    }
}
