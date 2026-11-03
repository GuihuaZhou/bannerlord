using Helpers;
using ModifiedPolitics.Tool;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Library;

using ModifiedPolitics.SettlementDevelopment.Services;

namespace ModifiedPolitics.Models
{
    public class AIBuildingAutoBoostBehavior : CampaignBehaviorBase
    {
        private const int BOOST_AMOUNT = 10000;       // Fixed investment amount.
        private const int MIN_GOLD_REQUIRED = 50000;  // Require five times the investment before acting.

        public override void RegisterEvents()
        {
            // Evaluate AI construction investment once per week.
            CampaignEvents.WeeklyTickEvent.AddNonSerializedListener(this, OnWeeklyTick);
        }

        private void OnWeeklyTick()
        {
            // Evaluate every settlement owned by the AI.
            foreach (var settlement in Settlement.All)
            {
                if (settlement?.Town == null)
                    continue;

                // Player settlements retain player control over construction reserves.
                if (settlement.OwnerClan == Clan.PlayerClan)
                    continue;

                Town town = settlement.Town;
                Clan ownerClan = settlement.OwnerClan;

                // Do not replace an existing construction reserve.
                if (town.BoostBuildingProcess > 0)
                    continue;

                // A reserve is useful only while a normal building is queued.
                if (town.BuildingsInProgress.Count == 0)
                    continue;

                // Preserve a large treasury buffer before investing.
                if (ownerClan.Gold < MIN_GOLD_REQUIRED)
                    continue;

                SettlementBuildingStateService.TryInvestConstructionBoost(
                    town,
                    ownerClan,
                    BOOST_AMOUNT);
            }
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}
