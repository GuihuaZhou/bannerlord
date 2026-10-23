using ModifiedArmy.ForceManagement.Services;
using ModifiedArmy.Garrison.Behaviors;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

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
            EvaluateVisit(party, settlement);
        }

        /// <summary>Retries a legal task for a party that remains in town overnight.</summary>
        private void OnDailyPartyTick(MobileParty party)
        {
            if (party?.CurrentSettlement != null)
                EvaluateVisit(party, party.CurrentSettlement);
        }

        /// <summary>Applies the once-per-party, settlement and day visit gate.</summary>
        private void EvaluateVisit(
            MobileParty party,
            Settlement settlement)
        {
            // When the atomic switch is off, this behavior is intentionally
            // inert. Native Bannerlord handlers then provide the complete
            // rollback path without competing with an old Mod transaction.
            if (!ManagedPartyPolicy.IsEnabled())
            {
                return;
            }
            if (party == null || settlement == null ||
                party.CurrentSettlement != settlement)
            {
                return;
            }

            string key = (party.StringId ?? string.Empty) + "|" +
                (settlement.StringId ?? string.Empty);
            int day = (int)CampaignTime.Now.ToDays;
            if (_lastEvaluationDay.TryGetValue(key, out int lastDay) &&
                lastDay == day)
            {
                return;
            }
            _lastEvaluationDay[key] = day;
            if (ManagedPartyPolicy.IsModManagedAiParty(party))
            {
                GarrisonCaptureSupplyBehavior capture = Campaign.Current?
                    .GetCampaignBehavior<GarrisonCaptureSupplyBehavior>();
                // The capture permit is queried without side effects and is
                // consumed only by the unified transaction below.
                if (capture?.CanDonateTroopsAfterCapture(
                        party, settlement) == true)
                {
                    Campaign.Current?.GetCampaignBehavior<
                        AiForcePlanningBehavior>()?.ExecuteCaptureDonation(
                            party, settlement, capture);
                    return;
                }
                Campaign.Current?.GetCampaignBehavior<AiForcePlanningBehavior>()?
                    .ExecuteAtSettlement(party, settlement);
                return;
            }

            // Parties outside the managed scope remain entirely native. This
            // event listener must not add a second transaction for them.
        }
    }
}
