using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.PartyFinance.Behaviors
{
    /// <summary>
    /// Reports actual desertion and party disbanding for military parties in
    /// the player's kingdom. Native behaviors remain responsible for removing
    /// or transferring troops; this behavior is notification-only.
    /// </summary>
    public sealed class PartyPersonnelLossLogBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnTroopsDesertedEvent
                .AddNonSerializedListener(this, OnTroopsDeserted);
            CampaignEvents.OnPartyDisbandStartedEvent
                .AddNonSerializedListener(this, OnPartyDisbandStarted);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        /// <summary>
        /// Emits one compact Notice after native desertion has removed troops.
        /// The pre-desertion headcount is reconstructed from the event roster
        /// so over-capacity can still be identified accurately.
        /// </summary>
        private static void OnTroopsDeserted(
            MobileParty party,
            TroopRoster desertedTroops)
        {
            if (!ShouldNotify(party)
                || desertedTroops == null
                || desertedTroops.TotalManCount <= 0)
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_troops_deserted");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable(
                "REASON",
                GetDesertionReason(party, desertedTroops.TotalManCount));
            message.SetTextVariable(
                "DESERTED",
                desertedTroops.TotalManCount);
            message.SetTextVariable(
                "REMAINING",
                party.MemberRoster.TotalManCount);
            ModLogger.Notice(message.ToString());
        }

        /// <summary>
        /// Reports the beginning rather than the end of disbanding, while the
        /// party still has a stable owner, display name and troop count.
        /// </summary>
        private static void OnPartyDisbandStarted(MobileParty party)
        {
            if (!ShouldNotify(party))
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_party_disband_started");
            message.SetTextVariable(
                "PARTY_NAME",
                PartyLogFormatter.GetDisplayName(party));
            message.SetTextVariable(
                "MEMBERS",
                party.MemberRoster.TotalManCount);
            ModLogger.Notice(message.ToString());
        }

        /// <summary>
        /// Restricts important notifications to lord parties and garrisons in
        /// the player's current kingdom. Caravans, villagers and temporary
        /// logistics parties are intentionally excluded.
        /// </summary>
        private static bool ShouldNotify(MobileParty party)
        {
            Kingdom playerKingdom = Clan.PlayerClan?.Kingdom;

            if (party == null
                || playerKingdom == null
                || (!party.IsLordParty && !party.IsGarrison))
            {
                return false;
            }

            Clan ownerClan = party.IsGarrison
                ? party.CurrentSettlement?.OwnerClan ?? party.ActualClan
                : party.ActualClan ?? party.LeaderHero?.Clan;
            return ownerClan?.Kingdom == playerKingdom;
        }

        /// <summary>
        /// Resolves the most actionable native desertion condition. Multiple
        /// conditions can coexist, so severe supply and finance causes take
        /// precedence over party size and morale.
        /// </summary>
        private static TextObject GetDesertionReason(
            MobileParty party,
            int desertedCount)
        {
            if (party.Party.IsStarving)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_desertion_reason_starvation");
            }

            if (party.HasUnpaidWages > 0f)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_desertion_reason_unpaid_wages");
            }

            if (party.HasLimitedWage()
                && party.TotalWage > party.PaymentLimit)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_desertion_reason_wage_limit");
            }

            int membersBeforeDesertion =
                party.Party.NumberOfAllMembers + desertedCount;

            if (membersBeforeDesertion > party.Party.PartySizeLimit)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_desertion_reason_party_size");
            }

            int moraleThreshold = Campaign.Current.Models
                .PartyDesertionModel
                .GetMoraleThresholdForTroopDesertion();

            if (party.Morale <= moraleThreshold)
            {
                return GameTexts.FindText(
                    "str_modifiedarmy_desertion_reason_morale");
            }

            return GameTexts.FindText(
                "str_modifiedarmy_desertion_reason_unknown");
        }
    }
}
