using ModifiedArmy.ForceManagement.Services;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>
    /// Owns the natural-visit safety window. The native settlement-entered
    /// patch is only a bridge and does not contain exchange policy.
    /// </summary>
    public sealed class PartyGarrisonExchangeBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, int> _lastEvaluationDay =
            new Dictionary<string, int>();

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_party_garrison_exchange_days",
                ref _lastEvaluationDay);
            _lastEvaluationDay ??= new Dictionary<string, int>();
        }

        public void OnNaturalVisit(
            MobileParty party,
            Settlement settlement)
        {
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
            ForceManagementFacade.Current.TroopTransfers
                .TryExecuteNaturalVisit(party, settlement);
        }
    }
}
