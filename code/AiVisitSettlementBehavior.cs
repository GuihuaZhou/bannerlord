using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors.AiBehaviors
{
	// Token: 0x02000471 RID: 1137
	public class AiVisitSettlementBehavior : CampaignBehaviorBase
	{
		// Token: 0x0600476F RID: 18287 RVA: 0x001680EF File Offset: 0x001662EF
		private static float GetMaximumDistanceAsDays(MobileParty.NavigationType navigationType)
		{
			return Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType) * 4f / (Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay);
		}

		// Token: 0x06004770 RID: 18288 RVA: 0x00168114 File Offset: 0x00166314
		private float MaximumMeaningfulDistanceAsDays(MobileParty.NavigationType navigationType)
		{
			return AiVisitSettlementBehavior.GetMaximumDistanceAsDays(navigationType) * 0.7f;
		}

		// Token: 0x17000E35 RID: 3637
		// (get) Token: 0x06004771 RID: 18289 RVA: 0x00168122 File Offset: 0x00166322
		private static float SearchForNeutralSettlementRadiusAsDays
		{
			get
			{
				return 0.5f;
			}
		}

		// Token: 0x17000E36 RID: 3638
		// (get) Token: 0x06004772 RID: 18290 RVA: 0x00168129 File Offset: 0x00166329
		private float NumberOfHoursAtDay
		{
			get
			{
				return (float)Campaign.Current.Models.CampaignTimeModel.HoursInDay;
			}
		}

		// Token: 0x17000E37 RID: 3639
		// (get) Token: 0x06004773 RID: 18291 RVA: 0x00168140 File Offset: 0x00166340
		private float IdealTimePeriodForVisitingOwnedSettlement
		{
			get
			{
				return (float)Campaign.Current.Models.CampaignTimeModel.HoursInDay * 15f;
			}
		}

		// Token: 0x06004774 RID: 18292 RVA: 0x0016815D File Offset: 0x0016635D
		public override void RegisterEvents()
		{
			CampaignEvents.AiHourlyTickEvent.AddNonSerializedListener(this, new Action<MobileParty, PartyThinkParams>(this.AiHourlyTick));
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
		}

		// Token: 0x06004775 RID: 18293 RVA: 0x0016818D File Offset: 0x0016638D
		private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
		{
			this._disbandPartyCampaignBehavior = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
		}

		// Token: 0x06004776 RID: 18294 RVA: 0x0016819F File Offset: 0x0016639F
		public override void SyncData(IDataStore dataStore)
		{
		}

		// Token: 0x06004777 RID: 18295 RVA: 0x001681A4 File Offset: 0x001663A4
		private void AiHourlyTick(MobileParty mobileParty, PartyThinkParams p)
		{
			Settlement currentSettlement = mobileParty.CurrentSettlement;
			if (((currentSettlement != null) ? currentSettlement.SiegeEvent : null) != null)
			{
				return;
			}
			Settlement currentSettlementOfMobilePartyForAICalculation = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(mobileParty);
			if (mobileParty.IsBandit)
			{
				this.CalculateVisitHideoutScoresForBanditParty(mobileParty, currentSettlementOfMobilePartyForAICalculation, p);
				return;
			}
			IFaction mapFaction = mobileParty.MapFaction;
			if (mobileParty.IsMilitia || mobileParty.IsCaravan || mobileParty.IsPatrolParty || mobileParty.IsVillager || (!mapFaction.IsMinorFaction && !mapFaction.IsKingdomFaction && (mobileParty.LeaderHero == null || !mobileParty.LeaderHero.IsLord)))
			{
				return;
			}
			if (mobileParty.Army == null || mobileParty.AttachedTo == null || mobileParty.Army.LeaderParty == mobileParty)
			{
				Hero leaderHero = mobileParty.LeaderHero;
				ValueTuple<float, float, int, int> valueTuple = this.CalculatePartyParameters(mobileParty);
				float item = valueTuple.Item1;
				float item2 = valueTuple.Item2;
				int item3 = valueTuple.Item3;
				int item4 = valueTuple.Item4;
				float num = item2 / Math.Min(1f, Math.Max(0.1f, item));
				float num2 = (num >= 1f) ? 0.33f : ((MathF.Max(1f, MathF.Min(2f, num)) - 0.5f) / 1.5f);
				float num3 = mobileParty.Food;
				float num4 = -mobileParty.FoodChange;
				int num5 = mobileParty.PartyTradeGold;
				if (mobileParty.Army != null && mobileParty == mobileParty.Army.LeaderParty)
				{
					foreach (MobileParty mobileParty2 in mobileParty.Army.LeaderParty.AttachedParties)
					{
						num3 += mobileParty2.Food;
						num4 += -mobileParty2.FoodChange;
						num5 += mobileParty2.PartyTradeGold;
					}
				}
				float num6 = 1f;
				if (leaderHero != null && mobileParty.IsLordParty)
				{
					num6 = this.CalculateSellItemScore(mobileParty);
				}
				int num7 = mobileParty.Party.PrisonerSizeLimit;
				if (mobileParty.Army != null)
				{
					foreach (MobileParty mobileParty3 in mobileParty.Army.LeaderParty.AttachedParties)
					{
						num7 += mobileParty3.Party.PrisonerSizeLimit;
					}
				}
				this._settlementsWithDistances.Clear();
				AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays(mobileParty, this._settlementsWithDistances);
				float num8 = PartyBaseHelper.FindPartySizeNormalLimit(mobileParty);
				float num9 = 2000f;
				float num10 = 2000f;
				if (leaderHero != null)
				{
					num9 = HeroHelper.StartRecruitingMoneyLimitForClanLeader(leaderHero);
					num10 = HeroHelper.StartRecruitingMoneyLimit(leaderHero);
				}
				float num11 = 0.2f;
				float num12 = 1f;
				foreach (KeyValuePair<ValueTuple<float, int>, ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>> keyValuePair in this._settlementsWithDistances)
				{
					Settlement item5 = keyValuePair.Value.Item1;
					MobileParty.NavigationType item6 = keyValuePair.Value.Item2;
					float item7 = keyValuePair.Key.Item1;
					bool item8 = keyValuePair.Value.Item3;
					bool item9 = keyValuePair.Value.Item4;
					float num13 = 1.6f;
					if (mobileParty.IsDisbanding)
					{
						goto IL_2F4;
					}
					IDisbandPartyCampaignBehavior disbandPartyCampaignBehavior = this._disbandPartyCampaignBehavior;
					if (disbandPartyCampaignBehavior != null && disbandPartyCampaignBehavior.IsPartyWaitingForDisband(mobileParty))
					{
						goto IL_2F4;
					}
					if (leaderHero == null)
					{
						bool flag;
						float visitingNearbySettlementScore = this.CalculateMergeScoreForLeaderlessParty(mobileParty, item5, item7, out flag);
						if (flag)
						{
							this.AddBehaviorTupleWithScore(p, item5, visitingNearbySettlementScore, item6, item8, item9);
						}
					}
					else
					{
						if (item7 >= this.MaximumMeaningfulDistanceAsDays(item6))
						{
							this.AddBehaviorTupleWithScore(p, item5, 0.025f, item6, item8, item9);
							continue;
						}
						float num14 = MathF.Max(num11, item7);
						float num15 = 1f;
						if (item7 > num11)
						{
							num15 = num12 / (num12 - num11 + item7);
						}
						float num16 = num15;
						if (item < 0.6f)
						{
							num16 = MathF.Pow(num15, MathF.Pow(0.6f / MathF.Max(0.15f, item), 0.3f));
						}
						float num17 = 1f;
						float num18 = (float)item3 / (float)item4;
						bool flag2 = mobileParty.Army != null && mobileParty.AttachedTo == null && mobileParty.Army.LeaderParty != mobileParty;
						if (item5.IsFortification && num18 > 0.2f)
						{
							num17 = MBMath.Map(num18 - 0.2f, 0f, 0.8f, 1f, 5f);
							if (flag2 || mobileParty.MapEvent != null || mobileParty.SiegeEvent != null)
							{
								num17 *= 0.6f;
							}
						}
						float num19 = 1f;
						if (mobileParty.DefaultBehavior == AiBehavior.GoToSettlement && ((item5 == currentSettlementOfMobilePartyForAICalculation && currentSettlementOfMobilePartyForAICalculation.IsFortification) || (currentSettlementOfMobilePartyForAICalculation == null && item5 == mobileParty.TargetSettlement)))
						{
							num19 = 1.2f;
						}
						else if (currentSettlementOfMobilePartyForAICalculation == null && item5 == mobileParty.LastVisitedSettlement)
						{
							num19 = 0.8f;
						}
						float num20 = (num18 > 0.2f) ? 1f : 0.16f;
						float num21 = Math.Max(0f, num3) / num4;
						if (num4 > 0f && (mobileParty.BesiegedSettlement == null || num21 <= 1f) && num5 > 100 && (item5.IsTown || (item5.IsVillage && mobileParty.Army == null)))
						{
							float neededFoodsInDaysThresholdForSiege = Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege;
							if (num21 < neededFoodsInDaysThresholdForSiege)
							{
								float num22 = (float)((int)(num4 * ((num21 < 1f && item5.IsVillage) ? Campaign.Current.Models.PartyFoodBuyingModel.MinimumDaysFoodToLastWhileBuyingFoodFromVillage : Campaign.Current.Models.PartyFoodBuyingModel.MinimumDaysFoodToLastWhileBuyingFoodFromTown)) + 1);
								float num23 = neededFoodsInDaysThresholdForSiege * 0.5f;
								float num24 = num23 - Math.Min(num23, Math.Max(0f, num21 - 1f));
								float num25 = num22 + 20f * (float)(item5.IsTown ? 2 : 1) * ((num14 > num12) ? 1f : (num14 / num12));
								int val = (int)((float)(num5 - 100) / Campaign.Current.Models.PartyFoodBuyingModel.LowCostFoodPriceAverage);
								num20 += num24 * num24 * 0.093f * ((num21 < num23) ? (15f + 0.5f * (num23 - num21)) : 1f) * Math.Min(num25, (float)Math.Min(val, item5.ItemRoster.TotalFood)) / num25;
							}
						}
						float num26 = 0f;
						float num27 = 1f;
						if (!item5.IsCastle && item < 1f && mobileParty.GetAvailableWageBudget() > 0)
						{
							int num28 = item5.NumberOfLordPartiesAt;
							int num29 = item5.NumberOfLordPartiesTargeting;
							if (currentSettlementOfMobilePartyForAICalculation == item5)
							{
								int num30 = num28;
								Army army = mobileParty.Army;
								num28 = num30 - ((army != null) ? army.LeaderPartyAndAttachedPartiesCount : 1);
								if (num28 < 0)
								{
									num28 = 0;
								}
							}
							if (mobileParty.TargetSettlement == item5 || (mobileParty.Army != null && mobileParty.Army.LeaderParty.TargetSettlement == item5))
							{
								int num31 = num29;
								Army army2 = mobileParty.Army;
								num29 = num31 - ((army2 != null) ? army2.LeaderPartyAndAttachedPartiesCount : 1);
								if (num29 < 0)
								{
									num29 = 0;
								}
							}
							if (mobileParty.Army != null)
							{
								num29 += mobileParty.Army.LeaderPartyAndAttachedPartiesCount;
							}
							if (!mobileParty.Party.IsStarving && (float)mobileParty.PartyTradeGold > num10 && (leaderHero.Clan.Leader == leaderHero || (float)leaderHero.Clan.Gold > num9) && num8 > mobileParty.PartySizeRatio)
							{
								ValueTuple<int, float> approximateVolunteersCanBeRecruitedDataFromSettlement = this.GetApproximateVolunteersCanBeRecruitedDataFromSettlement(leaderHero, item5);
								num26 = (float)approximateVolunteersCanBeRecruitedDataFromSettlement.Item1;
								if (num26 > 0f)
								{
									float item10 = approximateVolunteersCanBeRecruitedDataFromSettlement.Item2;
									num26 = Math.Min(num26, (float)MathF.Floor((float)mobileParty.GetAvailableWageBudget() / item10));
								}
							}
							float num32 = num26 * num15 / MathF.Sqrt((float)(1 + num28 + num29));
							float num33 = (num32 < 1f) ? num32 : ((float)Math.Pow((double)num32, (double)num2));
							num27 = Math.Max(Math.Min(1f, num20), Math.Max((mapFaction == item5.MapFaction) ? 0.25f : 0.16f, num * Math.Max(1f, Math.Min(2f, num)) * num33 * (1f - 0.9f * num18) * (1f - 0.9f * num18)));
						}
						num13 *= num27 * num17 * num20 * num16;
						if (num13 >= 8f)
						{
							this.AddBehaviorTupleWithScore(p, item5, num13, item6, item8, item9);
							break;
						}
						float num34 = 1f;
						if (num26 > 0f && !flag2)
						{
							num34 = 1f + ((mobileParty.DefaultBehavior == AiBehavior.GoToSettlement && item5 != currentSettlementOfMobilePartyForAICalculation && num14 < num11) ? (0.1f * MathF.Min(5f, num26) - 0.1f * MathF.Min(5f, num26) * (num14 / num11) * (num14 / num11)) : 0f);
						}
						float num35 = (item5.IsCastle && !flag2 && num20 < 1f) ? 1.4f : 1f;
						num13 *= (item5.IsTown ? num6 : 1f) * num34 * num35;
						if (num13 >= 8f)
						{
							this.AddBehaviorTupleWithScore(p, item5, num13, item6, item8, item9);
							break;
						}
						int num36 = mobileParty.PrisonRoster.TotalRegulars;
						if (mobileParty.PrisonRoster.TotalHeroes > 0)
						{
							foreach (TroopRosterElement troopRosterElement in mobileParty.PrisonRoster.GetTroopRoster())
							{
								if (troopRosterElement.Character.IsHero && troopRosterElement.Character.HeroObject.Clan.IsAtWarWith(item5.MapFaction))
								{
									num36 += 6;
								}
							}
						}
						float num37 = 1f;
						float num38 = 1f;
						if (mobileParty.Army != null && mobileParty.Army.LeaderParty.AttachedParties.Contains(mobileParty))
						{
							if (mobileParty.Army.LeaderParty != mobileParty)
							{
								num37 = ((float)mobileParty.Army.CohesionThresholdForDispersion - mobileParty.Army.Cohesion) / (float)mobileParty.Army.CohesionThresholdForDispersion;
							}
							num38 = ((MobileParty.MainParty != null && mobileParty.Army == MobileParty.MainParty.Army) ? 0.6f : 0.8f);
							foreach (MobileParty mobileParty4 in mobileParty.Army.LeaderParty.AttachedParties)
							{
								num36 += mobileParty4.PrisonRoster.TotalRegulars;
								if (mobileParty4.PrisonRoster.TotalHeroes > 0)
								{
									foreach (TroopRosterElement troopRosterElement2 in mobileParty4.PrisonRoster.GetTroopRoster())
									{
										if (troopRosterElement2.Character.IsHero && troopRosterElement2.Character.HeroObject.Clan.IsAtWarWith(item5.MapFaction))
										{
											num36 += 6;
										}
									}
								}
							}
						}
						float num39 = item5.IsFortification ? (1f + 2f * (float)(num36 / num7)) : 1f;
						float num40 = (mobileParty.DesiredAiNavigationType == item6) ? 1.5f : 1f;
						float num41 = 1f;
						float num42 = 1f;
						float num43 = 1f;
						float num44 = 1f;
						float num45 = 1f;
						if (num20 <= 0.5f)
						{
							ValueTuple<float, float, float, float> valueTuple2 = this.CalculateBeingSettlementOwnerScores(mobileParty, item5, currentSettlementOfMobilePartyForAICalculation, -1f, num15, item);
							num41 = valueTuple2.Item1;
							num42 = valueTuple2.Item2;
							num43 = valueTuple2.Item3;
							num44 = valueTuple2.Item4;
						}
						float num46 = 1f;
						if (item5.HasPort && mobileParty.Ships.Any<Ship>())
						{
							float num47 = mobileParty.Ships.AverageQ((Ship x) => x.HitPoints / x.MaxHitPoints);
							if (num47 < 0.8f)
							{
								if (num47 > 0.6f)
								{
									num46 = 1.5f;
								}
								else if (num47 > 0.4f)
								{
									num46 = 1.75f;
								}
								else
								{
									num46 = 3f;
								}
							}
						}
						num13 *= num45 * num19 * num37 * num39 * num38 * num41 * num43 * num42 * num44 * num40 * num46;
					}
					IL_C14:
					if (num13 > 0.025f)
					{
						this.AddBehaviorTupleWithScore(p, item5, num13, item6, item8, item9);
						continue;
					}
					continue;
					IL_2F4:
					float visitingNearbySettlementScore2 = this.CalculateMergeScoreForDisbandingParty(mobileParty, item5, item7);
					this.AddBehaviorTupleWithScore(p, item5, visitingNearbySettlementScore2, item6, item8, item9);
					goto IL_C14;
				}
			}
		}

		// Token: 0x06004778 RID: 18296 RVA: 0x00168E90 File Offset: 0x00167090
		private ValueTuple<int, float> GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero hero, Settlement settlement)
		{
			int num = 4;
			if (hero.MapFaction != settlement.MapFaction)
			{
				num = 2;
			}
			int num2 = 0;
			int num3 = 0;
			foreach (Hero hero2 in settlement.Notables)
			{
				if (hero2.IsAlive)
				{
					for (int i = 0; i < num; i++)
					{
						if (hero2.VolunteerTypes[i] != null)
						{
							num2++;
							num3 += Campaign.Current.Models.PartyWageModel.GetCharacterWage(hero2.VolunteerTypes[i]);
						}
					}
				}
			}
			if (num2 > 0)
			{
				num3 /= num2;
			}
			return new ValueTuple<int, float>(num2, (float)num3);
		}

		// Token: 0x06004779 RID: 18297 RVA: 0x00168F50 File Offset: 0x00167150
		private float CalculateSellItemScore(MobileParty mobileParty)
		{
			float num = 0f;
			float num2 = 0f;
			for (int i = 0; i < mobileParty.ItemRoster.Count; i++)
			{
				ItemRosterElement itemRosterElement = mobileParty.ItemRoster[i];
				if (itemRosterElement.EquipmentElement.Item.IsMountable)
				{
					num2 += (float)(itemRosterElement.Amount * itemRosterElement.EquipmentElement.Item.Value);
				}
				else if (!itemRosterElement.EquipmentElement.Item.IsFood)
				{
					num += (float)(itemRosterElement.Amount * itemRosterElement.EquipmentElement.Item.Value);
				}
			}
			float num3 = (num2 > (float)mobileParty.PartyTradeGold * 0.1f) ? MathF.Min(3f, MathF.Pow((num2 + 1000f) / ((float)mobileParty.PartyTradeGold * 0.1f + 1000f), 0.33f)) : 1f;
			float num4 = 1f + MathF.Min(3f, MathF.Pow(num / (((float)mobileParty.MemberRoster.TotalManCount + 5f) * 100f), 0.33f));
			float num5 = num3 * num4;
			if (mobileParty.Army != null)
			{
				num5 = MathF.Sqrt(num5);
			}
			return num5;
		}

		// Token: 0x0600477A RID: 18298 RVA: 0x00169098 File Offset: 0x00167298
		private ValueTuple<float, float, int, int> CalculatePartyParameters(MobileParty mobileParty)
		{
			float num = 0f;
			int num2 = 0;
			int num3 = 0;
			float item;
			if (mobileParty.Army != null && (mobileParty.AttachedTo != null || mobileParty.Army.LeaderParty == mobileParty))
			{
				float num4 = 0f;
				foreach (MobileParty mobileParty2 in mobileParty.AttachedParties)
				{
					float partySizeRatio = mobileParty2.PartySizeRatio;
					num4 += partySizeRatio;
					num2 += mobileParty2.MemberRoster.TotalWounded;
					num3 += mobileParty2.MemberRoster.TotalManCount;
					float num5 = PartyBaseHelper.FindPartySizeNormalLimit(mobileParty2);
					num += num5;
				}
				item = num4 / (float)mobileParty.Army.Parties.Count;
				num /= (float)mobileParty.Army.Parties.Count;
			}
			else
			{
				item = mobileParty.PartySizeRatio;
				num2 += mobileParty.MemberRoster.TotalWounded;
				num3 += mobileParty.MemberRoster.TotalManCount;
				num += PartyBaseHelper.FindPartySizeNormalLimit(mobileParty);
			}
			return new ValueTuple<float, float, int, int>(item, num, num2, num3);
		}

		// Token: 0x0600477B RID: 18299 RVA: 0x001691B8 File Offset: 0x001673B8
		private void CalculateVisitHideoutScoresForBanditParty(MobileParty mobileParty, Settlement currentSettlement, PartyThinkParams p)
		{
			if (!mobileParty.MapFaction.Culture.CanHaveSettlement)
			{
				return;
			}
			if (currentSettlement != null && currentSettlement.IsHideout)
			{
				return;
			}
			int num = 0;
			for (int i = 0; i < mobileParty.ItemRoster.Count; i++)
			{
				ItemRosterElement itemRosterElement = mobileParty.ItemRoster[i];
				num += itemRosterElement.Amount * itemRosterElement.EquipmentElement.Item.Value;
			}
			float num2 = 1f + 4f * Math.Min((float)num, 1000f) / 1000f;
			int num3 = 0;
			MBReadOnlyList<Hideout> allHideouts = Campaign.Current.AllHideouts;
			foreach (Hideout hideout in allHideouts)
			{
				if (hideout.Settlement.Culture == mobileParty.Party.Culture && hideout.IsInfested)
				{
					num3++;
				}
			}
			float num4 = 1f + 4f * (float)Math.Sqrt((double)(mobileParty.PrisonRoster.TotalManCount / mobileParty.Party.PrisonerSizeLimit));
			int numberOfMinimumBanditPartiesInAHideoutToInfestIt = Campaign.Current.Models.BanditDensityModel.NumberOfMinimumBanditPartiesInAHideoutToInfestIt;
			int numberOfMaximumBanditPartiesInEachHideout = Campaign.Current.Models.BanditDensityModel.NumberOfMaximumBanditPartiesInEachHideout;
			int numberOfMaximumHideoutsAtEachBanditFaction = Campaign.Current.Models.BanditDensityModel.NumberOfMaximumHideoutsAtEachBanditFaction;
			foreach (Hideout hideout2 in allHideouts)
			{
				Settlement settlement = hideout2.Settlement;
				if (settlement.Party.MapEvent == null && settlement.Culture == mobileParty.Party.Culture)
				{
					bool isTargetingPort = false;
					MobileParty.NavigationType navigationType;
					float num5;
					bool flag;
					AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mobileParty, settlement, isTargetingPort, out navigationType, out num5, out flag);
					if (navigationType != MobileParty.NavigationType.None)
					{
						float averageDistanceBetweenClosestTwoTownsWithNavigationType = Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(navigationType);
						float num6 = averageDistanceBetweenClosestTwoTownsWithNavigationType * 6f / (Campaign.Current.EstimatedAverageBanditPartySpeed * (float)CampaignTime.HoursInDay);
						num5 = Math.Max(averageDistanceBetweenClosestTwoTownsWithNavigationType * 0.15f, num5);
						float num7 = num5 / (Campaign.Current.EstimatedAverageBanditPartySpeed * (float)CampaignTime.HoursInDay);
						float num8 = num6 / (num6 + num7);
						int num9 = 0;
						foreach (MobileParty mobileParty2 in settlement.Parties)
						{
							if (mobileParty2.IsBandit && !mobileParty2.IsBanditBossParty)
							{
								num9++;
							}
						}
						float num11;
						if (num9 < numberOfMinimumBanditPartiesInAHideoutToInfestIt)
						{
							float num10 = (float)(numberOfMaximumHideoutsAtEachBanditFaction - num3) / (float)numberOfMaximumHideoutsAtEachBanditFaction;
							num11 = ((num3 < numberOfMaximumHideoutsAtEachBanditFaction) ? (0.25f + 0.75f * num10) : 0f);
						}
						else
						{
							num11 = Math.Max(0f, 1f * (1f - (float)(Math.Min(numberOfMaximumBanditPartiesInEachHideout, num9) - numberOfMinimumBanditPartiesInAHideoutToInfestIt) / (float)(numberOfMaximumBanditPartiesInEachHideout - numberOfMinimumBanditPartiesInAHideoutToInfestIt)));
						}
						float num12 = (mobileParty.DefaultBehavior == AiBehavior.GoToSettlement && mobileParty.TargetSettlement == settlement) ? 1f : (MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat * MBRandom.RandomFloat);
						float num13 = num8 * num11 * num2 * num12 * num4;
						if (num13 > 0f)
						{
							this.AddBehaviorTupleWithScore(p, hideout2.Settlement, num13, navigationType, false, false);
						}
					}
				}
			}
		}

		// Token: 0x0600477C RID: 18300 RVA: 0x0016955C File Offset: 0x0016775C
		private ValueTuple<float, float, float, float> CalculateBeingSettlementOwnerScores(MobileParty mobileParty, Settlement settlement, Settlement currentSettlement, float idealGarrisonStrengthPerWalledCenter, float distanceScorePure, float averagePartySizeRatioToMaximumSize)
		{
			float num = 1f;
			float num2 = 1f;
			float num3 = 1f;
			float item = 1f;
			Hero leaderHero = mobileParty.LeaderHero;
			IFaction mapFaction = mobileParty.MapFaction;
			if (currentSettlement != settlement && (mobileParty.Army == null || mobileParty.Army.LeaderParty != mobileParty))
			{
				if (settlement.OwnerClan.Leader == leaderHero)
				{
					float currentTime = Campaign.CurrentTime;
					float lastVisitTimeOfOwner = settlement.LastVisitTimeOfOwner;
					float num4 = ((currentTime - lastVisitTimeOfOwner > this.NumberOfHoursAtDay) ? (currentTime - lastVisitTimeOfOwner) : ((this.NumberOfHoursAtDay - (currentTime - lastVisitTimeOfOwner)) * (this.IdealTimePeriodForVisitingOwnedSettlement / this.NumberOfHoursAtDay))) / this.IdealTimePeriodForVisitingOwnedSettlement;
					num += num4;
				}
				if (MBRandom.RandomFloatWithSeed((uint)mobileParty.RandomValue, (uint)CampaignTime.Now.ToDays) < 0.5f && settlement.IsFortification && leaderHero.Clan != Clan.PlayerClan && (settlement.OwnerClan.Leader == leaderHero || settlement.OwnerClan == leaderHero.Clan))
				{
					if (idealGarrisonStrengthPerWalledCenter == -1f)
					{
						idealGarrisonStrengthPerWalledCenter = FactionHelper.FindIdealGarrisonStrengthPerWalledCenter(mapFaction as Kingdom, null);
					}
					int num5 = Campaign.Current.Models.SettlementGarrisonModel.FindNumberOfTroopsToTakeFromGarrison(mobileParty, settlement, idealGarrisonStrengthPerWalledCenter);
					if (num5 > 0)
					{
						num2 = 1f + MathF.Pow((float)num5, 0.67f);
						if (mobileParty.Army != null && mobileParty.Army.LeaderParty == mobileParty)
						{
							num2 = 1f + (num2 - 1f) / MathF.Sqrt((float)mobileParty.Army.Parties.Count);
						}
					}
				}
			}
			if (settlement == leaderHero.HomeSettlement && mobileParty.Army == null && !settlement.IsVillage)
			{
				float num6 = leaderHero.HomeSettlement.IsCastle ? 1.5f : 1f;
				if (currentSettlement == settlement)
				{
					num3 += 3000f * num6 / (250f + leaderHero.PassedTimeAtHomeSettlement * leaderHero.PassedTimeAtHomeSettlement);
				}
				else
				{
					num3 += 1000f * num6 / (250f + leaderHero.PassedTimeAtHomeSettlement * leaderHero.PassedTimeAtHomeSettlement);
				}
			}
			if (settlement != currentSettlement)
			{
				float num7 = 1f;
				if (mobileParty.LastVisitedSettlement == settlement)
				{
					num7 = 0.25f;
				}
				if (settlement.IsFortification && settlement.MapFaction == mapFaction && settlement.OwnerClan != Clan.PlayerClan)
				{
					float num8 = (settlement.Town.GarrisonParty != null) ? settlement.Town.GarrisonParty.Party.EstimatedStrength : 0f;
					float num9 = FactionHelper.OwnerClanEconomyEffectOnGarrisonSizeConstant(settlement.OwnerClan);
					float num10 = FactionHelper.SettlementProsperityEffectOnGarrisonSizeConstant(settlement.Town);
					float num11 = FactionHelper.SettlementFoodPotentialEffectOnGarrisonSizeConstant(settlement);
					if (idealGarrisonStrengthPerWalledCenter == -1f)
					{
						idealGarrisonStrengthPerWalledCenter = FactionHelper.FindIdealGarrisonStrengthPerWalledCenter(mapFaction as Kingdom, null);
					}
					float num12 = idealGarrisonStrengthPerWalledCenter;
					if (settlement.Town.GarrisonParty != null && settlement.Town.GarrisonParty.HasLimitedWage())
					{
						num12 = (float)settlement.Town.GarrisonParty.PaymentLimit / Campaign.Current.AverageWage;
					}
					else
					{
						if (mobileParty.Army != null)
						{
							num12 *= 0.75f;
						}
						num12 *= num9 * num10 * num11;
					}
					float num13 = num12;
					if (num8 < num13)
					{
						float num14 = (settlement.OwnerClan == leaderHero.Clan) ? 149f : 99f;
						if (settlement.OwnerClan == Clan.PlayerClan)
						{
							num14 *= 0.5f;
						}
						float num15 = 1f - num8 / num13;
						item = 1f + num14 * distanceScorePure * distanceScorePure * (averagePartySizeRatioToMaximumSize - 0.5f) * num15 * num15 * num15 * num7;
					}
				}
			}
			return new ValueTuple<float, float, float, float>(num, num2, num3, item);
		}

		// Token: 0x0600477D RID: 18301 RVA: 0x001698F8 File Offset: 0x00167AF8
		private float CalculateMergeScoreForDisbandingParty(MobileParty disbandParty, Settlement settlement, float distanceAsDays)
		{
			float num = Campaign.MapDiagonal / (disbandParty._lastCalculatedSpeed * (float)CampaignTime.HoursInDay);
			float num2 = MathF.Pow(3.5f - 0.95f * (Math.Min(num, distanceAsDays) / num), 3f);
			Hero owner = disbandParty.Party.Owner;
			float num3;
			if (((owner != null) ? owner.Clan : null) != settlement.OwnerClan)
			{
				Hero owner2 = disbandParty.Party.Owner;
				num3 = ((((owner2 != null) ? owner2.MapFaction : null) == settlement.MapFaction) ? 0.35f : 0.025f);
			}
			else
			{
				num3 = 1f;
			}
			float num4 = num3;
			float num5 = (disbandParty.DefaultBehavior == AiBehavior.GoToSettlement && disbandParty.TargetSettlement == settlement) ? 1f : 0.3f;
			float num6 = settlement.IsFortification ? 3f : 1f;
			float num7 = num2 * num4 * num5 * num6;
			if (num7 < 0.025f)
			{
				num7 = 0.035f;
			}
			return num7;
		}

		// Token: 0x0600477E RID: 18302 RVA: 0x001699D8 File Offset: 0x00167BD8
		private float CalculateMergeScoreForLeaderlessParty(MobileParty leaderlessParty, Settlement settlement, float distanceAsDays, out bool canMerge)
		{
			if (settlement.IsVillage)
			{
				canMerge = false;
				return -1f;
			}
			float num = Campaign.MapDiagonal / (leaderlessParty._lastCalculatedSpeed * (float)CampaignTime.HoursInDay);
			float num2 = MathF.Pow(3.5f - 0.95f * (Math.Min(num, distanceAsDays) / num), 3f);
			float num3;
			if (leaderlessParty.ActualClan != settlement.OwnerClan)
			{
				Clan actualClan = leaderlessParty.ActualClan;
				num3 = ((((actualClan != null) ? actualClan.MapFaction : null) == settlement.MapFaction) ? 0.35f : 0f);
			}
			else
			{
				num3 = 2f;
			}
			float num4 = num3;
			float num5 = (leaderlessParty.DefaultBehavior == AiBehavior.GoToSettlement && leaderlessParty.TargetSettlement == settlement) ? 1f : 0.3f;
			float num6 = settlement.IsFortification ? 3f : 0.5f;
			canMerge = true;
			return num2 * num4 * num5 * num6;
		}

		// Token: 0x0600477F RID: 18303 RVA: 0x00169AA4 File Offset: 0x00167CA4
		private static void FillSettlementsToVisitWithDistancesAsDays(MobileParty mobileParty, SortedDictionary<ValueTuple<float, int>, ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>> listToFill)
		{
			float num = AiVisitSettlementBehavior.SearchForNeutralSettlementRadiusAsDays * Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay;
			if (mobileParty.LeaderHero != null && mobileParty.LeaderHero.MapFaction.IsKingdomFaction)
			{
				List<Settlement> settlements = mobileParty.MapFaction.Settlements;
				float num2 = 0f;
				foreach (Settlement settlement in settlements)
				{
					if (AiVisitSettlementBehavior.IsSettlementSuitableForVisitingCondition(mobileParty, settlement))
					{
						MobileParty.NavigationType navigationType;
						float num3;
						bool item;
						bool item2;
						AiVisitSettlementBehavior.GetBestNavigationDataForVisitingSettlement(mobileParty, settlement, out navigationType, out num3, out item, out item2);
						if (navigationType != MobileParty.NavigationType.None && num3 < AiVisitSettlementBehavior.GetMaximumDistanceAsDays(navigationType))
						{
							num2 += num3;
							listToFill.Add(new ValueTuple<float, int>(num3, settlement.GetHashCode()), new ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>(settlement, navigationType, item, item2));
						}
					}
				}
				num2 /= (float)listToFill.Count;
				if (num2 > AiVisitSettlementBehavior.GetMaximumDistanceAsDays(mobileParty.NavigationCapability) * 0.7f && (mobileParty.Army == null || mobileParty.Army.LeaderParty == mobileParty))
				{
					LocatableSearchData<Settlement> locatableSearchData = Settlement.StartFindingLocatablesAroundPosition(mobileParty.Position.ToVec2(), num);
					for (Settlement settlement2 = Settlement.FindNextLocatable(ref locatableSearchData); settlement2 != null; settlement2 = Settlement.FindNextLocatable(ref locatableSearchData))
					{
						if (!settlement2.IsCastle && settlement2.MapFaction != mobileParty.MapFaction && AiVisitSettlementBehavior.IsSettlementSuitableForVisitingCondition(mobileParty, settlement2))
						{
							MobileParty.NavigationType navigationType2;
							float num4;
							bool item3;
							bool item4;
							AiVisitSettlementBehavior.GetBestNavigationDataForVisitingSettlement(mobileParty, settlement2, out navigationType2, out num4, out item3, out item4);
							if (navigationType2 != MobileParty.NavigationType.None && num4 < AiVisitSettlementBehavior.GetMaximumDistanceAsDays(navigationType2))
							{
								listToFill.Add(new ValueTuple<float, int>(num4, settlement2.GetHashCode()), new ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>(settlement2, navigationType2, item3, item4));
							}
						}
					}
				}
			}
			else
			{
				LocatableSearchData<Settlement> locatableSearchData2 = Settlement.StartFindingLocatablesAroundPosition(mobileParty.Position.ToVec2(), num * 1.6f);
				for (Settlement settlement3 = Settlement.FindNextLocatable(ref locatableSearchData2); settlement3 != null; settlement3 = Settlement.FindNextLocatable(ref locatableSearchData2))
				{
					if (AiVisitSettlementBehavior.IsSettlementSuitableForVisitingCondition(mobileParty, settlement3))
					{
						MobileParty.NavigationType navigationType3;
						float num5;
						bool item5;
						bool item6;
						AiVisitSettlementBehavior.GetBestNavigationDataForVisitingSettlement(mobileParty, settlement3, out navigationType3, out num5, out item5, out item6);
						if (navigationType3 != MobileParty.NavigationType.None && num5 < AiVisitSettlementBehavior.GetMaximumDistanceAsDays(navigationType3))
						{
							listToFill.Add(new ValueTuple<float, int>(num5, settlement3.GetHashCode()), new ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>(settlement3, navigationType3, item5, item6));
						}
					}
				}
			}
			if (!listToFill.AnyQ<KeyValuePair<ValueTuple<float, int>, ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>>>())
			{
				Settlement factionMidSettlement = mobileParty.MapFaction.FactionMidSettlement;
				if (factionMidSettlement != null)
				{
					if (factionMidSettlement.IsFortification)
					{
						using (List<Village>.Enumerator enumerator2 = factionMidSettlement.BoundVillages.GetEnumerator())
						{
							while (enumerator2.MoveNext())
							{
								Village village = enumerator2.Current;
								if (AiVisitSettlementBehavior.IsSettlementSuitableForVisitingCondition(mobileParty, village.Settlement))
								{
									MobileParty.NavigationType navigationType4;
									float item7;
									bool item8;
									bool item9;
									AiVisitSettlementBehavior.GetBestNavigationDataForVisitingSettlement(mobileParty, village.Settlement, out navigationType4, out item7, out item8, out item9);
									if (navigationType4 != MobileParty.NavigationType.None)
									{
										listToFill.Add(new ValueTuple<float, int>(item7, village.GetHashCode()), new ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>(village.Settlement, navigationType4, item8, item9));
									}
								}
							}
							return;
						}
					}
					if (AiVisitSettlementBehavior.IsSettlementSuitableForVisitingCondition(mobileParty, factionMidSettlement))
					{
						MobileParty.NavigationType navigationType5;
						float item10;
						bool item11;
						bool item12;
						AiVisitSettlementBehavior.GetBestNavigationDataForVisitingSettlement(mobileParty, factionMidSettlement, out navigationType5, out item10, out item11, out item12);
						if (navigationType5 != MobileParty.NavigationType.None)
						{
							listToFill.Add(new ValueTuple<float, int>(item10, factionMidSettlement.GetHashCode()), new ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>(factionMidSettlement, navigationType5, item11, item12));
						}
					}
				}
			}
		}

		// Token: 0x06004780 RID: 18304 RVA: 0x00169DC0 File Offset: 0x00167FC0
		private static void GetBestNavigationDataForVisitingSettlement(MobileParty mobileParty, Settlement settlement, out MobileParty.NavigationType bestNavigationType, out float distanceAsDays, out bool isFromPort, out bool isTargetingPortBetter)
		{
			bestNavigationType = MobileParty.NavigationType.None;
			float num = float.MaxValue;
			bool flag = false;
			isTargetingPortBetter = false;
			isFromPort = false;
			if (!settlement.HasPort || settlement.SiegeEvent == null || settlement.SiegeEvent.IsBlockadeActive || !mobileParty.HasNavalNavigationCapability)
			{
				AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mobileParty, settlement, false, out bestNavigationType, out num, out flag);
			}
			if (mobileParty.HasNavalNavigationCapability && settlement.HasPort)
			{
				MobileParty.NavigationType navigationType;
				float num2;
				bool flag2;
				AiHelper.GetBestNavigationTypeAndAdjustedDistanceOfSettlementForMobileParty(mobileParty, settlement, true, out navigationType, out num2, out flag2);
				if (num2 < num)
				{
					bestNavigationType = navigationType;
					num = num2;
					isFromPort = flag2;
					isTargetingPortBetter = true;
				}
				else
				{
					isFromPort = flag;
					isTargetingPortBetter = false;
				}
			}
			distanceAsDays = num / (Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay);
		}

		// Token: 0x06004781 RID: 18305 RVA: 0x00169E64 File Offset: 0x00168064
		private void AddBehaviorTupleWithScore(PartyThinkParams p, Settlement settlement, float visitingNearbySettlementScore, MobileParty.NavigationType navigationType, bool isFromPort, bool isTargetingPortBetter)
		{
			AIBehaviorData item = new AIBehaviorData(settlement, AiBehavior.GoToSettlement, navigationType, false, isFromPort, isTargetingPortBetter);
			float num;
			if (p.TryGetBehaviorScore(item, out num))
			{
				p.SetBehaviorScore(item, num + visitingNearbySettlementScore);
				return;
			}
			ValueTuple<AIBehaviorData, float> valueTuple = new ValueTuple<AIBehaviorData, float>(item, visitingNearbySettlementScore);
			p.AddBehaviorScore(valueTuple);
		}

		// Token: 0x06004782 RID: 18306 RVA: 0x00169EAC File Offset: 0x001680AC
		private static bool IsSettlementSuitableForVisitingCondition(MobileParty mobileParty, Settlement settlement)
		{
			return settlement.Party.MapEvent == null && (settlement.Party.SiegeEvent == null || (!settlement.Party.SiegeEvent.IsBlockadeActive && mobileParty.HasNavalNavigationCapability)) && (!mobileParty.Party.Owner.MapFaction.IsAtWarWith(settlement.MapFaction) || ((mobileParty.Party.Owner.MapFaction.IsMinorFaction || mobileParty.MapFaction.Settlements.Count == 0) && settlement.IsVillage)) && (settlement.IsVillage || settlement.IsFortification) && (!settlement.IsVillage || settlement.Village.VillageState == Village.VillageStates.Normal);
		}

		// Token: 0x040013A6 RID: 5030
		public const float GoodEnoughScore = 8f;

		// Token: 0x040013A7 RID: 5031
		public const float MeaningfulScoreThreshold = 0.025f;

		// Token: 0x040013A8 RID: 5032
		public const float BaseVisitScore = 1.6f;

		// Token: 0x040013A9 RID: 5033
		private const float DefaultMoneyLimitForRecruiting = 2000f;

		// Token: 0x040013AA RID: 5034
		private SortedDictionary<ValueTuple<float, int>, ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>> _settlementsWithDistances = new SortedDictionary<ValueTuple<float, int>, ValueTuple<Settlement, MobileParty.NavigationType, bool, bool>>();

		// Token: 0x040013AB RID: 5035
		private IDisbandPartyCampaignBehavior _disbandPartyCampaignBehavior;
	}
}
