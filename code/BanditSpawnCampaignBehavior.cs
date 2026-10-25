using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x020003D2 RID: 978
	public class BanditSpawnCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x17000E1F RID: 3615
		// (get) Token: 0x06003AA4 RID: 15012 RVA: 0x000F2CC7 File Offset: 0x000F0EC7
		private float BanditSpawnRadiusAsDays
		{
			get
			{
				return 0.5f * Campaign.Current.EstimatedAverageBanditPartySpeed * (float)CampaignTime.HoursInDay;
			}
		}

		// Token: 0x17000E20 RID: 3616
		// (get) Token: 0x06003AA5 RID: 15013 RVA: 0x000F2CE0 File Offset: 0x000F0EE0
		private float _radiusAroundPlayerPartySquared
		{
			get
			{
				return MobileParty.MainParty.SeeingRange * MobileParty.MainParty.SeeingRange;
			}
		}

		// Token: 0x17000E21 RID: 3617
		// (get) Token: 0x06003AA6 RID: 15014 RVA: 0x000F2CF7 File Offset: 0x000F0EF7
		private float _numberOfMinimumBanditPartiesInAHideoutToInfestIt
		{
			get
			{
				return (float)Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt;
			}
		}

		// Token: 0x17000E22 RID: 3618
		// (get) Token: 0x06003AA7 RID: 15015 RVA: 0x000F2D0E File Offset: 0x000F0F0E
		private int _numberOfMaxBanditPartiesAroundEachHideout
		{
			get
			{
				return Campaign.Current.Models.BanditDensityModel.NumberOfMaximumBanditPartiesAroundEachHideout;
			}
		}

		// Token: 0x17000E23 RID: 3619
		// (get) Token: 0x06003AA8 RID: 15016 RVA: 0x000F2D24 File Offset: 0x000F0F24
		private int _numberOfMaxHideoutsAtEachBanditFaction
		{
			get
			{
				return Campaign.Current.Models.BanditDensityModel.NumberOfMaximumHideoutsAtEachBanditFaction;
			}
		}

		// Token: 0x17000E24 RID: 3620
		// (get) Token: 0x06003AA9 RID: 15017 RVA: 0x000F2D3A File Offset: 0x000F0F3A
		private int _numberOfInitialHideoutsAtEachBanditFaction
		{
			get
			{
				return Campaign.Current.Models.BanditDensityModel.NumberOfInitialHideoutsAtEachBanditFaction;
			}
		}

		// Token: 0x17000E25 RID: 3621
		// (get) Token: 0x06003AAA RID: 15018 RVA: 0x000F2D50 File Offset: 0x000F0F50
		private int _numberOfMaximumBanditPartiesInEachHideout
		{
			get
			{
				return Campaign.Current.Models.BanditDensityModel.NumberOfMaximumBanditPartiesInEachHideout;
			}
		}

		// Token: 0x17000E26 RID: 3622
		// (get) Token: 0x06003AAB RID: 15019 RVA: 0x000F2D66 File Offset: 0x000F0F66
		private int _numberOfMaxBanditCountPerClanHideout
		{
			get
			{
				return this._numberOfMaxBanditPartiesAroundEachHideout + this._numberOfMaximumBanditPartiesInEachHideout;
			}
		}

		// Token: 0x06003AAC RID: 15020 RVA: 0x000F2D78 File Offset: 0x000F0F78
		public override void RegisterEvents()
		{
			CampaignEvents.MobilePartyCreated.AddNonSerializedListener(this, new Action<MobileParty>(this.MobilePartyCreated));
			CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, new Action<MobileParty, PartyBase>(this.MobilePartyDestroyed));
			CampaignEvents.SettlementEntered.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(this.OnSettlementEntered));
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));
			CampaignEvents.HourlyTickClanEvent.AddNonSerializedListener(this, new Action<Clan>(this.HourlyTickClan));
			CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
			CampaignEvents.OnHomeHideoutChangedEvent.AddNonSerializedListener(this, new Action<BanditPartyComponent, Hideout>(this.OnHomeHideoutChanged));
			CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter, int>(this.OnNewGameCreatedPartialFollowUp));
		}

		// Token: 0x06003AAD RID: 15021 RVA: 0x000F2E40 File Offset: 0x000F1040
		private void MobilePartyDestroyed(MobileParty party, PartyBase destroyerParty)
		{
			if (party.IsBandit && party.ActualClan != null && (this.IsBanditFaction(party.ActualClan) || BanditSpawnCampaignBehavior.IsLooterFaction(party.ActualClan)))
			{
				int num = 0;
				this._banditCountsPerHideout.TryGetValue(party.HomeSettlement, out num);
				this._banditCountsPerHideout[party.HomeSettlement] = num - 1;
			}
		}

		// Token: 0x06003AAE RID: 15022 RVA: 0x000F2EA4 File Offset: 0x000F10A4
		private void MobilePartyCreated(MobileParty party)
		{
			if (party.IsBandit && party.ActualClan != null && (this.IsBanditFaction(party.ActualClan) || BanditSpawnCampaignBehavior.IsLooterFaction(party.ActualClan)))
			{
				int num = 0;
				this._banditCountsPerHideout.TryGetValue(party.HomeSettlement, out num);
				this._banditCountsPerHideout[party.HomeSettlement] = num + 1;
			}
		}

		// Token: 0x06003AAF RID: 15023 RVA: 0x000F2F06 File Offset: 0x000F1106
		private void OnGameLoaded(CampaignGameStarter starter)
		{
			this.CacheHideouts();
			this.CacheBanditCounts();
		}

		// Token: 0x06003AB0 RID: 15024 RVA: 0x000F2F14 File Offset: 0x000F1114
		public override void SyncData(IDataStore dataStore)
		{
		}

		// Token: 0x06003AB1 RID: 15025 RVA: 0x000F2F16 File Offset: 0x000F1116
		private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int i)
		{
			if (i == 10)
			{
				this.CacheHideouts();
				if (this._numberOfInitialHideoutsAtEachBanditFaction > 0)
				{
					this.InitializeInitialHideouts();
					return;
				}
			}
			else if (i == 11)
			{
				this.SpawnBanditsAroundHideoutAtNewGame();
				this.SpawnLootersAtNewGame();
				this.CacheBanditCounts();
			}
		}

		// Token: 0x06003AB2 RID: 15026 RVA: 0x000F2F4C File Offset: 0x000F114C
		private void CacheHideouts()
		{
			foreach (Hideout hideout in Hideout.All)
			{
				List<Hideout> list;
				if (!this._hideouts.TryGetValue(hideout.Settlement.Culture, out list))
				{
					this._hideouts[hideout.Settlement.Culture] = new List<Hideout>();
				}
				this._hideouts[hideout.Settlement.Culture].Add(hideout);
			}
		}

		// Token: 0x06003AB3 RID: 15027 RVA: 0x000F2FE8 File Offset: 0x000F11E8
		private void CacheBanditCounts()
		{
			this._banditCountsPerHideout = new Dictionary<Settlement, int>();
			foreach (MobileParty mobileParty in MobileParty.AllBanditParties)
			{
				if (this.IsBanditFaction(mobileParty.ActualClan) || BanditSpawnCampaignBehavior.IsLooterFaction(mobileParty.ActualClan))
				{
					int num = 0;
					this._banditCountsPerHideout.TryGetValue(mobileParty.HomeSettlement, out num);
					this._banditCountsPerHideout[mobileParty.HomeSettlement] = num + 1;
				}
			}
		}

		// Token: 0x06003AB4 RID: 15028 RVA: 0x000F3084 File Offset: 0x000F1284
		public void InitializeInitialHideouts()
		{
			foreach (Clan clan in Clan.BanditFactions)
			{
				if (this.IsBanditFaction(clan))
				{
					this.SpawnHideoutsAndBanditsPartiallyOnNewGame(clan);
				}
			}
		}

		// Token: 0x06003AB5 RID: 15029 RVA: 0x000F30DC File Offset: 0x000F12DC
		private void SpawnHideoutsAndBanditsPartiallyOnNewGame(Clan banditClan)
		{
			for (int i = 0; i < this._numberOfInitialHideoutsAtEachBanditFaction; i++)
			{
				this.FillANewHideoutWithBandits(banditClan);
			}
		}

		// Token: 0x06003AB6 RID: 15030 RVA: 0x000F3104 File Offset: 0x000F1304
		public void OnSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
		{
			this.CheckForSpawningBanditBoss(settlement, mobileParty);
			if (Campaign.Current.GameStarted && mobileParty != null && mobileParty.IsBandit && settlement.IsHideout)
			{
				if (!settlement.Hideout.IsSpotted && settlement.Hideout.IsInfested && mobileParty.IsVisible)
				{
					settlement.Hideout.IsSpotted = true;
					settlement.Party.UpdateVisibilityAndInspected(MobileParty.MainParty.Position, 0f);
					CampaignEventDispatcher.Instance.OnHideoutSpotted(MobileParty.MainParty.Party, settlement.Party);
				}
				int num = 0;
				foreach (ItemRosterElement itemRosterElement in mobileParty.ItemRoster)
				{
					int num2 = itemRosterElement.EquipmentElement.Item.IsFood ? MBRandom.RoundRandomized((float)mobileParty.MemberRoster.TotalManCount * ((3f + 6f * MBRandom.RandomFloat) / (float)itemRosterElement.EquipmentElement.Item.Value)) : 0;
					if (itemRosterElement.Amount > num2)
					{
						int num3 = itemRosterElement.Amount - num2;
						num += num3 * itemRosterElement.EquipmentElement.Item.Value;
					}
				}
				if (num > 0)
				{
					if (mobileParty.IsPartyTradeActive)
					{
						mobileParty.PartyTradeGold += (int)(0.25f * (float)num);
					}
					settlement.SettlementComponent.ChangeGold((int)(0.25f * (float)num));
				}
			}
		}

		// Token: 0x06003AB7 RID: 15031 RVA: 0x000F32A4 File Offset: 0x000F14A4
		private void CheckForSpawningBanditBoss(Settlement settlement, MobileParty mobileParty)
		{
			if (settlement.IsHideout && settlement.Hideout.IsSpotted)
			{
				if (settlement.Parties.Any((MobileParty x) => x.IsBandit || x.IsBanditBossParty))
				{
					CultureObject culture = settlement.Culture;
					MobileParty mobileParty2 = settlement.Parties.FirstOrDefault((MobileParty x) => x.IsBanditBossParty);
					if (mobileParty2 == null)
					{
						this.AddBossParty(settlement, culture);
						return;
					}
					if (!mobileParty2.MemberRoster.Contains(culture.BanditBoss))
					{
						mobileParty2.MemberRoster.AddToCounts(culture.BanditBoss, 1, false, 0, 0, true, -1);
					}
				}
			}
		}

		// Token: 0x06003AB8 RID: 15032 RVA: 0x000F3364 File Offset: 0x000F1564
		private void AddBossParty(Settlement settlement, CultureObject culture)
		{
			PartyTemplateObject banditBossPartyTemplate = culture.BanditBossPartyTemplate;
			if (banditBossPartyTemplate != null)
			{
				this.AddBanditToHideout(settlement.Hideout, banditBossPartyTemplate, true).Ai.DisableAi();
			}
		}

		// Token: 0x06003AB9 RID: 15033 RVA: 0x000F3394 File Offset: 0x000F1594
		public void DailyTick()
		{
			if (this._numberOfMaxHideoutsAtEachBanditFaction > 0)
			{
				this.AddNewHideouts();
			}
			foreach (MobileParty mobileParty in MobileParty.AllBanditParties)
			{
				if (mobileParty.IsPartyTradeActive)
				{
					mobileParty.PartyTradeGold = (int)((double)mobileParty.PartyTradeGold * 0.95 + (double)(50f * (float)mobileParty.Party.MemberRoster.TotalManCount * 0.05f));
					if (MBRandom.RandomFloat < 0.03f && mobileParty.MapEvent != null)
					{
						foreach (ItemObject itemObject in Items.All)
						{
							if (itemObject.IsFood)
							{
								int num = BanditSpawnCampaignBehavior.IsLooterFaction(mobileParty.MapFaction) ? 8 : 16;
								int num2 = MBRandom.RoundRandomized((float)mobileParty.MemberRoster.TotalManCount * (1f / (float)itemObject.Value) * (float)num * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat);
								if (num2 > 0)
								{
									mobileParty.ItemRoster.AddToCounts(itemObject, num2);
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06003ABA RID: 15034 RVA: 0x000F3510 File Offset: 0x000F1710
		private void HourlyTickClan(Clan clan)
		{
			if (Campaign.Current.IsNight && clan.IsBanditFaction)
			{
				if (BanditSpawnCampaignBehavior.IsLooterFaction(clan))
				{
					this.SpawnLooters(clan, 0.07f, false);
					return;
				}
				if (this.IsBanditFaction(clan))
				{
					this.SpawnBanditsAroundHideout(clan, 0.1f);
				}
			}
		}

		// Token: 0x06003ABB RID: 15035 RVA: 0x000F355C File Offset: 0x000F175C
		private void SpawnBanditsAroundHideout(Clan clan, float ratio)
		{
			int count = clan.WarPartyComponents.Count;
			int num = MBRandom.RoundRandomized((float)(this.GetInfestedHideoutCount(clan) * this._numberOfMaxBanditCountPerClanHideout - count) * ratio);
			for (int i = 0; i < num; i++)
			{
				this.SpawnBanditParty(clan);
			}
		}

		// Token: 0x06003ABC RID: 15036 RVA: 0x000F35A4 File Offset: 0x000F17A4
		private void SpawnLooters(Clan clan, float ratio, bool uniformDistribution)
		{
			int count = clan.WarPartyComponents.Count;
			int num = MBRandom.RoundRandomized((float)(this.GetCurrentLimitForLooters(clan) - count) * ratio);
			for (int i = 0; i < num; i++)
			{
				this.SpawnLooterParty(clan, uniformDistribution);
			}
		}

		// Token: 0x06003ABD RID: 15037 RVA: 0x000F35E4 File Offset: 0x000F17E4
		private void AddNewHideouts()
		{
			List<ValueTuple<ValueTuple<Clan, int>, float>> list = new List<ValueTuple<ValueTuple<Clan, int>, float>>();
			foreach (Clan clan in Clan.BanditFactions)
			{
				if (this.IsBanditFaction(clan))
				{
					int infestedHideoutCount = this.GetInfestedHideoutCount(clan);
					if (infestedHideoutCount < this._numberOfMaxHideoutsAtEachBanditFaction)
					{
						list.Add(new ValueTuple<ValueTuple<Clan, int>, float>(new ValueTuple<Clan, int>(clan, infestedHideoutCount), 1f - (float)infestedHideoutCount / (float)this._numberOfMaxHideoutsAtEachBanditFaction));
					}
				}
			}
			int num;
			ValueTuple<Clan, int> valueTuple = MBRandom.ChooseWeighted<ValueTuple<Clan, int>>(list, out num);
			Clan item = valueTuple.Item1;
			int item2 = valueTuple.Item2;
			if (item != null)
			{
				float num2 = ((float)item2 < (float)this._numberOfMaxHideoutsAtEachBanditFaction * 0.5f) ? (0.2f + (float)(this._numberOfMaxHideoutsAtEachBanditFaction - item2) * 0.1f) : (0.1f + 0.5f * MathF.Pow(1f - 0.25f * ((float)item2 - (float)this._numberOfMaxHideoutsAtEachBanditFaction * 0.5f), 3f));
				if (MBRandom.RandomFloat < num2)
				{
					this.FillANewHideoutWithBandits(item);
				}
			}
		}

		// Token: 0x06003ABE RID: 15038 RVA: 0x000F36FC File Offset: 0x000F18FC
		private void FillANewHideoutWithBandits(Clan faction)
		{
			Hideout hideout = this.SelectANonInfestedHideoutOfSameCultureByWeight(faction);
			if (hideout != null)
			{
				int num = 0;
				while ((float)num < this._numberOfMinimumBanditPartiesInAHideoutToInfestIt)
				{
					this.AddBanditToHideout(hideout, null, false);
					num++;
				}
			}
		}

		// Token: 0x06003ABF RID: 15039 RVA: 0x000F3730 File Offset: 0x000F1930
		public MobileParty AddBanditToHideout(Hideout hideoutComponent, PartyTemplateObject overridenPartyTemplate = null, bool isBanditBossParty = false)
		{
			if (hideoutComponent.Owner.Settlement.Culture.IsBandit)
			{
				Clan clan = null;
				foreach (Clan clan2 in Clan.BanditFactions)
				{
					if (hideoutComponent.Owner.Settlement.Culture == clan2.Culture && (this.IsBanditFaction(clan2) || BanditSpawnCampaignBehavior.IsLooterFaction(clan2)))
					{
						clan = clan2;
					}
				}
				PartyTemplateObject pt = overridenPartyTemplate ?? clan.DefaultPartyTemplate;
				MobileParty mobileParty = BanditPartyComponent.CreateBanditParty(clan.StringId + "_1", clan, hideoutComponent, isBanditBossParty, pt, hideoutComponent.Owner.Settlement.GatePosition);
				this.InitializeBanditParty(mobileParty, clan);
				mobileParty.SetMoveGoToSettlement(hideoutComponent.Owner.Settlement, mobileParty.NavigationCapability, false);
				mobileParty.RecalculateShortTermBehavior();
				EnterSettlementAction.ApplyForParty(mobileParty, hideoutComponent.Owner.Settlement);
				return mobileParty;
			}
			return null;
		}

		// Token: 0x06003AC0 RID: 15040 RVA: 0x000F3834 File Offset: 0x000F1A34
		private Hideout SelectBanditHideout(Clan faction)
		{
			MBList<ValueTuple<Hideout, float>> mblist = new MBList<ValueTuple<Hideout, float>>();
			foreach (Hideout hideout in Hideout.All)
			{
				if (hideout.Settlement.Culture == faction.Culture && hideout.IsInfested)
				{
					mblist.Add(new ValueTuple<Hideout, float>(hideout, this.GetSpawnChanceInSettlement(hideout.Settlement)));
				}
			}
			if (mblist.Count != 0)
			{
				return MBRandom.ChooseWeighted<Hideout>(mblist);
			}
			return this.SelectAHideoutByCheckingCultureAndInfestedState(faction);
		}

		// Token: 0x06003AC1 RID: 15041 RVA: 0x000F38D0 File Offset: 0x000F1AD0
		private float GetSpawnChanceInSettlement(Settlement settlement)
		{
			if (this._banditCountsPerHideout.ContainsKey(settlement) && this._banditCountsPerHideout[settlement] != 0)
			{
				return 1f / MathF.Pow((float)this._banditCountsPerHideout[settlement], 2f);
			}
			return 1f;
		}

		// Token: 0x06003AC2 RID: 15042 RVA: 0x000F391C File Offset: 0x000F1B1C
		private void OnHomeHideoutChanged(BanditPartyComponent banditPartyComponent, Hideout oldHomeHideout)
		{
			int num = 0;
			this._banditCountsPerHideout.TryGetValue(oldHomeHideout.Settlement, out num);
			this._banditCountsPerHideout[oldHomeHideout.Settlement] = num - 1;
			num = 0;
			this._banditCountsPerHideout.TryGetValue(banditPartyComponent.HomeSettlement, out num);
			this._banditCountsPerHideout[banditPartyComponent.HomeSettlement] = num + 1;
		}

		// Token: 0x06003AC3 RID: 15043 RVA: 0x000F3980 File Offset: 0x000F1B80
		private Hideout SelectAHideoutByCheckingCultureAndInfestedState(Clan faction)
		{
			List<Hideout> list = new List<Hideout>();
			bool flag = false;
			bool flag2 = false;
			foreach (Hideout hideout in Hideout.All)
			{
				bool flag3 = hideout.Settlement.Culture == faction.Culture;
				bool isInfested = hideout.IsInfested;
				if (!flag2 && flag3)
				{
					flag2 = true;
					list.Clear();
				}
				if (flag2 && !flag && isInfested)
				{
					flag = true;
					list.Clear();
				}
				if ((!flag2 || flag3) && (!flag || isInfested))
				{
					list.Add(hideout);
				}
			}
			return list.GetRandomElement<Hideout>();
		}

		// Token: 0x06003AC4 RID: 15044 RVA: 0x000F3A40 File Offset: 0x000F1C40
		private Hideout SelectANonInfestedHideoutOfSameCultureByWeight(Clan faction)
		{
			float averageDistanceBetweenClosestTwoTownsWithNavigationType = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.Default);
			float num = averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.33f * averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.33f;
			List<ValueTuple<Hideout, float>> list = new List<ValueTuple<Hideout, float>>();
			foreach (Hideout hideout in Hideout.All)
			{
				if (!hideout.IsInfested && hideout.Settlement.Culture == faction.Culture)
				{
					int num2 = 1;
					if (hideout.Settlement.LastThreatTime.ElapsedDaysUntilNow > 1.5f)
					{
						float num3 = Campaign.MapDiagonalSquared;
						float num4 = Campaign.MapDiagonalSquared;
						foreach (Hideout hideout2 in Hideout.All)
						{
							if (hideout != hideout2 && hideout2.IsInfested)
							{
								float num5 = hideout.Settlement.Position.DistanceSquared(hideout2.Settlement.Position);
								if (hideout.Settlement.Culture == hideout2.Settlement.Culture && num5 < num3)
								{
									num3 = num5;
								}
								if (num5 < num4)
								{
									num4 = num5;
								}
							}
							num2 = (int)MathF.Max(averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.015f, num3 / num + averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.076f * (num4 / num));
						}
					}
					list.Add(new ValueTuple<Hideout, float>(hideout, (float)num2));
				}
			}
			return MBRandom.ChooseWeighted<Hideout>(list);
		}

		// Token: 0x06003AC5 RID: 15045 RVA: 0x000F3BF8 File Offset: 0x000F1DF8
		public void SpawnBanditsAroundHideoutAtNewGame()
		{
			foreach (Clan clan in Clan.BanditFactions)
			{
				if (this.IsBanditFaction(clan))
				{
					this.SpawnBanditsAroundHideout(clan, MBRandom.RandomFloatRanged(0.5f, 0.75f));
				}
			}
		}

		// Token: 0x06003AC6 RID: 15046 RVA: 0x000F3C5C File Offset: 0x000F1E5C
		public void SpawnLootersAtNewGame()
		{
			foreach (Clan clan in Clan.BanditFactions)
			{
				if (BanditSpawnCampaignBehavior.IsLooterFaction(clan))
				{
					this.SpawnLooters(clan, MBRandom.RandomFloatRanged(0.5f, 0.75f), true);
				}
			}
		}

		// Token: 0x06003AC7 RID: 15047 RVA: 0x000F3CC0 File Offset: 0x000F1EC0
		private void SpawnLooterParty(Clan selectedFaction, bool uniformDistribution)
		{
			Settlement settlement = this.SelectARandomSettlementForLooterParty(uniformDistribution);
			CampaignVec2 spawnPositionAroundSettlement = this.GetSpawnPositionAroundSettlement(selectedFaction, settlement);
			MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(selectedFaction.StringId + "_1", selectedFaction, settlement, false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
			this.InitializeBanditParty(mobileParty, selectedFaction);
			mobileParty.SetMovePatrolAroundPoint(mobileParty.Position, MobileParty.NavigationType.Default);
		}

		// Token: 0x06003AC8 RID: 15048 RVA: 0x000F3D14 File Offset: 0x000F1F14
		private void SpawnBanditParty(Clan selectedFaction)
		{
			Hideout hideout = this.SelectBanditHideout(selectedFaction);
			CampaignVec2 spawnPositionAroundSettlement = this.GetSpawnPositionAroundSettlement(selectedFaction, hideout.Settlement);
			MobileParty mobileParty = BanditPartyComponent.CreateBanditParty(selectedFaction.StringId + "_1", selectedFaction, hideout, false, selectedFaction.DefaultPartyTemplate, spawnPositionAroundSettlement);
			this.InitializeBanditParty(mobileParty, selectedFaction);
			mobileParty.SetMovePatrolAroundPoint(mobileParty.Position, mobileParty.NavigationCapability);
		}

		// Token: 0x06003AC9 RID: 15049 RVA: 0x000F3D71 File Offset: 0x000F1F71
		private static bool IsLooterFaction(IFaction faction)
		{
			return !faction.Culture.CanHaveSettlement && !faction.HasNavalNavigationCapability && faction.StringId != "deserters";
		}

		// Token: 0x06003ACA RID: 15050 RVA: 0x000F3D9A File Offset: 0x000F1F9A
		private float GetSpawnRadiusForClan(Clan selectedFaction)
		{
			return this.BanditSpawnRadiusAsDays * (BanditSpawnCampaignBehavior.IsLooterFaction(selectedFaction) ? 1.5f : 1f);
		}

		// Token: 0x06003ACB RID: 15051 RVA: 0x000F3DB8 File Offset: 0x000F1FB8
		private int GetInfestedHideoutCount(Clan banditFaction)
		{
			int num = 0;
			foreach (Hideout hideout in this._hideouts[banditFaction.Culture])
			{
				if (hideout.IsInfested && hideout.MapFaction == banditFaction)
				{
					num++;
				}
			}
			return num;
		}

		// Token: 0x06003ACC RID: 15052 RVA: 0x000F3E28 File Offset: 0x000F2028
		private int GetCurrentLimitForLooters(Clan clan)
		{
			return Math.Min(Hideout.All.Count((Hideout x) => x.IsInfested) * 7, Campaign.Current.Models.BanditDensityModel.GetMaxSupportedNumberOfLootersForClan(clan));
		}

		// Token: 0x06003ACD RID: 15053 RVA: 0x000F3E7C File Offset: 0x000F207C
		private Settlement SelectARandomSettlementForLooterParty(bool uniformDistribution)
		{
			MBList<ValueTuple<Settlement, float>> mblist = new MBList<ValueTuple<Settlement, float>>();
			foreach (Settlement settlement in Settlement.All)
			{
				if (settlement.IsTown || settlement.IsVillage)
				{
					mblist.Add(new ValueTuple<Settlement, float>(settlement, this.GetSpawnChanceInSettlement(settlement)));
				}
			}
			return MBRandom.ChooseWeighted<Settlement>(mblist);
		}

		// Token: 0x06003ACE RID: 15054 RVA: 0x000F3EF8 File Offset: 0x000F20F8
		private void GiveFoodToBanditParty(MobileParty banditParty)
		{
			int num = BanditSpawnCampaignBehavior.IsLooterFaction(banditParty.MapFaction) ? 8 : 16;
			foreach (ItemObject itemObject in Items.All)
			{
				if (itemObject.IsFood)
				{
					int num2 = MBRandom.RoundRandomized((float)banditParty.MemberRoster.TotalManCount * (1f / (float)itemObject.Value) * (float)num * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat);
					if (num2 > 0)
					{
						banditParty.ItemRoster.AddToCounts(itemObject, num2);
					}
				}
			}
		}

		// Token: 0x06003ACF RID: 15055 RVA: 0x000F3FAC File Offset: 0x000F21AC
		private CampaignVec2 GetSpawnPositionAroundSettlement(Clan clan, Settlement settlement)
		{
			CampaignVec2 campaignVec = NavigationHelper.FindPointAroundPosition(settlement.GatePosition, MobileParty.NavigationType.Default, this.GetSpawnRadiusForClan(clan), 0f, true, false);
			if (campaignVec.DistanceSquared(MobileParty.MainParty.Position) < this._radiusAroundPlayerPartySquared)
			{
				for (int i = 0; i < 15; i++)
				{
					CampaignVec2 campaignVec2 = NavigationHelper.FindReachablePointAroundPosition(campaignVec, MobileParty.NavigationType.Default, this.GetSpawnRadiusForClan(clan), 0f, false);
					if (NavigationHelper.IsPositionValidForNavigationType(campaignVec2, MobileParty.NavigationType.Default))
					{
						float num2;
						float num = DistanceHelper.FindClosestDistanceFromMobilePartyToPoint(MobileParty.MainParty, campaignVec2, MobileParty.NavigationType.Default, out num2);
						if (num * num > this._radiusAroundPlayerPartySquared)
						{
							campaignVec = campaignVec2;
							break;
						}
					}
				}
			}
			return campaignVec;
		}

		// Token: 0x06003AD0 RID: 15056 RVA: 0x000F4035 File Offset: 0x000F2235
		private bool IsBanditFaction(Clan clan)
		{
			return !clan.HasNavalNavigationCapability && clan.IsBanditFaction && clan.Culture.CanHaveSettlement;
		}

		// Token: 0x06003AD1 RID: 15057 RVA: 0x000F4054 File Offset: 0x000F2254
		private void InitializeBanditParty(MobileParty banditParty, Clan faction)
		{
			banditParty.Party.SetVisualAsDirty();
			banditParty.ActualClan = faction;
			banditParty.Aggressiveness = 1f - 0.2f * MBRandom.RandomFloat;
			BanditSpawnCampaignBehavior.CreatePartyTrade(banditParty);
			this.GiveFoodToBanditParty(banditParty);
		}

		// Token: 0x06003AD2 RID: 15058 RVA: 0x000F408C File Offset: 0x000F228C
		private static void CreatePartyTrade(MobileParty banditParty)
		{
			int initialGold = (int)(10f * (float)banditParty.Party.MemberRoster.TotalManCount * (0.5f + 1f * MBRandom.RandomFloat));
			banditParty.InitializePartyTrade(initialGold);
		}

		// Token: 0x04001240 RID: 4672
		private const float BanditStartGoldPerBandit = 10f;

		// Token: 0x04001241 RID: 4673
		private const float BanditLongTermGoldPerBandit = 50f;

		// Token: 0x04001242 RID: 4674
		private const float HideoutInfestCooldownAfterFightInDays = 1.5f;

		// Token: 0x04001243 RID: 4675
		private Dictionary<CultureObject, List<Hideout>> _hideouts = new Dictionary<CultureObject, List<Hideout>>();

		// Token: 0x04001244 RID: 4676
		private Dictionary<Settlement, int> _banditCountsPerHideout = new Dictionary<Settlement, int>();
	}
}
