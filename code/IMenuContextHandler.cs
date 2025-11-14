using System;
using TaleWorlds.CampaignSystem.Roster;

namespace TaleWorlds.CampaignSystem.GameState
{
	// Token: 0x02000391 RID: 913
	public interface IMenuContextHandler
	{
		// Token: 0x06003421 RID: 13345
		void OnBackgroundMeshNameSet(string name);

		// Token: 0x06003422 RID: 13346
		void OnOpenTownManagement();

		// Token: 0x06003423 RID: 13347
		void OnOpenRecruitVolunteers();

		// Token: 0x06003424 RID: 13348
		void OnOpenTournamentLeaderboard();

		// Token: 0x06003425 RID: 13349
		void OnOpenTroopSelection(TroopRoster fullRoster, TroopRoster initialSelections, Func<CharacterObject, bool> canChangeStatusOfTroop, Action<TroopRoster> onDone, int maxSelectableTroopCount, int minSelectableTroopCount);

		// Token: 0x06003426 RID: 13350
		void OnMenuCreate();

		// Token: 0x06003427 RID: 13351
		void OnMenuActivate();

		// Token: 0x06003428 RID: 13352
		void OnMenuRefresh();

		// Token: 0x06003429 RID: 13353
		void OnHourlyTick();

		// Token: 0x0600342A RID: 13354
		void OnPanelSoundIDSet(string panelSoundID);

		// Token: 0x0600342B RID: 13355
		void OnAmbientSoundIDSet(string ambientSoundID);
    }
}
