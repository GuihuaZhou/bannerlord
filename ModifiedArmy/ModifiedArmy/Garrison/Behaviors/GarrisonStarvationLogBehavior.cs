using ModifiedArmy.Tool;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

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

            ModLogger.Notice(
                $"[GarrisonLogistics] Starving garrison desertion | " +
                $"Settlement='{party.CurrentSettlement.Name}' | " +
                $"Deserted={desertedTroops.TotalManCount} | " +
                $"Remaining={party.MemberRoster.TotalManCount}");
        }
    }
}
