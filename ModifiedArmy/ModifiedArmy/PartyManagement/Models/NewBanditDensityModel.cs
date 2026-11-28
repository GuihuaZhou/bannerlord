using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace ModifiedArmy.Models
{
    /// <summary>
    /// Keeps the native/DLC density model as the authority, then applies only
    /// the bounded land-bandit policy owned by ModifiedArmy.
    /// </summary>
    public sealed class NewBanditDensityModel : DefaultBanditDensityModel
    {
        private const int SharedLooterAndDeserterBudget = 120;
        private const int DeserterBudget = 50;

        private Clan _deserterClan;

        public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt
            => Math.Min(2, BaseModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt);

        public override int NumberOfMaximumBanditPartiesInEachHideout
            => Math.Min(2, BaseModel.NumberOfMaximumBanditPartiesInEachHideout);

        public override int NumberOfMaximumBanditPartiesAroundEachHideout
            => Math.Min(3, BaseModel.NumberOfMaximumBanditPartiesAroundEachHideout);

        public override int NumberOfMaximumHideoutsAtEachBanditFaction
            => Math.Min(7, BaseModel.NumberOfMaximumHideoutsAtEachBanditFaction);

        public override int NumberOfInitialHideoutsAtEachBanditFaction
            => Math.Min(5, BaseModel.NumberOfInitialHideoutsAtEachBanditFaction);

        public override int GetMaxSupportedNumberOfLootersForClan(Clan clan)
        {
            int baseLimit = clan == null
                ? SharedLooterAndDeserterBudget
                : BaseModel.GetMaxSupportedNumberOfLootersForClan(clan);
            if (clan == null || baseLimit <= 0) return 0;
            if (HasNavalNavigationCapability(clan)) return baseLimit;

            Clan deserters = GetDeserterClan();
            if (clan == deserters || clan.StringId == "deserters")
            {
                return Math.Min(baseLimit, DeserterBudget);
            }

            if (clan.StringId == "looters" && deserters != null)
            {
                return Math.Min(baseLimit, Math.Max(0,
                    SharedLooterAndDeserterBudget -
                    (deserters?.WarPartyComponents.Count ?? 0)));
            }

            return Math.Min(baseLimit, SharedLooterAndDeserterBudget);
        }

        public override bool IsPositionInsideNavalSafeZone(CampaignVec2 position)
            => BaseModel.IsPositionInsideNavalSafeZone(position);

        private static bool HasNavalNavigationCapability(Clan clan)
        {
            var property = clan?.GetType().GetProperty(
                "HasNavalNavigationCapability");
            return property?.PropertyType == typeof(bool) &&
                (bool)property.GetValue(clan, null);
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
