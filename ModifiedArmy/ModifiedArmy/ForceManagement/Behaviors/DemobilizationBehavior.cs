using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>
    /// Persists stable-peace dates and actual per-party daily demobilization
    /// totals. Execution occurs only on a natural same-clan fort visit.
    /// </summary>
    public sealed class DemobilizationBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, int> _clanPeaceSinceDay = new();
        private Dictionary<string, int> _partyLastDemobDay = new();
        private Dictionary<string, int> _partyDemobCountToday = new();
        private readonly Dictionary<string, Settlement> _routingTargets =
            new Dictionary<string, Settlement>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(
                this, OnDailyClanTick);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this, OnGameLoadFinished);
            CampaignEvents.WarDeclared.AddNonSerializedListener(
                this, OnWarDeclared);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_demobilization_clan_peace_days",
                ref _clanPeaceSinceDay);
            dataStore.SyncData(
                "modifiedarmy_demobilization_party_last_days",
                ref _partyLastDemobDay);
            dataStore.SyncData(
                "modifiedarmy_demobilization_party_daily_counts",
                ref _partyDemobCountToday);
            EnsureCollections();
        }

        /// <summary>
        /// Re-evaluates a party after native garrison handling, then charges
        /// only soldiers that were actually returned or moved.
        /// </summary>
        public void OnNaturalVisit(MobileParty party, Settlement settlement)
        {
            if (!IsEnabled()) return;
            if (!ManagedPartyPolicy.IsModManagedAiParty(party) ||
                string.IsNullOrEmpty(party.StringId)) return;

            _routingTargets.Remove(party.StringId);

            EnsureCollections();
            int today = CurrentDay();
            string clanId = party.ActualClan.StringId;
            if (!_clanPeaceSinceDay.TryGetValue(clanId, out int peaceSinceDay))
            {
                if (ForceManagementFacade.IsAtFormalWar(party.ActualClan))
                    return;
                peaceSinceDay = today;
                _clanPeaceSinceDay[clanId] = today;
            }

            int already = GetTodayCount(party.StringId, today);
            PeaceDemobilizationContext context =
                new PeaceDemobilizationContext
                {
                    PeaceSinceDay = peaceSinceDay,
                    Today = today,
                    AlreadyDemobilizedToday = already,
                    VisitedSettlement = settlement
                };
            DemobilizationExecutionResult result = ForceManagementFacade
                .Current.DemobilizationRunner.TryExecuteAtSettlement(
                    party, settlement, context);
            if (result.ActualCount > 0)
            {
                _partyLastDemobDay[party.StringId] = today;
                _partyDemobCountToday[party.StringId] =
                    already + result.ActualCount;
            }
            LogResult(party, settlement, peaceSinceDay, today, result);
        }

        /// <summary>
        /// Returns the unified live decision used by the optional daily
        /// low-priority routing pass. No roster or movement state is changed.
        /// </summary>
        public PeaceDemobilizationDecision EvaluateRouting(
            MobileParty party,
            bool isExistingRoute = false)
        {
            if (!IsEnabled() ||
                !ManagedPartyPolicy.IsModManagedAiParty(party) ||
                string.IsNullOrEmpty(party.StringId))
            {
                return new PeaceDemobilizationDecision
                {
                    ReasonCode = "AutomaticDemobilizationDisabled"
                };
            }
            EnsureCollections();
            int today = CurrentDay();
            if (!_clanPeaceSinceDay.TryGetValue(
                    party.ActualClan.StringId,
                    out int peaceSinceDay))
            {
                return new PeaceDemobilizationDecision
                {
                    ReasonCode = "PeaceNotObserved"
                };
            }
            return ForceManagementFacade.Current.PeaceDemobilization.Evaluate(
                party,
                new PeaceDemobilizationContext
                {
                    PeaceSinceDay = peaceSinceDay,
                    Today = today,
                    AlreadyDemobilizedToday = GetTodayCount(
                        party.StringId,
                        today),
                    IsRoutingEvaluation = true,
                    IsExistingDemobilizationRoute = isExistingRoute
                });
        }

        /// <summary>
        /// Marks the one movement order issued by D4-B so a later war or
        /// threat can cancel only that order, without touching native tasks.
        /// </summary>
        public void RecordRouting(MobileParty party, Settlement destination)
        {
            if (party == null || destination == null ||
                string.IsNullOrEmpty(party.StringId)) return;
            _routingTargets[party.StringId] = destination;
        }

        /// <summary>
        /// Returns true only when the still-active D4 route has become unsafe
        /// or ineligible. Changed native orders are merely untracked.
        /// </summary>
        public bool ShouldCancelRouting(MobileParty party)
        {
            if (party == null || string.IsNullOrEmpty(party.StringId) ||
                !_routingTargets.TryGetValue(
                    party.StringId,
                    out Settlement destination))
            {
                return false;
            }
            bool stillOwnsOrder = party.DefaultBehavior ==
                    AiBehavior.GoToSettlement &&
                party.TargetSettlement == destination;
            if (!stillOwnsOrder)
            {
                _routingTargets.Remove(party.StringId);
                return false;
            }
            bool destinationValid = destination.IsFortification &&
                !destination.IsUnderSiege &&
                destination.OwnerClan == party.ActualClan;
            bool canContinue = destinationValid &&
                EvaluateRouting(party, true).CanDemobilize;
            if (canContinue) return false;
            _routingTargets.Remove(party.StringId);
            return true;
        }

        /// <summary>
        /// Starts or clears the clan stable-peace timer. Old saves begin on
        /// the first peaceful day observed after loading.
        /// </summary>
        private void OnDailyClanTick(Clan clan)
        {
            if (!IsEnabled()) return;
            if (clan == null || clan == Clan.PlayerClan || clan.IsEliminated ||
                string.IsNullOrEmpty(clan.StringId)) return;
            EnsureCollections();
            if (ForceManagementFacade.IsAtFormalWar(clan))
            {
                _clanPeaceSinceDay.Remove(clan.StringId);
                return;
            }
            if (!_clanPeaceSinceDay.ContainsKey(clan.StringId))
                _clanPeaceSinceDay[clan.StringId] = CurrentDay();
        }

        /// <summary>
        /// Repairs missing collections only; historical peace dates and
        /// troop-source decisions are deliberately not inferred on load.
        /// </summary>
        private void OnGameLoadFinished()
        {
            EnsureCollections();
        }

        /// <summary>
        /// Invalidates peace timers immediately and releases only D4-owned
        /// travel orders when either affected faction enters a new war.
        /// </summary>
        private void OnWarDeclared(
            IFaction first,
            IFaction second,
            DeclareWarAction.DeclareWarDetail detail)
        {
            EnsureCollections();
            foreach (Clan clan in Clan.All)
            {
                if (clan?.MapFaction == first || clan?.MapFaction == second)
                    _clanPeaceSinceDay.Remove(clan.StringId);
            }
            foreach (MobileParty party in MobileParty.AllLordParties)
            {
                if (party?.MapFaction != first && party?.MapFaction != second)
                    continue;
                if (string.IsNullOrEmpty(party.StringId) ||
                    !_routingTargets.TryGetValue(
                        party.StringId,
                        out Settlement destination))
                {
                    continue;
                }
                bool ownsOrder = party.DefaultBehavior ==
                        AiBehavior.GoToSettlement &&
                    party.TargetSettlement == destination;
                _routingTargets.Remove(party.StringId);
                if (ownsOrder && party.Army == null && party.MapEvent == null)
                {
                    party.SetMovePatrolAroundPoint(
                        party.Position,
                        MobileParty.NavigationType.Default);
                }
            }
        }

        /// <summary>Returns the actual amount charged to this party today.</summary>
        private int GetTodayCount(string partyId, int today)
        {
            if (!_partyLastDemobDay.TryGetValue(partyId, out int lastDay) ||
                lastDay != today) return 0;
            return _partyDemobCountToday.TryGetValue(partyId, out int count)
                ? Math.Max(0, count)
                : 0;
        }

        /// <summary>Repairs collections missing from an older save.</summary>
        private void EnsureCollections()
        {
            _clanPeaceSinceDay ??= new Dictionary<string, int>();
            _partyLastDemobDay ??= new Dictionary<string, int>();
            _partyDemobCountToday ??= new Dictionary<string, int>();
        }

        /// <summary>Returns the whole current campaign day.</summary>
        private static int CurrentDay() => (int)CampaignTime.Now.ToDays;

        /// <summary>Reports whether the safely-defaulted feature is active.</summary>
        private static bool IsEnabled() => MilitaryConfigManager.Instance
            .GetActiveConfig()?.AiRecruitment.ForceManagement.Demobilization
            .EnableAutomaticDemobilization == true;

        /// <summary>Writes one localized audit record for an evaluated visit.</summary>
        private static void LogResult(
            MobileParty party,
            Settlement settlement,
            int peaceSinceDay,
            int today,
            DemobilizationExecutionResult result)
        {
            if (!RecruitmentLogFilter.ShouldLog(party)) return;
            PeaceDemobilizationDecision decision = result.Decision ??
                new PeaceDemobilizationDecision();
            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_peace_demobilization");
            message.SetTextVariable(
                "PARTY_NAME", PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable(
                "SETTLEMENT_NAME",
                settlement?.Name ?? TextObject.GetEmpty());
            message.SetTextVariable(
                "PEACE_DAYS", Math.Max(0, today - peaceSinceDay));
            message.SetTextVariable("CURRENT", decision.CurrentCount);
            message.SetTextVariable("TARGET", decision.TargetCount);
            message.SetTextVariable("APPROVED", decision.ApprovedToday);
            message.SetTextVariable("FIEF_RETURNED", result.ReturnedFiefCount);
            message.SetTextVariable(
                "REGULAR_TRANSFERRED", result.TransferredRegularCount);
            message.SetTextVariable(
                "REMAINING",
                Math.Max(0, decision.SurplusCount - result.ActualCount));
            message.SetTextVariable("RESULT", GetReasonText(result.ReasonCode));
            ModLogger.Notice(message.ToString());
        }

        /// <summary>Converts internal outcomes into readable localized text.</summary>
        private static TextObject GetReasonText(string reason)
        {
            switch (reason)
            {
                case "Completed":
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_completed");
                case "PeaceNotStable":
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_peace_unstable");
                case "NoSurplus":
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_no_surplus");
                case "DailyLimitReached":
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_daily_limit");
                case "NoEligibleTroopsOrCapacity":
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_no_capacity");
                case "AtFormalWar":
                case "NotPeaceScenario":
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_at_war");
                case "HigherPriorityMilitaryTask":
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_military_task");
                default:
                    return GameTexts.FindText(
                        "str_modifiedarmy_demobilization_reason_unsafe");
            }
        }
    }
}
