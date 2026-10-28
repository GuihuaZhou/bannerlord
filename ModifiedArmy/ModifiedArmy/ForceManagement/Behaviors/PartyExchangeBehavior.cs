using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.ForceManagement.Diagnostics;
using ModifiedArmy.Garrison.Behaviors;
using ModifiedArmy.Recruitment.Diagnostics;
using ModifiedArmy.Tool;
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
    /// Owns the sole settlement-entry transaction for unified force tasks.
    /// Harmony patches only block competing native decisions.
    /// </summary>
    public sealed class PartyExchangeBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, int> _lastEvaluationDay =
            new Dictionary<string, int>();
        private int _lastCleanupDay = -1;

        public override void RegisterEvents()
        {
            CampaignEvents.SettlementEntered.AddNonSerializedListener(
                this,
                OnSettlementEntered);
            CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(
                this,
                OnDailyPartyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_party_garrison_exchange_days",
                ref _lastEvaluationDay);
            _lastEvaluationDay ??= new Dictionary<string, int>();
        }

        private void OnSettlementEntered(
            MobileParty party,
            Settlement settlement,
            Hero hero)
        {
            EvaluateVisit(party, settlement, "进入定居点事件");
        }

        /// <summary>Retries a legal task for a party that remains in town overnight.</summary>
        private void OnDailyPartyTick(MobileParty party)
        {
            if (party?.CurrentSettlement != null)
                EvaluateVisit(
                    party, party.CurrentSettlement, "每日驻留检查");
        }

        /// <summary>Applies the once-per-party, settlement and day visit gate.</summary>
        private void EvaluateVisit(
            MobileParty party,
            Settlement settlement,
            string eventName)
        {
            AiForcePlanningBehavior planning = Campaign.Current?
                .GetCampaignBehavior<AiForcePlanningBehavior>();
            PartyForceTask task = planning?.GetTaskForDiagnostics(party);
            // When the atomic switch is off, this behavior is intentionally
            // inert. Native Bannerlord handlers then provide the complete
            // rollback path without competing with an old Mod transaction.
            if (!ManagedPartyPolicy.IsEnabled())
            {
                ForceRecruitmentDiagnostics.LogVisit(
                    party, settlement, eventName,
                    "统一调兵模式未启用", -1, task);
                return;
            }
            if (party == null || settlement == null ||
                party.CurrentSettlement != settlement)
            {
                ForceRecruitmentDiagnostics.LogVisit(
                    party, settlement, eventName,
                    "部队当前并未实际驻留在该定居点", -1, task);
                return;
            }

            string key = (party.StringId ?? string.Empty) + "|" +
                (settlement.StringId ?? string.Empty);
            int day = (int)CampaignTime.Now.ToDays;
            CleanupOldVisitKeys(day);
            if (_lastEvaluationDay.TryGetValue(key, out int lastDay) &&
                lastDay == day)
            {
                ForceRecruitmentDiagnostics.LogVisit(
                    party, settlement, eventName,
                    "同一部队、定居点和游戏日已经检查，按现有规则跳过",
                    lastDay, task);
                return;
            }
            _lastEvaluationDay[key] = day;
            if (ManagedPartyPolicy.IsModManagedAiParty(party))
            {
                ForceRecruitmentDiagnostics.LogVisit(
                    party, settlement, eventName,
                    "通过访问去重，准备检查统一调兵任务", day, task);
                try
                {
                    GarrisonCaptureSupplyBehavior capture = Campaign.Current?
                        .GetCampaignBehavior<GarrisonCaptureSupplyBehavior>();
                    // The capture permit is queried without side effects and is
                    // consumed only by the unified transaction below.
                    if (capture?.CanDonateTroopsAfterCapture(
                            party, settlement) == true)
                    {
                        ForceRecruitmentDiagnostics.LogVisit(
                            party, settlement, eventName,
                            "进入攻城后一次性驻军移交入口", day, task);
                        Campaign.Current?.GetCampaignBehavior<
                            AiForcePlanningBehavior>()?.ExecuteCaptureDonation(
                                party, settlement, capture);
                        return;
                    }
                    ForceRecruitmentDiagnostics.LogVisit(
                        party, settlement, eventName,
                        "进入普通补员任务入口", day, task);
                    planning?.ExecuteAtSettlement(party, settlement);
                    return;
                }
                catch (Exception ex)
                {
                    // One malformed party or legacy roster must not abort the
                    // campaign tick. The transaction has its own validation,
                    // so a failed visit can be retried on a later day.
                    if (RecruitmentLogFilter.ShouldLog(party))
                    {
                        TextObject message = GameTexts.FindText(
                            "str_modifiedarmy_force_visit_failed");
                        message.SetTextVariable(
                            "PARTY_ID", party.StringId ?? "<unknown>");
                        message.SetTextVariable(
                            "SETTLEMENT_ID", settlement.StringId ?? "<unknown>");
                        message.SetTextVariable("EXCEPTION", ex.ToString());
                        ModLogger.Error(message.ToString());
                    }
                    return;
                }
            }

            // Parties outside the managed scope remain entirely native. This
            // event listener must not add a second transaction for them.
        }

        /// <summary>
        /// Bounds the compatibility dictionary so long campaigns do not save
        /// every historical party/settlement visit forever.
        /// </summary>
        private void CleanupOldVisitKeys(int today)
        {
            if (_lastCleanupDay == today) return;
            _lastCleanupDay = today;
            int oldestRetainedDay = today - 2;
            foreach (string key in new List<string>(_lastEvaluationDay.Keys))
            {
                if (_lastEvaluationDay[key] < oldestRetainedDay)
                    _lastEvaluationDay.Remove(key);
            }
        }
    }
}
