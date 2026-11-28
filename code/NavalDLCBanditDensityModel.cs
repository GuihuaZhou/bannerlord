using System;
using StoryMode;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace NavalDLC.GameComponents
{
	// Token: 0x0200010D RID: 269
	public class NavalDLCBanditDensityModel : BanditDensityModel
	{
		// Token: 0x1700034D RID: 845
		// (get) Token: 0x060013C9 RID: 5065 RVA: 0x0008FA65 File Offset: 0x0008DC65
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

		// Token: 0x1700034E RID: 846
		// (get) Token: 0x060013CA RID: 5066 RVA: 0x0008FA9F File Offset: 0x0008DC9F
		public override int NumberOfMinimumBanditPartiesInAHideoutToInfestIt
		{
			get
			{
				return base.BaseModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt;
			}
		}

		// Token: 0x1700034F RID: 847
		// (get) Token: 0x060013CB RID: 5067 RVA: 0x0008FAAC File Offset: 0x0008DCAC
		public override int NumberOfMaximumBanditPartiesInEachHideout
		{
			get
			{
				return base.BaseModel.NumberOfMaximumBanditPartiesInEachHideout;
			}
		}

		// Token: 0x17000350 RID: 848
		// (get) Token: 0x060013CC RID: 5068 RVA: 0x0008FAB9 File Offset: 0x0008DCB9
		public override int NumberOfMaximumBanditPartiesAroundEachHideout
		{
			get
			{
				return base.BaseModel.NumberOfMaximumBanditPartiesAroundEachHideout;
			}
		}

		// Token: 0x17000351 RID: 849
		// (get) Token: 0x060013CD RID: 5069 RVA: 0x0008FAC6 File Offset: 0x0008DCC6
		public override int NumberOfMinimumBanditTroopsInHideoutMission
		{
			get
			{
				return base.BaseModel.NumberOfMinimumBanditTroopsInHideoutMission;
			}
		}

		// Token: 0x17000352 RID: 850
		// (get) Token: 0x060013CE RID: 5070 RVA: 0x0008FAD4 File Offset: 0x0008DCD4
		public override int NumberOfInitialHideoutsAtEachBanditFaction
		{
			get
			{
				StoryModeManager storyModeManager = StoryModeManager.Current;
				bool flag;
				if (storyModeManager == null)
				{
					flag = false;
				}
				else
				{
					MainStoryLine mainStoryLine = storyModeManager.MainStoryLine;
					bool? flag2 = (mainStoryLine != null) ? new bool?(mainStoryLine.IsPlayerInteractionRestricted) : null;
					bool flag3 = true;
					flag = (flag2.GetValueOrDefault() == flag3 & flag2 != null);
				}
				if (flag)
				{
					return 0;
				}
				return base.BaseModel.NumberOfInitialHideoutsAtEachBanditFaction + 1;
			}
		}

		// Token: 0x17000353 RID: 851
		// (get) Token: 0x060013CF RID: 5071 RVA: 0x0008FB34 File Offset: 0x0008DD34
		public override int NumberOfMaximumHideoutsAtEachBanditFaction
		{
			get
			{
				StoryModeManager storyModeManager = StoryModeManager.Current;
				bool flag;
				if (storyModeManager == null)
				{
					flag = false;
				}
				else
				{
					MainStoryLine mainStoryLine = storyModeManager.MainStoryLine;
					bool? flag2 = (mainStoryLine != null) ? new bool?(mainStoryLine.IsPlayerInteractionRestricted) : null;
					bool flag3 = true;
					flag = (flag2.GetValueOrDefault() == flag3 & flag2 != null);
				}
				if (flag)
				{
					return 0;
				}
				return base.BaseModel.NumberOfMaximumHideoutsAtEachBanditFaction + 1;
			}
		}

		// Token: 0x17000354 RID: 852
		// (get) Token: 0x060013D0 RID: 5072 RVA: 0x0008FB91 File Offset: 0x0008DD91
		public override int NumberOfMaximumTroopCountForFirstFightInHideout
		{
			get
			{
				return base.BaseModel.NumberOfMaximumTroopCountForFirstFightInHideout;
			}
		}

		// Token: 0x17000355 RID: 853
		// (get) Token: 0x060013D1 RID: 5073 RVA: 0x0008FB9E File Offset: 0x0008DD9E
		public override int NumberOfMaximumTroopCountForBossFightInHideout
		{
			get
			{
				return base.BaseModel.NumberOfMaximumTroopCountForBossFightInHideout;
			}
		}

		// Token: 0x17000356 RID: 854
		// (get) Token: 0x060013D2 RID: 5074 RVA: 0x0008FBAB File Offset: 0x0008DDAB
		public override float SpawnPercentageForFirstFightInHideoutMission
		{
			get
			{
				return base.BaseModel.SpawnPercentageForFirstFightInHideoutMission;
			}
		}

		// Token: 0x060013D3 RID: 5075 RVA: 0x0008FBB8 File Offset: 0x0008DDB8
		public override int GetMaximumTroopCountForHideoutMission(MobileParty party, bool isAssault)
		{
			return base.BaseModel.GetMaximumTroopCountForHideoutMission(party, isAssault);
		}

		// Token: 0x060013D4 RID: 5076 RVA: 0x0008FBC8 File Offset: 0x0008DDC8
		public override bool IsPositionInsideNavalSafeZone(CampaignVec2 position)
		{
			if (position.IsValid() && !position.IsOnLand)
			{
				Settlement item = Campaign.Current.Models.MapDistanceModel.GetClosestEntranceToFace(position.Face, MobileParty.NavigationType.Naval).Item1;
				float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(item, position, true, MobileParty.NavigationType.Naval);
				float num = item.IsVillage ? 7f : 15f;
				if (distance < num)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x060013D5 RID: 5077 RVA: 0x0008FC3C File Offset: 0x0008DE3C
		public override int GetMaxSupportedNumberOfLootersForClan(Clan clan)
		{
			StoryModeManager storyModeManager = StoryModeManager.Current;
			bool flag;
			if (storyModeManager == null)
			{
				flag = false;
			}
			else
			{
				MainStoryLine mainStoryLine = storyModeManager.MainStoryLine;
				bool? flag2 = (mainStoryLine != null) ? new bool?(mainStoryLine.IsPlayerInteractionRestricted) : null;
				bool flag3 = true;
				flag = (flag2.GetValueOrDefault() == flag3 & flag2 != null);
			}
			if (flag)
			{
				return 0;
			}
			if (clan.HasNavalNavigationCapability)
			{
				return NavalDLCManager.Instance.NavalMapSceneWrapper.GetSpawnPoints(clan.StringId).Count;
			}
			if (clan.StringId == "looters")
			{
				return 300 - ((this.DeserterClan != null) ? this.DeserterClan.WarPartyComponents.Count : 0);
			}
			return base.BaseModel.GetMaxSupportedNumberOfLootersForClan(clan);
		}

		// Token: 0x060013D6 RID: 5078 RVA: 0x0008FCEF File Offset: 0x0008DEEF
		public override int GetMinimumTroopCountForHideoutMission(MobileParty party, bool isAssault)
		{
			return base.BaseModel.GetMinimumTroopCountForHideoutMission(party, isAssault);
		}

		// Token: 0x04000B20 RID: 2848
		private const float GetNavalSafeZoneRadiusForFortificationPort = 15f;

		// Token: 0x04000B21 RID: 2849
		private const float GetNavalSafeZoneRadiusForVillagePort = 7f;

		// Token: 0x04000B22 RID: 2850
		private Clan _deserterClan;
	}
}
