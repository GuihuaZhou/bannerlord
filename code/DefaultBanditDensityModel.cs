using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x020000F2 RID: 242
	public class DefaultBanditDensityModel : BanditDensityModel
	{
		// Token: 0x1700061C RID: 1564
		// (get) Token: 0x06001650 RID: 5712 RVA: 0x000670A3 File Offset: 0x000652A3
		public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt
		{
			get
			{
				return 2;
			}
		}

		// Token: 0x1700061D RID: 1565
		// (get) Token: 0x06001651 RID: 5713 RVA: 0x000670A6 File Offset: 0x000652A6
		public override int NumberOfMaximumBanditPartiesInEachHideout
		{
			get
			{
				return 3;
			}
		}

		// Token: 0x1700061E RID: 1566
		// (get) Token: 0x06001652 RID: 5714 RVA: 0x000670A9 File Offset: 0x000652A9
		public override int NumberOfMaximumBanditPartiesAroundEachHideout
		{
			get
			{
				return 3;
			}
		}

		// Token: 0x1700061F RID: 1567
		// (get) Token: 0x06001653 RID: 5715 RVA: 0x000670AC File Offset: 0x000652AC
		public override int NumberOfMaximumHideoutsAtEachBanditFaction
		{
			get
			{
				return 9;
			}
		}

		// Token: 0x17000620 RID: 1568
		// (get) Token: 0x06001654 RID: 5716 RVA: 0x000670B0 File Offset: 0x000652B0
		public override int NumberOfInitialHideoutsAtEachBanditFaction
		{
			get
			{
				return 7;
			}
		}

		// Token: 0x17000621 RID: 1569
		// (get) Token: 0x06001655 RID: 5717 RVA: 0x000670B3 File Offset: 0x000652B3
		public override int NumberOfMinimumBanditTroopsInHideoutMission
		{
			get
			{
				return 10;
			}
		}

		// Token: 0x17000622 RID: 1570
		// (get) Token: 0x06001656 RID: 5718 RVA: 0x000670B7 File Offset: 0x000652B7
		public override int NumberOfMaximumTroopCountForFirstFightInHideout
		{
			get
			{
				return MathF.Floor(11f * (2f + Campaign.Current.PlayerProgress));
			}
		}

		// Token: 0x17000623 RID: 1571
		// (get) Token: 0x06001657 RID: 5719 RVA: 0x000670D4 File Offset: 0x000652D4
		public override int NumberOfMaximumTroopCountForBossFightInHideout
		{
			get
			{
				return MathF.Floor(1f + 5f * (1f + Campaign.Current.PlayerProgress));
			}
		}

		// Token: 0x17000624 RID: 1572
		// (get) Token: 0x06001658 RID: 5720 RVA: 0x000670F7 File Offset: 0x000652F7
		public override float SpawnPercentageForFirstFightInHideoutMission
		{
			get
			{
				return 0.8f;
			}
		}

		// Token: 0x17000625 RID: 1573
		// (get) Token: 0x06001659 RID: 5721 RVA: 0x000670FE File Offset: 0x000652FE
		private Clan DeserterClan
		{
			get
			{
				if (this._deserterClan == null)
				{
					this._deserterClan = Clan.FindFirst((Clan x) => x.StringId == "deserters");
				}
				return this._deserterClan;
			}
		}

		// Token: 0x0600165A RID: 5722 RVA: 0x00067138 File Offset: 0x00065338
		public override int GetMinimumTroopCountForHideoutMission(MobileParty party, bool isAssault)
		{
			if (!isAssault)
			{
				return 25;
			}
			return 8;
		}

		// Token: 0x0600165B RID: 5723 RVA: 0x00067144 File Offset: 0x00065344
		public override int GetMaxSupportedNumberOfLootersForClan(Clan clan)
		{
			if (clan == this.DeserterClan)
			{
				return 50;
			}
			if (clan.StringId == "looters" && this.DeserterClan != null)
			{
				return 270 - this.DeserterClan.WarPartyComponents.Count;
			}
			return 270;
		}

		// Token: 0x0600165C RID: 5724 RVA: 0x00067194 File Offset: 0x00065394
		public override int GetMaximumTroopCountForHideoutMission(MobileParty party, bool isAssault)
		{
			int num = isAssault ? 15 : 40;
			if (party.HasPerk(DefaultPerks.Tactics.SmallUnitTactics, false))
			{
				num += (int)DefaultPerks.Tactics.SmallUnitTactics.PrimaryBonus;
			}
			return num;
		}

		// Token: 0x0600165D RID: 5725 RVA: 0x000671C8 File Offset: 0x000653C8
		public override bool IsPositionInsideNavalSafeZone(CampaignVec2 position)
		{
			return false;
		}

		// Token: 0x0400077E RID: 1918
		private const int MinimumTroopCountForHideoutMission = 25;

		// Token: 0x0400077F RID: 1919
		private Clan _deserterClan;
	}
}
