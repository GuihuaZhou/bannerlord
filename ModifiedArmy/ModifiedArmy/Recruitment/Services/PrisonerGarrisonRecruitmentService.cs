using System;
using System.Collections.Generic;
using ModifiedArmy.Recruitment.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.Recruitment.Services
{
    internal sealed class PrisonerRecruitmentOffer
    {
        public PrisonerRecruitmentOffer(int conformityCost)
        {
            ConformityCost = Math.Max(0, conformityCost);
        }

        public int ConformityCost { get; }
    }

    /// <summary>Owns prisoner retention/conformity preparation and roster commit.</summary>
    internal static class PrisonerGarrisonRecruitmentService
    {
        public static List<RecruitmentCandidate> SelectAndPrepare(
            Settlement settlement, out int targetCount, out int readyCount,
            out int soldCount)
        {
            List<RecruitmentCandidate> result = new List<RecruitmentCandidate>();
            TroopRoster prison = settlement.Party.PrisonRoster;
            TroopRoster sale = TroopRoster.CreateDummyTroopRoster();
            bool allowSale = settlement.OwnerClan != Clan.PlayerClan;
            targetCount = 0;
            readyCount = 0;
            soldCount = 0;
            if (prison == null) return result;

            int capacity = Math.Max(0,
                settlement.Party.PrisonerSizeLimit / 2 - prison.TotalHeroes);
            foreach (TroopRosterElement element in prison.GetTroopRoster())
            {
                CharacterObject troop = element.Character;
                if (troop == null || troop.IsHero) continue;
                int retained = IsRegularTroop(troop) && troop.Tier >= 3
                    ? Math.Min(element.Number, capacity) : 0;
                int sell = allowSale ? element.Number - retained : 0;
                if (sell > 0)
                    sale.AddToCounts(troop, sell, false,
                        Math.Min(sell, element.WoundedNumber), 0, true, -1);
                if (retained <= 0) continue;

                capacity -= retained;
                targetCount += retained;
                prison.AddXpToTroop(troop, CalculateDailyConformity(settlement));
                int needed = Campaign.Current.Models
                    .PrisonerRecruitmentCalculationModel
                    .GetConformityNeededToRecruitPrisoner(troop);
                int ready = needed <= 0 ? retained : Math.Min(
                    retained, prison.GetElementXp(troop) / needed);
                if (ready <= 0) continue;
                readyCount += ready;
                result.Add(new RecruitmentCandidate(troop, ready,
                    RecruitmentSource.Prisoner,
                    new PrisonerRecruitmentOffer(needed)));
            }

            soldCount = sale.TotalRegulars;
            if (soldCount > 0)
                SellPrisonersAction.ApplyForSelectedPrisoners(
                    settlement.Party, null, sale);
            return result;
        }

        public static bool TryCommit(Settlement settlement, MobileParty garrison,
            RecruitmentEvaluationResult evaluation, int requested,
            out int committed)
        {
            committed = 0;
            if (settlement == null || garrison == null || evaluation?.Troop == null ||
                requested <= 0 || !(evaluation.Candidate?.SourceContext is
                    PrisonerRecruitmentOffer offer)) return false;
            TroopRoster prison = settlement.Party.PrisonRoster;
            committed = Math.Min(requested,
                prison.GetTroopCount(evaluation.Troop));
            if (committed <= 0) return false;
            int wounded = GetWoundedCount(prison, evaluation.Troop, committed);
            prison.AddToCounts(evaluation.Troop, -committed, false, -wounded,
                -offer.ConformityCost * committed, true, -1);
            garrison.MemberRoster.AddToCounts(evaluation.Troop, committed,
                false, wounded, 0, true, -1);
            return true;
        }

        public static bool IsRegularTroop(CharacterObject troop) => troop != null &&
            !troop.IsHero && (troop.Occupation == Occupation.Soldier ||
                troop.Occupation == Occupation.Mercenary);

        public static int NormalizeGarrison(MobileParty garrison)
        {
            if (garrison == null) return 0;
            TroopRoster roster = garrison.MemberRoster;
            int dismissed = 0;
            foreach (TroopRosterElement element in
                new List<TroopRosterElement>(roster.GetTroopRoster()))
            {
                if (element.Character == null || element.Character.IsHero ||
                    IsRegularTroop(element.Character)) continue;
                roster.AddToCounts(element.Character, -element.Number, false,
                    -element.WoundedNumber, 0, true, -1);
                dismissed += element.Number;
            }
            return dismissed;
        }

        private static int CalculateDailyConformity(Settlement settlement)
        {
            float hourly = 10f;
            Hero governor = settlement.Town?.Governor;
            if (governor != null)
                hourly += governor.GetSkillValue(DefaultSkills.Leadership) * 0.05f;
            return Math.Max(0, MathF.Round(hourly * CampaignTime.HoursInDay));
        }

        private static int GetWoundedCount(TroopRoster roster,
            CharacterObject troop, int count)
        {
            foreach (TroopRosterElement element in roster.GetTroopRoster())
                if (element.Character == troop)
                    return Math.Min(count, element.WoundedNumber);
            return 0;
        }
    }
}
