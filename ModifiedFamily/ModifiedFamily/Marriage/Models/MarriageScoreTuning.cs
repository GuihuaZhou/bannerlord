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

        public static float CalculateAcceptanceChance(float score)
        {
            if (float.IsNaN(score) || float.IsInfinity(score)) return 0f;
            float ratio = Math.Max(0f, Math.Min(1f,
                score / MaxPositiveScore));
            MarriageAiConfig config = MarriageAiConfig.Current;
            return config.AcceptanceMaxChance * (float)Math.Pow(
                ratio, config.AcceptanceExponent);
        }
    }
}
