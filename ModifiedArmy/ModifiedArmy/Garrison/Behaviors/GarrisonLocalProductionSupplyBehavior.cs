using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Routes surrounding-lands and castle Farmlands production into the real
    /// military granary before Bannerlord applies the daily food change.
    /// </summary>
    public class GarrisonLocalProductionSupplyBehavior
        : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent
                .AddNonSerializedListener(this, OnDailySettlementTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnDailySettlementTick(Settlement settlement)
        {
            Town town = settlement?.Town;

            if (settlement?.IsFortification != true
                || town?.GarrisonParty == null)
            {
                return;
            }

            NewSettlementFoodModel.SetLocalProductionAllocation(town, 0);

            int allocation = GarrisonLocalProductionSupplyModel
                .CalculateDailyAllocation(town);

            if (allocation <= 0)
            {
                return;
            }

            town.GarrisonParty.ItemRoster.AddToCounts(
                DefaultItems.Grain,
                allocation);

            // Campaign registers its own settlement daily-tick listener after
            // campaign behaviors. NewSettlementFoodModel therefore observes
            // this allocation when applying the civilian food change.
            NewSettlementFoodModel.SetLocalProductionAllocation(
                town,
                allocation);

            if (settlement.OwnerClan == Clan.PlayerClan)
            {
                ModLogger.Notice(
                    $"[GarrisonLogistics] Local production allocation | " +
                    $"Settlement='{settlement.Name}' | " +
                    $"Transferred={allocation} | " +
                    $"MilitaryFood=" +
                    $"{town.GarrisonParty.ItemRoster.TotalFood}");
            }
        }
    }
}
