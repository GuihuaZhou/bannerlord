using ModifiedArmy.common;
using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// Lets AI fortifications absorb eligible non-bandit prisoners into their
    /// garrisons. Every offered prisoner is evaluated by the same composition,
    /// wage and clan-budget rules used by the other AI recruitment sources.
    /// </summary>
    public class GarrisonRecruitFromPrisonersBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(
                this,
                OnDailySettlementTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnDailySettlementTick(Settlement settlement)
        {
            Town town = settlement?.Town;

            // Player settlements retain direct player control. This behavior
            // is an AI recruitment policy and must not alter their prisoners.
            if (settlement?.IsFortification != true ||
                town?.GarrisonParty == null ||
                settlement.OwnerClan == null ||
                settlement.OwnerClan == Clan.PlayerClan)
            {
                return;
            }

            List<RecruitmentCandidate> candidates =
                CollectPrisonerCandidates(settlement.Party.PrisonRoster);
            int dailyLimit = GetDailyRecruitmentLimit(town);
            RecruitmentPlan plan = null;
            int recruited = 0;

            if (dailyLimit > 0 && candidates.Count > 0)
            {
                plan = RecruitmentModelManager.Model.BuildPlan(
                    town.GarrisonParty,
                    candidates);
                recruited = ExecutePlan(
                    settlement,
                    town.GarrisonParty,
                    plan,
                    dailyLimit);
            }

            // Recruitment runs first. Ordinary prisoners that remain are sold
            // only when the prison exceeds half of its capacity. Hero
            // prisoners are never included in the sale roster.
            int sold = SellExcessPrisoners(settlement);

            if (plan != null)
            {
                LogPlan(
                    town,
                    plan,
                    GetOfferedCount(candidates),
                    recruited,
                    dailyLimit);
            }

            LogSale(town, sold);
        }

        /// <summary>
        /// Preserves native daily garrison growth and party capacity as the
        /// transaction limit. The unified model applies finance constraints.
        /// </summary>
        private static int GetDailyRecruitmentLimit(Town town)
        {
            ExplainedNumber baseChange = Campaign.Current.Models
                .SettlementGarrisonModel
                .CalculateBaseGarrisonChange(town.Settlement, false);
            int freeSlots = Math.Max(
                0,
                town.GarrisonParty.Party.PartySizeLimit
                    - town.GarrisonParty.Party.NumberOfAllMembers);

            return Math.Min(
                freeSlots,
                Math.Max(0, (int)baseChange.ResultNumber));
        }

        /// <summary>
        /// Selects valuable regular troops for the unified recruitment plan.
        /// Bandits and other non-military characters are never candidates.
        /// Tier-four troops and above are valuable by quality; configured fief
        /// troop lines are valuable even before reaching tier four.
        /// </summary>
        private static List<RecruitmentCandidate>
            CollectPrisonerCandidates(TroopRoster prisonRoster)
        {
            List<RecruitmentCandidate> result =
                new List<RecruitmentCandidate>();

            if (prisonRoster == null)
            {
                return result;
            }

            foreach (TroopRosterElement element in
                prisonRoster.GetTroopRoster())
            {
                CharacterObject troop = element.Character;

                if (!IsHighValueRecruitmentTarget(troop))
                {
                    continue;
                }

                result.Add(new RecruitmentCandidate(
                    troop,
                    element.Number,
                    RecruitmentSource.Prisoner));
            }

            return result;
        }

        private static bool IsHighValueRecruitmentTarget(
            CharacterObject troop)
        {
            if (troop == null ||
                troop.IsHero ||
                (troop.Occupation != Occupation.Soldier &&
                    troop.Occupation != Occupation.Mercenary))
            {
                return false;
            }

            if (troop.Tier >= 4)
            {
                return true;
            }

            SoldierType type = SoldierTypeClassifier.GetSoldierType(troop);
            return type == SoldierType.Retinue ||
                type == SoldierType.Sergeant ||
                type == SoldierType.Marine ||
                type == SoldierType.Slave;
        }

        /// <summary>
        /// Transfers approved prisoners directly between rosters. Recruitment
        /// grants no ransom gold and fires no prisoner-sale event. Wounded
        /// state is retained during the transfer.
        /// </summary>
        private static int ExecutePlan(
            Settlement settlement,
            MobileParty garrison,
            RecruitmentPlan plan,
            int dailyLimit)
        {
            TroopRoster prisonRoster = settlement.Party.PrisonRoster;
            int recruited = 0;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (recruited >= dailyLimit)
                {
                    break;
                }

                CharacterObject troop = evaluation.Troop;
                int currentCount = troop == null
                    ? 0
                    : prisonRoster.GetTroopCount(troop);
                int transferCount = Math.Min(
                    Math.Min(evaluation.RecruitableCount, currentCount),
                    dailyLimit - recruited);

                if (transferCount <= 0)
                {
                    continue;
                }

                int woundedCount = GetWoundedTransferCount(
                    prisonRoster,
                    troop,
                    transferCount);
                prisonRoster.AddToCounts(
                    troop,
                    -transferCount,
                    false,
                    -woundedCount,
                    0,
                    true,
                    -1);
                garrison.MemberRoster.AddToCounts(
                    troop,
                    transferCount,
                    false,
                    woundedCount,
                    0,
                    true,
                    -1);
                ClanRecruitmentBudgetManager.CommitRecruitment(
                    garrison,
                    transferCount,
                    0f,
                    evaluation.UnitDailyWage);
                recruited += transferCount;
            }

            return recruited;
        }

        private static int GetWoundedTransferCount(
            TroopRoster roster,
            CharacterObject troop,
            int transferCount)
        {
            foreach (TroopRosterElement element in roster.GetTroopRoster())
            {
                if (element.Character == troop)
                {
                    return Math.Min(transferCount, element.WoundedNumber);
                }
            }

            return 0;
        }

        /// <summary>
        /// Uses the native prisoner-sale transaction for disposable captives.
        /// High-value recruitment targets and heroes remain in custody even
        /// when selling every disposable prisoner cannot reach the target.
        /// </summary>
        private static int SellExcessPrisoners(Settlement settlement)
        {
            TroopRoster prisonRoster = settlement.Party.PrisonRoster;
            int targetCount = Math.Max(
                0,
                settlement.Party.PrisonerSizeLimit / 2);
            int amountToSell = Math.Max(
                0,
                prisonRoster.TotalManCount - targetCount);

            if (amountToSell <= 0)
            {
                return 0;
            }

            TroopRoster saleRoster = TroopRoster.CreateDummyTroopRoster();
            IEnumerable<TroopRosterElement> orderedPrisoners = prisonRoster
                .GetTroopRoster()
                .Where(element =>
                    element.Character != null &&
                    !element.Character.IsHero &&
                    !IsHighValueRecruitmentTarget(element.Character))
                .OrderBy(element => element.Character.Tier);

            foreach (TroopRosterElement element in orderedPrisoners)
            {
                int count = Math.Min(amountToSell, element.Number);

                if (count <= 0)
                {
                    continue;
                }

                int woundedCount = Math.Min(count, element.WoundedNumber);
                saleRoster.AddToCounts(
                    element.Character,
                    count,
                    false,
                    woundedCount,
                    0,
                    true,
                    -1);
                amountToSell -= count;

                if (amountToSell <= 0)
                {
                    break;
                }
            }

            int sold = saleRoster.TotalRegulars;

            if (sold > 0)
            {
                SellPrisonersAction.ApplyForSelectedPrisoners(
                    settlement.Party,
                    null,
                    saleRoster);
            }

            return sold;
        }

        private static int GetOfferedCount(
            IEnumerable<RecruitmentCandidate> candidates)
        {
            int count = 0;

            foreach (RecruitmentCandidate candidate in candidates)
            {
                count += candidate.AvailableCount;
            }

            return count;
        }

        private static void LogSale(Town town, int soldCount)
        {
            if (soldCount <= 0 ||
                !RecruitmentLogFilter.ShouldLog(town.GarrisonParty))
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_ai_recruitment_prisoner_sale");
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("SOLD", soldCount);
            message.SetTextVariable(
                "REMAINING",
                town.Settlement.Party.PrisonRoster.TotalManCount);
            ModLogger.Info(message.ToString());
        }

        private static void LogPlan(
            Town town,
            RecruitmentPlan plan,
            int offeredCount,
            int approvedCount,
            int dailyLimit)
        {
            if (!RecruitmentLogFilter.ShouldLog(town.GarrisonParty))
            {
                return;
            }

            RecruitmentLimitReason mainLimit = RecruitmentLimitReason.None;

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.RecruitableCount <= 0)
                {
                    mainLimit = evaluation.PrimaryLimit;
                    break;
                }
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_ai_recruitment_prisoner_plan");
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", approvedCount);
            message.SetTextVariable("DAILY_LIMIT", dailyLimit);
            message.SetTextVariable("LIMIT", GetLimitText(mainLimit));

            if (approvedCount > 0)
            {
                ModLogger.Info(message.ToString());
            }
            else
            {
                ModLogger.Debug(message.ToString());
            }
        }

        private static TextObject GetLimitText(
            RecruitmentLimitReason reason)
        {
            switch (reason)
            {
                case RecruitmentLimitReason.InvalidParty:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_invalid_party");
                case RecruitmentLimitReason.InvalidTroop:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_invalid_troop");
                case RecruitmentLimitReason.PartySize:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_party_size");
                case RecruitmentLimitReason.CombatRole:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_combat_role");
                case RecruitmentLimitReason.Quality:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_quality");
                case RecruitmentLimitReason.WageLimit:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_wage");
                case RecruitmentLimitReason.RecruitmentCost:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_cost");
                case RecruitmentLimitReason.MaintenanceFunds:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_maintenance");
                default:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_none");
            }
        }
    }
}
