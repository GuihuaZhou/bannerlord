using ModifiedArmy.common;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>Defines the exact parties whose autonomous force decisions belong to this mod.</summary>
    public static class ManagedPartyPolicy
    {
        /// <summary>
        /// Returns true only for a live AI lord party. Player-controlled,
        /// player-clan, minor-faction, caravan, villager, bandit and
        /// disbanding parties keep their existing specialized behavior.
        /// </summary>
        public static bool IsModManagedAiParty(MobileParty party)
        {
            return IsEnabled() && party != null && party.IsActive &&
                party.IsLordParty && !party.IsMainParty &&
                !party.IsDisbanding && party.LeaderHero?.IsAlive == true &&
                party.ActualClan != null &&
                !party.ActualClan.IsMinorFaction &&
                party.ActualClan != Clan.PlayerClan;
        }

        public static bool IsModManagedAiLeader(Hero leader)
        {
            Clan clan = leader?.Clan;
            return IsEnabled() && leader != null && leader.IsAlive &&
                !leader.IsHumanPlayerCharacter && clan != null &&
                clan != Clan.PlayerClan && !clan.IsMinorFaction &&
                !clan.IsRebelClan && !clan.IsEliminated;
        }

        /// <summary>Reads the atomic-switch flag with a safe false fallback.</summary>
        public static bool IsEnabled()
        {
            return MilitaryConfigManager.Instance.GetActiveConfig()?
                .AiRecruitment.ForceManagement.EnableUnifiedAiForcePlanning == true;
        }
    }
}
