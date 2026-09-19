using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Supply
{
    /// <summary>
    /// Provides a peaceful encounter for the player's own supply parties.
    /// Custom parties do not inherit the native villager conversation tree,
    /// so they need their own entry from the generic party encounter state.
    /// </summary>
    public partial class SupplyPartyBehavior
    {
        private void OnSessionLaunched(
            CampaignGameStarter campaignGameStarter)
        {
            campaignGameStarter.AddDialogLine(
                "modifiedarmy_supply_party_talk_start",
                "start",
                "modifiedarmy_supply_party_talk",
                "{=ModifiedArmy_SupplyPartyGreeting}" +
                "We are carrying supplies for {HOME_SETTLEMENT}.",
                SupplyPartyTalkStartOnCondition,
                null,
                200,
                null);

            campaignGameStarter.AddPlayerLine(
                "modifiedarmy_supply_party_demand_supplies",
                "modifiedarmy_supply_party_talk",
                "modifiedarmy_supply_party_threat",
                "{=ModifiedArmy_SupplyPartyDemandSupplies}" +
                "Hand over your supplies, or die!",
                SupplyPartyThreatOnCondition,
                null,
                100,
                SupplyPartyThreatOnClickableCondition,
                null);

            campaignGameStarter.AddDialogLine(
                "modifiedarmy_supply_party_refuses",
                "modifiedarmy_supply_party_threat",
                "modifiedarmy_supply_party_attack_decision",
                "{=ModifiedArmy_SupplyPartyRefuses}" +
                "These supplies are not yours. You will have to fight us!" +
                "[rf:idle_angry][ib:aggressive]",
                null,
                null,
                100,
                null);

            campaignGameStarter.AddPlayerLine(
                "modifiedarmy_supply_party_attack",
                "modifiedarmy_supply_party_attack_decision",
                "close_window",
                "{=ModifiedArmy_SupplyPartyAttack}Attack!",
                null,
                SupplyPartyAttackOnConsequence,
                100,
                null,
                null);

            campaignGameStarter.AddPlayerLine(
                "modifiedarmy_supply_party_cancel_attack",
                "modifiedarmy_supply_party_attack_decision",
                "close_window",
                "{=ModifiedArmy_SupplyPartyCancelAttack}" +
                "Never mind. Go in peace.",
                null,
                SupplyPartyLeaveOnConsequence,
                100,
                null,
                null);

            campaignGameStarter.AddPlayerLine(
                "modifiedarmy_supply_party_carry_on",
                "modifiedarmy_supply_party_talk",
                "close_window",
                "{=ModifiedArmy_SupplyPartyCarryOn}" +
                "Carry on. Farewell.",
                null,
                SupplyPartyLeaveOnConsequence,
                100,
                null,
                null);
        }

        private static bool SupplyPartyTalkStartOnCondition()
        {
            if (PlayerEncounter.Current == null
                || Campaign.Current.CurrentConversationContext
                    != ConversationContext.PartyEncounter)
            {
                return false;
            }

            MobileParty encounteredParty =
                PlayerEncounter.EncounteredParty?.MobileParty;
            SupplyPartyComponent component = GetComponent(encounteredParty);

            if (component?.HomeSettlement == null)
            {
                return false;
            }

            MBTextManager.SetTextVariable(
                "HOME_SETTLEMENT",
                component.HomeSettlement.EncyclopediaLinkWithName,
                false);
            return true;
        }

        private static bool SupplyPartyThreatOnCondition()
        {
            MobileParty party = MobileParty.ConversationParty;

            return GetComponent(party) != null
                && party.MapFaction != Hero.MainHero.MapFaction;
        }

        private static bool SupplyPartyThreatOnClickableCondition(
            out TextObject explanation)
        {
            MobileParty party = MobileParty.ConversationParty;

            if (party != null
                && !Hero.MainHero.MapFaction.IsAtWarWith(
                    party.MapFaction))
            {
                explanation = new TextObject(
                    "{=ModifiedArmy_SupplyPartyWarWarning}" +
                    "This action may start a war.");
            }
            else
            {
                explanation = null;
            }

            return true;
        }

        private static void SupplyPartyAttackOnConsequence()
        {
            if (MobileParty.ConversationParty != null)
            {
                BeHostileAction.ApplyEncounterHostileAction(
                    PartyBase.MainParty,
                    MobileParty.ConversationParty.Party);
            }
        }

        private static void SupplyPartyLeaveOnConsequence()
        {
            PlayerEncounter.LeaveEncounter = true;
        }
    }
}
