using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Tool;
using ModifiedArmy.common;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
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

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(
                this, OnDailyClanTick);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this, OnGameLoadFinished);
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
            if (party == null || !party.IsActive || !party.IsLordParty ||
                party.IsMainParty || party.IsDisbanding ||
                party.ActualClan == null ||
                party.ActualClan == Clan.PlayerClan ||
                string.IsNullOrEmpty(party.StringId)) return;

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
            ModLogger.FileNotice(message.ToString());
        }

        /// <summary>Converts internal outcomes into readable localized text.</summary>
        private static TextObject GetReasonText(string reason)
        {
            switch (reason)
            {
                case "Completed":
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationCompleted}本次复员已经办完");
                case "PeaceNotStable":
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationPeaceUnstable}和平时间还不够长");
                case "NoSurplus":
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationNoSurplus}部队没有超出和平编制");
                case "DailyLimitReached":
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationDailyLimit}今天的复员额度已经用完");
                case "NoEligibleTroopsOrCapacity":
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationNoCapacity}没有能返乡的封邑兵，驻军也接收不了更多普通兵");
                case "AtFormalWar":
                case "NotPeaceScenario":
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationAtWar}当前仍处于战争状态");
                case "HigherPriorityMilitaryTask":
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationMilitaryTask}部队正在执行更重要的军事任务");
                default:
                    return new TextObject(
                        "{=ModifiedArmy_DemobilizationUnsafe}当前不适合办理复员");
            }
        }
    }
}
