using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Helpers
{
	// Token: 0x02000009 RID: 9
	public static class SettlementHelper
	{
		// Token: 0x0600002C RID: 44 RVA: 0x0000401C File Offset: 0x0000221C
		public static string GetRandomStuff(bool isFemale)
		{
			string result;
			if (isFemale)
			{
				result = SettlementHelper.StuffToCarryForWoman[SettlementHelper._stuffToCarryIndex % SettlementHelper.StuffToCarryForWoman.Length];
			}
			else
			{
				result = SettlementHelper.StuffToCarryForMan[SettlementHelper._stuffToCarryIndex % SettlementHelper.StuffToCarryForMan.Length];
			}
			SettlementHelper._stuffToCarryIndex++;
			return result;
		}

		// Token: 0x0600002D RID: 45 RVA: 0x00004064 File Offset: 0x00002264
		public static Settlement FindNearestSettlementToSettlement(Settlement fromSettlement, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement result = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Settlement settlement in Settlement.All)
			{
				if (condition == null || condition(settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						result = settlement;
						num = num2;
					}
				}
			}
			return result;
		}

		// Token: 0x0600002E RID: 46 RVA: 0x000040E0 File Offset: 0x000022E0
		public static Settlement FindNearestSettlementToMobileParty(MobileParty mobileParty, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement result = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Settlement settlement in Settlement.All)
			{
				if (condition == null || condition(settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						result = settlement;
						num = num2;
					}
				}
			}
			return result;
		}

		// Token: 0x0600002F RID: 47 RVA: 0x0000415C File Offset: 0x0000235C
		public static Settlement FindNearestSettlementToPoint(in CampaignVec2 point, Func<Settlement, bool> condition = null)
		{
			Settlement result = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Settlement settlement in Settlement.All)
			{
				if (condition == null || condition(settlement))
				{
					float num2 = settlement.Position.Distance(point);
					if (num2 < num)
					{
						result = settlement;
						num = num2;
					}
				}
			}
			return result;
		}

		// Token: 0x06000030 RID: 48 RVA: 0x000041E4 File Offset: 0x000023E4
		public static Hideout FindNearestHideoutToSettlement(Settlement fromSettlement, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement settlement = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Hideout hideout in Hideout.All)
			{
				if (condition == null || condition(hideout.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, hideout.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						settlement = hideout.Settlement;
						num = num2;
					}
				}
			}
			if (settlement == null)
			{
				return null;
			}
			return settlement.Hideout;
		}

		// Token: 0x06000031 RID: 49 RVA: 0x0000427C File Offset: 0x0000247C
		public static Hideout FindNearestHideoutToMobileParty(MobileParty fromMobileParty, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement settlement = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Hideout hideout in Hideout.All)
			{
				if (condition == null || condition(hideout.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(fromMobileParty, hideout.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						settlement = hideout.Settlement;
						num = num2;
					}
				}
			}
			if (settlement == null)
			{
				return null;
			}
			return settlement.Hideout;
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00004314 File Offset: 0x00002514
		public static Town FindNearestTownToSettlement(Settlement fromSettlement, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement settlement = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Town town in Town.AllTowns)
			{
				if (condition == null || condition(town.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, town.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						settlement = town.Settlement;
						num = num2;
					}
				}
			}
			if (settlement == null)
			{
				return null;
			}
			return settlement.Town;
		}

		// Token: 0x06000033 RID: 51 RVA: 0x000043AC File Offset: 0x000025AC
		public static Town FindNearestTownToMobileParty(MobileParty mobileParty, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement settlement = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Town town in Town.AllTowns)
			{
				if (condition == null || condition(town.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, town.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						settlement = town.Settlement;
						num = num2;
					}
				}
			}
			if (settlement == null)
			{
				return null;
			}
			return settlement.Town;
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00004444 File Offset: 0x00002644
		public static int FindNextSettlementAroundMobileParty(MobileParty mobileParty, MobileParty.NavigationType navCapabilities, float maxDistance, int lastIndex, Func<Settlement, bool> condition = null)
		{
			for (int i = lastIndex + 1; i < Settlement.All.Count; i++)
			{
				Settlement settlement = Settlement.All[i];
				float num;
				if ((condition == null || condition(settlement)) && DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, settlement, navCapabilities, out num) < maxDistance)
				{
					return i;
				}
			}
			return -1;
		}

		// Token: 0x06000035 RID: 53 RVA: 0x00004494 File Offset: 0x00002694
		public static Settlement FindNearestCastleToSettlement(Settlement fromSettlement, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement result = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Town town in Town.AllCastles)
			{
				if (condition == null || condition(town.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, town.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						result = town.Settlement;
						num = num2;
					}
				}
			}
			return result;
		}

		// Token: 0x06000036 RID: 54 RVA: 0x00004520 File Offset: 0x00002720
		public static Settlement FindNearestCastleToMobileParty(MobileParty mobileParty, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement result = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Town town in Town.AllCastles)
			{
				if (condition == null || condition(town.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, town.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						result = town.Settlement;
						num = num2;
					}
				}
			}
			return result;
		}

		// Token: 0x06000037 RID: 55 RVA: 0x000045AC File Offset: 0x000027AC
		public static Settlement FindNearestFortificationToSettlement(Settlement fromSettlement, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Town town = SettlementHelper.FindNearestTownToSettlement(fromSettlement, navCapabilities, condition);
			Settlement settlement = SettlementHelper.FindNearestCastleToSettlement(fromSettlement, navCapabilities, condition);
			float num = Campaign.MapDiagonal;
			if (settlement != null)
			{
				float num2;
				num = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, settlement, navCapabilities, out num2);
			}
			float num3 = Campaign.MapDiagonal;
			if (town != null)
			{
				float num2;
				num3 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, town.Settlement, navCapabilities, out num2);
			}
			if (num > num3)
			{
				return town.Settlement;
			}
			return settlement;
		}

		// Token: 0x06000038 RID: 56 RVA: 0x00004604 File Offset: 0x00002804
		public static Settlement FindNearestFortificationToMobileParty(MobileParty mobileParty, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Town town = (mobileParty.CurrentSettlement != null) ? SettlementHelper.FindNearestTownToSettlement(mobileParty.CurrentSettlement, navCapabilities, condition) : SettlementHelper.FindNearestTownToMobileParty(mobileParty, navCapabilities, condition);
			Settlement settlement = (mobileParty.CurrentSettlement != null) ? SettlementHelper.FindNearestCastleToSettlement(mobileParty.CurrentSettlement, navCapabilities, condition) : SettlementHelper.FindNearestCastleToMobileParty(mobileParty, navCapabilities, condition);
			float num = Campaign.MapDiagonal;
			if (settlement != null)
			{
				float num2;
				num = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, settlement, navCapabilities, out num2);
			}
			float num3 = Campaign.MapDiagonal;
			if (town != null)
			{
				float num2;
				num3 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(mobileParty, town.Settlement, navCapabilities, out num2);
			}
			if (num > num3)
			{
				return town.Settlement;
			}
			return settlement;
		}

		// Token: 0x06000039 RID: 57 RVA: 0x0000468C File Offset: 0x0000288C
		public static Settlement FindFurthestFortificationToSettlement(MBReadOnlyList<Town> candidates, MobileParty.NavigationType navCapabilities, Settlement fromSettlement, out float furthestDistance)
		{
			Settlement result = null;
			float num = float.MinValue;
			foreach (Town town in candidates)
			{
				float num3;
				float num2 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, town.Settlement, navCapabilities, out num3);
				if (num2 > num)
				{
					result = town.Settlement;
					num = num2;
				}
			}
			furthestDistance = num;
			return result;
		}

		// Token: 0x0600003A RID: 58 RVA: 0x00004700 File Offset: 0x00002900
		public static Village FindNearestVillageToSettlement(Settlement fromSettlement, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement settlement = null;
			float num = Campaign.MapDiagonal * 2f;
			foreach (Village village in Village.All)
			{
				if (condition == null || condition(village.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromSettlementToSettlement(fromSettlement, village.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						settlement = village.Settlement;
						num = num2;
					}
				}
			}
			if (settlement == null)
			{
				return null;
			}
			return settlement.Village;
		}

		// Token: 0x0600003B RID: 59 RVA: 0x00004798 File Offset: 0x00002998
		public static Village FindNearestVillageToMobileParty(MobileParty fromParty, MobileParty.NavigationType navCapabilities, Func<Settlement, bool> condition = null)
		{
			Settlement settlement = null;
			float num = float.MaxValue;
			foreach (Village village in Village.All)
			{
				if (condition == null || condition(village.Settlement))
				{
					float num3;
					float num2 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(fromParty, village.Settlement, navCapabilities, out num3);
					if (num2 < num)
					{
						settlement = village.Settlement;
						num = num2;
					}
				}
			}
			if (settlement == null)
			{
				return null;
			}
			return settlement.Village;
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00004828 File Offset: 0x00002A28
		private static Settlement FindRandomInternal(Func<Settlement, bool> condition, IEnumerable<Settlement> settlementsToIterate)
		{
			List<Settlement> list = new List<Settlement>();
			foreach (Settlement settlement in settlementsToIterate)
			{
				if (condition(settlement))
				{
					list.Add(settlement);
				}
			}
			if (list.Count > 0)
			{
				return list[MBRandom.RandomInt(list.Count)];
			}
			return null;
		}

		// Token: 0x0600003D RID: 61 RVA: 0x0000489C File Offset: 0x00002A9C
		public static Settlement FindRandomSettlement(Func<Settlement, bool> condition = null)
		{
			return SettlementHelper.FindRandomInternal(condition, Settlement.All);
		}

		// Token: 0x0600003E RID: 62 RVA: 0x000048A9 File Offset: 0x00002AA9
		public static Settlement FindRandomHideout(Func<Settlement, bool> condition = null)
		{
			return SettlementHelper.FindRandomInternal(condition, from x in Hideout.All
			select x.Settlement);
		}

		// Token: 0x0600003F RID: 63 RVA: 0x000048DC File Offset: 0x00002ADC
		public static void TakeEnemyVillagersOutsideSettlements(Settlement settlementWhichChangedFaction)
		{
			if (settlementWhichChangedFaction.IsFortification)
			{
				bool flag;
				do
				{
					flag = false;
					MobileParty mobileParty = null;
					foreach (MobileParty mobileParty2 in settlementWhichChangedFaction.Parties)
					{
						if (mobileParty2.IsVillager && mobileParty2.HomeSettlement.IsVillage && mobileParty2.HomeSettlement.Village.Bound == settlementWhichChangedFaction && mobileParty2.HomeSettlement.MapFaction != settlementWhichChangedFaction.MapFaction)
						{
							mobileParty = mobileParty2;
							flag = true;
							break;
						}
					}
					if (flag && mobileParty.MapEvent == null)
					{
						LeaveSettlementAction.ApplyForParty(mobileParty);
						mobileParty.SetMoveModeHold();
					}
				}
				while (flag);
				bool flag2;
				do
				{
					flag2 = false;
					MobileParty mobileParty3 = null;
					foreach (MobileParty mobileParty4 in settlementWhichChangedFaction.Parties)
					{
						if (mobileParty4.IsCaravan && FactionManager.IsAtWarAgainstFaction(mobileParty4.MapFaction, settlementWhichChangedFaction.MapFaction))
						{
							mobileParty3 = mobileParty4;
							flag2 = true;
							break;
						}
					}
					if (flag2 && mobileParty3.MapEvent == null)
					{
						LeaveSettlementAction.ApplyForParty(mobileParty3);
						mobileParty3.SetMoveModeHold();
					}
				}
				while (flag2);
				foreach (MobileParty mobileParty5 in MobileParty.All)
				{
					if ((mobileParty5.IsVillager || mobileParty5.IsCaravan) && mobileParty5.TargetSettlement == settlementWhichChangedFaction && mobileParty5.CurrentSettlement != settlementWhichChangedFaction)
					{
						mobileParty5.SetMoveModeHold();
					}
				}
			}
			if (settlementWhichChangedFaction.IsVillage)
			{
				foreach (MobileParty mobileParty6 in MobileParty.AllVillagerParties)
				{
					if (mobileParty6.HomeSettlement == settlementWhichChangedFaction && mobileParty6.CurrentSettlement != settlementWhichChangedFaction)
					{
						if (mobileParty6.CurrentSettlement != null && mobileParty6.MapEvent == null)
						{
							LeaveSettlementAction.ApplyForParty(mobileParty6);
							mobileParty6.SetMoveModeHold();
						}
						else
						{
							mobileParty6.SetMoveModeHold();
						}
					}
				}
			}
		}

		// Token: 0x06000040 RID: 64 RVA: 0x00004B08 File Offset: 0x00002D08
		public static Settlement GetRandomTown(Clan fromFaction = null)
		{
			int num = 0;
			foreach (Settlement settlement in Campaign.Current.Settlements)
			{
				if ((fromFaction == null || settlement.MapFaction == fromFaction) && (settlement.IsTown || settlement.IsVillage))
				{
					num++;
				}
			}
			int num2 = MBRandom.RandomInt(0, num - 1);
			foreach (Settlement settlement2 in Campaign.Current.Settlements)
			{
				if ((fromFaction == null || settlement2.MapFaction == fromFaction) && (settlement2.IsTown || settlement2.IsVillage))
				{
					num2--;
					if (num2 < 0)
					{
						return settlement2;
					}
				}
			}
			return null;
		}

		// Token: 0x06000041 RID: 65 RVA: 0x00004BF8 File Offset: 0x00002DF8
		public static Settlement GetBestSettlementToSpawnAround(Hero hero)
		{
			Settlement result = null;
			float num = -1f;
			IFaction mapFaction = hero.MapFaction;
			foreach (Settlement settlement in Settlement.All)
			{
				if (settlement.Party.MapEvent == null)
				{
					IFaction mapFaction2 = settlement.MapFaction;
					float num2;
					if (settlement.IsTown)
					{
						num2 = 1f;
					}
					else if (settlement.IsCastle)
					{
						num2 = 0.9f;
					}
					else if (settlement.IsVillage)
					{
						num2 = 0.8f;
					}
					else
					{
						if (!settlement.IsHideout)
						{
							continue;
						}
						num2 = ((mapFaction2 == mapFaction) ? 0.2f : 0f);
					}
					float num3 = 0.0001f;
					if (mapFaction2 == mapFaction)
					{
						num3 = 1f;
					}
					else if (DiplomacyHelper.IsSameFactionAndNotEliminated(mapFaction2, mapFaction))
					{
						num3 = 0.01f;
					}
					else if (FactionManager.IsNeutralWithFaction(mapFaction2, mapFaction))
					{
						num3 = 0.0005f;
					}
					float num4 = (settlement.Town != null && settlement.Town.GarrisonParty != null && settlement.OwnerClan == hero.Clan) ? (settlement.Town.GarrisonParty.Party.CalculateCurrentStrength() / (settlement.IsTown ? 60f : 30f)) : 1f;
					float num5 = (settlement.IsUnderRaid || settlement.IsUnderSiege) ? 0.1f : 1f;
					float num6 = (settlement.OwnerClan == hero.Clan) ? 1f : 0.25f;
					float num7 = settlement.RandomFloatWithSeed((uint)hero.RandomInt(), 0.5f, 1f);
					float value = Campaign.Current.Models.MapDistanceModel.GetDistance(hero.MapFaction.FactionMidSettlement, settlement, false, false, MobileParty.NavigationType.Default) / Campaign.MapDiagonal;
					float num8 = 1f - MathF.Clamp(value, 0f, 1f);
					float num9 = num8 * num8;
					float num10 = 1f;
					if (hero.LastKnownClosestSettlement != null)
					{
						value = Campaign.Current.Models.MapDistanceModel.GetDistance(hero.LastKnownClosestSettlement, settlement, false, false, MobileParty.NavigationType.Default) / Campaign.MapDiagonal;
						num10 = 1f - MathF.Clamp(value, 0f, 1f);
						num10 *= num10;
					}
					float num11 = num9 * 0.33f + num10 * 0.66f;
					float num12 = num3 * num2 * num5 * num6 * num4 * num7 * num11;
					if (num12 > num)
					{
						num = num12;
						result = settlement;
					}
				}
			}
			return result;
		}

		// Token: 0x06000042 RID: 66 RVA: 0x00004E94 File Offset: 0x00003094
		public static IEnumerable<Hero> GetAllHeroesOfSettlement(Settlement settlement, bool includePrisoners)
		{
			foreach (MobileParty mobileParty in settlement.Parties)
			{
				if (mobileParty.LeaderHero != null)
				{
					yield return mobileParty.LeaderHero;
				}
			}
			List<MobileParty>.Enumerator enumerator = default(List<MobileParty>.Enumerator);
			foreach (Hero hero in settlement.HeroesWithoutParty)
			{
				yield return hero;
			}
			List<Hero>.Enumerator enumerator2 = default(List<Hero>.Enumerator);
			if (includePrisoners)
			{
				foreach (TroopRosterElement troopRosterElement in settlement.Party.PrisonRoster.GetTroopRoster())
				{
					if (troopRosterElement.Character.IsHero)
					{
						yield return troopRosterElement.Character.HeroObject;
					}
				}
				List<TroopRosterElement>.Enumerator enumerator3 = default(List<TroopRosterElement>.Enumerator);
			}
			yield break;
			yield break;
		}

		// Token: 0x06000043 RID: 67 RVA: 0x00004EAC File Offset: 0x000030AC
		public static bool IsGarrisonStarving(Settlement settlement)
		{
			bool result = false;
			if (settlement.IsStarving)
			{
				result = (settlement.Town.FoodChange < -(settlement.Town.Prosperity / (float)Campaign.Current.Models.SettlementFoodModel.NumberOfProsperityToEatOneFood));
			}
			return result;
		}

		// Token: 0x06000044 RID: 68 RVA: 0x00004EF4 File Offset: 0x000030F4
		public static void SpawnNotablesIfNeeded(Settlement settlement)
		{
			if (settlement.IsTown || settlement.IsVillage)
			{
				List<Occupation> list = new List<Occupation>();
				if (settlement.IsTown)
				{
					list = new List<Occupation>
					{
						Occupation.GangLeader,
						Occupation.Artisan,
						Occupation.Merchant
					};
				}
				else if (settlement.IsVillage)
				{
					list = new List<Occupation>
					{
						Occupation.RuralNotable,
						Occupation.Headman
					};
				}
				float randomFloat = MBRandom.RandomFloat;
				int num = 0;
				foreach (Occupation occupation in list)
				{
					num += Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(settlement, occupation);
				}
				float num2 = (settlement.Notables.Count > 0) ? ((float)(num - settlement.Notables.Count) / (float)num) : 1f;
				num2 *= MathF.Pow(num2, 0.36f);
				if (randomFloat <= num2)
				{
					MBList<Occupation> mblist = new MBList<Occupation>();
					foreach (Occupation occupation2 in list)
					{
						int num3 = 0;
						using (List<Hero>.Enumerator enumerator2 = settlement.Notables.GetEnumerator())
						{
							while (enumerator2.MoveNext())
							{
								if (enumerator2.Current.CharacterObject.Occupation == occupation2)
								{
									num3++;
								}
							}
						}
						int targetNotableCountForSettlement = Campaign.Current.Models.NotableSpawnModel.GetTargetNotableCountForSettlement(settlement, occupation2);
						if (num3 < targetNotableCountForSettlement)
						{
							mblist.Add(occupation2);
						}
					}
					if (mblist.Count > 0)
					{
						EnterSettlementAction.ApplyForCharacterOnly(HeroCreator.CreateNotable(mblist.GetRandomElement<Occupation>(), settlement), settlement);
					}
				}
			}
		}

		// Token: 0x06000045 RID: 69 RVA: 0x000050D8 File Offset: 0x000032D8
		public static ExplainedNumber GetGarrisonChangeExplainedNumber(Town town)
		{
			ExplainedNumber result = new ExplainedNumber(0f, true, null);
			ExplainedNumber garrisonChangeExplainedNumber = Campaign.Current.GetCampaignBehavior<IGarrisonRecruitmentBehavior>().GetGarrisonChangeExplainedNumber(town);
			if (garrisonChangeExplainedNumber.BaseNumber > 0f)
			{
				result.AddFromExplainedNumber(garrisonChangeExplainedNumber, new TextObject("{=basevalue}Base", null));
			}
			if (town.GarrisonParty != null)
			{
				int totalManCount = Campaign.Current.Models.PartyDesertionModel.GetTroopsToDesert(town.GarrisonParty).TotalManCount;
				if (totalManCount > 0)
				{
					TextObject baseText = new TextObject("{=ojBJ3aTO}Desertion", null);
					result.SubtractFromExplainedNumber(new ExplainedNumber((float)totalManCount, true, null), baseText);
				}
			}
			return result;
		}

		// Token: 0x06000046 RID: 70 RVA: 0x00005170 File Offset: 0x00003370
		public static float GetNeighborScoreForConsideringClan(Settlement settlement, Clan consideringClan)
		{
			float num = 0f;
			if (settlement.MapFaction == consideringClan.MapFaction && settlement.IsFortification)
			{
				HashSet<Settlement> hashSet = new HashSet<Settlement>();
				HashSet<Settlement> hashSet2 = new HashSet<Settlement>();
				foreach (Settlement settlement2 in settlement.Town.GetNeighborFortifications(MobileParty.NavigationType.All))
				{
					if (!hashSet.Contains(settlement2))
					{
						hashSet.Add(settlement2);
						if (settlement.MapFaction.IsAtWarWith(settlement2.MapFaction))
						{
							num -= 0.2f;
						}
						else if (settlement.MapFaction == consideringClan.MapFaction)
						{
							num += 0.1f;
						}
						else
						{
							num += 0.05f;
						}
					}
				}
				foreach (Settlement settlement3 in hashSet)
				{
					foreach (Settlement settlement4 in settlement3.Town.GetNeighborFortifications(MobileParty.NavigationType.All))
					{
						if (!hashSet.Contains(settlement4) && !hashSet2.Contains(settlement4))
						{
							hashSet2.Add(settlement4);
							if (settlement.MapFaction.IsAtWarWith(settlement4.MapFaction))
							{
								num -= 0.04f;
							}
							else if (settlement.MapFaction == consideringClan.MapFaction)
							{
								num += 0.02f;
							}
							else
							{
								num += 0.01f;
							}
						}
					}
				}
			}
			return num;
		}

		// Token: 0x04000001 RID: 1
		private static readonly string[] StuffToCarryForMan = new string[]
		{
			"_to_carry_foods_basket_apple",
			"_to_carry_merchandise_hides_b",
			"_to_carry_bed_convolute_g",
			"_to_carry_bed_convolute_a",
			"_to_carry_bd_fabric_c",
			"_to_carry_bd_basket_a",
			"practice_spear_t1",
			"simple_sparth_axe_t2"
		};

		// Token: 0x04000002 RID: 2
		private static readonly string[] StuffToCarryForWoman = new string[]
		{
			"_to_carry_kitchen_pot_c",
			"_to_carry_arm_kitchen_pot_c",
			"_to_carry_foods_basket_apple",
			"_to_carry_bd_basket_a"
		};

		// Token: 0x04000003 RID: 3
		private static int _stuffToCarryIndex = MBRandom.NondeterministicRandomInt % 1024;
	}
}
