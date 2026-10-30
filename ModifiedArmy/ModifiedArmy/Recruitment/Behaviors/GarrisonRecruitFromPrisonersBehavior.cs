using ModifiedArmy.Recruitment;
using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
using ModifiedArmy.Recruitment.Services;
using ModifiedArmy.common;
using ModifiedArmy.Recruitment.Orders.Behaviors;
using ModifiedArmy.Recruitment.Orders.Models;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
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
    /// garrisons. Prisoner retention has its own simple legality and quality
    /// policy; every ready recruit still passes the shared capacity, wage and
    /// clan-budget rules used by the other AI recruitment sources.
    /// </summary>
    public class GarrisonRecruitFromPrisonersBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(
                this,
                OnDailySettlementTick);
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(
                this,
                OnWeeklyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnDailySettlementTick(Settlement settlement)
        {
            Town town = settlement?.Town;

            if (settlement?.IsFortification != true || town == null)
            {
                return;
            }

            SettlementManpowerProductionBehavior pools = Campaign.Current
                .GetCampaignBehavior<SettlementManpowerProductionBehavior>();
            pools?.EnsureDailyProduction(settlement);
            int availableProfessionalOffers = pools
                ?.GetAvailableProfessionalOffers(settlement)
                .Values.Sum() ?? 0;
            SettlementManpowerProductionData productionData = pools
                ?.GetOrCreateProductionData(settlement);
            int day = (int)CampaignTime.Now.ToDays;
            productionData?.RecordProfessionalRecruitment(day, 0);

            bool orderSchedulingEnabled = MilitaryConfigManager.Instance
                .GetActiveConfig()?.AiRecruitment.ForceManagement
                .GarrisonRecruitmentOrders.Enabled == true;
            if (orderSchedulingEnabled && settlement.OwnerClan != Clan.PlayerClan)
            {
                return;
            }

            if (town.GarrisonParty == null &&
                town.GarrisonAutoRecruitmentIsEnabled &&
                settlement.Party.MapEvent == null &&
                settlement.Party.SiegeEvent == null)
            {
                settlement.AddGarrisonParty();
            }

            if (town.GarrisonParty == null ||
                settlement.OwnerClan == null ||
                !town.GarrisonAutoRecruitmentIsEnabled ||
                settlement.Party.MapEvent != null ||
                settlement.Party.SiegeEvent != null)
            {
                if (availableProfessionalOffers > 0 &&
                    RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
                {
                    TextObject blockedMessage = GameTexts.FindText(
                        "str_modifiedarmy_manpower_recruitment_blocked");
                    blockedMessage.SetTextVariable("DAY", day);
                    blockedMessage.SetTextVariable(
                        "SETTLEMENT_NAME",
                        settlement.Name);
                    blockedMessage.SetTextVariable(
                        "OFFERS",
                        availableProfessionalOffers);
                    blockedMessage.SetTextVariable(
                        "GARRISON",
                        GetBooleanLogText(town.GarrisonParty != null));
                    blockedMessage.SetTextVariable(
                        "OWNER",
                        GetBooleanLogText(settlement.OwnerClan != null));
                    blockedMessage.SetTextVariable(
                        "AUTO_RECRUIT",
                        GetBooleanLogText(
                            town.GarrisonAutoRecruitmentIsEnabled));
                    blockedMessage.SetTextVariable(
                        "MAP_EVENT",
                        GetBooleanLogText(
                            settlement.Party.MapEvent != null));
                    blockedMessage.SetTextVariable(
                        "SIEGE",
                        GetBooleanLogText(
                            settlement.Party.SiegeEvent != null));
                    ModLogger.Debug(blockedMessage.ToString());
                }
                return;
            }

            int dailyLimit = GetDailyRecruitmentLimit(town);
            TroopRoster prisonRoster = settlement.Party.PrisonRoster;
            int initialPrisonerCount = prisonRoster.TotalRegulars;

            int targetCount;
            int readyCount;
            int sold;
            List<RecruitmentCandidate> prisonerCandidates =
                SelectAndPreparePrisonerCandidates(
                    settlement,
                    out targetCount,
                    out readyCount,
                    out sold);
            List<RecruitmentCandidate> candidates = pools == null
                ? new List<RecruitmentCandidate>()
                : pools.GetAvailableProfessionalOffers(settlement)
                    .Where(entry => entry.Key != null && entry.Value > 0)
                    .Select(entry => new RecruitmentCandidate(
                        entry.Key,
                        entry.Value,
                        RecruitmentSource.ProfessionalProduction))
                    .ToList();
            candidates.AddRange(prisonerCandidates);
            RecruitmentPlan plan = null;
            int recruited = 0;
            int professionalRecruited = 0;
            int prisonerRecruited = 0;

            if (dailyLimit > 0 && candidates.Count > 0)
            {
                plan = RecruitmentModelManager.Model.BuildPlan(
                    town.GarrisonParty,
                    candidates);
                recruited = ExecuteUnifiedPlan(
                    settlement,
                    town.GarrisonParty,
                    plan,
                    dailyLimit,
                    pools,
                    out professionalRecruited,
                    out prisonerRecruited);
            }

            productionData?.RecordProfessionalRecruitment(
                day,
                professionalRecruited);

            int professionalOffered = candidates.Where(candidate =>
                    candidate.Source == RecruitmentSource.ProfessionalProduction)
                .Sum(candidate => candidate.AvailableCount);
            int professionalApproved = plan?.Evaluations
                .Where(evaluation => evaluation.Source ==
                    RecruitmentSource.ProfessionalProduction)
                .Sum(evaluation => evaluation.RecruitableCount) ?? 0;
            if (professionalOffered > 0 &&
                RecruitmentLogFilter.ShouldLog(settlement.OwnerClan))
            {
                // Read capacity from the live roster after execution, but reuse the
                // plan that was already evaluated. Logging must never rebuild the
                // plan because evaluation can depend on mutable financial state.
                int reservedEscorts = GarrisonRecruitmentCapacity
                    .GetActiveSupplyEscortCount(settlement);
                int hardFreeSlots = GarrisonRecruitmentCapacity.GetHardFreeSlots(
                    town.GarrisonParty,
                    reservedEscorts);
                int softFreeSlots = GarrisonRecruitmentCapacity
                    .GetSoftRecruitmentFreeSlots(
                        town.GarrisonParty,
                        reservedEscorts);
                TextObject recruitmentMessage = GameTexts.FindText(
                    professionalRecruited > 0
                        ? "str_modifiedarmy_manpower_recruitment_completed"
                        : "str_modifiedarmy_manpower_recruitment_skipped");
                recruitmentMessage.SetTextVariable(
                    "SETTLEMENT_NAME",
                    settlement.Name);
                recruitmentMessage.SetTextVariable(
                    "GARRISON_BEFORE",
                    town.GarrisonParty.MemberRoster.TotalManCount - recruited);
                recruitmentMessage.SetTextVariable(
                    "OFFERS",
                    professionalOffered);
                recruitmentMessage.SetTextVariable("DAILY_LIMIT", dailyLimit);
                recruitmentMessage.SetTextVariable(
                    "APPROVED",
                    professionalApproved);
                recruitmentMessage.SetTextVariable(
                    "PROFESSIONAL_ACTUAL",
                    professionalRecruited);
                recruitmentMessage.SetTextVariable(
                    "PRISONER_ACTUAL",
                    prisonerRecruited);
                recruitmentMessage.SetTextVariable(
                    "GARRISON_AFTER",
                    town.GarrisonParty.MemberRoster.TotalManCount);
                recruitmentMessage.SetTextVariable("RESERVED_ESCORTS", reservedEscorts);
                recruitmentMessage.SetTextVariable("HARD_FREE", hardFreeSlots);
                recruitmentMessage.SetTextVariable("SOFT_FREE", softFreeSlots);
                recruitmentMessage.SetTextVariable(
                    "PRIMARY_LIMIT",
                    GetProfessionalFinalLimit(
                        plan,
                        dailyLimit,
                        professionalApproved,
                        professionalRecruited,
                        hardFreeSlots,
                        softFreeSlots));
                ModLogger.Debug(recruitmentMessage.ToString());
            }
            if (plan != null && professionalOffered > 0)
            {
                LogProfessionalRecruitment(
                    town,
                    professionalOffered,
                    professionalRecruited,
                    plan);
            }

            if (initialPrisonerCount > 0 || sold > 0)
            {
                LogDailyResult(
                    town,
                    initialPrisonerCount,
                    sold,
                    targetCount,
                    readyCount,
                    dailyLimit,
                    plan,
                    prisonerRecruited);
            }
        }

        /// <summary>
        /// Reads the first limiting reason from professional evaluations already
        /// present in this plan. None means the model approved every candidate and
        /// a daily or capacity boundary may have limited the final execution.
        /// </summary>
        private static RecruitmentLimitReason GetProfessionalPrimaryLimit(
            RecruitmentPlan plan)
        {
            if (plan?.Evaluations == null)
                return RecruitmentLimitReason.None;
            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
                if (evaluation.Source == RecruitmentSource.ProfessionalProduction &&
                    evaluation.RecruitableCount < evaluation.RequestedCount)
                    return evaluation.PrimaryLimit;
            return RecruitmentLimitReason.None;
        }

        /// <summary>
        /// Explains the final bottleneck across the daily quota, hard capacity,
        /// soft recruitment target, model approval, and execution stages. Capacity
        /// is checked before the model reason because a plan may be absent when the
        /// native daily limit is already zero.
        /// </summary>
        private static TextObject GetProfessionalFinalLimit(
            RecruitmentPlan plan,
            int dailyLimit,
            int approved,
            int recruited,
            int hardFreeSlots,
            int softFreeSlots)
        {
            if (hardFreeSlots <= 0)
                return GameTexts.FindText(
                    "str_modifiedarmy_recruit_limit_hard_capacity");
            if (softFreeSlots <= 0)
                return GameTexts.FindText(
                    "str_modifiedarmy_recruit_limit_soft_capacity");
            if (dailyLimit <= 0)
                return GameTexts.FindText(
                    "str_modifiedarmy_recruit_limit_daily_quota");

            RecruitmentLimitReason modelLimit =
                GetProfessionalPrimaryLimit(plan);
            if (modelLimit != RecruitmentLimitReason.None)
                return GetLimitText(modelLimit);
            if (approved <= 0)
                return GameTexts.FindText(
                    "str_modifiedarmy_recruit_limit_no_approved_candidate");
            if (recruited < approved)
                return GameTexts.FindText(
                    "str_modifiedarmy_recruit_limit_execution_changed");
            return GameTexts.FindText(
                "str_modifiedarmy_recruit_limit_none");
        }

        private static TextObject GetBooleanLogText(bool value)
        {
            return new TextObject(
                value
                    ? "{=ModifiedArmy_Common_Enabled}Enabled"
                    : "{=ModifiedArmy_Common_Disabled}Disabled");
        }

        /// <summary>
        /// Executes one priority-ordered plan for both sources, ensuring that
        /// neither professional troops nor prisoners reserve the whole daily
        /// quota before composition evaluation sees the other source.
        /// </summary>
        private static int ExecuteUnifiedPlan(
            Settlement settlement,
            MobileParty garrison,
            RecruitmentPlan plan,
            int dailyLimit,
            SettlementManpowerProductionBehavior pools,
            out int professionalRecruited,
            out int prisonerRecruited)
        {
            return GarrisonRecruitmentExecutionService.Execute(
                settlement, garrison, plan, dailyLimit, pools,
                out professionalRecruited, out prisonerRecruited);
        }

        private static void LogProfessionalRecruitment(
            Town town,
            int offeredCount,
            int recruitedCount,
            RecruitmentPlan plan)
        {
            if (!RecruitmentLogFilter.ShouldLog(town.GarrisonParty) ||
                recruitedCount <= 0)
            {
                return;
            }

            RecruitmentLimitReason reason = RecruitmentLimitReason.None;
            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
            {
                if (evaluation.RecruitableCount < evaluation.RequestedCount)
                {
                    reason = evaluation.PrimaryLimit;
                    break;
                }
            }

            // This method is reached only after at least one professional troop
            // was recruited. Zero-approval diagnostics are emitted earlier from
            // the already-computed plan and must not rerun recruitment evaluation.
            // A successful daily refill is routine file information, not a
            // player-facing military Notice.
            string textId = recruitedCount > 0
                ? "str_modifiedarmy_ai_recruitment_garrison_plan"
                : "str_modifiedarmy_garrison_professional_recruitment";
            TextObject message = GameTexts.FindText(textId);
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", recruitedCount);
            message.SetTextVariable("RECRUITED", recruitedCount);
            message.SetTextVariable("LIMIT", GetLimitText(reason));

            ModLogger.Info(message.ToString());
        }

        private static void OnWeeklyTick()
        {
            foreach (Settlement settlement in Campaign.Current.Settlements)
            {
                Town town = settlement?.Town;

                if (settlement?.IsFortification != true ||
                    town?.GarrisonParty == null ||
                    settlement.OwnerClan == Clan.PlayerClan)
                {
                    continue;
                }

                NormalizeGarrisonNow(town);
            }
        }

        public static int NormalizeGarrisonNow(Town town)
        {
            if (town?.GarrisonParty == null)
            {
                return 0;
            }

            int dismissed = NormalizeGarrison(town.GarrisonParty);
            LogWeeklyAdjustment(town, dismissed);
            return dismissed;
        }

        /// <summary>
        /// Uses the native maximum automatic recruitment count as a daily
        /// transaction limit. CalculateBaseGarrisonChange is intentionally not
        /// used here: that value describes rebellion and issue-driven natural
        /// garrison changes, and is normally zero for an ordinary settlement.
        /// The unified model applies all financial constraints separately.
        /// </summary>
        private static int GetDailyRecruitmentLimit(Town town)
        {
            int maximumDailyRecruitment = Campaign.Current.Models
                .SettlementGarrisonModel
                .GetMaximumDailyAutoRecruitmentCount(town);
            int freeSlots = GarrisonRecruitmentCapacity
                .GetHardFreeSlots(town.GarrisonParty);

            return Math.Min(
                freeSlots,
                Math.Max(0, maximumDailyRecruitment));
        }

        /// <summary>
        /// Returns the same safe, stock-aware upper bound used by the custom
        /// recruitment path without consuming manpower or prisoner progress.
        /// </summary>
        public static int GetPredictedDailyRecruitmentCount(Town town)
        {
            Settlement settlement = town?.Settlement;
            MobileParty garrison = town?.GarrisonParty;
            if (settlement == null || garrison == null ||
                !town.GarrisonAutoRecruitmentIsEnabled ||
                settlement.Party.MapEvent != null ||
                settlement.Party.SiegeEvent != null)
            {
                return 0;
            }

            bool orderSchedulingEnabled = MilitaryConfigManager.Instance
                .GetActiveConfig()?.AiRecruitment.ForceManagement
                .GarrisonRecruitmentOrders.Enabled == true;
            int orderRemaining = int.MaxValue;
            if (orderSchedulingEnabled && settlement.OwnerClan != Clan.PlayerClan)
            {
                GarrisonRecruitmentOrderRecord order = Campaign.Current
                    .GetCampaignBehavior<GarrisonRecruitmentOrderBehavior>()?
                    .GetActive(settlement);
                if (order == null || order.Remaining <= 0) return 0;
                orderRemaining = order.Remaining;
            }

            List<RecruitmentCandidate> candidates =
                new List<RecruitmentCandidate>();
            SettlementManpowerProductionBehavior pools = Campaign.Current
                .GetCampaignBehavior<SettlementManpowerProductionBehavior>();
            if (pools != null)
            {
                candidates.AddRange(pools.GetPredictedProfessionalOffers(
                        settlement)
                    .Where(entry => entry.Key != null && entry.Value > 0)
                    .Select(entry => new RecruitmentCandidate(
                        entry.Key,
                        entry.Value,
                        RecruitmentSource.ProfessionalProduction)));
            }

            foreach (TroopRosterElement element in
                settlement.Party.PrisonRoster.GetTroopRoster())
            {
                CharacterObject troop = element.Character;
                if (troop == null || troop.IsHero || element.Number <= 0)
                {
                    continue;
                }

                int conformity = Campaign.Current.Models
                    .PrisonerRecruitmentCalculationModel
                    .GetConformityNeededToRecruitPrisoner(troop);
                int ready = conformity <= 0
                    ? element.Number
                    : Math.Min(
                        element.Number,
                        settlement.Party.PrisonRoster.GetElementXp(troop) /
                            conformity);
                if (ready > 0)
                {
                    candidates.Add(new RecruitmentCandidate(
                        troop,
                        ready,
                        RecruitmentSource.Prisoner,
                        new PrisonerRecruitmentOffer(conformity)));
                }
            }

            if (candidates.Count == 0)
            {
                return 0;
            }

            RecruitmentPlan plan = orderRemaining == int.MaxValue
                ? RecruitmentModelManager.Model.BuildPlan(garrison, candidates)
                : RecruitmentModelManager.Model.BuildPlan(
                    garrison, candidates,
                    new AiGarrisonOrderRecruitmentContext(orderRemaining));
            return Math.Min(
                Math.Min(GetDailyRecruitmentLimit(town), orderRemaining),
                plan.Evaluations.Sum(result => result.RecruitableCount));
        }

        /// <summary>
        /// Retains eligible military prisoners within the prison reserve and
        /// adds one day of conformity to retained targets. The retention rule
        /// is intentionally independent of army composition: legal troops of
        /// tier three or higher are kept, while lower tiers remain available
        /// to the existing economic sale policy. A
        /// settlement party has no MobileParty, so the native hourly method
        /// cannot be called safely. The same base rate, governor leadership
        /// bonus and active model threshold are applied directly to the
        /// settlement prison roster instead.
        /// </summary>
        private static List<RecruitmentCandidate>
            SelectAndPreparePrisonerCandidates(
                Settlement settlement,
                out int targetCount,
                out int readyCount,
                out int soldCount)
        {
            return PrisonerGarrisonRecruitmentService.SelectAndPrepare(
                settlement, out targetCount, out readyCount, out soldCount);
        }

        private static void LogDailyResult(
            Town town,
            int initialPrisonerCount,
            int soldCount,
            int targetCount,
            int readyCount,
            int dailyLimit,
            RecruitmentPlan plan,
            int recruitedCount)
        {
            if (!RecruitmentLogFilter.ShouldLog(town.GarrisonParty))
            {
                return;
            }

            if (recruitedCount > 0)
            {
                TextObject message = GameTexts.FindText(
                    "str_modifiedarmy_garrison_prisoner_recruited");
                message.SetTextVariable("SETTLEMENT_NAME", town.Name);
                message.SetTextVariable("RECRUITED", recruitedCount);
                message.SetTextVariable("SOLD", soldCount);
                ModLogger.Info(message.ToString());
                return;
            }

            TextObject reason;

            if (targetCount <= 0)
            {
                reason = GameTexts.FindText(
                    "str_modifiedarmy_garrison_prisoner_no_retention_target");
            }
            else if (readyCount <= 0)
            {
                reason = GameTexts.FindText(
                    "str_modifiedarmy_garrison_prisoner_no_conformity");
            }
            else if (dailyLimit <= 0)
            {
                reason = GameTexts.FindText(
                    "str_modifiedarmy_garrison_prisoner_no_daily_capacity");
            }
            else
            {
                reason = GetPlanLimitText(plan);
            }

            TextObject skippedMessage = GameTexts.FindText(
                "str_modifiedarmy_garrison_prisoner_skipped");
            skippedMessage.SetTextVariable("SETTLEMENT_NAME", town.Name);
            skippedMessage.SetTextVariable("INITIAL", initialPrisonerCount);
            skippedMessage.SetTextVariable("SOLD", soldCount);
            skippedMessage.SetTextVariable("REASON", reason);
            ModLogger.Debug(skippedMessage.ToString());
        }

        private static TextObject GetPlanLimitText(RecruitmentPlan plan)
        {
            if (plan == null)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_garrison_prisoner_no_plan");
            }

            foreach (RecruitmentEvaluationResult evaluation in
                plan.Evaluations)
            {
                if (evaluation.RecruitableCount <= 0)
                {
                    return GetLimitText(evaluation.PrimaryLimit);
                }
            }

            return GameTexts.FindText(
                "str_modifiedarmy_garrison_prisoner_plan_rejected");
        }

        /// <summary>
        /// Removes only characters that are not valid garrison soldiers.
        /// The prisoner retention policy never dismisses existing legal troops.
        /// </summary>
        private static int NormalizeGarrison(MobileParty garrison)
        {
            return PrisonerGarrisonRecruitmentService.NormalizeGarrison(
                garrison);
        }

        private static bool IsRegularGarrisonTroop(CharacterObject troop)
        {
            return PrisonerGarrisonRecruitmentService.IsRegularTroop(troop);
        }

        private static void LogWeeklyAdjustment(
            Town town,
            int dismissedCount)
        {
            if (dismissedCount <= 0 ||
                !RecruitmentLogFilter.ShouldLog(town.GarrisonParty))
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_garrison_weekly_adjustment");
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("DISMISSED", dismissedCount);
            ModLogger.Notice(message.ToString());
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
                case RecruitmentLimitReason.WageLimit:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_wage");
                case RecruitmentLimitReason.RecruitmentCost:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_cost");
                case RecruitmentLimitReason.MaintenanceFunds:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_maintenance");
                case RecruitmentLimitReason.BanditTroopExcluded:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_bandit");
                default:
                    return GameTexts.FindText(
                        "str_modifiedarmy_recruit_limit_none");
            }
        }

    }
}
