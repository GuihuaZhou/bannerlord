using ModifiedArmy.ForceManagement.Transport;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace ModifiedArmy.Garrison.Supply.Services
{
    /// <summary>
    /// Owns campaign-runtime route throttles. It never changes mission state
    /// and deliberately yields to native flee/avoidance behavior.
    /// </summary>
    public sealed class SupplyRouteController
    {
        private const float ThreatRecoveryDelayHours = 6f;
        private readonly Dictionary<string, CampaignTime>
            _threatAvoidanceUntil = new Dictionary<string, CampaignTime>();
        private readonly Dictionary<string, int> _lastMismatchDay =
            new Dictionary<string, int>();
        private readonly Dictionary<string, double> _lastCommandHours =
            new Dictionary<string, double>();

        public bool ShouldPauseForThreat(
            MobileParty party,
            Action<bool, bool> onAvoiding,
            Action onResumed)
        {
            bool nativeAvoidance =
                TransportThreatModel.IsNativeAvoidanceActive(party);
            bool superiorEnemy = TransportThreatModel.HasSuperiorEnemy(
                party, party.Position, party.MapFaction);
            if (nativeAvoidance || superiorEnemy)
            {
                if (!_threatAvoidanceUntil.ContainsKey(party.StringId))
                    onAvoiding?.Invoke(nativeAvoidance, superiorEnemy);
                _threatAvoidanceUntil[party.StringId] = CampaignTime.Now +
                    CampaignTime.Hours(ThreatRecoveryDelayHours);
                return true;
            }
            if (_threatAvoidanceUntil.TryGetValue(party.StringId,
                    out CampaignTime until))
            {
                if (!until.IsPast) return true;
                _threatAvoidanceUntil.Remove(party.StringId);
                onResumed?.Invoke();
            }
            return false;
        }

        public void RecordRouteCommand(MobileParty party)
        {
            if (!string.IsNullOrEmpty(party?.StringId))
                _lastCommandHours[party.StringId] =
                    CampaignTime.Now.ToHours;
        }

        public void ForgetParty(string partyId)
        {
            if (string.IsNullOrEmpty(partyId)) return;
            _threatAvoidanceUntil.Remove(partyId);
            _lastMismatchDay.Remove(partyId);
            _lastCommandHours.Remove(partyId);
        }

        public bool ShouldReportUnexpectedOverride(
            MobileParty party,
            Settlement expected)
        {
            if (party == null || expected == null ||
                party.CurrentSettlement == expected ||
                (party.DefaultBehavior == AiBehavior.GoToSettlement &&
                 party.TargetSettlement == expected))
                return false;
            if (_lastCommandHours.TryGetValue(party.StringId,
                    out double commandHour) &&
                CampaignTime.Now.ToHours - commandHour < 2d)
                return false;
            int day = (int)Math.Floor(CampaignTime.Now.ToDays);
            if (_lastMismatchDay.TryGetValue(party.StringId,
                    out int loggedDay) && loggedDay == day)
                return false;
            _lastMismatchDay[party.StringId] = day;
            return true;
        }
    }
}
