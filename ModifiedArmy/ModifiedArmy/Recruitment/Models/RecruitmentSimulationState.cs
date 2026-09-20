using ModifiedArmy.Recruitment.Classification;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Models
{
    /// <summary>
    /// Mutable projected party state. Approved candidates update this state so
    /// later candidates cannot reuse already committed space or money.
    /// </summary>
    public sealed class RecruitmentSimulationState
    {
        private readonly Dictionary<CombatRole, int> _roleCounts =
            new Dictionary<CombatRole, int>();
        private readonly Dictionary<TroopQuality, int> _qualityCounts =
            new Dictionary<TroopQuality, int>();

        private RecruitmentSimulationState()
        {
        }

        public int PartySizeLimit { get; private set; }

        public int ProjectedMemberCount { get; private set; }

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
                    Budget = RecruitmentBudget.Create(party)
                };

            result.ProjectedDailyWage =
                result.Budget.CurrentLongTermWage;

            if (party?.MemberRoster == null)
            {
                return result;
            }

            foreach (TroopRosterElement element in
                party.MemberRoster.GetTroopRoster())
            {
                CharacterObject troop = element.Character;

                if (troop == null || troop.IsHero || element.Number <= 0)
                {
                    continue;
                }

                result.AddCount(
                    RecruitmentTroopClassifier.GetCombatRole(troop),
                    element.Number);
                result.AddCount(
                    RecruitmentTroopClassifier.GetQuality(troop),
                    element.Number);
            }

            return result;
        }

        public int GetCount(CombatRole role) =>
            _roleCounts.TryGetValue(role, out int count) ? count : 0;

        public int GetCount(TroopQuality quality) =>
            _qualityCounts.TryGetValue(quality, out int count) ? count : 0;

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
            AddCount(RecruitmentTroopClassifier.GetCombatRole(troop), count);
            AddCount(RecruitmentTroopClassifier.GetQuality(troop), count);
        }

        private void AddCount(CombatRole role, int count)
        {
            _roleCounts[role] = GetCount(role) + count;
        }

        private void AddCount(TroopQuality quality, int count)
        {
            _qualityCounts[quality] = GetCount(quality) + count;
        }
    }
}
