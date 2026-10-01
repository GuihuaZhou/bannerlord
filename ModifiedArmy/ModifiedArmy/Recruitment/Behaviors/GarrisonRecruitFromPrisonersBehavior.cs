using ModifiedArmy.Recruitment;
using ModifiedArmy.Recruitment.Classification;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Recruitment.Finance;
using ModifiedArmy.Recruitment.Models;
using ModifiedArmy.Recruitment.Pools.Behaviors;
using ModifiedArmy.Recruitment.Pools.Models;
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

            // Player settlements retain direct player control. This behavior
            // is an AI recruitment policy and must not alter their prisoners.
            if (settlement?.IsFortification != true ||
                town?.GarrisonParty == null ||
                settlement.OwnerClan == null ||
                settlement.OwnerClan == Clan.PlayerClan)
            {
                return;
            }

            // Professional manpower has first claim on the native daily
            // garrison recruitment quota. Prisoners may use only what remains
            // so the two sources cannot double the intended daily growth.
            int dailyLimit = GetDailyRecruitmentLimit(town);
            int professionalRecruited = RecruitProfessionalTroops(
                settlement,
                town.GarrisonParty,
                dailyLimit);
            dailyLimit = Math.Max(0, dailyLimit - professionalRecruited);

            TroopRoster prisonRoster = settlement.Party.PrisonRoster;
            int initialPrisonerCount = prisonRoster.TotalRegulars;

            // Hero prisoners never participate in this system and do not
            // cause a daily recruitment message by themselves.
            if (initialPrisonerCount <= 0)
            {
                return;
            }

            int targetCount;
            int readyCount;
            int sold;
            List<RecruitmentCandidate> candidates =
                SelectAndPreparePrisonerCandidates(
                    settlement,
                    town.GarrisonParty,
                    out targetCount,
                    out readyCount,
                    out sold);
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

            LogDailyResult(
                town,
                initialPrisonerCount,
                sold,
                targetCount,
                readyCount,
                dailyLimit,
                plan,
                recruited);
        }

        /// <summary>
        /// Recruits from the settlement's professional pool through the same
        /// composition, wage and clan-budget model used by mobile parties.
        /// Garrison purchases are charged through AutoRecruitmentExpenses,
        /// matching Bannerlord's deferred settlement accounting.
        /// </summary>
        private static int RecruitProfessionalTroops(
            Settlement settlement,
            MobileParty garrison,
            int dailyLimit)
        {
            if (dailyLimit <= 0)
            {
                return 0;
            }

            SettlementRecruitmentPoolBehavior pools = Campaign.Current
                .GetCampaignBehavior<SettlementRecruitmentPoolBehavior>();
            if (pools == null)
            {
                return 0;
            }

            IReadOnlyDictionary<CharacterObject, int> available =
                pools.GetAvailableTroops(
                    settlement,
                    RecruitmentPoolKind.Professional);
            List<RecruitmentCandidate> candidates = available
                .Where(entry => entry.Key != null && entry.Value > 0)
                .Select(entry => new RecruitmentCandidate(
                    entry.Key,
                    entry.Value,
                    RecruitmentSource.Professional))
                .ToList();
            if (candidates.Count == 0)
            {
                return 0;
            }

            RecruitmentPlan plan = RecruitmentModelManager.Model.BuildPlan(
                garrison,
                candidates);
            int recruited = 0;

            foreach (RecruitmentEvaluationResult evaluation in plan.Evaluations)
            {
                int count = Math.Min(
                    evaluation.RecruitableCount,
                    dailyLimit - recruited);
                if (evaluation.Troop == null || count <= 0 ||
                    !pools.TryConsume(
                        settlement,
                        RecruitmentPoolKind.Professional,
                        evaluation.Troop,
                        count))
                {
                    continue;
                }

                garrison.MemberRoster.AddToCounts(
                    evaluation.Troop,
                    count,
                    false,
                    0,
                    0,
                    true,
                    -1);
                settlement.OwnerClan.AutoRecruitmentExpenses +=
                    evaluation.UnitRecruitmentCost * count;
                ClanRecruitmentBudgetManager.CommitRecruitment(
                    garrison,
                    count,
                    evaluation.UnitRecruitmentCost,
                    evaluation.UnitDailyWage);
                recruited += count;

                if (recruited >= dailyLimit)
                {
                    break;
                }
            }

            LogProfessionalRecruitment(
                settlement.Town,
                candidates.Sum(candidate => candidate.AvailableCount),
                recruited,
                plan);
            return recruited;
        }

        private static void LogProfessionalRecruitment(
            Town town,
            int offeredCount,
            int recruitedCount,
            RecruitmentPlan plan)
        {
            if (!RecruitmentLogFilter.ShouldLog(town.GarrisonParty))
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

            // Keep successful and failed recruitment at Info so routine
            // garrison activity does not surface as a player-facing Notice, and
            // neither message exposes the underlying manpower-pool mechanism.
            string textId = recruitedCount > 0
                ? "str_modifiedarmy_ai_recruitment_garrison_plan"
                : "str_modifiedarmy_garrison_professional_recruitment";
            TextObject message = GameTexts.FindText(textId);
            message.SetTextVariable("SETTLEMENT_NAME", town.Name);
            message.SetTextVariable("OFFERED", offeredCount);
            message.SetTextVariable("APPROVED", recruitedCount);
            message.SetTextVariable("RECRUITED", recruitedCount);
            message.SetTextVariable("LIMIT", GetLimitText(reason));

            if (recruitedCount > 0)
            {
                ModLogger.Info(message.ToString());
            }
            else
            {
                ModLogger.Info(message.ToString());
            }
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
            int freeSlots = Math.Max(
                0,
                town.GarrisonParty.Party.PartySizeLimit
                    - town.GarrisonParty.Party.NumberOfAllMembers);

            return Math.Min(
                freeSlots,
                Math.Max(0, maximumDailyRecruitment));
        }

        /// <summary>
        /// Selects only prisoners that fill a current minimum-ratio shortage,
        /// sells every surplus or non-military prisoner, then adds one day of
        /// conformity to the retained targets. A
        /// settlement party has no MobileParty, so the native hourly method
        /// cannot be called safely. The same base rate, governor leadership
        /// bonus and active model threshold are applied directly to the
        /// settlement prison roster instead.
        /// </summary>
        private static List<RecruitmentCandidate>
            SelectAndPreparePrisonerCandidates(
                Settlement settlement,
                MobileParty garrison,
                out int targetCount,
                out int readyCount,
                out int soldCount)
        {
            List<RecruitmentCandidate> result =
                new List<RecruitmentCandidate>();
            TroopRoster prisonRoster = settlement.Party.PrisonRoster;
            TroopRoster saleRoster = TroopRoster.CreateDummyTroopRoster();
            targetCount = 0;
            readyCount = 0;
            soldCount = 0;

            if (prisonRoster == null)
            {
                return result;
            }

            ArmyCompositionTemplate template = GetGarrisonTemplate(garrison);
            int capacity = Math.Max(0, garrison.Party.PartySizeLimit);
            Dictionary<CombatRole, int> roleCounts =
                CreateRoleCounts(garrison.MemberRoster);
            Dictionary<TroopQuality, int> qualityCounts =
                CreateQualityCounts(garrison.MemberRoster);
            // Hero prisoners are never sold. Their occupied capacity is
            // deducted first so retained regular prisoners keep the whole
            // prison roster as close as possible to half of its limit.
            int remainingRetentionCapacity = Math.Max(
                0,
                settlement.Party.PrisonerSizeLimit / 2 -
                    prisonRoster.TotalHeroes);

            foreach (TroopRosterElement element in
                prisonRoster.GetTroopRoster())
            {
                CharacterObject troop = element.Character;

                if (troop == null || troop.IsHero)
                {
                    continue;
                }

                int retainedCount = GetTargetRetentionCount(
                    troop,
                    Math.Min(element.Number, remainingRetentionCapacity),
                    capacity,
                    template,
                    roleCounts,
                    qualityCounts);
                int sellCount = element.Number - retainedCount;

                if (sellCount > 0)
                {
                    saleRoster.AddToCounts(
                        troop,
                        sellCount,
                        false,
                        Math.Min(sellCount, element.WoundedNumber),
                        0,
                        true,
                        -1);
                }

                if (retainedCount <= 0)
                {
                    continue;
                }

                CombatRole role =
                    RecruitmentTroopClassifier.GetCombatRole(troop);
                TroopQuality quality =
                    RecruitmentTroopClassifier.GetQuality(troop);
                roleCounts[role] += retainedCount;
                qualityCounts[quality] += retainedCount;
                remainingRetentionCapacity -= retainedCount;
                targetCount += retainedCount;
                int dailyConformity = CalculateDailyConformity(settlement);
                prisonRoster.AddXpToTroop(troop, dailyConformity);
                int conformityNeeded = Campaign.Current.Models
                    .PrisonerRecruitmentCalculationModel
                    .GetConformityNeededToRecruitPrisoner(troop);
                int recruitableCount = conformityNeeded <= 0
                    ? retainedCount
                    : Math.Min(
                        retainedCount,
                        prisonRoster.GetElementXp(troop) /
                            conformityNeeded);

                if (recruitableCount <= 0)
                {
                    continue;
                }

                readyCount += recruitableCount;
                result.Add(new RecruitmentCandidate(
                    troop,
                    recruitableCount,
                    RecruitmentSource.Prisoner,
                    new PrisonerRecruitmentOffer(conformityNeeded)));
            }

            soldCount = saleRoster.TotalRegulars;

            if (soldCount > 0)
            {
                SellPrisonersAction.ApplyForSelectedPrisoners(
                    settlement.Party,
                    null,
                    saleRoster);
            }

            return result;
        }

        private static int GetTargetRetentionCount(
            CharacterObject troop,
            int availableCount,
            int capacity,
            ArmyCompositionTemplate template,
            IDictionary<CombatRole, int> roleCounts,
            IDictionary<TroopQuality, int> qualityCounts)
        {
            if (!IsRegularGarrisonTroop(troop) || availableCount <= 0)
            {
                return 0;
            }

            CombatRole role =
                RecruitmentTroopClassifier.GetCombatRole(troop);
            TroopQuality quality =
                RecruitmentTroopClassifier.GetQuality(troop);
            RatioRange roleRange = template.GetRange(role);
            RatioRange qualityRange = template.GetRange(quality);
            int roleMinimum = (int)Math.Ceiling(
                capacity * roleRange.MinimumRatio);
            int qualityMinimum = (int)Math.Ceiling(
                capacity * qualityRange.MinimumRatio);
            int roleMaximum = (int)Math.Floor(
                capacity * roleRange.MaximumRatio);
            int qualityMaximum = (int)Math.Floor(
                capacity * qualityRange.MaximumRatio);
            int roleShortage = Math.Max(
                0,
                roleMinimum - roleCounts[role]);
            int qualityShortage = Math.Max(
                0,
                qualityMinimum - qualityCounts[quality]);

            if (roleShortage <= 0 && qualityShortage <= 0)
            {
                return 0;
            }

            return Math.Max(
                0,
                Math.Min(
                    availableCount,
                    Math.Min(
                        Math.Max(roleShortage, qualityShortage),
                        Math.Min(
                            roleMaximum - roleCounts[role],
                            qualityMaximum - qualityCounts[quality]))));
        }

        private static int CalculateDailyConformity(
            Settlement settlement)
        {
            float hourlyConformity = 10f;
            Hero governor = settlement.Town?.Governor;

            if (governor != null)
            {
                hourlyConformity += governor.GetSkillValue(
                    DefaultSkills.Leadership) * 0.05f;
            }

            return Math.Max(
                0,
                MathF.Round(hourlyConformity * CampaignTime.HoursInDay));
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

                if (transferCount <= 0 ||
                    !(evaluation.Candidate?.SourceContext is
                        PrisonerRecruitmentOffer offer))
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
                    -offer.ConformityCost * transferCount,
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
                settlement.OwnerClan.AutoRecruitmentExpenses +=
                    evaluation.UnitRecruitmentCost * transferCount;
                ClanRecruitmentBudgetManager.CommitRecruitment(
                    garrison,
                    transferCount,
                    evaluation.UnitRecruitmentCost,
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
                    "str_modifiedarmy_garrison_prisoner_no_template_target");
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
            ModLogger.Info(skippedMessage.ToString());
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

        private static ArmyCompositionTemplate GetGarrisonTemplate(
            MobileParty garrison)
        {
            string cultureId = garrison.CurrentSettlement?.OwnerClan
                ?.Culture?.StringId
                ?? garrison.CurrentSettlement?.Culture?.StringId
                ?? "default";
            return RecruitmentModelManager.Templates.GetTemplate(
                cultureId,
                RecruitmentPartyType.Garrison);
        }

        private static Dictionary<CombatRole, int> CreateRoleCounts(
            TroopRoster roster)
        {
            Dictionary<CombatRole, int> result = Enum
                .GetValues(typeof(CombatRole))
                .Cast<CombatRole>()
                .ToDictionary(role => role, role => 0);

            foreach (TroopRosterElement element in roster.GetTroopRoster())
            {
                if (!IsRegularGarrisonTroop(element.Character))
                {
                    continue;
                }

                result[RecruitmentTroopClassifier.GetCombatRole(
                    element.Character)] += element.Number;
            }

            return result;
        }

        private static Dictionary<TroopQuality, int> CreateQualityCounts(
            TroopRoster roster)
        {
            Dictionary<TroopQuality, int> result = Enum
                .GetValues(typeof(TroopQuality))
                .Cast<TroopQuality>()
                .ToDictionary(quality => quality, quality => 0);

            foreach (TroopRosterElement element in roster.GetTroopRoster())
            {
                if (!IsRegularGarrisonTroop(element.Character))
                {
                    continue;
                }

                result[RecruitmentTroopClassifier.GetQuality(
                    element.Character)] += element.Number;
            }

            return result;
        }

        /// <summary>
        /// Removes non-military characters unconditionally, then repeatedly
        /// removes the lowest-tier soldier contributing to a hard template
        /// maximum. Establishment, wages and wounds do not independently
        /// select a soldier for this weekly composition adjustment.
        /// </summary>
        private static int NormalizeGarrison(MobileParty garrison)
        {
            TroopRoster roster = garrison.MemberRoster;
            int dismissed = 0;

            foreach (TroopRosterElement element in
                new List<TroopRosterElement>(roster.GetTroopRoster()))
            {
                if (element.Character == null ||
                    element.Character.IsHero ||
                    IsRegularGarrisonTroop(element.Character))
                {
                    continue;
                }

                roster.AddToCounts(
                    element.Character,
                    -element.Number,
                    false,
                    -element.WoundedNumber,
                    0,
                    true,
                    -1);
                dismissed += element.Number;
            }

            ArmyCompositionTemplate template =
                GetGarrisonTemplate(garrison);
            int capacity = Math.Max(0, garrison.Party.PartySizeLimit);
            int remainingSafetyIterations = roster.TotalRegulars;

            while (remainingSafetyIterations-- > 0)
            {
                List<TroopRosterElement> troops = roster.GetTroopRoster()
                    .Where(element =>
                        element.Character != null &&
                        !element.Character.IsHero &&
                        IsRegularGarrisonTroop(element.Character) &&
                        element.Number > 0)
                    .ToList();

                if (troops.Count == 0)
                {
                    break;
                }

                TroopRosterElement? removal = troops
                    .Where(element =>
                        ExceedsRoleMaximum(
                            troops,
                            element.Character,
                            capacity,
                            template) ||
                        ExceedsQualityMaximum(
                            troops,
                            element.Character,
                            capacity,
                            template))
                    // Every candidate already violates at least one hard
                    // maximum. Remove lower-tier troops first; the excess
                    // score only breaks ties between troops of equal tier.
                    .OrderBy(element => element.Character.Tier)
                    .ThenByDescending(element =>
                        GetExcessScore(
                            troops,
                            element.Character,
                            capacity,
                            template))
                    .Cast<TroopRosterElement?>()
                    .FirstOrDefault();

                if (!removal.HasValue)
                {
                    break;
                }

                TroopRosterElement selected = removal.Value;
                int healthy = selected.Number - selected.WoundedNumber;
                int wounded = healthy > 0 ? 0 : 1;
                roster.AddToCounts(
                    selected.Character,
                    -1,
                    false,
                    -wounded,
                    0,
                    true,
                    -1);
                dismissed++;
            }

            return dismissed;
        }

        private static bool IsRegularGarrisonTroop(CharacterObject troop)
        {
            return troop != null &&
                !troop.IsHero &&
                (troop.Occupation == Occupation.Soldier ||
                    troop.Occupation == Occupation.Mercenary);
        }

        private static int GetExcessScore(
            IEnumerable<TroopRosterElement> troops,
            CharacterObject troop,
            int capacity,
            ArmyCompositionTemplate template)
        {
            int score = 0;

            if (ExceedsRoleMaximum(troops, troop, capacity, template))
            {
                score++;
            }

            if (ExceedsQualityMaximum(troops, troop, capacity, template))
            {
                score++;
            }

            return score;
        }

        private static bool ExceedsRoleMaximum(
            IEnumerable<TroopRosterElement> troops,
            CharacterObject troop,
            int capacity,
            ArmyCompositionTemplate template)
        {
            CombatRole role =
                RecruitmentTroopClassifier.GetCombatRole(troop);
            int count = troops
                .Where(element =>
                    RecruitmentTroopClassifier.GetCombatRole(
                        element.Character) == role)
                .Sum(element => element.Number);
            int maximum = (int)Math.Floor(
                capacity * template.GetRange(role).MaximumRatio);
            return count > maximum;
        }

        private static bool ExceedsQualityMaximum(
            IEnumerable<TroopRosterElement> troops,
            CharacterObject troop,
            int capacity,
            ArmyCompositionTemplate template)
        {
            TroopQuality quality =
                RecruitmentTroopClassifier.GetQuality(troop);
            int count = troops
                .Where(element =>
                    RecruitmentTroopClassifier.GetQuality(
                        element.Character) == quality)
                .Sum(element => element.Number);
            int maximum = (int)Math.Floor(
                capacity * template.GetRange(quality).MaximumRatio);
            return count > maximum;
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

        private sealed class PrisonerRecruitmentOffer
        {
            public PrisonerRecruitmentOffer(int conformityCost)
            {
                ConformityCost = Math.Max(0, conformityCost);
            }

            public int ConformityCost { get; }
        }
    }
}
