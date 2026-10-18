using ModifiedArmy.common;
using ModifiedArmy.ForceManagement.Contracts;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;

namespace ModifiedArmy.ForceManagement.Services
{
    /// <summary>
    /// Provides the mission lifecycle and Army protection boundary without
    /// enabling automatic dispatch. Persistent transport execution is added
    /// only after route and Army-creation protections have been verified.
    /// </summary>
    public sealed class ReinforcementMissionService
        : IArmyMissionProtectionModel
    {
        private readonly Dictionary<MobileParty, ReinforcementMission>
            _missions = new Dictionary<MobileParty, ReinforcementMission>();

        public bool IsProtected(MobileParty party)
        {
            return party != null &&
                _missions.TryGetValue(party, out ReinforcementMission mission) &&
                mission?.IsActive == true;
        }

        public bool MayStartReinforcement(MobileParty party)
        {
            return party != null && party.IsActive && party.Army == null &&
                !IsProtected(party) &&
                MilitaryConfigManager.Instance.GetActiveConfig()?
                    .AiRecruitment.ForceManagement
                    .EnableAutomaticReinforcement == true;
        }

        public bool TryRegister(ReinforcementMission mission)
        {
            MobileParty carrier = mission?.Carrier;
            if (!MayStartReinforcement(carrier) || !mission.IsActive)
            {
                return false;
            }

            _missions[carrier] = mission;
            return true;
        }

        public void Complete(MobileParty party)
        {
            if (party != null && _missions.TryGetValue(
                party,
                out ReinforcementMission mission))
            {
                mission.State = ReinforcementMissionState.Completed;
                _missions.Remove(party);
            }
        }

        public void Cancel(
            MobileParty party,
            ReinforcementCancelReason reason)
        {
            if (party != null && _missions.TryGetValue(
                party,
                out ReinforcementMission mission))
            {
                mission.CancelReason = reason;
                mission.State = ReinforcementMissionState.Cancelled;
                _missions.Remove(party);
            }
        }
    }
}
