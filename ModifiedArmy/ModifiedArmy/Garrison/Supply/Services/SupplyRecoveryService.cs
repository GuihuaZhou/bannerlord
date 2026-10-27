using ModifiedArmy.ForceManagement.Transport;
using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Supply.Services
{
    /// <summary>Restores saved supply component ownership and mission routes.</summary>
    public sealed class SupplyRecoveryService
    {
        public void RestoreLoadedMissions(
            Action<MobileParty, SupplyPartyComponent> advanceAid,
            Action<MobileParty, Settlement> restoreRoute)
        {
            foreach (MobileParty party in MobileParty.All)
            {
                SupplyPartyComponent component = party?.PartyComponent as
                    SupplyPartyComponent;
                if (component == null) continue;
                component.SynchronizeOwnership();
                party.Ai.SetDoNotMakeNewDecisions(false);
                if (party.MapEvent != null ||
                    TransportThreatModel.IsNativeAvoidanceActive(party))
                    continue;
                if (component.IsAidMission)
                {
                    advanceAid(party, component);
                    continue;
                }
                Settlement destination = component.MissionState ==
                        SupplyPartyMissionState.TravelingToSource
                    ? component.SourceSettlement
                    : component.HomeSettlement;
                if (destination != null)
                    restoreRoute(party, destination);
            }
        }
    }
}
