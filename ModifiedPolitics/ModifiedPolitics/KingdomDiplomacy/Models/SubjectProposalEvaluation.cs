using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedPolitics.KingdomDiplomacy.Models
{
    /// <summary>
    /// Observation result for a subject proposal. Clan preferences are kept as
    /// continuous scores and converted to the native-style 200/0 support vote.
    /// </summary>
    public sealed class SubjectProposalEvaluation
    {
        public Kingdom Overlord { get; set; }
        public Kingdom Subject { get; set; }
        public Kingdom EvaluatingKingdom { get; set; }
        public SubjectType SubjectType { get; set; }
        public bool IsSubmissionOffer { get; set; }
        public bool EvaluatesSubjectConsent { get; set; }
        public float StrengthRatio { get; set; }
        public int SubjectSettlementCount { get; set; }
        public float OverlordWarProgress { get; set; }
        public float SubjectWarProgress { get; set; }
        public int ActiveWarCount { get; set; }
        public Kingdom MostDangerousExternalEnemy { get; set; }
        public float ExternalEnemyPressureScore { get; set; }
        public float MultiFrontPressureScore { get; set; }
        public float ProtectorStrengthScore { get; set; }
        public int OverlordActiveWarCount { get; set; }
        public int NewWarObligationCount { get; set; }
        public int UnsharedSubjectWarCount { get; set; }
        public int ExpectedFormerWarEscalationCount { get; set; }
        public float OverlordDefenseCapacity { get; set; }
        public float CurrentEnemyStrength { get; set; }
        public float PotentialEnemyStrength { get; set; }
        public float AvailableWarCapacity { get; set; }
        public float CombinedEnemyStrength { get; set; }
        public float CombinedWarPressureRatio { get; set; }
        public float DynamicWarRiskScore { get; set; }
        public int ExistingSubjectCount { get; set; }
        public int NominalDailyTribute { get; set; }
        public List<SubjectClanSupportEvaluation> ClanEvaluations { get; } =
            new List<SubjectClanSupportEvaluation>();

        public float AcceptWeight => ClanEvaluations.Where(x => x.WouldAccept).Sum(x => x.VoteWeight);
        public float TotalWeight => ClanEvaluations.Sum(x => x.VoteWeight);
        public float AcceptShare => TotalWeight <= 0f ? 0f : AcceptWeight / TotalWeight;
        public bool WouldAccept => TotalWeight > 0f && AcceptShare >= 0.5f;
    }
}
