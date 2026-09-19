using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Actively writes calculated garrison wage limits to settlements. This
    /// keeps existing saves correct immediately after loading and avoids
    /// relying solely on the timing of the vanilla private update method.
    /// </summary>
    public class GarrisonWageLimitBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(
                this,
                OnGameLoaded);

            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent
                .AddNonSerializedListener(
                    this,
                    OnNewGameCreated);

            CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(
                this,
                OnDailyTickClan);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            UpdateAllClanWageLimits();
        }

        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            UpdateAllClanWageLimits();
        }

        private void OnDailyTickClan(Clan clan)
        {
            UpdateClanWageLimits(clan);
        }

        private static void UpdateAllClanWageLimits()
        {
            foreach (Clan clan in Clan.All)
            {
                UpdateClanWageLimits(clan);
            }
        }

        private static void UpdateClanWageLimits(Clan clan)
        {
            if (clan == Clan.PlayerClan
                || clan?.MapFaction == null
                || (!clan.IsRebelClan
                    && !clan.MapFaction.IsKingdomFaction))
            {
                return;
            }

            foreach (Town town in clan.Fiefs)
            {
                int previousLimit =
                    town.Settlement.GarrisonWagePaymentLimit;

                int newLimit =
                    GarrisonWageLimitModel.CalculateWagePaymentLimit(
                        town,
                        out int desiredSize,
                        out float goldFactor,
                        out float expectedAverageWage,
                        out float rawWageLimit,
                        out bool isBorder);

                town.Settlement.SetGarrisonWagePaymentLimit(newLimit);

                if (previousLimit != newLimit)
                {
                    ModLogger.Debug(
                        $"[GarrisonWageLimitBehavior] Settlement='{town.Name}' | " +
                        $"Border={isBorder} | DesiredSize={desiredSize} | " +
                        $"ExpectedAverageWage={expectedAverageWage:F2} | " +
                        $"GoldFactor={goldFactor:F2} | RawLimit={rawWageLimit:F2} | " +
                        $"PreviousLimit={previousLimit} | NewLimit={newLimit}");
                }
            }
        }
    }
}
