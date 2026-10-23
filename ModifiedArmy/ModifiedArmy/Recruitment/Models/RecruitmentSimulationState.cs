using ModifiedArmy.Garrison.Models;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Mutable projected party state. Approved candidates update this state so
    /// later candidates cannot reuse already committed space or money.
    /// </summary>
    public sealed class RecruitmentSimulationState
    {
        private RecruitmentSimulationState()
        {
        }

        public int PartySizeLimit { get; private set; }

        public int ProjectedMemberCount { get; private set; }

        public int ReservedReturningMembers { get; private set; }

        public int EffectiveProjectedMemberCount =>
            ProjectedMemberCount + ReservedReturningMembers;

        public float ProjectedDailyWage { get; private set; }

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

        /// <summary>
        /// Applies an approved candidate only to the projected state. The live
        /// party is deliberately left unchanged until an integration executes
        /// the completed RecruitmentPlan.
        /// </summary>
        public void Commit(
            CharacterObject troop,
            int count,
            float unitDailyWage,
            int unitRecruitmentCost)
        {
            if (troop == null || count <= 0)
            {
                return;
            }

            ProjectedMemberCount += count;
            ProjectedDailyWage += count * unitDailyWage;
            CommittedRecruitmentCost += count * unitRecruitmentCost;
        }
    }
}
