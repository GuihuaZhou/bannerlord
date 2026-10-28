using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// Reduces future bandit spawning pressure without mutating existing
    /// parties, hideouts, rosters, or save data.
    /// </summary>
    public sealed class NewBanditDensityModel : DefaultBanditDensityModel
    {
        private const int SharedLooterAndDeserterBudget = 120;
        private const int DeserterBudget = 50;

        private Clan _deserterClan;

        public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt
            => 2;

        public override int NumberOfMaximumBanditPartiesInEachHideout => 2;

        public override int NumberOfMaximumBanditPartiesAroundEachHideout => 3;

        public override int NumberOfMaximumHideoutsAtEachBanditFaction => 7;

        public override int NumberOfInitialHideoutsAtEachBanditFaction => 5;

        public override int GetMaxSupportedNumberOfLootersForClan(Clan clan)
        {
            if (clan == null)
            {
                return SharedLooterAndDeserterBudget;
            }

            Clan deserters = GetDeserterClan();
            if (clan == deserters || clan.StringId == "deserters")
            {
                return DeserterBudget;
            }

            if (clan.StringId == "looters" && deserters != null)
            {
                return Math.Max(
                    0,
                    SharedLooterAndDeserterBudget -
                    deserters.WarPartyComponents.Count);
            }

            return SharedLooterAndDeserterBudget;
        }

        private Clan GetDeserterClan()
        {
            if (_deserterClan == null)
            {
                _deserterClan = Clan.FindFirst(
                    clan => clan.StringId == "deserters");
            }

            return _deserterClan;
        }
    }
}
