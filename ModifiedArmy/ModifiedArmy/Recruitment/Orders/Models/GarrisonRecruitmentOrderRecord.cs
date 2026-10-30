using System;
using TaleWorlds.SaveSystem;

namespace ModifiedArmy.Recruitment.Orders.Models
{
    public enum GarrisonRecruitmentOrderState
    {
        Active,
        Paused,
        RecoveryRequired
    }

    /// <summary>Persistent demand authorization; it never owns troops or money.</summary>
    public sealed class GarrisonRecruitmentOrderRecord
    {
        [SaveableProperty(1)] public string OrderId { get; set; }
        [SaveableProperty(2)] public string SettlementId { get; set; }
        [SaveableProperty(3)] public string OwnerClanId { get; set; }
        [SaveableProperty(4)] public int AuthorizedTotal { get; set; }
        [SaveableProperty(5)] public int RecruitedProfessional { get; set; }
        [SaveableProperty(6)] public int RecruitedPrisoner { get; set; }
        [SaveableProperty(7)] public int AuthorizedGoalAtLastRevision { get; set; }
        [SaveableProperty(8)] public int CreatedDay { get; set; }
        [SaveableProperty(9)] public int LastReviewedDay { get; set; }
        [SaveableProperty(10)] public int LastExecutionDay { get; set; }
        [SaveableProperty(11)] public int Revision { get; set; }
        [SaveableProperty(12)] public GarrisonRecruitmentOrderState State { get; set; }
        [SaveableProperty(13)] public string LastReasonCode { get; set; }
        [SaveableProperty(14)] public string LastOperationId { get; set; }

        public int RecruitedTotal => Math.Max(
            0, RecruitedProfessional) + Math.Max(0, RecruitedPrisoner);

        public int Remaining => Math.Max(0, AuthorizedTotal - RecruitedTotal);
    }
}
