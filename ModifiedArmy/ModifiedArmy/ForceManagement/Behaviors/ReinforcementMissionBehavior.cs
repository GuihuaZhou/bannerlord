using ModifiedArmy.ForceManagement.Contracts;
using ModifiedArmy.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace ModifiedArmy.ForceManagement.Behaviors
{
    /// <summary>
    /// Compatibility reader for the retired lord-party reinforcement design.
    /// It preserves the old save key and safely cancels loaded active tasks.
    /// </summary>
    [Obsolete("Legacy save compatibility only")]
    public sealed class ReinforcementMissionBehavior : CampaignBehaviorBase
    {
        private List<ReinforcementMission> _missions =
            new List<ReinforcementMission>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(
                this,
                OnGameLoadFinished);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData(
                "modifiedarmy_reinforcement_missions",
                ref _missions);
            _missions ??= new List<ReinforcementMission>();
        }

        private void OnGameLoadFinished()
        {
            int cancelled = 0;
            foreach (ReinforcementMission mission in _missions
                .Where(item => item?.IsActive == true))
            {
                mission.State = ReinforcementMissionState.Cancelled;
                cancelled++;
            }
            if (cancelled > 0)
            {
                ModLogger.Notice(
                    "Cancelled retired lord reinforcement missions: " +
                    cancelled);
            }
        }
    }
}
