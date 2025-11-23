using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x02000417 RID: 1047
	public class MilitiasCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x0600425F RID: 16991 RVA: 0x0013F76D File Offset: 0x0013D96D
		public override void RegisterEvents()
		{
			CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter, int>(this.OnNewGameCreatedPartialFollowUp));
			CampaignEvents.AfterSiegeCompletedEvent.AddNonSerializedListener(this, new Action<Settlement, MobileParty, bool, MapEvent.BattleTypes>(this.OnAfterSiegeCompleted));
		}

		// Token: 0x06004260 RID: 16992 RVA: 0x0013F7A0 File Offset: 0x0013D9A0
		private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int i)
		{
			int count = Town.AllTowns.Count;
			int count2 = Town.AllCastles.Count;
			int count3 = Village.All.Count;
			int num = count / 100 + ((count % 100 > i) ? 1 : 0);
			int num2 = count2 / 100 + ((count2 % 100 > i) ? 1 : 0);
			int num3 = count3 / 100 + ((count3 % 100 > i) ? 1 : 0);
			int num4 = count / 100 * i;
			int num5 = count2 / 100 * i;
			int num6 = count3 / 100 * i;
			for (int j = 0; j < i; j++)
			{
				num4 += ((count % 100 > j) ? 1 : 0);
				num5 += ((count2 % 100 > j) ? 1 : 0);
				num6 += ((count3 % 100 > j) ? 1 : 0);
			}
			for (int k = 0; k < num; k++)
			{
				Town.AllTowns[num4 + k].Settlement.Militia = Town.AllTowns[num4 + k].Settlement.Town.MilitiaChange * 45f;
			}
			for (int l = 0; l < num2; l++)
			{
				Town.AllCastles[num5 + l].Settlement.Militia = Town.AllCastles[num5 + l].Settlement.Town.MilitiaChange * 45f;
			}
			for (int m = 0; m < num3; m++)
			{
				Village.All[num6 + m].Settlement.Militia = Village.All[num6 + m].Settlement.Village.MilitiaChange * 45f;
			}
		}

		// Token: 0x06004261 RID: 16993 RVA: 0x0013F947 File Offset: 0x0013DB47
		private void OnAfterSiegeCompleted(Settlement siegeSettlement, MobileParty attackerParty, bool isWin, MapEvent.BattleTypes battleType)
		{
			if ((battleType == MapEvent.BattleTypes.SallyOut || battleType == MapEvent.BattleTypes.Siege) && isWin)
			{
				siegeSettlement.Militia += (float)Campaign.Current.Models.SettlementMilitiaModel.MilitiaToSpawnAfterSiege(siegeSettlement.Town);
			}
		}

		// Token: 0x06004262 RID: 16994 RVA: 0x0013F982 File Offset: 0x0013DB82
		public override void SyncData(IDataStore dataStore)
		{
		}
	}
}
