using ModifiedArmy.Garrison.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Mutable projected party state. Troop growth and recruitment-finance
    /// commitments are tracked separately so source-specific exemptions do
    /// not hide occupied party capacity or create a second money ledger.
    /// </summary>
    public sealed class RecruitmentSimulationState
    {
        private RecruitmentSimulationState()
        {
        }

        public int PartySizeLimit { get; private set; }

        public int ProjectedMemberCount { get; private set; }

        public int ProjectedRegularCount { get; private set; }

        public int ReservedReturningMembers { get; private set; }

        public int EffectiveProjectedMemberCount =>
            ProjectedMemberCount + ReservedReturningMembers;

        public float ProjectedDailyWage { get; private set; }

        public float CommittedRecruitmentDailyWage { get; private set; }

        public float CommittedRecruitmentCost { get; private set; }

        public RecruitmentBudget Budget { get; private set; }

        public static RecruitmentSimulationState Create(MobileParty party)
        {
            RecruitmentSimulationState result =
                new RecruitmentSimulationState
                {
                    PartySizeLimit = party?.Party.PartySizeLimit ?? 0,
                    ProjectedMemberCount =
                        party?.Party.NumberOfAllMembers ?? 0,
                    ProjectedRegularCount =
                        party?.MemberRoster.TotalRegulars ?? 0,
                    ReservedReturningMembers = party?.IsGarrison == true
                        ? GarrisonRecruitmentCapacity
                            .GetActiveSupplyEscortCount(
                                party.CurrentSettlement)
                        : 0,
                    Budget = RecruitmentBudget.Create(party)
                };

            result.ProjectedDailyWage =
                result.Budget.CurrentLongTermWage;

            return result;
        }

        /// <summary>Creates a detached state for a planned or live subject.</summary>
        public static RecruitmentSimulationState Create(
            RecruitmentSubject subject)
        {
            if (subject?.Kind == RecruitmentSubjectKind.ExistingParty)
            {
                return Create(subject.LiveParty);
            }

            RecruitmentSimulationState result =
                new RecruitmentSimulationState
                {
                    PartySizeLimit = Math.Max(0, subject?.PartySizeLimit ?? 0),
                    ProjectedMemberCount = Math.Max(
                        0,
                        subject?.CurrentMemberCount ?? 0),
                    ProjectedRegularCount = Math.Max(
                        0,
                        subject?.CurrentRegularCount ?? 0),
                    ReservedReturningMembers = 0,
                    Budget = RecruitmentBudget.Create(subject)
                };
            result.ProjectedDailyWage = result.Budget.CurrentLongTermWage;
            return result;
        }

        /// <summary>Copies the local projection for transactional preview branches.</summary>
        public RecruitmentSimulationState Clone() =>
            new RecruitmentSimulationState
            {
                PartySizeLimit = PartySizeLimit,
                ProjectedMemberCount = ProjectedMemberCount,
                ProjectedRegularCount = ProjectedRegularCount,
                ReservedReturningMembers = ReservedReturningMembers,
                ProjectedDailyWage = ProjectedDailyWage,
                CommittedRecruitmentDailyWage =
                    CommittedRecruitmentDailyWage,
                CommittedRecruitmentCost = CommittedRecruitmentCost,
                Budget = Budget
            };

        /// <summary>
        /// Applies an approved candidate only to the projected state. The live
        /// party is deliberately left unchanged until an integration executes
        /// the completed RecruitmentPlan.
        /// </summary>
        public void Commit(
            CharacterObject troop,
            int count,
            float unitDailyWage,
            int unitRecruitmentCost,
            RecruitmentSource source = RecruitmentSource.Special)
        {
            if (troop == null || count <= 0)
            {
                return;
            }

            ProjectedMemberCount += count;
            ProjectedRegularCount += count;
            if (source != RecruitmentSource.Fief)
            {
                float committedWage = Math.Max(0f, unitDailyWage) * count;
                ProjectedDailyWage += committedWage;
                CommittedRecruitmentDailyWage += committedWage;
                CommittedRecruitmentCost +=
                    Math.Max(0, unitRecruitmentCost) * count;
            }
        }
    }
}
