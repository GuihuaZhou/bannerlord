using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using NavalDLC.GameComponents;
using StoryMode;
using StoryMode.StoryModePhases;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.SaveSystem;

namespace NavalDLC.CampaignBehaviors
{
	// Token: 0x02000179 RID: 377
	public class PiratesCampaignBehavior : CampaignBehaviorBase, IPiratePatrolBehavior
	{
		// Token: 0x060018E3 RID: 6371 RVA: 0x000AD57C File Offset: 0x000AB77C
		public override void RegisterEvents()
		{
			CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnNewGameCreated));
			CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnGameLoaded));
			CampaignEvents.OnNewGameCreatedPartialFollowUpEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter, int>(this.OnNewGameCreatedPartialFollowUp));
			CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, new Action<Clan>(this.DailyTickClan));
			CampaignEvents.DailyTickPartyEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.DailyTickParty));
			CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, new Action<MobileParty, PartyBase>(this.OnMobilePartyDestroyed));
			CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, new Action<MobileParty, PartyThinkParams>(this.AiHourlyTick));
			CampaignEvents.MapEventEnded.AddNonSerializedListener(this, new Action<MapEvent>(this.MapEventEnded));
			TutorialPhase instance = TutorialPhase.Instance;
			if (instance != null && !instance.IsCompleted)
			{
				StoryModeEvents.OnStoryModeTutorialEndedEvent.AddNonSerializedListener(this, new Action(this.OnTutorialEnded));
			}
		}

		// Token: 0x060018E4 RID: 6372 RVA: 0x000AD670 File Offset: 0x000AB870
		private void MapEventEnded(MapEvent mapEvent)
		{
			foreach (PartyBase partyBase in mapEvent.InvolvedParties)
			{
				if (partyBase.IsMobile && mapEvent.WinningSide == partyBase.Side && this.IsPirateParty(partyBase.MobileParty) && partyBase.MobileParty.Ships.Count > Campaign.Current.Models.PartyShipLimitModel.GetIdealShipNumber(partyBase.MobileParty))
				{
					this.DiscardShips(partyBase.MobileParty);
				}
			}
		}

		// Token: 0x060018E5 RID: 6373 RVA: 0x000AD714 File Offset: 0x000AB914
		private void DiscardShips(MobileParty pirateParty)
		{
			MBList<Ship> mblist = (from x in pirateParty.Ships
			orderby Campaign.Current.Models.PartyShipLimitModel.GetShipPriority(pirateParty, x, false) descending
			select x).ToMBList<Ship>();
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < mblist.Count; i++)
			{
				Ship ship = mblist[i];
				ShipHull.ShipType type = ship.ShipHull.Type;
				if (num2 < 2 && (type != ShipHull.ShipType.Medium || num2 == 0 || num < 2) && num < Campaign.Current.Models.PartyShipLimitModel.GetIdealShipNumber(pirateParty))
				{
					num++;
					if (type == ShipHull.ShipType.Medium)
					{
						num2++;
					}
				}
				else
				{
					DestroyShipAction.ApplyByDiscard(ship);
				}
			}
		}

		// Token: 0x060018E6 RID: 6374 RVA: 0x000AD7CC File Offset: 0x000AB9CC
		private void OnTutorialEnded()
		{
			if (TutorialPhase.Instance.IsSkipped)
			{
				for (int i = 0; i < 3; i++)
				{
					foreach (Clan clan in Clan.BanditFactions)
					{
						this.DailyTickClan(clan);
					}
				}
			}
			StoryModeEvents.OnStoryModeTutorialEndedEvent.ClearListeners(this);
		}

		// Token: 0x060018E7 RID: 6375 RVA: 0x000AD83C File Offset: 0x000ABA3C
		private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyerParty)
		{
			PiratesCampaignBehavior.PatrolZone assignedZone;
			if (this._assignedZones.TryGetValue(party, out assignedZone))
			{
				this.UnassignPartyFromZone(assignedZone, party);
			}
		}

		// Token: 0x060018E8 RID: 6376 RVA: 0x000AD864 File Offset: 0x000ABA64
		private void AiHourlyTick(MobileParty mobileParty, PartyThinkParams p)
		{
			if (this.IsPirateParty(mobileParty))
			{
				PiratesCampaignBehavior.PatrolZone assignedZone = this.GetAssignedZone(mobileParty);
				if (assignedZone != null)
				{
					MobileParty.NavigationType navigationType = MobileParty.NavigationType.Naval;
					AIBehaviorData item = new AIBehaviorData(assignedZone.Position, AiBehavior.PatrolAroundPoint, navigationType, false, false, false);
					ValueTuple<AIBehaviorData, float> valueTuple = new ValueTuple<AIBehaviorData, float>(item, 5f);
					p.AddBehaviorScore(valueTuple);
					return;
				}
				Debug.FailedAssert("This should only be possible for cheats & mods.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\NavalDLC\\CampaignBehaviors\\PiratesCampaignBehavior.cs", "AiHourlyTick", 248);
			}
		}

		// Token: 0x060018E9 RID: 6377 RVA: 0x000AD8C8 File Offset: 0x000ABAC8
		private void OnNewGameCreated(CampaignGameStarter starter)
		{
			this.AssignPatrolZones();
		}

		// Token: 0x060018EA RID: 6378 RVA: 0x000AD8D0 File Offset: 0x000ABAD0
		private void AssignPatrolZones()
		{
			foreach (Clan clan in Clan.BanditFactions)
			{
				if (this.IsPirateClan(clan))
				{
					this.AssignPatrolZones(clan);
				}
			}
		}

		// Token: 0x060018EB RID: 6379 RVA: 0x000AD928 File Offset: 0x000ABB28
		private void OnGameLoaded(CampaignGameStarter starter)
		{
			this.AssignPatrolZones();
			this.AdjustAssignedPatrolZones();
		}

		// Token: 0x060018EC RID: 6380 RVA: 0x000AD938 File Offset: 0x000ABB38
		private void OnNewGameCreatedPartialFollowUp(CampaignGameStarter starter, int index)
		{
			if (index % 33 == 0)
			{
				foreach (Clan clan in Clan.BanditFactions)
				{
					this.DailyTickClan(clan);
				}
			}
		}

		// Token: 0x060018ED RID: 6381 RVA: 0x000AD98C File Offset: 0x000ABB8C
		private void DailyTickClan(Clan clan)
		{
			if (this.IsPirateClan(clan))
			{
				this.TrySpawnPirateParties(clan);
			}
		}

		// Token: 0x060018EE RID: 6382 RVA: 0x000AD99E File Offset: 0x000ABB9E
		private void DailyTickParty(MobileParty mobileParty)
		{
			if (this.IsPirateParty(mobileParty))
			{
				this.TryRemoveWeakPirate(mobileParty);
			}
		}

		// Token: 0x060018EF RID: 6383 RVA: 0x000AD9B0 File Offset: 0x000ABBB0
		private void TryRemoveWeakPirate(MobileParty pirateParty)
		{
			if (pirateParty.HasNavalNavigationCapability && pirateParty.MapEvent == null)
			{
				bool flag;
				if ((float)pirateParty.MemberRoster.TotalHealthyCount >= (float)pirateParty.ActualClan.DefaultPartyTemplate.GetLowerTroopLimit() * 0.7f)
				{
					flag = ((float)pirateParty.Ships.Count < (float)pirateParty.ActualClan.DefaultPartyTemplate.ShipHulls.SumQ((ShipTemplateStack t) => t.MinValue) * 0.7f);
				}
				else
				{
					flag = true;
				}
				float num = MobileParty.MainParty.SeeingRange * 2f;
				if (flag && pirateParty.Position.DistanceSquared(MobileParty.MainParty.Position) >= num * num)
				{
					DestroyPartyAction.ApplyForDisbanding(pirateParty, Settlement.FindFirst((Settlement t) => t.IsHideout));
				}
			}
		}

		// Token: 0x060018F0 RID: 6384 RVA: 0x000ADA9E File Offset: 0x000ABC9E
		public override void SyncData(IDataStore dataStore)
		{
			dataStore.SyncData<Dictionary<MobileParty, PiratesCampaignBehavior.PatrolZone>>("_assignedZones", ref this._assignedZones);
		}

		// Token: 0x060018F1 RID: 6385 RVA: 0x000ADAB4 File Offset: 0x000ABCB4
		private void AssignPatrolZones(Clan clan)
		{
			this._patrolZones[clan] = new List<PiratesCampaignBehavior.PatrolZone>();
			foreach (ValueTuple<CampaignVec2, float> valueTuple in NavalDLCManager.Instance.NavalMapSceneWrapper.GetSpawnPoints(clan.StringId))
			{
				PiratesCampaignBehavior.PatrolZone item;
				if (!this.FindIdenticalZone(valueTuple.Item1, valueTuple.Item2, out item))
				{
					item = new PiratesCampaignBehavior.PatrolZone(valueTuple.Item1, valueTuple.Item2);
				}
				this._patrolZones[clan].Add(item);
			}
		}

		// Token: 0x060018F2 RID: 6386 RVA: 0x000ADB5C File Offset: 0x000ABD5C
		private bool FindIdenticalZone(CampaignVec2 position, float radius, out PiratesCampaignBehavior.PatrolZone zone)
		{
			zone = null;
			foreach (KeyValuePair<MobileParty, PiratesCampaignBehavior.PatrolZone> keyValuePair in this._assignedZones)
			{
				if (keyValuePair.Value.Position.Distance(position).ApproximatelyEqualsTo(0f, 1E-05f) && keyValuePair.Value.Radius.ApproximatelyEqualsTo(radius, 1E-05f))
				{
					zone = keyValuePair.Value;
					return true;
				}
			}
			return false;
		}

		// Token: 0x060018F3 RID: 6387 RVA: 0x000ADBFC File Offset: 0x000ABDFC
		private PiratesCampaignBehavior.PatrolZone GetClosestPatrolZone(MobileParty mobileParty)
		{
			List<PiratesCampaignBehavior.PatrolZone> list = this._patrolZones[mobileParty.ActualClan];
			PiratesCampaignBehavior.PatrolZone result = null;
			float num = float.MaxValue;
			foreach (PiratesCampaignBehavior.PatrolZone patrolZone in list)
			{
				float num2 = mobileParty.TargetPosition.Distance(patrolZone.Position);
				if (num2 < num && this.CanSpawnPiratePartyInZone(mobileParty.ActualClan, patrolZone))
				{
					num = num2;
					result = patrolZone;
				}
			}
			return result;
		}

		// Token: 0x060018F4 RID: 6388 RVA: 0x000ADC8C File Offset: 0x000ABE8C
		private void AdjustAssignedPatrolZones()
		{
			foreach (MobileParty mobileParty in MobileParty.AllBanditParties)
			{
				if (this.IsPirateParty(mobileParty) && this.GetAssignedZone(mobileParty) == null)
				{
					PiratesCampaignBehavior.PatrolZone patrolZone = this.GetClosestPatrolZone(mobileParty);
					if (patrolZone == null)
					{
						Debug.FailedAssert("zone != null", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\NavalDLC\\CampaignBehaviors\\PiratesCampaignBehavior.cs", "AdjustAssignedPatrolZones", 391);
						List<PiratesCampaignBehavior.PatrolZone> patrolZones = this.GetPatrolZones(mobileParty.ActualClan);
						if (patrolZones.Count == 0)
						{
							patrolZone = new PiratesCampaignBehavior.PatrolZone(mobileParty.TargetPosition, 20f);
						}
						else
						{
							patrolZone = patrolZones.GetRandomElement<PiratesCampaignBehavior.PatrolZone>();
						}
					}
					this.AssignPartyToZone(patrolZone, mobileParty);
					if (mobileParty.MapEvent == null)
					{
						mobileParty.SetMovePatrolAroundPoint(patrolZone.Position, MobileParty.NavigationType.Naval);
					}
				}
			}
			foreach (KeyValuePair<MobileParty, PiratesCampaignBehavior.PatrolZone> keyValuePair in this._assignedZones)
			{
				PiratesCampaignBehavior.PatrolZone value = keyValuePair.Value;
				int density = value.Density;
				value.Density = density + 1;
			}
		}

		// Token: 0x060018F5 RID: 6389 RVA: 0x000ADDB4 File Offset: 0x000ABFB4
		private bool IsPirateParty(MobileParty mobileParty)
		{
			return mobileParty.ActualClan != null && this.IsPirateClan(mobileParty.ActualClan) && !mobileParty.IsCurrentlyUsedByAQuest && mobileParty.HasNavalNavigationCapability && mobileParty.IsCurrentlyAtSea;
		}

		// Token: 0x060018F6 RID: 6390 RVA: 0x000ADDE4 File Offset: 0x000ABFE4
		private List<PiratesCampaignBehavior.PatrolZone> GetPatrolZones(Clan clan)
		{
			return this._patrolZones[clan];
		}

		// Token: 0x060018F7 RID: 6391 RVA: 0x000ADDF4 File Offset: 0x000ABFF4
		private List<PiratesCampaignBehavior.PatrolZone> GetAllPatrolZones()
		{
			List<PiratesCampaignBehavior.PatrolZone> list = new List<PiratesCampaignBehavior.PatrolZone>();
			foreach (KeyValuePair<Clan, List<PiratesCampaignBehavior.PatrolZone>> keyValuePair in this._patrolZones)
			{
				list.AddRange(keyValuePair.Value);
			}
			return list;
		}

		// Token: 0x060018F8 RID: 6392 RVA: 0x000ADE54 File Offset: 0x000AC054
		private PiratesCampaignBehavior.PatrolZone GetAssignedZone(MobileParty party)
		{
			PiratesCampaignBehavior.PatrolZone result;
			if (this._assignedZones.TryGetValue(party, out result))
			{
				return result;
			}
			return null;
		}

		// Token: 0x060018F9 RID: 6393 RVA: 0x000ADE74 File Offset: 0x000AC074
		private void AssignPartyToZone(PiratesCampaignBehavior.PatrolZone zone, MobileParty mobileParty)
		{
			PiratesCampaignBehavior.PatrolZone assignedZone;
			if (this._assignedZones.TryGetValue(mobileParty, out assignedZone))
			{
				this.UnassignPartyFromZone(assignedZone, mobileParty);
			}
			int density = zone.Density;
			zone.Density = density + 1;
			this._assignedZones[mobileParty] = zone;
		}

		// Token: 0x060018FA RID: 6394 RVA: 0x000ADEB8 File Offset: 0x000AC0B8
		private void UnassignPartyFromZone(PiratesCampaignBehavior.PatrolZone assignedZone, MobileParty mobileParty)
		{
			int density = assignedZone.Density;
			assignedZone.Density = density - 1;
			this._assignedZones.Remove(mobileParty);
		}

		// Token: 0x060018FB RID: 6395 RVA: 0x000ADEE4 File Offset: 0x000AC0E4
		private void TrySpawnPirateParties(Clan clan)
		{
			int num;
			int num2;
			this.GetPirateData(clan, out num, out num2);
			int num3 = MathF.Floor(MathF.Pow((float)(num2 - num), 0.66f));
			if (num3 > 0)
			{
				int num4 = 0;
				List<PiratesCampaignBehavior.PatrolZone> patrolZones = this.GetPatrolZones(clan);
				patrolZones.Shuffle<PiratesCampaignBehavior.PatrolZone>();
				int num5 = 0;
				int num6 = 0;
				for (int i = 0; i < num3; i++)
				{
					PiratesCampaignBehavior.PatrolZone randomSuitableZone = this.GetRandomSuitableZone(clan, patrolZones, ref num5, ref num6);
					if (randomSuitableZone == null)
					{
						break;
					}
					this.SpawnPirateParty(clan, randomSuitableZone);
					num4++;
				}
			}
		}

		// Token: 0x060018FC RID: 6396 RVA: 0x000ADF5C File Offset: 0x000AC15C
		private void GetPirateData(Clan clan, out int pirateMemberCount, out int maximumPirateCount)
		{
			pirateMemberCount = clan.WarPartyComponents.Count;
			maximumPirateCount = Campaign.Current.Models.BanditDensityModel.GetMaxSupportedNumberOfLootersForClan(clan);
		}

		// Token: 0x060018FD RID: 6397 RVA: 0x000ADF84 File Offset: 0x000AC184
		private void SpawnPirateParty(Clan clan, PiratesCampaignBehavior.PatrolZone patrolZone)
		{
			Settlement relatedSettlement = SettlementHelper.FindNearestSettlementToPoint(patrolZone.Position, (Settlement x) => x.IsTown && x.HasPort);
			CampaignVec2 spawnPosition = this.GetSpawnPosition(patrolZone);
			MobileParty mobileParty = BanditPartyComponent.CreateLooterParty(clan.StringId + "_1", clan, relatedSettlement, false, clan.DefaultPartyTemplate, spawnPosition);
			this.InitializePirateParty(mobileParty, clan);
			this.AssignPartyToZone(patrolZone, mobileParty);
			mobileParty.SetMovePatrolAroundPoint(patrolZone.Position, MobileParty.NavigationType.Naval);
		}

		// Token: 0x060018FE RID: 6398 RVA: 0x000AE004 File Offset: 0x000AC204
		private CampaignVec2 GetSpawnPosition(PiratesCampaignBehavior.PatrolZone zone)
		{
			if (Campaign.Current.GameStarted && this.IsPointVisibleToPlayer(zone))
			{
				float num = float.MaxValue;
				PiratesCampaignBehavior.PatrolZone patrolZone = null;
				foreach (PiratesCampaignBehavior.PatrolZone patrolZone2 in this.GetAllPatrolZones())
				{
					float zoneDistance = this.GetZoneDistance(zone, patrolZone2);
					if (!this.IsPointVisibleToPlayer(patrolZone2) && (zoneDistance < num || patrolZone == null))
					{
						patrolZone = patrolZone2;
						num = zoneDistance;
					}
				}
				if (patrolZone != null)
				{
					zone = patrolZone;
				}
			}
			int num2 = 0;
			CampaignVec2 campaignVec = NavigationHelper.FindPointAroundPosition(zone.Position, MobileParty.NavigationType.Naval, zone.Radius, 0f, true, false);
			do
			{
				campaignVec = NavigationHelper.FindPointAroundPosition(zone.Position, MobileParty.NavigationType.Naval, zone.Radius, 0f, true, false);
				num2++;
			}
			while (num2 < 100 && Campaign.Current.Models.BanditDensityModel.IsPositionInsideNavalSafeZone(campaignVec));
			return campaignVec;
		}

		// Token: 0x060018FF RID: 6399 RVA: 0x000AE0F0 File Offset: 0x000AC2F0
		private float GetZoneDistance(PiratesCampaignBehavior.PatrolZone p1, PiratesCampaignBehavior.PatrolZone p2)
		{
			int[] invalidTerrainTypesForNavigationType = Campaign.Current.Models.PartyNavigationModel.GetInvalidTerrainTypesForNavigationType(MobileParty.NavigationType.Naval);
			float result;
			Campaign.Current.MapSceneWrapper.GetPathDistanceBetweenAIFaces(p1.Position.Face, p2.Position.Face, p1.Position.ToVec2(), p2.Position.ToVec2(), 0.5f, (float)Campaign.PathFindingMaxCostLimit, out result, invalidTerrainTypesForNavigationType, Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromLandToSea, Campaign.Current.Models.MapDistanceModel.RegionSwitchCostFromSeaToLand);
			return result;
		}

		// Token: 0x06001900 RID: 6400 RVA: 0x000AE194 File Offset: 0x000AC394
		private PiratesCampaignBehavior.PatrolZone GetRandomSuitableZone(Clan clan, List<PiratesCampaignBehavior.PatrolZone> zones, ref int iter, ref int bestScore)
		{
			int num = iter + zones.Count;
			int num2 = -1;
			PiratesCampaignBehavior.PatrolZone result = null;
			while (iter < num)
			{
				PiratesCampaignBehavior.PatrolZone patrolZone = zones[iter % zones.Count];
				iter++;
				if (this.CanSpawnPiratePartyInZone(clan, patrolZone))
				{
					if (bestScore == patrolZone.Density)
					{
						return patrolZone;
					}
					if (num2 < patrolZone.Density)
					{
						num2 = patrolZone.Density;
						result = patrolZone;
					}
				}
			}
			iter = 0;
			bestScore = num2;
			return result;
		}

		// Token: 0x06001901 RID: 6401 RVA: 0x000AE200 File Offset: 0x000AC400
		private bool CanSpawnPiratePartyInZone(Clan clan, PiratesCampaignBehavior.PatrolZone zone)
		{
			List<PiratesCampaignBehavior.PatrolZone> list;
			return this._patrolZones.TryGetValue(clan, out list) && list.Contains(zone);
		}

		// Token: 0x06001902 RID: 6402 RVA: 0x000AE226 File Offset: 0x000AC426
		private bool IsPirateClan(Clan clan)
		{
			return !clan.Culture.CanHaveSettlement && clan.HasNavalNavigationCapability && clan.IsBanditFaction;
		}

		// Token: 0x06001903 RID: 6403 RVA: 0x000AE245 File Offset: 0x000AC445
		private void InitializePirateParty(MobileParty pirateParty, Clan faction)
		{
			pirateParty.Party.SetVisualAsDirty();
			pirateParty.ActualClan = faction;
			pirateParty.Aggressiveness = 1f - 0.2f * MBRandom.RandomFloat;
			PiratesCampaignBehavior.CreatePartyTrade(pirateParty);
			this.GiveFoodToBanditParty(pirateParty);
			pirateParty.SetLandNavigationAccess(false);
		}

		// Token: 0x06001904 RID: 6404 RVA: 0x000AE284 File Offset: 0x000AC484
		private bool IsPointVisibleToPlayer(PiratesCampaignBehavior.PatrolZone zone)
		{
			float seeingRange = MobileParty.MainParty.SeeingRange;
			return MobileParty.MainParty.Position.DistanceSquared(zone.Position) < (seeingRange + zone.Radius) * (seeingRange + zone.Radius);
		}

		// Token: 0x06001905 RID: 6405 RVA: 0x000AE2C8 File Offset: 0x000AC4C8
		private static void CreatePartyTrade(MobileParty banditParty)
		{
			int initialGold = (int)(10f * (float)banditParty.Party.MemberRoster.TotalManCount * (0.5f + 1f * MBRandom.RandomFloat));
			banditParty.InitializePartyTrade(initialGold);
		}

		// Token: 0x06001906 RID: 6406 RVA: 0x000AE308 File Offset: 0x000AC508
		private void GiveFoodToBanditParty(MobileParty banditParty)
		{
			foreach (ItemObject itemObject in Items.All)
			{
				if (itemObject.IsFood)
				{
					int num = MBRandom.RoundRandomized((float)banditParty.MemberRoster.TotalManCount * (1f / (float)itemObject.Value) * 8f * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat);
					if (num > 0)
					{
						banditParty.ItemRoster.AddToCounts(itemObject, num);
					}
				}
			}
		}

		// Token: 0x06001907 RID: 6407 RVA: 0x000AE3AC File Offset: 0x000AC5AC
		public float GetPatrolRadius(MobileParty mobileParty)
		{
			PiratesCampaignBehavior.PatrolZone patrolZone;
			if (this._assignedZones.TryGetValue(mobileParty, out patrolZone))
			{
				return patrolZone.Radius;
			}
			return 25f;
		}

		// Token: 0x04000C7C RID: 3196
		private const float PirateStartGoldPerBandit = 10f;

		// Token: 0x04000C7D RID: 3197
		private const float PatrollingScore = 5f;

		// Token: 0x04000C7E RID: 3198
		private const float DefaultPatrolRadius = 20f;

		// Token: 0x04000C7F RID: 3199
		private const float WeakPirateRemovalStrengthThreshold = 0.7f;

		// Token: 0x04000C80 RID: 3200
		private Dictionary<Clan, List<PiratesCampaignBehavior.PatrolZone>> _patrolZones = new Dictionary<Clan, List<PiratesCampaignBehavior.PatrolZone>>();

		// Token: 0x04000C81 RID: 3201
		private Dictionary<MobileParty, PiratesCampaignBehavior.PatrolZone> _assignedZones = new Dictionary<MobileParty, PiratesCampaignBehavior.PatrolZone>();

		// Token: 0x020002B9 RID: 697
		private class PatrolZone
		{
			// Token: 0x17000471 RID: 1137
			// (get) Token: 0x06001E92 RID: 7826 RVA: 0x000C5254 File Offset: 0x000C3454
			// (set) Token: 0x06001E93 RID: 7827 RVA: 0x000C525C File Offset: 0x000C345C
			public int Density { get; set; }

			// Token: 0x06001E94 RID: 7828 RVA: 0x000C5265 File Offset: 0x000C3465
			public PatrolZone(CampaignVec2 position, float radius)
			{
				this.Position = position;
				this.Radius = radius;
				this.Density = 0;
			}

			// Token: 0x0400129B RID: 4763
			[SaveableField(0)]
			public readonly CampaignVec2 Position;

			// Token: 0x0400129C RID: 4764
			[SaveableField(1)]
			public readonly float Radius;
		}

		// Token: 0x020002BA RID: 698
		private class PiratesCampaignBehaviorSaveDefiner : SaveableTypeDefiner
		{
			// Token: 0x06001E95 RID: 7829 RVA: 0x000C5282 File Offset: 0x000C3482
			public PiratesCampaignBehaviorSaveDefiner() : base(2277221)
			{
			}

			// Token: 0x06001E96 RID: 7830 RVA: 0x000C528F File Offset: 0x000C348F
			protected override void DefineClassTypes()
			{
				base.AddClassDefinition(typeof(PiratesCampaignBehavior.PatrolZone), 1, null);
			}

			// Token: 0x06001E97 RID: 7831 RVA: 0x000C52A3 File Offset: 0x000C34A3
			protected override void DefineContainerDefinitions()
			{
				base.ConstructContainerDefinition(typeof(Dictionary<MobileParty, PiratesCampaignBehavior.PatrolZone>));
			}
		}
	}
}
