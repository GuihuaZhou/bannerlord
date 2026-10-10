using System.Linq;
using ModifiedPolitics.Governor.Config;
using ModifiedPolitics.Governor.Models;
using ModifiedPolitics.Governor.Services;
using ModifiedPolitics.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace ModifiedPolitics.Governor.Behaviors
{
    /// <summary>
    /// Replans centralized kingdom governors weekly after the initial seven-day grace period.
    /// </summary>
    public sealed class KingdomGovernorAssignmentBehavior : CampaignBehaviorBase
    {
        private const string FirstEligibleTimeKey = "_modifiedPoliticsGovernorFirstEligibleTime";

        private CampaignTime _firstEligibleTime = CampaignTime.Zero;
        private readonly GovernorAssignmentPlanner _planner = new GovernorAssignmentPlanner();
        private readonly GovernorAssignmentExecutor _executor = new GovernorAssignmentExecutor();
        private readonly GovernorRelationService _relationService = new GovernorRelationService();
        private readonly GovernorNotificationService _notificationService = new GovernorNotificationService();

        public override void RegisterEvents()
        {
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, AssignGovernorsForAllKingdoms);
            CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(FirstEligibleTimeKey, ref _firstEligibleTime);
        }

        private void OnNewGameCreated(CampaignGameStarter campaignGameStarter)
        {
            _firstEligibleTime = CampaignTime.DaysFromNow(7f);
        }

        private void AssignGovernorsForAllKingdoms()
        {
            if (_firstEligibleTime != CampaignTime.Zero && CampaignTime.Now < _firstEligibleTime)
                return;

            foreach (Kingdom kingdom in Kingdom.All.ToList())
            {
                if (!IsEligibleKingdom(kingdom))
                    continue;

                RemoveInvalidBorrowedGovernors(kingdom);
                ExecutePlan(kingdom);
            }
        }

        private void ExecutePlan(Kingdom kingdom)
        {
            GovernorAssignmentPlan plan = _planner.Build(kingdom);
            int candidateCount = kingdom.Clans
                .Where(clan => clan != null && !clan.IsEliminated && !clan.IsClanTypeMercenary)
                .SelectMany(clan => clan.Heroes)
                .Count(hero => GovernorCandidateSelector.IsGovernorCandidate(hero, kingdom));
            LogStart(kingdom, plan.Targets.Count, candidateCount);

            GovernorAssignmentResult result = _executor.Execute(plan);
            if (result.FailedTowns.Count == 0)
            {
                _relationService.Apply(plan, result);
                _notificationService.Publish(plan, result);
                LogChanges(kingdom, plan, result);
            }
            else
                LogExecutionFailure(kingdom, result.FailedTowns.Count);

            LogSummary(kingdom, plan, result);
        }

        private static bool IsEligibleKingdom(Kingdom kingdom)
        {
            return kingdom != null
                   && !kingdom.IsEliminated
                   && kingdom.Culture != null
                   && GovernorPolicyManager.Instance.IsCentralizedAssignment(kingdom.Culture);
        }

        private static void RemoveInvalidBorrowedGovernors(Kingdom kingdom)
        {
            foreach (Town town in Town.AllTowns.Concat(Town.AllCastles).ToList())
            {
                Hero governor = town?.Governor;
                Clan ownerClan = town?.OwnerClan;
                if (governor == null || ownerClan?.Kingdom != kingdom)
                    continue;

                if (governor.Clan != ownerClan && governor.Clan?.Kingdom != ownerClan.Kingdom)
                {
                    ChangeGovernorAction.RemoveGovernorOf(governor);
                    if (ShouldLogForKingdom(kingdom))
                    {
                        TextObject message = new TextObject(
                            "{=MP_GovernorBorrowedRemoved}[Governor Assignment] Removed {HERO} from {SETTLEMENT} because the borrowed governor no longer belongs to the owning kingdom.");
                        message.SetTextVariable("HERO", governor.Name);
                        message.SetTextVariable("SETTLEMENT", town.Settlement.Name);
                        ModLogger.Info(message.ToString());
                    }
                }
            }
        }

        private static void LogStart(Kingdom kingdom, int settlementCount, int candidateCount)
        {
            if (!ShouldLogForKingdom(kingdom))
                return;

            TextObject message = new TextObject(
                "{=MP_GovernorPlanStarted}[Governor Assignment] Planning governors for {KINGDOM}: {SETTLEMENTS} settlements and {CANDIDATES} available heroes.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("SETTLEMENTS", settlementCount);
            message.SetTextVariable("CANDIDATES", candidateCount);
            ModLogger.Info(message.ToString());
        }

        private static void LogSummary(Kingdom kingdom, GovernorAssignmentPlan plan, GovernorAssignmentResult result)
        {
            if (!ShouldLogForKingdom(kingdom))
                return;

            int appointed = 0;
            int moved = 0;
            int removed = 0;
            int retained = 0;
            int vacant = 0;
            foreach (Town town in plan.Targets.Keys)
            {
                Hero before = result.Before[town];
                Hero after = result.After[town];
                if (before == after && after != null) retained++;
                else if (before == null && after != null) appointed++;
                else if (before != null && after == null) removed++;
                else if (before != after) moved++;
                if (after == null) vacant++;
            }

            TextObject message = new TextObject(
                "{=MP_GovernorPlanFinished}[Governor Assignment] Finished {KINGDOM}: appointed {APPOINTED}, moved {MOVED}, removed {REMOVED}, retained {RETAINED}, vacant {VACANT}, failed {FAILED}.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("APPOINTED", appointed);
            message.SetTextVariable("MOVED", moved);
            message.SetTextVariable("REMOVED", removed);
            message.SetTextVariable("RETAINED", retained);
            message.SetTextVariable("VACANT", vacant);
            message.SetTextVariable("FAILED", result.FailedTowns.Count);
            ModLogger.Info(message.ToString());
        }

        private static void LogExecutionFailure(Kingdom kingdom, int failedCount)
        {
            if (!ShouldLogForKingdom(kingdom))
                return;

            TextObject message = new TextObject(
                "{=MP_GovernorPlanFailed}[Governor Assignment] {KINGDOM} had {COUNT} governor changes rejected by the game; real state is retained for the next weekly pass.");
            message.SetTextVariable("KINGDOM", kingdom.Name);
            message.SetTextVariable("COUNT", failedCount);
            ModLogger.Warn(message.ToString());
        }

        private static void LogChanges(
            Kingdom kingdom,
            GovernorAssignmentPlan plan,
            GovernorAssignmentResult result)
        {
            if (!ShouldLogForKingdom(kingdom))
                return;

            foreach (Town town in plan.Targets.Keys)
            {
                Hero before = result.Before[town];
                Hero after = result.After[town];
                if (before == after)
                    continue;

                TextObject message = new TextObject(
                    "{=MP_GovernorAssignmentChanged}[Governor Assignment] {SETTLEMENT}: {OLD_GOVERNOR} -> {NEW_GOVERNOR}.");
                message.SetTextVariable("SETTLEMENT", town.Settlement.Name);
                message.SetTextVariable("OLD_GOVERNOR", before?.Name ?? new TextObject("{=MP_NoGovernor}No Governor"));
                message.SetTextVariable("NEW_GOVERNOR", after?.Name ?? new TextObject("{=MP_NoGovernor}No Governor"));
                ModLogger.Info(message.ToString());
            }

            foreach (GovernorSettlementContext context in plan.Contexts.Values
                         .Where(context => result.After[context.Town] == null))
            {
                TextObject message = new TextObject(
                    "{=MP_GovernorNoQualifiedCandidate}[Governor Assignment] No qualified hero for {SETTLEMENT}; required ability is {ABILITY}.");
                message.SetTextVariable("SETTLEMENT", context.Town.Settlement.Name);
                message.SetTextVariable("ABILITY", (int)context.RequiredAbility);
                ModLogger.Debug(message.ToString());
            }
        }

        private static bool ShouldLogForKingdom(Kingdom kingdom)
        {
            return kingdom != null && Clan.PlayerClan?.Kingdom == kingdom;
        }
    }
}
