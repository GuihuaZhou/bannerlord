using ModifiedArmy.Garrison.Models;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Supply.Services
{
    /// <summary>Owns dispatch eligibility sequencing and persisted cooldowns.</summary>
    public sealed class SupplyDispatchService
    {
        public void EvaluateDaily(
            Settlement settlement,
            Dictionary<Settlement, CampaignTime> nextDispatchTimes,
            Func<Settlement, bool> canDispatch,
            Func<Settlement, bool> hasActiveMission,
            Func<Settlement, GarrisonLogisticsStatus, bool> localSupplyDone,
            Func<GarrisonLogisticsStatus, bool> tryDispatch)
        {
            if (!canDispatch(settlement) || hasActiveMission(settlement) ||
                IsCoolingDown(nextDispatchTimes, settlement))
                return;
            GarrisonLogisticsStatus status =
                GarrisonLogisticsModel.Calculate(settlement.Town);
            if (!GarrisonLogisticsModel.NeedsReplenishment(status) ||
                !localSupplyDone(settlement, status))
                return;
            if (!tryDispatch(status))
                SetCooldown(nextDispatchTimes, settlement, 1f);
        }

        public bool IsCoolingDown(
            Dictionary<Settlement, CampaignTime> nextDispatchTimes,
            Settlement home)
        {
            if (!nextDispatchTimes.TryGetValue(home, out CampaignTime next))
                return false;
            if (!next.IsPast) return true;
            nextDispatchTimes.Remove(home);
            return false;
        }

        public void SetCooldown(
            Dictionary<Settlement, CampaignTime> nextDispatchTimes,
            Settlement home,
            float days)
        {
            if (home != null)
                nextDispatchTimes[home] = CampaignTime.Now +
                    CampaignTime.Days(days);
        }
    }
}
