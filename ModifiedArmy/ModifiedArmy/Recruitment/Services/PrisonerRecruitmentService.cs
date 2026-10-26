using ModifiedArmy.Models;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.Recruitment;
using TaleWorlds.Core;

namespace ModifiedArmy.Recruitment.Services
{
    public static class PrisonerRecruitmentService
    {
        public static void RecruitAiPrisoners(MobileParty party,
            CharacterObject troop, int requested, int conformityCost)
        {
            if (party == null || troop == null ||
                (troop.Occupation != Occupation.Mercenary &&
                 troop.Occupation != Occupation.Soldier)) return;
            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                party, new List<RecruitmentCandidate>
                {
                    new RecruitmentCandidate(troop, requested,
                        RecruitmentSource.Prisoner)
                });
            RecruitmentEvaluationResult evaluation = plan.Evaluations.Count > 0
                ? plan.Evaluations[0] : null;
            int approved = Math.Min(evaluation?.RecruitableCount ?? 0,
                party.PrisonRoster.GetTroopCount(troop));
            if (approved <= 0) return;

            party.PrisonRoster.AddToCounts(troop, -approved, false, 0,
                -conformityCost * approved, true, -1);
            party.MemberRoster.AddToCounts(troop, approved, false, 0, 0,
                true, -1);
            int totalCost = evaluation.UnitRecruitmentCost * approved;
            if (totalCost > 0 && party.LeaderHero != null)
                GiveGoldAction.ApplyBetweenCharacters(
                    party.LeaderHero, null, totalCost, false);
            ClanRecruitmentBudgetManager.CommitRecruitment(party, approved,
                evaluation.UnitRecruitmentCost, evaluation.UnitDailyWage);
            CampaignEventDispatcher.Instance.OnTroopRecruited(
                party.LeaderHero, null, null, troop, approved);
            int morale = Campaign.Current.Models
                .PrisonerRecruitmentCalculationModel
                .GetPrisonerRecruitmentMoraleEffect(
                    party.Party, troop, approved);
            party.RecentEventsMorale += morale;
        }

        public static void RecruitMainPartyPrisoners(FlattenedTroopRoster roster)
        {
            if (roster == null) return;
            int cost = 0;
            foreach (CharacterObject troop in roster.Troops)
            {
                CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1);
                cost += GetGoldCost(troop, 1, Hero.MainHero);
            }
            if (cost > 0)
                GiveGoldAction.ApplyBetweenCharacters(
                    Hero.MainHero, null, (int)(cost * 0.5f), false);
        }

        private static int GetGoldCost(CharacterObject troop, int count,
            Hero buyer) => Campaign.Current.Models
                .PrisonerRecruitmentCalculationModel is
                    NewPrisonerRecruitmentCalculationModel model
                ? model.CalculateGoldCostForRecruitment(troop, count, buyer)
                : 0;
    }
}
