using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x0200015B RID: 347
	public class DefaultTargetScoreCalculatingModel : TargetScoreCalculatingModel
	{
		// Token: 0x170006DD RID: 1757
		// (get) Token: 0x06001AC2 RID: 6850 RVA: 0x00088B0E File Offset: 0x00086D0E
		public override float TravelingToAssignmentFactor
		{
			get
			{
				return 1.33f;
			}
		}

		// Token: 0x170006DE RID: 1758
		// (get) Token: 0x06001AC3 RID: 6851 RVA: 0x00088B15 File Offset: 0x00086D15
		public override float BesiegingFactor
		{
			get
			{
				return 1.67f;
			}
		}

		// Token: 0x170006DF RID: 1759
		// (get) Token: 0x06001AC4 RID: 6852 RVA: 0x00088B1C File Offset: 0x00086D1C
		public override float AssaultingTownFactor
		{
			get
			{
				return 2f;
			}
		}

		// Token: 0x170006E0 RID: 1760
		// (get) Token: 0x06001AC5 RID: 6853 RVA: 0x00088B23 File Offset: 0x00086D23
		public override float RaidingFactor
		{
			get
			{
				return 1.67f;
			}
		}

		// Token: 0x170006E1 RID: 1761
		// (get) Token: 0x06001AC6 RID: 6854 RVA: 0x00088B2A File Offset: 0x00086D2A
		public override float DefendingFactor
		{
			get
			{
				return 2f;
			}
		}

		// Token: 0x06001AC7 RID: 6855 RVA: 0x00088B31 File Offset: 0x00086D31
		public override float GetDefensivePatrollingFactor(bool isNavalPatrolling)
		{
			return 0.66f;
		}

		// Token: 0x06001AC8 RID: 6856 RVA: 0x00088B38 File Offset: 0x00086D38
		public override float GetOffensivePatrollingFactor(bool isNavalPatrolling)
		{
			return 0f;
		}

		// Token: 0x06001AC9 RID: 6857 RVA: 0x00088B3F File Offset: 0x00086D3F
		public override float CalculateOffensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
		{
			return 0f;
		}

		// Token: 0x06001ACA RID: 6858 RVA: 0x00088B48 File Offset: 0x00086D48
		public override float CurrentObjectiveValue(MobileParty mobileParty)
		{
			float num = 0f;
			if (mobileParty.TargetSettlement == null)
			{
				return 0f;
			}
			if (mobileParty.DefaultBehavior != AiBehavior.BesiegeSettlement && mobileParty.DefaultBehavior != AiBehavior.RaidSettlement && mobileParty.DefaultBehavior != AiBehavior.DefendSettlement)
			{
				return num;
			}
			float totalLandStrengthWithFollowers = mobileParty.GetTotalLandStrengthWithFollowers(false);
			num = this.GetTargetScoreForFaction(mobileParty.TargetSettlement, (mobileParty.DefaultBehavior == AiBehavior.BesiegeSettlement) ? Army.ArmyTypes.Besieger : ((mobileParty.DefaultBehavior == AiBehavior.RaidSettlement) ? Army.ArmyTypes.Raider : Army.ArmyTypes.Defender), mobileParty, totalLandStrengthWithFollowers);
			AiBehavior defaultBehavior = mobileParty.DefaultBehavior;
			if (defaultBehavior != AiBehavior.RaidSettlement)
			{
				if (defaultBehavior != AiBehavior.BesiegeSettlement)
				{
					if (defaultBehavior == AiBehavior.DefendSettlement)
					{
						num *= ((mobileParty.Party.MapEvent != null && mobileParty.MapEvent.MapEventSettlement == mobileParty.TargetSettlement) ? this.DefendingFactor : this.TravelingToAssignmentFactor);
					}
				}
				else
				{
					num *= ((mobileParty.Party.MapEvent == null && mobileParty.TargetSettlement.SiegeEvent != null && mobileParty.TargetSettlement.SiegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(mobileParty.Party, MapEvent.BattleTypes.Siege)) ? this.BesiegingFactor : ((mobileParty.Party.MapEvent != null && mobileParty.Party.MapEvent.MapEventSettlement == mobileParty.TargetSettlement) ? this.AssaultingTownFactor : this.TravelingToAssignmentFactor));
				}
			}
			else
			{
				num *= ((mobileParty.Party.MapEvent != null && mobileParty.MapEvent.MapEventSettlement == mobileParty.TargetSettlement) ? this.RaidingFactor : this.TravelingToAssignmentFactor);
			}
			return num;
		}

		// Token: 0x06001ACB RID: 6859 RVA: 0x00088CB4 File Offset: 0x00086EB4
		public override float CalculateDefensivePatrollingScoreForSettlement(Settlement settlement, bool isTargetingPort, MobileParty mobileParty)
		{
			bool flag = mobileParty.Army != null && mobileParty.Army.LeaderParty == mobileParty && !mobileParty.Army.IsWaitingForArmyMembers();
			if (mobileParty.Army != null && !flag && mobileParty.Army.Cohesion > (float)mobileParty.Army.CohesionThresholdForDispersion && mobileParty.AttachedTo != null)
			{
				return 0f;
			}
			float num = (mobileParty.LeaderHero != null && mobileParty.LeaderHero.Clan != null && mobileParty.LeaderHero.Clan.Fiefs.Count > 0) ? (mobileParty.LeaderHero.RandomFloat(0.2f, 0.4f) + (float)MathF.Min(4, mobileParty.LeaderHero.Clan.Fiefs.Count - 1) * 0.05f + mobileParty.LeaderHero.RandomFloatWithSeed((uint)CampaignTime.Now.ToHours, 0.2f)) : 0.5f;
			float num2 = 1f - num;
			Hero leaderHero = mobileParty.LeaderHero;
			float num3 = num2 + ((((leaderHero != null) ? leaderHero.Clan : null) != null && (settlement.OwnerClan == mobileParty.LeaderHero.Clan || mobileParty.LeaderHero.Clan.Settlements.Count == 0)) ? num : 0f);
			float num4 = 1f;
			if (settlement.MapFaction == mobileParty.MapFaction)
			{
				float nearbyLandThreatIntensity = settlement.NearbyLandThreatIntensity;
				float nearbyLandAllyIntensity = settlement.NearbyLandAllyIntensity;
				float num5 = MathF.Max(0f, nearbyLandThreatIntensity - nearbyLandAllyIntensity * 0.25f);
				if (num5 > 1f)
				{
					int num6 = 0;
					foreach (WarPartyComponent warPartyComponent in mobileParty.MapFaction.WarPartyComponents)
					{
						MobileParty mobileParty2 = warPartyComponent.MobileParty;
						if (mobileParty2 != mobileParty && (mobileParty2.Army == null || mobileParty2.Army != mobileParty.Army) && (mobileParty2.Army == null || mobileParty2.Army.LeaderParty == mobileParty) && mobileParty2.DefaultBehavior == AiBehavior.PatrolAroundPoint && mobileParty2.TargetSettlement == settlement)
						{
							num6++;
						}
					}
					num4 += MathF.Pow(MathF.Min(10f, num5), 0.25f) - (float)num6;
				}
				else
				{
					num4 += num5;
				}
			}
			float num7 = (mobileParty.Army != null && mobileParty.Army.LeaderParty != mobileParty && mobileParty.Army.Cohesion < (float)mobileParty.Army.CohesionThresholdForDispersion) ? (((float)mobileParty.Army.CohesionThresholdForDispersion - mobileParty.Army.Cohesion) / (float)mobileParty.Army.CohesionThresholdForDispersion) : 1f;
			float num8 = 1f;
			if (mobileParty.MapFaction.IsMinorFaction)
			{
				num8 = settlement.RandomFloatWithSeed((uint)CampaignTime.Now.ToWeeks, 0.2f, 1.8f);
			}
			float num9 = (mobileParty.DefaultBehavior == AiBehavior.PatrolAroundPoint && !mobileParty.TargetPosition.IsOnLand && mobileParty.TargetSettlement != null && !mobileParty.TargetSettlement.MapFaction.IsAtWarWith(mobileParty.MapFaction)) ? 0.5f : 1f;
			float num10 = (mobileParty.LeaderHero != null && settlement.OwnerClan == mobileParty.LeaderHero.Clan) ? 1f : 0.5f;
			return num8 * num4 * num3 * num7 * num9 * num10 * Campaign.Current.Models.TargetScoreCalculatingModel.GetDefensivePatrollingFactor(false);
		}

		// Token: 0x06001ACC RID: 6860 RVA: 0x0008902C File Offset: 0x0008722C
		public override float GetTargetScoreForFaction(Settlement targetSettlement, Army.ArmyTypes missionType, MobileParty mobileParty, float ourStrength)
		{
			IFaction mapFaction = mobileParty.MapFaction;
			MobileParty.NavigationType navigationType;
			float num;
			bool flag;
			AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mobileParty, targetSettlement, false, out navigationType, out num, out flag);
			float num6;
			if (missionType == Army.ArmyTypes.Defender)
			{
				float num2 = 0f;
				float num3 = 0f;
				foreach (WarPartyComponent warPartyComponent in mapFaction.WarPartyComponents)
				{
					MobileParty mobileParty2 = warPartyComponent.MobileParty;
					if (mobileParty2 != mobileParty && (mobileParty2.Army == null || mobileParty2.Army != mobileParty.Army) && mobileParty2.AttachedTo == null)
					{
						if (mobileParty2.Army != null)
						{
							Army army = mobileParty2.Army;
							if ((army.IsWaitingForArmyMembers() && army.AiBehaviorObject == targetSettlement) || (!army.IsWaitingForArmyMembers() && army.LeaderParty.DefaultBehavior == AiBehavior.DefendSettlement && army.AiBehaviorObject == targetSettlement) || (army.LeaderParty.TargetParty != null && targetSettlement.LastAttackerParty != null && (army.LeaderParty.TargetParty == targetSettlement.LastAttackerParty || (army.LeaderParty.TargetParty.MapEvent != null && army.LeaderParty.TargetParty.MapEvent == targetSettlement.LastAttackerParty.MapEvent) || (army.LeaderParty.TargetParty.BesiegedSettlement != null && army.LeaderParty.TargetParty.BesiegedSettlement == targetSettlement.LastAttackerParty.BesiegedSettlement))))
							{
								num3 += army.EstimatedStrength;
							}
						}
						else if ((mobileParty2.DefaultBehavior == AiBehavior.DefendSettlement && mobileParty2.TargetSettlement == targetSettlement) || (mobileParty2.TargetParty != null && (mobileParty2.TargetParty == targetSettlement.LastAttackerParty || (mobileParty2.TargetParty.MapEvent != null && mobileParty2.TargetParty.MapEvent == targetSettlement.LastAttackerParty.MapEvent) || (mobileParty2.TargetParty.BesiegedSettlement != null && mobileParty2.TargetParty.BesiegedSettlement == targetSettlement.LastAttackerParty.BesiegedSettlement))))
						{
							num3 += mobileParty2.Party.EstimatedStrength;
						}
					}
				}
				MobileParty lastAttackerParty = targetSettlement.LastAttackerParty;
				if (lastAttackerParty != null)
				{
					if ((lastAttackerParty.MapEvent != null && lastAttackerParty.MapEvent.MapEventSettlement == targetSettlement) || lastAttackerParty.BesiegedSettlement == targetSettlement)
					{
						LocatableSearchData<MobileParty> locatableSearchData = MobileParty.StartFindingLocatablesAroundPosition(targetSettlement.GatePosition.ToVec2(), Campaign.Current.Models.EncounterModel.GetEncounterJoiningRadius * 3f);
						for (MobileParty mobileParty3 = MobileParty.FindNextLocatable(ref locatableSearchData); mobileParty3 != null; mobileParty3 = MobileParty.FindNextLocatable(ref locatableSearchData))
						{
							if (mobileParty3.Aggressiveness > 0f && mobileParty3.MapFaction == lastAttackerParty.MapFaction)
							{
								num2 += ((mobileParty3.Aggressiveness > 0.5f) ? 1f : (mobileParty3.Aggressiveness * 2f)) * mobileParty3.Party.EstimatedStrength;
							}
						}
					}
					else
					{
						Army army2 = lastAttackerParty.Army;
						num2 = ((army2 != null) ? army2.EstimatedStrength : lastAttackerParty.Party.EstimatedStrength);
					}
				}
				float num4 = ourStrength + num3;
				float num5 = MathF.Max(100f, num2) * 1.1f;
				num6 = num4 / num2;
				if (num4 < num5)
				{
					num6 *= 0.9f;
				}
				if (num3 > num5 * 1.75f)
				{
					num6 *= 0.25f;
				}
				if (ourStrength < num2)
				{
					num6 *= MathF.Pow(ourStrength / num2, 0.25f);
				}
			}
			else
			{
				float num7 = 0f;
				float num8 = 0f;
				bool flag2 = Hero.MainHero.CurrentSettlement == targetSettlement;
				foreach (MobileParty mobileParty4 in targetSettlement.Parties)
				{
					if (mobileParty4.Aggressiveness > 0.01f || mobileParty4.IsGarrison || mobileParty4.IsMilitia)
					{
						float num9 = (mobileParty4 == MobileParty.MainParty) ? 0.5f : ((mobileParty4.Army != null && mobileParty4.Army.LeaderParty == MobileParty.MainParty) ? 0.8f : 1f);
						float num10 = flag2 ? 0.8f : 1f;
						num7 += num9 * num10 * mobileParty4.Party.EstimatedStrength;
						if (!mobileParty4.IsGarrison && !mobileParty4.IsMilitia && mobileParty4.LeaderHero != null)
						{
							num8 += num9 * num10 * mobileParty4.Party.EstimatedStrength;
						}
					}
				}
				float num11 = 0f;
				float num12 = (missionType == Army.ArmyTypes.Besieger && mobileParty.BesiegedSettlement != targetSettlement) ? (targetSettlement.IsTown ? 4f : 3f) : 1f;
				float num13 = 0.7f;
				num12 *= 1f - 0.6f * (1f - num13) * (1f - num13);
				if (num7 < 100f && missionType == Army.ArmyTypes.Besieger)
				{
					num12 *= 0.5f + 0.5f * (num7 / 100f);
				}
				if (missionType == Army.ArmyTypes.Raider)
				{
					num12 *= 0.66f;
				}
				if ((mobileParty.MapEvent == null || mobileParty.MapEvent.MapEventSettlement != targetSettlement) && targetSettlement.MapFaction.IsKingdomFaction)
				{
					int count = targetSettlement.MapFaction.Settlements.Count;
					float b = (targetSettlement.MapFaction.CurrentTotalStrength * 0.25f - num7 - num8) / ((float)count + 10f);
					num11 = MathF.Max(0f, b) * num12;
				}
				float num14 = (missionType == Army.ArmyTypes.Besieger) ? (1f + 0.33f * (float)targetSettlement.Town.GetWallLevel()) : 1f;
				if (missionType == Army.ArmyTypes.Besieger && targetSettlement.Town.FoodStocks < 100f)
				{
					num14 -= 0.5f * (num14 - 1f) * ((100f - targetSettlement.Town.FoodStocks) / 100f);
				}
				float num15 = (missionType == Army.ArmyTypes.Besieger && mobileParty.LeaderHero != null) ? (mobileParty.LeaderHero.RandomFloat(0.1f) + (MathF.Max(MathF.Min(1.2f, mobileParty.Aggressiveness), 0.8f) - 0.8f) * 0.5f) : 0f;
				float num16 = num7 * (num14 - num15) + num11 + 0.1f;
				if (targetSettlement.SiegeEvent == null)
				{
					float num17 = (missionType == Army.ArmyTypes.Besieger) ? 2f : 0.75f;
					if (mobileParty.SiegeEvent != null && mobileParty.BesiegedSettlement == targetSettlement)
					{
						num17 = 1.5f;
					}
					if (ourStrength < num16 * num17)
					{
						return 0f;
					}
				}
				float num18 = 0f;
				if (missionType == Army.ArmyTypes.Besieger || (missionType == Army.ArmyTypes.Raider && targetSettlement.Party.MapEvent != null))
				{
					float num19 = (missionType == Army.ArmyTypes.Besieger) ? (Campaign.Current.EstimatedAverageLordPartySpeed * 2f) : Campaign.Current.EstimatedAverageLordPartySpeed;
					float averageDistanceBetweenClosestTwoTownsWithNavigationType = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
					if (num < Math.Max(averageDistanceBetweenClosestTwoTownsWithNavigationType, num19))
					{
						LocatableSearchData<MobileParty> locatableSearchData2 = MobileParty.StartFindingLocatablesAroundPosition(targetSettlement.GatePosition.ToVec2(), num19);
						for (MobileParty mobileParty5 = MobileParty.FindNextLocatable(ref locatableSearchData2); mobileParty5 != null; mobileParty5 = MobileParty.FindNextLocatable(ref locatableSearchData2))
						{
							if (mobileParty5.CurrentSettlement != targetSettlement && mobileParty5.Aggressiveness > 0.01f && mobileParty5.MapFaction == targetSettlement.Party.MapFaction && (!mobileParty5.IsMainParty || !mobileParty5.ShouldBeIgnored))
							{
								float num20 = (mobileParty5 == MobileParty.MainParty || (mobileParty5.Army != null && mobileParty5.Army.LeaderParty == MobileParty.MainParty)) ? 0.5f : 1f;
								float distance;
								if (mobileParty5.CurrentSettlement != null)
								{
									distance = Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty5.CurrentSettlement, targetSettlement, false, false, MobileParty.NavigationType.All);
								}
								else
								{
									float num21;
									distance = Campaign.Current.Models.MapDistanceModel.GetDistance(mobileParty5, targetSettlement, false, MobileParty.NavigationType.All, out num21);
								}
								float num22 = num19 / (distance + 0.001f);
								num22 = MBMath.ClampFloat(num22, 0.3f, (missionType == Army.ArmyTypes.Raider) ? 0.8f : 1.2f);
								num18 += num22 * mobileParty5.Party.EstimatedStrength * num20;
							}
						}
					}
					if (num18 < ourStrength)
					{
						num18 = MathF.Max(0f, num18 - ourStrength * 0.33f);
					}
					num16 += num18;
					num16 -= num11;
					if (targetSettlement.MapFaction.IsKingdomFaction)
					{
						int count2 = targetSettlement.MapFaction.Settlements.Count;
						float b2 = (targetSettlement.MapFaction.CurrentTotalStrength * 0.5f - (num8 + num18)) / ((float)count2 + 10f);
						num11 = MathF.Max(0f, b2) * num12;
					}
					num16 += num11;
				}
				num6 = ourStrength / num16;
			}
			num6 = ((num6 > 2f) ? 2f : num6);
			float averageDistanceBetweenClosestTwoTownsWithNavigationType2 = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All);
			float num23 = MBMath.Map((5f * averageDistanceBetweenClosestTwoTownsWithNavigationType2 - num) / averageDistanceBetweenClosestTwoTownsWithNavigationType2, 0f, 5f, 0.9f, (missionType == Army.ArmyTypes.Raider) ? 3f : 10f);
			float num24 = (missionType == Army.ArmyTypes.Raider) ? targetSettlement.GetSettlementValueForEnemyHero(mobileParty.LeaderHero) : targetSettlement.GetSettlementValueForFaction(mapFaction);
			float y = targetSettlement.IsVillage ? 0.5f : 0.33f;
			float num25 = MathF.Pow(num24 / 50000f, y);
			float num26 = 1f;
			if (missionType == Army.ArmyTypes.Raider)
			{
				if (targetSettlement.Village.Bound.Town.FoodStocks < 100f)
				{
					num25 *= 1f + 0.3f * ((100f - targetSettlement.Village.Bound.Town.FoodStocks) / 100f);
				}
				num25 *= 1.5f;
				num26 += ((mobileParty.Army != null) ? 0.5f : 1f) * ((mobileParty.LeaderHero != null && mobileParty.LeaderHero.Clan != null && mobileParty.LeaderHero.Clan.Gold < 10000) ? ((10000f - (float)mobileParty.LeaderHero.Clan.Gold) / 20000f) : 0f);
			}
			float num27 = (missionType == Army.ArmyTypes.Defender) ? (targetSettlement.IsVillage ? 1.28f : 1.75f) : ((missionType == Army.ArmyTypes.Besieger) ? 0.8f : (0.875f * (1f + (1f - targetSettlement.SettlementHitPoints))));
			if (missionType == Army.ArmyTypes.Defender && targetSettlement.LastAttackerParty != null && ((targetSettlement.IsFortification && targetSettlement.LastAttackerParty.BesiegedSettlement != targetSettlement) || (!targetSettlement.IsFortification && targetSettlement.LastAttackerParty.MapEvent == null)))
			{
				MobileParty lastAttackerParty2 = targetSettlement.LastAttackerParty;
				MobileParty.NavigationType navCapabilities = lastAttackerParty2.HasNavalNavigationCapability ? MobileParty.NavigationType.All : MobileParty.NavigationType.Default;
				float b3 = DistanceHelper.FindClosestDistanceFromMobilePartyToSettlement(lastAttackerParty2, targetSettlement, navCapabilities) / (lastAttackerParty2._lastCalculatedSpeed * (float)CampaignTime.HoursInDay);
				float num28 = MathF.Min(0.5f, b3) / 0.5f;
				num27 = num28 * 0.8f + (1f - num28) * num27;
			}
			float num29 = 1f;
			if ((missionType == Army.ArmyTypes.Raider || missionType == Army.ArmyTypes.Besieger) && targetSettlement.OwnerClan != null && mobileParty.LeaderHero != null)
			{
				int relationWithClan = mobileParty.LeaderHero.Clan.GetRelationWithClan(targetSettlement.OwnerClan);
				if (relationWithClan > 0)
				{
					num29 = 1f - ((missionType == Army.ArmyTypes.Besieger) ? 0.4f : 0.8f) * (MathF.Sqrt((float)relationWithClan) / 10f);
				}
				else if (relationWithClan < 0)
				{
					num29 = 1f + ((missionType == Army.ArmyTypes.Besieger) ? 0.1f : 0.05f) * (MathF.Sqrt((float)(-(float)relationWithClan)) / 10f);
				}
			}
			float num30 = 1f;
			if (mobileParty.MapFaction != null && mobileParty.MapFaction.IsKingdomFaction && mobileParty.MapFaction.Leader == Hero.MainHero && (missionType != Army.ArmyTypes.Defender || (targetSettlement.LastAttackerParty != null && targetSettlement.LastAttackerParty.MapFaction != Hero.MainHero.MapFaction)))
			{
				StanceLink stanceLink = (missionType != Army.ArmyTypes.Defender) ? Hero.MainHero.MapFaction.GetStanceWith(targetSettlement.MapFaction) : Hero.MainHero.MapFaction.GetStanceWith(targetSettlement.LastAttackerParty.MapFaction);
				if (stanceLink != null)
				{
					if (stanceLink.BehaviorPriority == 1)
					{
						if (missionType == Army.ArmyTypes.Besieger || missionType == Army.ArmyTypes.Raider)
						{
							num30 = 0.65f;
						}
						else if (missionType == Army.ArmyTypes.Defender)
						{
							num30 = 1.1f;
						}
					}
					else if (stanceLink.BehaviorPriority == 2 && (missionType == Army.ArmyTypes.Besieger || missionType == Army.ArmyTypes.Raider))
					{
						num30 = 1.3f;
					}
				}
			}
			float num31 = 1f;
			if (mobileParty.TargetSettlement == targetSettlement)
			{
				num31 = 1.3f;
				if (mobileParty.SiegeEvent != null && mobileParty.SiegeEvent.BesiegedSettlement == targetSettlement)
				{
					num31 = 4f;
				}
				if (mobileParty.MapEvent != null && mobileParty.MapEvent.IsRaid && mobileParty.MapEvent.MapEventSettlement == targetSettlement)
				{
					num31 = 1.5f;
				}
			}
			float num32 = 1f;
			if (mobileParty.SiegeEvent == null && targetSettlement.SiegeEvent != null && targetSettlement.SiegeEvent.BesiegerCamp.MapFaction == mobileParty.MapFaction)
			{
				float num33 = targetSettlement.SiegeEvent.BesiegerCamp.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Sum((PartyBase x) => x.EstimatedStrength);
				float num34 = targetSettlement.GetInvolvedPartiesForEventType(MapEvent.BattleTypes.Siege).Sum((PartyBase x) => x.EstimatedStrength);
				if (num34 > 0f)
				{
					float num35 = num33 / num34 - 5f;
					if (num35 > 0f)
					{
						num35 = MBMath.ClampFloat(num35, 0f, 1f);
						num32 = MBMath.Map(num35, 0f, 1f, 1f, 0f);
					}
				}
				else
				{
					num32 = 0f;
				}
			}
			float num36 = num29 * num6 * num25 * num26 * num27 * num30 * num31 * num32 * num23;
			if (mobileParty.Objective == MobileParty.PartyObjective.Defensive && missionType == Army.ArmyTypes.Defender)
			{
				num36 *= 1.2f;
			}
			else if (mobileParty.Objective == MobileParty.PartyObjective.Aggressive && (missionType == Army.ArmyTypes.Besieger || missionType == Army.ArmyTypes.Raider))
			{
				num36 *= 1.2f;
			}
			return (num36 < 0f) ? 0f : num36;
		}

		// Token: 0x040008F8 RID: 2296
		private const float SiegeBaseValueFactor = 0.8f;

		// Token: 0x040008F9 RID: 2297
		private const float RaidBaseValueFactor = 0.875f;

		// Token: 0x040008FA RID: 2298
		private const float DefenseBaseValueFactor = 1.75f;

		// Token: 0x040008FB RID: 2299
		private const float DefenseVillageBaseValueFactor = 1.28f;

		// Token: 0x040008FC RID: 2300
		private const float DefenseFollowEnemyBaseValueFactor = 0.8f;

		// Token: 0x040008FD RID: 2301
		private const float GiveUpDistanceLimitAsDays = 0.5f;
	}
}
