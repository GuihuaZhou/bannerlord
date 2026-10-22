using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>
    /// Persisted daily planning shell. During phase three it is intentionally
    /// not registered in Main, so it cannot compete with legacy routing.
    /// </summary>
    public sealed class AiForcePlanningBehavior : CampaignBehaviorBase
    {
        private List<PartyForceTask> _tasks = new List<PartyForceTask>();
        private readonly PartyForceCandidateService _candidates =
            new PartyForceCandidateService();

        /// <summary>Registers the daily shadow pass when phase five enables it.</summary>
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(
                this,
                OnDailyTick);
        }

        /// <summary>
        /// Phase three keeps tasks runtime-only because this behavior is not
        /// registered yet. Phase five will add the final save contract at the
        /// same time as the atomic behavior switch.
        /// </summary>
        public override void SyncData(IDataStore dataStore)
        {
            _tasks = _tasks ?? new List<PartyForceTask>();
        }

        /// <summary>Runs one read-only planning pass without issuing movement.</summary>
        private void OnDailyTick()
        {
            MilitaryConfig config = MilitaryConfigManager.Instance
                .GetActiveConfig();
            if (Campaign.Current == null || config == null) return;
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.IsEliminated) continue;
                foreach (Clan clan in kingdom.Clans)
                {
                    if (clan.IsEliminated) continue;
                    foreach (var component in clan.WarPartyComponents)
                    {
                        MobileParty party = component?.MobileParty;
                        if (party == null) continue;
                        bool gate = MBRandom.RandomFloat <=
                            config.AiRecruitRandomChance;
                        EvaluateParty(party, gate);
                    }
                }
            }
        }

        /// <summary>
        /// Produces and records a task decision. It deliberately does not call
        /// any SetMove method or exchange executor in the shadow phase.
        /// </summary>
        public PartyForcePlan EvaluateParty(
            MobileParty party,
            bool randomGateOpen)
        {
            PartyForceTask current = FindTask(party);
            if (current?.OwnsMovementOrder == true &&
                party.TargetSettlement != current.TargetSettlement)
            {
                current.Status = PartyForceTaskStatus.Cancelled;
                current.ReasonCode = "MovementOrderChangedExternally";
            }
            PartyForcePlanningContext context = _candidates.BuildContext(
                party,
                current?.Status == PartyForceTaskStatus.Cancelled
                    ? null
                    : current,
                randomGateOpen);
            PartyForcePlan plan = ForceManagementFacade.Current.ForcePlanning
                .BuildDailyPlan(context);
            ApplyShadowPlan(party, current, plan, context.Today);
            LogPlan(party, plan);
            return plan;
        }

        /// <summary>Stores the planner result without taking ownership of movement.</summary>
        private void ApplyShadowPlan(
            MobileParty party,
            PartyForceTask current,
            PartyForcePlan plan,
            int today)
        {
            PartyForceTask next = plan?.Task;
            if (next == null) return;
            if (next == current)
            {
                current.LastValidatedDay = today;
                return;
            }
            if (current != null)
            {
                current.Status = PartyForceTaskStatus.Cancelled;
                current.ReasonCode = "ReplacedByDailyPlan";
            }
            if (next.Status == PartyForceTaskStatus.Completed ||
                next.TargetSettlement == null)
                return;
            next.Party = party;
            next.PartyId = party?.StringId;
            next.ClanId = party?.ActualClan?.StringId;
            next.LastValidatedDay = today;
            next.OwnsMovementOrder = false;
            _tasks.Add(next);
        }

        /// <summary>Finds the newest unfinished task owned by this behavior.</summary>
        private PartyForceTask FindTask(MobileParty party) => _tasks
            .Where(task => task != null &&
                (task.Party == party || task.PartyId == party?.StringId) &&
                task.Status != PartyForceTaskStatus.Completed &&
                task.Status != PartyForceTaskStatus.Cancelled)
            .OrderByDescending(task => task.Version)
            .FirstOrDefault();

        /// <summary>Writes an explainable shadow decision using display names.</summary>
        private static void LogPlan(
            MobileParty party,
            PartyForcePlan plan)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            PartyForceTask task = plan?.Task;
            ModLogger.Debug(string.Format(
                "[兵力规划预览] {0}今日决定为{1}；目标是{2}，计划使用{3}，原因是{4}。" +
                "本次共比较了{5}个可行方案，没有修改路线或兵员。",
                PartyLogFormatter.GetDisplayName(party),
                task?.Intent.ToString() ?? "无计划",
                task?.TargetSettlement?.Name?.ToString() ?? "无",
                task?.SourceKind.ToString() ?? "无",
                plan?.ReasonCode ?? "unknown",
                plan?.RankedCandidates?.Count ?? 0));
        }
    }
}
