// Recruitment planning context. Live parties remain authoritative for existing
// operations; planned lord parties use immutable campaign snapshots only.
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using ModifiedArmy.ForceManagement.Services;

namespace ModifiedArmy.Recruitment.Models
{
    public enum RecruitmentSubjectKind
    {
        ExistingParty,
        PlannedLordParty
    }

    /// <summary>
    /// Supplies the common admission and financial facts used by both live-party
    /// recruitment and creation-time dry runs. It never owns campaign resources.
    /// </summary>
    public sealed class RecruitmentSubject
    {
        public RecruitmentSubjectKind Kind { get; internal set; }
        public MobileParty LiveParty { get; internal set; }
        public Hero Leader { get; internal set; }
        public Clan Clan { get; internal set; }
        public Settlement Location { get; internal set; }
        public bool IsLordParty { get; internal set; }
        public bool IsGarrison { get; internal set; }
        public bool IsMainParty { get; internal set; }
        public bool IsActiveForAdmission { get; internal set; }
        public bool IsAtFormalWar { get; internal set; }
        public int PartySizeLimit { get; internal set; }
        public int TargetCount { get; internal set; }
        public int PaymentLimit { get; internal set; }
        public int CurrentMemberCount { get; internal set; }
        public int CurrentRegularCount { get; internal set; }

        /// <summary>Creates a compatibility subject without changing live behavior.</summary>
        public static RecruitmentSubject FromLiveParty(
            MobileParty party,
            int targetCount)
        {
            return new RecruitmentSubject
            {
                Kind = RecruitmentSubjectKind.ExistingParty,
                LiveParty = party,
                Leader = party?.LeaderHero,
                Clan = party?.ActualClan ?? party?.CurrentSettlement?.OwnerClan,
                Location = party?.CurrentSettlement ?? party?.HomeSettlement,
                IsLordParty = party?.IsLordParty == true,
                IsGarrison = party?.IsGarrison == true,
                IsMainParty = party?.IsMainParty == true,
                IsActiveForAdmission = party?.IsActive == true,
                IsAtFormalWar = ForceManagementFacade.IsAtFormalWar(
                    party?.ActualClan),
                PartySizeLimit = party?.Party.PartySizeLimit ?? 0,
                TargetCount = targetCount,
                PaymentLimit = party?.PaymentLimit ?? 0,
                CurrentMemberCount = party?.Party.NumberOfAllMembers ?? 0,
                CurrentRegularCount = party?.MemberRoster.TotalRegulars ?? 0
            };
        }
    }
}
