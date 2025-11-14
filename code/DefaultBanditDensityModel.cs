using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x020000F1 RID: 241
	public class DefaultBanditDensityModel : BanditDensityModel
	{
		// Token: 0x1700060F RID: 1551
		// (get) Token: 0x060015FE RID: 5630 RVA: 0x00064DFD File Offset: 0x00062FFD
		public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt
		{
			get
			{
				return 2;
			}
		}

		// Token: 0x17000610 RID: 1552
		// (get) Token: 0x060015FF RID: 5631 RVA: 0x00064E00 File Offset: 0x00063000
		public override int NumberOfMaximumBanditPartiesInEachHideout
		{
			get
			{
				return 4;
			}
		}

		// Token: 0x17000611 RID: 1553
		// (get) Token: 0x06001600 RID: 5632 RVA: 0x00064E03 File Offset: 0x00063003
		public override int NumberOfMaximumBanditPartiesAroundEachHideout
		{
			get
			{
				return 4;
			}
		}

		// Token: 0x17000612 RID: 1554
		// (get) Token: 0x06001601 RID: 5633 RVA: 0x00064E06 File Offset: 0x00063006
		public override int NumberOfMaximumHideoutsAtEachBanditFaction
		{
			get
			{
				return 8;
			}
		}

		// Token: 0x17000613 RID: 1555
		// (get) Token: 0x06001602 RID: 5634 RVA: 0x00064E09 File Offset: 0x00063009
		public override int NumberOfInitialHideoutsAtEachBanditFaction
		{
			get
			{
				return 6;
			}
		}

		// Token: 0x17000614 RID: 1556
		// (get) Token: 0x06001603 RID: 5635 RVA: 0x00064E0C File Offset: 0x0006300C
		public override int NumberOfMinimumBanditTroopsInHideoutMission
		{
			get
			{
				return 10;
			}
		}

		// Token: 0x17000615 RID: 1557
		// (get) Token: 0x06001604 RID: 5636 RVA: 0x00064E10 File Offset: 0x00063010
		public override int NumberOfMaximumTroopCountForFirstFightInHideout
		{
			get
			{
				return MathF.Floor(6f * (2f + Campaign.Current.PlayerProgress));
			}
		}

		// Token: 0x17000616 RID: 1558
		// (get) Token: 0x06001605 RID: 5637 RVA: 0x00064E2D File Offset: 0x0006302D
		public override int NumberOfMaximumTroopCountForBossFightInHideout
		{
			get
			{
				return MathF.Floor(1f + 5f * (1f + Campaign.Current.PlayerProgress));
			}
		}

		// Token: 0x17000617 RID: 1559
		// (get) Token: 0x06001606 RID: 5638 RVA: 0x00064E50 File Offset: 0x00063050
		public override float SpawnPercentageForFirstFightInHideoutMission
		{
			get
			{
				return 0.75f;
			}
		}

		// Token: 0x17000618 RID: 1560
		// (get) Token: 0x06001607 RID: 5639 RVA: 0x00064E57 File Offset: 0x00063057
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

		// Token: 0x06001608 RID: 5640 RVA: 0x00064E91 File Offset: 0x00063091
		public override int GetMinimumTroopCountForHideoutMission(MobileParty party)
		{
			return 25;
		}

		// Token: 0x06001609 RID: 5641 RVA: 0x00064E98 File Offset: 0x00063098
		public override int GetMaxSupportedNumberOfLootersForClan(Clan clan)
		{
			if (clan == this.DeserterClan)
			{
				return 50;
			}
			if (clan.StringId == "looters" && this.DeserterClan != null)
			{
				return 300 - this.DeserterClan.WarPartyComponents.Count;
			}
			return 300;
		}

		// Token: 0x0600160A RID: 5642 RVA: 0x00064EE8 File Offset: 0x000630E8
		public override int GetMaximumTroopCountForHideoutMission(MobileParty party)
		{
			int num = 40;
			if (party.HasPerk(DefaultPerks.Tactics.SmallUnitTactics, false))
			{
				num += (int)DefaultPerks.Tactics.SmallUnitTactics.PrimaryBonus;
			}
			return num;
		}

		// Token: 0x0600160B RID: 5643 RVA: 0x00064F15 File Offset: 0x00063115
		public override bool IsPositionInsideNavalSafeZone(CampaignVec2 position)
		{
			return false;
		}

		// Token: 0x04000740 RID: 1856
		private Clan _deserterClan;
	}
}
