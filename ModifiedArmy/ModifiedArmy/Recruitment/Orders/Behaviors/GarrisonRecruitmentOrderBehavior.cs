using ModifiedArmy.Recruitment.Orders.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Recruitment.Orders.Behaviors
{
    /// <summary>Single persistent owner of active garrison recruitment orders.</summary>
    public sealed class GarrisonRecruitmentOrderBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, GarrisonRecruitmentOrderRecord> _activeOrders =
            new Dictionary<string, GarrisonRecruitmentOrderRecord>();
        private Dictionary<string, int> _lastClosedDays =
            new Dictionary<string, int>();

        public override void RegisterEvents() { }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_garrison_recruit_orders_v1", ref _activeOrders);
            dataStore.SyncData(
                "modifiedarmy_garrison_recruit_order_closed_days_v1",
                ref _lastClosedDays);
            _activeOrders ??= new Dictionary<string, GarrisonRecruitmentOrderRecord>();
            _lastClosedDays ??= new Dictionary<string, int>();
        }

        public GarrisonRecruitmentOrderRecord GetActive(Settlement settlement)
        {
            if (settlement?.StringId == null) return null;
            _activeOrders.TryGetValue(settlement.StringId, out var order);
            return order;
        }

        public int GetLastClosedDay(Settlement settlement)
        {
            return settlement?.StringId != null &&
                _lastClosedDays.TryGetValue(settlement.StringId, out int day)
                ? day : int.MinValue;
        }

        public GarrisonRecruitmentOrderRecord Create(
            Settlement settlement, int authorized, int target, int day)
        {
            var order = new GarrisonRecruitmentOrderRecord
            {
                OrderId = (settlement.StringId ?? "unknown") + ":" + day + ":" +
                    Guid.NewGuid().ToString("N"),
                SettlementId = settlement.StringId,
                OwnerClanId = settlement.OwnerClan?.StringId,
                AuthorizedTotal = Math.Max(0, authorized),
                AuthorizedGoalAtLastRevision = Math.Max(0, target),
                CreatedDay = day,
                LastReviewedDay = day,
                LastExecutionDay = int.MinValue,
                Revision = 1,
                State = GarrisonRecruitmentOrderState.Active,
                LastReasonCode = "Created"
            };
            _activeOrders[settlement.StringId] = order;
            return order;
        }

        public void Revise(
            GarrisonRecruitmentOrderRecord order,
            int newRemaining,
            int target,
            int day,
            string reason)
        {
            if (order == null) return;
            order.AuthorizedTotal = order.RecruitedTotal + Math.Max(0, newRemaining);
            order.AuthorizedGoalAtLastRevision = Math.Max(0, target);
            order.LastReviewedDay = day;
            order.Revision++;
            order.LastReasonCode = reason ?? "Reviewed";
        }

        public void ApplyExecution(
            GarrisonRecruitmentOrderRecord order,
            int professional,
            int prisoner,
            int day,
            string operationId,
            bool recoveryRequired,
            string reason)
        {
            if (order == null) return;
            order.RecruitedProfessional += Math.Max(0, professional);
            order.RecruitedPrisoner += Math.Max(0, prisoner);
            order.LastExecutionDay = day;
            order.LastOperationId = operationId;
            order.State = recoveryRequired
                ? GarrisonRecruitmentOrderState.RecoveryRequired
                : GarrisonRecruitmentOrderState.Active;
            order.LastReasonCode = reason ??
                (recoveryRequired ? "RecoveryRequired" : "Executed");
        }

        public void Close(Settlement settlement, int day, string reason)
        {
            if (settlement?.StringId == null) return;
            if (_activeOrders.TryGetValue(settlement.StringId, out var order))
                order.LastReasonCode = reason;
            _activeOrders.Remove(settlement.StringId);
            _lastClosedDays[settlement.StringId] = day;
        }

        /// <summary>
        /// Explicit recovery boundary. Callers must first reconcile the live
        /// roster, source pool, clan expense and daily budget against the
        /// recorded operation id; ordinary scheduling never clears a freeze.
        /// </summary>
        public bool TryResolveRecovery(
            Settlement settlement,
            string verifiedOperationId,
            string reason)
        {
            GarrisonRecruitmentOrderRecord order = GetActive(settlement);
            if (order?.State != GarrisonRecruitmentOrderState.RecoveryRequired ||
                string.IsNullOrWhiteSpace(verifiedOperationId) ||
                order.LastOperationId != verifiedOperationId)
                return false;
            order.State = GarrisonRecruitmentOrderState.Active;
            order.Revision++;
            order.LastReasonCode = reason ?? "RecoveryReconciled";
            return true;
        }

        public void RevalidateAfterCampaignRecovery()
        {
            var settlements = Settlement.All
                .Where(x => x?.StringId != null)
                .ToDictionary(x => x.StringId, x => x);
            foreach (string key in _activeOrders.Keys.ToList())
            {
                GarrisonRecruitmentOrderRecord order = _activeOrders[key];
                if (order == null || !settlements.TryGetValue(key, out Settlement settlement) ||
                    settlement.OwnerClan?.StringId != order.OwnerClanId ||
                    order.AuthorizedTotal < 0 || order.RecruitedProfessional < 0 ||
                    order.RecruitedPrisoner < 0 ||
                    (order.Remaining <= 0 && order.State !=
                        GarrisonRecruitmentOrderState.RecoveryRequired))
                {
                    _activeOrders.Remove(key);
                }
            }
        }
    }
}
