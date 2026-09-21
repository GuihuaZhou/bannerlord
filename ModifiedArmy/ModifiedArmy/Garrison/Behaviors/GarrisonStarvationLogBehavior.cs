using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace ModifiedArmy.Garrison.Behaviors
{
    /// <summary>
    /// Reports player-garrison desertion while the military granary is empty.
    /// Vanilla desertion remains responsible for selecting and removing troops.
    /// </summary>
    public class GarrisonStarvationLogBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnTroopsDesertedEvent
                .AddNonSerializedListener(this, OnTroopsDeserted);
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        private static void OnTroopsDeserted(
            MobileParty party,
            TroopRoster desertedTroops)
        {
            if (party?.IsGarrison != true
                || party.CurrentSettlement?.OwnerClan != Clan.PlayerClan
                || party.Party.IsStarving != true
                || desertedTroops == null
                || desertedTroops.TotalManCount <= 0)
            {
                return;
            }

            TextObject message = GameTexts.FindText(
                "str_modifiedarmy_garrison_starvation_desertion");
            message.SetTextVariable(
                "SETTLEMENT_NAME",
                party.CurrentSettlement.Name);
            message.SetTextVariable(
                "DESERTED",
                desertedTroops.TotalManCount);
            message.SetTextVariable(
                "REMAINING",
                party.MemberRoster.TotalManCount);
            ModLogger.Notice(message.ToString());
        }
    }
}
