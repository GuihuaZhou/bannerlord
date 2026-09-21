using HarmonyLib;
using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Patches
{
    /// <summary>
    /// Logs the real daily food transaction for player-owned garrisons. The
    /// underlying item removal and fractional carry remain fully vanilla.
    /// </summary>
    [HarmonyPatch(typeof(FoodConsumptionBehavior), "DailyTickParty")]
    public static class GarrisonFoodConsumptionPatch
    {
        private sealed class ConsumptionState
        {
            public int FoodBefore { get; set; }

            public int RemainingPercentageBefore { get; set; }

            public bool WasStarving { get; set; }

            public float DailyConsumption { get; set; }
        }

        private static void Prefix(
            MobileParty party,
            out ConsumptionState __state)
        {
            __state = null;

            if (party?.IsGarrison != true
                || party.CurrentSettlement?.OwnerClan != Clan.PlayerClan)
            {
                return;
            }

            __state = new ConsumptionState
            {
                FoodBefore = party.ItemRoster.TotalFood,
                RemainingPercentageBefore =
                    party.Party.RemainingFoodPercentage,
                WasStarving = party.Party.IsStarving,
                DailyConsumption = -party.FoodChange
            };
        }

        private static void Postfix(
            MobileParty party,
            ConsumptionState __state)
        {
            if (__state == null || party?.CurrentSettlement == null)
            {
                return;
            }

            Clan ownerClan = party.CurrentSettlement.OwnerClan;
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;

            if (ownerClan != Clan.PlayerClan &&
                (playerKingdom == null || ownerClan?.Kingdom != playerKingdom))
            {
                return;
            }

            int foodAfter = party.ItemRoster.TotalFood;
            bool isStarving = party.Party.IsStarving;

            if (__state.FoodBefore == foodAfter
                && __state.WasStarving == isStarving
                && __state.RemainingPercentageBefore
                    == party.Party.RemainingFoodPercentage)
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_garrison_food_consumption");
            message.SetTextVariable(
                "SETTLEMENT_NAME",
                party.CurrentSettlement.Name);
            message.SetTextVariable(
                "DEMAND",
                __state.DailyConsumption.ToString("0.##"));
            message.SetTextVariable("FOOD_BEFORE", __state.FoodBefore);
            message.SetTextVariable("FOOD_AFTER", foodAfter);
            message.SetTextVariable(
                "REMAINDER",
                party.Party.RemainingFoodPercentage);
            message.SetTextVariable("STARVING", isStarving.ToString());
            ModLogger.Info(message.ToString());
        }
    }
}
