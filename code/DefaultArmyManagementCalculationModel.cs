using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x020000F1 RID: 241
	public class DefaultArmyManagementCalculationModel : ArmyManagementCalculationModel
	{
		// Token: 0x17000614 RID: 1556
		// (get) Token: 0x06001639 RID: 5689 RVA: 0x00065FEC File Offset: 0x000641EC
		public override float AIMobilePartySizeRatioToCallToArmy
		{
			get
			{
				return 0.6f;
			}
		}

		// Token: 0x17000615 RID: 1557
		// (get) Token: 0x0600163A RID: 5690 RVA: 0x00065FF3 File Offset: 0x000641F3
		public override float PlayerMobilePartySizeRatioToCallToArmy
		{
			get
			{
				return 0.4f;
			}
		}

		// Token: 0x17000616 RID: 1558
		// (get) Token: 0x0600163B RID: 5691 RVA: 0x00065FFA File Offset: 0x000641FA
		public override float MinimumNeededFoodInDaysToCallToArmy
		{
			get
			{
				return 15f;
			}
		}

		// Token: 0x17000617 RID: 1559
		// (get) Token: 0x0600163C RID: 5692 RVA: 0x00066001 File Offset: 0x00064201
		public override float MaximumDistanceToCallToArmy
		{
			get
			{
				return Campaign.Current.GetAverageDistanceBetweenClosestTwoTownsWithNavigationType(MobileParty.NavigationType.All) * 8f;
			}
		}

		// Token: 0x17000618 RID: 1560
		// (get) Token: 0x0600163D RID: 5693 RVA: 0x00066014 File Offset: 0x00064214
		public override int InfluenceValuePerGold
		{
			get
			{
				return 40;
			}
		}

		// Token: 0x17000619 RID: 1561
		// (get) Token: 0x0600163E RID: 5694 RVA: 0x00066018 File Offset: 0x00064218
		public override int AverageCallToArmyCost
		{
			get
			{
				return 20;
			}
		}

		// Token: 0x1700061A RID: 1562
		// (get) Token: 0x0600163F RID: 5695 RVA: 0x0006601C File Offset: 0x0006421C
		public override int CohesionThresholdForDispersion
		{
			get
			{
				return 10;
			}
		}

		// Token: 0x1700061B RID: 1563
		// (get) Token: 0x06001640 RID: 5696 RVA: 0x00066020 File Offset: 0x00064220
		public override float MaximumWaitTime
		{
			get
			{
				return (float)CampaignTime.HoursInDay * 3f;
			}
		}

		// Token: 0x06001641 RID: 5697 RVA: 0x00066030 File Offset: 0x00064230
		public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
		{
			float num = (armyMemberParty.Party.EstimatedStrength + 20f) / 200f;
			if (PartyBaseHelper.HasFeat(armyMemberParty.Party, DefaultCulturalFeats.EmpireArmyInfluenceFeat))
			{
				num += num * DefaultCulturalFeats.EmpireArmyInfluenceFeat.EffectBonus;
			}
			return num;
		}

		// Token: 0x06001642 RID: 5698 RVA: 0x00066078 File Offset: 0x00064278
		public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
		{
			if (armyLeaderParty.LeaderHero != null && party.LeaderHero != null && armyLeaderParty.LeaderHero.Clan == party.LeaderHero.Clan)
			{
				return 0;
			}
			float num = (float)armyLeaderParty.LeaderHero.GetRelation(party.LeaderHero);
			float partySizeScore = this.GetPartySizeScore(party);
			float b = (float)MathF.Round(party.Party.EstimatedStrength);
			float num2 = armyLeaderParty.IsMainParty ? Campaign.Current.Models.ArmyManagementCalculationModel.PlayerMobilePartySizeRatioToCallToArmy : Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy;
			float num3 = (num < 0f) ? (1f + MathF.Sqrt(MathF.Abs(MathF.Max(-100f, num))) / 10f) : (1f - MathF.Sqrt(MathF.Abs(MathF.Min(100f, num))) / 20f);
			float num4 = 0.5f + MathF.Min(1000f, b) / 100f;
			float num5 = 0.5f + 1f * (1f - (partySizeScore - num2) / (1f - num2));
			float num6;
			float distanceBetweenMobilePartyToMobileParty = DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(party, armyLeaderParty, party.NavigationCapability, out num6);
			float num7 = 1f + 1f * MathF.Pow(MathF.Min(Campaign.MapDiagonal * 10f, MathF.Max(1f, distanceBetweenMobilePartyToMobileParty)) / Campaign.MapDiagonal, 0.67f);
			float num8 = (party.LeaderHero != null) ? party.LeaderHero.RandomFloat(0.75f, 1.25f) : 1f;
			float num9 = 1f;
			float num10 = 1f;
			float num11 = 1f;
			Hero leaderHero = armyLeaderParty.LeaderHero;
			if (((leaderHero != null) ? leaderHero.Clan.Kingdom : null) != null)
			{
				if (armyLeaderParty.LeaderHero.Clan.Tier >= 5 && armyLeaderParty.LeaderHero.Clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.Marshals))
				{
					num9 -= 0.1f;
				}
				if (armyLeaderParty.LeaderHero.Clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.RoyalCommissions))
				{
					if (armyLeaderParty.LeaderHero == armyLeaderParty.LeaderHero.Clan.Kingdom.Leader)
					{
						num9 -= 0.3f;
					}
					else
					{
						num9 += 0.1f;
					}
				}
				if (party.LeaderHero != null)
				{
					if (armyLeaderParty.LeaderHero.Clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.LordsPrivyCouncil) && party.LeaderHero.Clan.Tier <= 4)
					{
						num9 += 0.2f;
					}
					if (armyLeaderParty.LeaderHero.Clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.Senate) && party.LeaderHero.Clan.Tier <= 2)
					{
						num9 += 0.1f;
					}
				}
				if (armyLeaderParty.LeaderHero.GetPerkValue(DefaultPerks.Leadership.InspiringLeader))
				{
					num10 += DefaultPerks.Leadership.InspiringLeader.PrimaryBonus;
				}
				if (armyLeaderParty.LeaderHero.GetPerkValue(DefaultPerks.Tactics.CallToArms))
				{
					num10 += DefaultPerks.Tactics.CallToArms.SecondaryBonus;
				}
			}
			if (PartyBaseHelper.HasFeat(armyLeaderParty.Party, DefaultCulturalFeats.VlandianArmyInfluenceFeat))
			{
				num11 += DefaultCulturalFeats.VlandianArmyInfluenceFeat.EffectBonus;
			}
			if (PartyBaseHelper.HasFeat(armyLeaderParty.Party, DefaultCulturalFeats.SturgianArmyInfluenceCostFeat))
			{
				num11 += DefaultCulturalFeats.SturgianArmyInfluenceCostFeat.EffectBonus;
			}
			return (int)(0.65f * num3 * num4 * num8 * num7 * num5 * num9 * num10 * num11 * (float)this.AverageCallToArmyCost);
		}

		// Token: 0x06001643 RID: 5699 RVA: 0x000663F8 File Offset: 0x000645F8
		public override bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)
		{
			possibleArmyMembers = new MBList<MobileParty>();
			Kingdom kingdom = mobileParty.MapFaction as Kingdom;
			if (!mobileParty.IsCurrentlyAtSea && mobileParty.LeaderHero.Clan.Influence > 100f && !mobileParty.LeaderHero.Clan.IsUnderMercenaryService && (float)mobileParty.GetNumDaysForFoodToLast() > Campaign.Current.Models.MobilePartyAIModel.NeededFoodsInDaysThresholdForSiege)
			{
				if (kingdom.FactionsAtWarWith.AnyQ((IFaction x) => x.Fiefs.Any<Town>()) && mobileParty.PartySizeRatio > Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy && (mobileParty.LeaderHero.Clan.Leader == mobileParty.LeaderHero || (mobileParty.LeaderHero.Clan.Leader.PartyBelongedTo == null && mobileParty.LeaderHero.Clan.WarPartyComponents != null && mobileParty.LeaderHero.Clan.WarPartyComponents.FirstOrDefault<WarPartyComponent>() == mobileParty.WarPartyComponent)))
				{
					this.GetInfluenceBudgetWhileCreatingArmy(mobileParty);
					List<ValueTuple<MobileParty, float, int>> list = new List<ValueTuple<MobileParty, float, int>>();
					foreach (WarPartyComponent warPartyComponent in mobileParty.MapFaction.WarPartyComponents)
					{
						MobileParty mobileParty2 = warPartyComponent.MobileParty;
						Hero leaderHero = mobileParty2.LeaderHero;
						if (mobileParty2.IsLordParty && mobileParty2.Army == null && mobileParty2 != mobileParty && leaderHero != null && !mobileParty2.IsMainParty && leaderHero != leaderHero.MapFaction.Leader && !mobileParty2.Ai.DoNotMakeNewDecisions)
						{
							Settlement currentSettlement = mobileParty2.CurrentSettlement;
							if (((currentSettlement != null) ? currentSettlement.SiegeEvent : null) == null && !mobileParty2.IsDisbanding && (float)mobileParty2.GetNumDaysForFoodToLast() > Campaign.Current.Models.ArmyManagementCalculationModel.MinimumNeededFoodInDaysToCallToArmy && mobileParty2.PartySizeRatio > Campaign.Current.Models.ArmyManagementCalculationModel.AIMobilePartySizeRatioToCallToArmy && leaderHero.CanLeadParty() && !mobileParty2.IsInRaftState && mobileParty2.MapEvent == null && mobileParty2.BesiegedSettlement == null)
							{
								IDisbandPartyCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
								if (campaignBehavior == null || !campaignBehavior.IsPartyWaitingForDisband(mobileParty2))
								{
									float maximumDistanceToCallToArmy = Campaign.Current.Models.ArmyManagementCalculationModel.MaximumDistanceToCallToArmy;
									float num;
									if (DistanceHelper.GetDistanceBetweenMobilePartyToMobileParty(mobileParty2, mobileParty, mobileParty2.NavigationCapability, out num) < maximumDistanceToCallToArmy)
									{
										bool flag = false;
										using (List<ValueTuple<MobileParty, float, int>>.Enumerator enumerator2 = list.GetEnumerator())
										{
											while (enumerator2.MoveNext())
											{
												if (enumerator2.Current.Item1 == mobileParty2)
												{
													flag = true;
													break;
												}
											}
										}
										if (!flag)
										{
											int num2 = Campaign.Current.Models.ArmyManagementCalculationModel.CalculatePartyInfluenceCost(mobileParty, mobileParty2);
											float estimatedStrength = mobileParty2.Party.EstimatedStrength;
											float num3 = 1f - (float)mobileParty2.Party.MemberRoster.TotalWounded / (float)mobileParty2.Party.MemberRoster.TotalManCount;
											float item = estimatedStrength / ((float)num2 + 0.1f) * num3;
											list.Add(new ValueTuple<MobileParty, float, int>(mobileParty2, item, num2));
										}
									}
								}
							}
						}
					}
					list = list.OrderByQ((ValueTuple<MobileParty, float, int> x) => x.Item2).ToListQ<ValueTuple<MobileParty, float, int>>();
					int count = kingdom.WarPartyComponents.Count;
					int num4 = kingdom.Armies.SumQ((Army x) => x.Parties.Count);
					int num5 = MathF.Ceiling((float)count * 0.7f - (float)num4);
					if (num5 > 0)
					{
						if (num5 < list.Count)
						{
							list.RemoveRange(num5, list.Count - num5);
						}
						possibleArmyMembers = list.SelectQ((ValueTuple<MobileParty, float, int> x) => x.Item1).ToMBList<MobileParty>();
						if (possibleArmyMembers.AnyQ<MobileParty>())
						{
							if (kingdom.Settlements.Count == 0)
							{
								return true;
							}
							float num6 = mobileParty.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
							foreach (MobileParty mobileParty3 in possibleArmyMembers)
							{
								num6 += mobileParty3.Party.GetCustomStrength(BattleSideEnum.Attacker, MapEvent.PowerCalculationContext.Siege);
							}
							if (num6 < 1000f)
							{
								possibleArmyMembers.Clear();
								return false;
							}
							return true;
						}
					}
				}
			}
			return false;
		}

		// Token: 0x06001644 RID: 5700 RVA: 0x000668F8 File Offset: 0x00064AF8
		public override int CalculateTotalInfluenceCost(Army army, float percentage)
		{
			int num = this.CalculateTotalInfluenceCostInternal(army, percentage);
			if (army != MobileParty.MainParty.Army)
			{
				num = (int)((float)num * 0.25f);
			}
			return num;
		}

		// Token: 0x06001645 RID: 5701 RVA: 0x00066928 File Offset: 0x00064B28
		private int CalculateTotalInfluenceCostInternal(Army army, float percentage)
		{
			int num = 0;
			foreach (MobileParty party in from p in army.Parties
			where !p.IsMainParty
			select p)
			{
				num += this.CalculatePartyInfluenceCost(army.LeaderParty, party);
			}
			ExplainedNumber explainedNumber = new ExplainedNumber((float)num, false, null);
			if (army.LeaderParty.MapFaction.IsKingdomFaction && ((Kingdom)army.LeaderParty.MapFaction).ActivePolicies.Contains(DefaultPolicies.RoyalCommissions))
			{
				explainedNumber.AddFactor(-0.3f, DefaultPolicies.RoyalCommissions.Name);
			}
			if (army.LeaderParty.LeaderHero.GetPerkValue(DefaultPerks.Tactics.Encirclement))
			{
				explainedNumber.AddFactor(DefaultPerks.Tactics.Encirclement.SecondaryBonus, null);
			}
			return MathF.Ceiling(explainedNumber.ResultNumber * percentage / 100f);
		}

		// Token: 0x06001646 RID: 5702 RVA: 0x00066A38 File Offset: 0x00064C38
		public override float GetPartySizeScore(MobileParty party)
		{
			return MathF.Min(1f, party.PartySizeRatio);
		}

		// Token: 0x06001647 RID: 5703 RVA: 0x00066A4C File Offset: 0x00064C4C
		public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(-2f, includeDescriptions, null);
			this.CalculateCohesionChangeInternal(army, ref result);
			PerkHelper.AddPerkBonusForParty(DefaultPerks.Tactics.HordeLeader, army.LeaderParty, false, ref result, army.LeaderParty.IsCurrentlyAtSea);
			SiegeEvent siegeEvent = army.LeaderParty.SiegeEvent;
			if (siegeEvent != null && siegeEvent.BesiegerCamp.IsBesiegerSideParty(army.LeaderParty) && army.LeaderParty.HasPerk(DefaultPerks.Engineering.CampBuilding, false))
			{
				result.AddFactor(DefaultPerks.Engineering.CampBuilding.PrimaryBonus, DefaultPerks.Engineering.CampBuilding.Name);
			}
			return result;
		}

		// Token: 0x06001648 RID: 5704 RVA: 0x00066AE0 File Offset: 0x00064CE0
		private void CalculateCohesionChangeInternal(Army army, ref ExplainedNumber cohesionChange)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			foreach (MobileParty mobileParty in army.LeaderParty.AttachedParties)
			{
				if (mobileParty.Party.IsStarving)
				{
					num++;
				}
				if (mobileParty.Morale <= 25f)
				{
					num2++;
				}
				if (mobileParty.Party.NumberOfHealthyMembers <= 10)
				{
					num3++;
				}
				num4++;
			}
			float num5 = (float)(-(float)num4);
			float num6 = -((float)(num + 1) / 2f);
			float num7 = -((float)(num2 + 1) / 2f);
			float num8 = -((float)(num3 + 1) / 2f);
			if (army.LeaderParty != MobileParty.MainParty)
			{
				num5 *= 0.25f;
				num6 *= 0.25f;
				num7 *= 0.25f;
				num8 *= 0.25f;
			}
			cohesionChange.Add(num5, this._numberOfPartiesText, null);
			cohesionChange.Add(num6, this._numberOfStarvingPartiesText, null);
			cohesionChange.Add(num7, this._numberOfLowMoralePartiesText, null);
			cohesionChange.Add(num8, this._numberOfLessMemberPartiesText, null);
		}

		// Token: 0x06001649 RID: 5705 RVA: 0x00066C10 File Offset: 0x00064E10
		public override int CalculateNewCohesion(Army army, PartyBase newParty, int calculatedCohesion, int sign)
		{
			if (army == null)
			{
				return calculatedCohesion;
			}
			sign = MathF.Sign(sign);
			int num = (sign == 1) ? (army.Parties.Count - 1) : army.Parties.Count;
			int num2 = (calculatedCohesion * num + 100 * sign) / (num + sign);
			if (num2 > 100)
			{
				return 100;
			}
			if (num2 >= 0)
			{
				return num2;
			}
			return 0;
		}

		// Token: 0x0600164A RID: 5706 RVA: 0x00066C69 File Offset: 0x00064E69
		public override int GetCohesionBoostInfluenceCost(Army army, int percentageToBoost = 100)
		{
			return this.CalculateTotalInfluenceCostInternal(army, (float)percentageToBoost);
		}

		// Token: 0x0600164B RID: 5707 RVA: 0x00066C74 File Offset: 0x00064E74
		private float GetInfluenceBudgetWhileCreatingArmy(MobileParty mobileParty)
		{
			return mobileParty.LeaderHero.Clan.Influence * 0.7f;
		}

		// Token: 0x0600164C RID: 5708 RVA: 0x00066C8C File Offset: 0x00064E8C
		public override int GetPartyRelation(Hero hero)
		{
			if (hero == null)
			{
				return -101;
			}
			if (hero == Hero.MainHero)
			{
				return 101;
			}
			return Hero.MainHero.GetRelation(hero);
		}

		// Token: 0x0600164D RID: 5709 RVA: 0x00066CAC File Offset: 0x00064EAC
		public override bool CanPlayerCreateArmy(out TextObject disabledReason)
		{
			if (Clan.PlayerClan.Kingdom == null)
			{
				disabledReason = new TextObject("{=XSQ0Y9gy}You need to be a part of a kingdom to create an army.", null);
				return false;
			}
			if (Clan.PlayerClan.IsUnderMercenaryService)
			{
				disabledReason = new TextObject("{=aRhQzJca}Mercenaries cannot create or manage armies.", null);
				return false;
			}
			if (MobileParty.MainParty.Army != null && MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty)
			{
				disabledReason = new TextObject("{=NAA4pajB}You need to leave your current army to create a new one.", null);
				return false;
			}
			if (MobileParty.MainParty.IsCurrentlyAtSea)
			{
				disabledReason = GameTexts.FindText("str_cannot_gather_army_at_sea", null);
				return false;
			}
			if (Hero.MainHero.IsPrisoner)
			{
				disabledReason = GameTexts.FindText("str_action_disabled_reason_prisoner", null);
				return false;
			}
			if (MobileParty.MainParty.IsInRaftState)
			{
				disabledReason = GameTexts.FindText("str_action_disabled_reason_raft_state", null);
				return false;
			}
			if (CampaignMission.Current != null)
			{
				disabledReason = new TextObject("{=FdzsOvDq}This action is disabled while in a mission", null);
				return false;
			}
			if (PlayerEncounter.Current != null)
			{
				if (PlayerEncounter.EncounterSettlement == null)
				{
					disabledReason = GameTexts.FindText("str_action_disabled_reason_encounter", null);
					return false;
				}
				Village village = PlayerEncounter.EncounterSettlement.Village;
				if (village != null && village.VillageState == Village.VillageStates.BeingRaided)
				{
					MapEvent mapEvent = MobileParty.MainParty.MapEvent;
					if (mapEvent != null && mapEvent.IsRaid)
					{
						disabledReason = GameTexts.FindText("str_action_disabled_reason_raid", null);
						return false;
					}
				}
				if (PlayerEncounter.EncounterSettlement.IsUnderSiege)
				{
					disabledReason = GameTexts.FindText("str_action_disabled_reason_siege", null);
					return false;
				}
			}
			else
			{
				if (PlayerSiege.PlayerSiegeEvent != null)
				{
					disabledReason = GameTexts.FindText("str_action_disabled_reason_siege", null);
					return false;
				}
				if (MobileParty.MainParty.MapEvent != null)
				{
					disabledReason = new TextObject("{=MIylzRc5}You can't perform this action while you are in a map event.", null);
					return false;
				}
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x0600164E RID: 5710 RVA: 0x00066E3C File Offset: 0x0006503C
		public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
		{
			bool result = true;
			if (PlayerSiege.PlayerSiegeEvent != null)
			{
				result = false;
				explanation = GameTexts.FindText("str_action_disabled_reason_siege", null);
			}
			else if (party == null)
			{
				result = false;
				explanation = new TextObject("{=f6vTzVar}Does not have a mobile party.", null);
			}
			else
			{
				Hero leaderHero = party.LeaderHero;
				IFaction mapFaction = Hero.MainHero.MapFaction;
				if (leaderHero == ((mapFaction != null) ? mapFaction.Leader : null))
				{
					result = false;
					explanation = new TextObject("{=ipLqVv1f}You cannot invite the ruler's party to your army.", null);
				}
				else
				{
					if (party.Army != null)
					{
						Army army = party.Army;
						MobileParty partyBelongedTo = Hero.MainHero.PartyBelongedTo;
						if (army != ((partyBelongedTo != null) ? partyBelongedTo.Army : null))
						{
							result = false;
							explanation = new TextObject("{=aROohsat}Already in another army.", null);
							return result;
						}
					}
					if (party.Army != null)
					{
						Army army2 = party.Army;
						MobileParty partyBelongedTo2 = Hero.MainHero.PartyBelongedTo;
						if (army2 == ((partyBelongedTo2 != null) ? partyBelongedTo2.Army : null))
						{
							result = false;
							explanation = new TextObject("{=Vq8yavES}Already in army.", null);
							return result;
						}
					}
					if (party.MapEvent != null || party.SiegeEvent != null || (party.CurrentSettlement != null && party.CurrentSettlement.IsUnderSiege))
					{
						result = false;
						explanation = new TextObject("{=pkbUiKFJ}Currently fighting an enemy.", null);
					}
					else if (this.GetPartySizeScore(party) <= Campaign.Current.Models.ArmyManagementCalculationModel.PlayerMobilePartySizeRatioToCallToArmy)
					{
						result = false;
						explanation = new TextObject("{=SVJlOYCB}Party has less men than 40% of it's party size limit.", null);
					}
					else
					{
						if (!party.IsDisbanding)
						{
							IDisbandPartyCampaignBehavior campaignBehavior = Campaign.Current.GetCampaignBehavior<IDisbandPartyCampaignBehavior>();
							if (campaignBehavior == null || !campaignBehavior.IsPartyWaitingForDisband(party))
							{
								if (MobileParty.MainParty.IsCurrentlyAtSea)
								{
									result = false;
									explanation = ((!party.HasNavalNavigationCapability) ? new TextObject("{=nqq84Dzq}Party cannot reach your army since it has no ships.", null) : new TextObject("{=gFixGQsr}You cannot call a party to your army while your party is at sea.", null));
									return result;
								}
								if (party.IsInRaftState)
								{
									result = false;
									explanation = new TextObject("{=TbXDmh3t}This party is lost at sea.", null);
									return result;
								}
								float num;
								if (DistanceHelper.FindClosestDistanceFromMobilePartyToMobileParty(party, MobileParty.MainParty, party.NavigationCapability, out num) > Campaign.Current.Models.ArmyManagementCalculationModel.MaximumDistanceToCallToArmy)
								{
									result = false;
									explanation = new TextObject("{=UINgZDN5}You can not call a party that is far away.", null);
									return result;
								}
								explanation = null;
								return result;
							}
						}
						result = false;
						explanation = new TextObject("{=tFGM0yav}This party is disbanding.", null);
					}
				}
			}
			return result;
		}

		// Token: 0x04000779 RID: 1913
		private const float MinimumInfluenceNeededToCreateArmy = 100f;

		// Token: 0x0400077A RID: 1914
		private readonly TextObject _numberOfPartiesText = GameTexts.FindText("str_number_of_parties", null);

		// Token: 0x0400077B RID: 1915
		private readonly TextObject _numberOfStarvingPartiesText = GameTexts.FindText("str_number_of_starving_parties", null);

		// Token: 0x0400077C RID: 1916
		private readonly TextObject _numberOfLowMoralePartiesText = GameTexts.FindText("str_number_of_low_morale_parties", null);

		// Token: 0x0400077D RID: 1917
		private readonly TextObject _numberOfLessMemberPartiesText = GameTexts.FindText("str_number_of_less_member_parties", null);
	}
}
