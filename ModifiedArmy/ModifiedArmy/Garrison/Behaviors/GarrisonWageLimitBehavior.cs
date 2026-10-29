using ModifiedArmy.Garrison.Models;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

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

                // Global daily recalculation is expected, but logging every small
                // fluctuation for every kingdom obscures actionable events. Keep a
                // diagnostic only for the player's kingdom and only when the limit
                // moves by at least five percent or one hundred denars.
                int meaningfulChange = System.Math.Max(
                    100,
                    System.Math.Max(1, previousLimit) / 20);
                if (clan.Kingdom == Clan.PlayerClan?.Kingdom &&
                    System.Math.Abs(previousLimit - newLimit) >= meaningfulChange)
                {
                    TextObject message = GameTexts.FindText(isBorder
                        ? "str_modifiedarmy_garrison_wage_limit_border_log"
                        : "str_modifiedarmy_garrison_wage_limit_interior_log");
                    message.SetTextVariable("SETTLEMENT_NAME", town.Name);
                    message.SetTextVariable("PREVIOUS_LIMIT", previousLimit);
                    message.SetTextVariable("NEW_LIMIT", newLimit);
                    message.SetTextVariable("DESIRED_SIZE", desiredSize);
                    message.SetTextVariable(
                        "AVERAGE_WAGE", expectedAverageWage.ToString("F2"));
                    message.SetTextVariable(
                        "WEALTH_FACTOR", goldFactor.ToString("F2"));
                    message.SetTextVariable(
                        "RAW_LIMIT", rawWageLimit.ToString("F2"));
                    ModLogger.Debug(message.ToString());
                }
            }
        }
    }
}
