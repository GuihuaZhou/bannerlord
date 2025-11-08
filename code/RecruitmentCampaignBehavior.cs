using System;
using System.Collections.Generic;
using Helpers;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x02000438 RID: 1080
	public class RecruitmentCampaignBehavior : CampaignBehaviorBase
	{
		// Token: 0x06004438 RID: 17464 RVA: 0x0014DDA8 File Offset: 0x0014BFA8
		public override void RegisterEvents()
		{
			CampaignEvents.BeforeSettlementEnteredEvent.AddNonSerializedListener(this, new Action<MobileParty, Settlement, Hero>(this.OnBeforeSettlementEntered));
			CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnSessionLaunched));
			CampaignEvents.DailyTickTownEvent.AddNonSerializedListener(this, new Action<Town>(this.DailyTickTown));
			CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, new Action<Settlement>(this.DailyTickSettlement));
			CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, new Action<MobileParty>(this.HourlyTickParty));
			CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(this.OnNewGameCreatedPartialFollowUpEnd));
			CampaignEvents.OnUnitRecruitedEvent.AddNonSerializedListener(this, new Action<CharacterObject, int>(this.OnUnitRecruited));
			CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, new Action<Hero, Settlement, Hero, CharacterObject, int>(this.OnTroopRecruited));
		}

		// Token: 0x06004439 RID: 17465 RVA: 0x0014DE6D File Offset: 0x0014C06D
		private void DailyTickSettlement(Settlement settlement)
		{
			this.UpdateVolunteersOfNotablesInSettlement(settlement);
		}

		// Token: 0x0600443A RID: 17466 RVA: 0x0014DE76 File Offset: 0x0014C076
		public override void SyncData(IDataStore dataStore)
		{
			dataStore.SyncData<CharacterObject>("_selectedTroop", ref this._selectedTroop);
			dataStore.SyncData<Dictionary<Town, RecruitmentCampaignBehavior.TownMercenaryData>>("_townMercenaryData", ref this._townMercenaryData);
		}

		// Token: 0x0600443B RID: 17467 RVA: 0x0014DE9C File Offset: 0x0014C09C
		public RecruitmentCampaignBehavior.TownMercenaryData GetMercenaryData(Town town)
		{
			RecruitmentCampaignBehavior.TownMercenaryData townMercenaryData;
			if (!this._townMercenaryData.TryGetValue(town, out townMercenaryData))
			{
				townMercenaryData = new RecruitmentCampaignBehavior.TownMercenaryData(town);
				this._townMercenaryData.Add(town, townMercenaryData);
			}
			return townMercenaryData;
		}

		// Token: 0x0600443C RID: 17468 RVA: 0x0014DED0 File Offset: 0x0014C0D0
		private void OnNewGameCreatedPartialFollowUpEnd(CampaignGameStarter starter)
		{
			foreach (Town town in Town.AllTowns)
			{
				this.UpdateCurrentMercenaryTroopAndCount(town, true);
			}
			foreach (Settlement settlement in Settlement.All)
			{
				this.UpdateVolunteersOfNotablesInSettlement(settlement);
			}
		}

		// Token: 0x0600443D RID: 17469 RVA: 0x0014DF64 File Offset: 0x0014C164
		private void OnTroopRecruited(Hero recruiter, Settlement settlement, Hero recruitmentSource, CharacterObject troop, int count)
		{
			if (recruiter != null && recruiter.PartyBelongedTo != null && recruiter.GetPerkValue(DefaultPerks.Leadership.FamousCommander))
			{
				recruiter.PartyBelongedTo.MemberRoster.AddXpToTroop(troop, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * count);
			}
			SkillLevelingManager.OnTroopRecruited(recruiter, count, troop.Tier);
			if (recruiter != null && recruiter.PartyBelongedTo != null && troop.Occupation == Occupation.Bandit)
			{
				SkillLevelingManager.OnBanditsRecruited(recruiter.PartyBelongedTo, troop, count);
			}
		}

		// Token: 0x0600443E RID: 17470 RVA: 0x0014DFE0 File Offset: 0x0014C1E0
		private void OnUnitRecruited(CharacterObject troop, int count)
		{
			if (Hero.MainHero.GetPerkValue(DefaultPerks.Leadership.FamousCommander))
			{
				MobileParty.MainParty.MemberRoster.AddXpToTroop(troop, (int)DefaultPerks.Leadership.FamousCommander.SecondaryBonus * count);
			}
			SkillLevelingManager.OnTroopRecruited(Hero.MainHero, count, troop.Tier);
			if (troop.Occupation == Occupation.Bandit)
			{
				SkillLevelingManager.OnBanditsRecruited(MobileParty.MainParty, troop, count);
			}
		}

		// Token: 0x0600443F RID: 17471 RVA: 0x0014E044 File Offset: 0x0014C244
		private void DailyTickTown(Town town)
		{
			this.UpdateCurrentMercenaryTroopAndCount(town, (int)CampaignTime.Now.ToDays % 2 == 0);
		}

		// Token: 0x06004440 RID: 17472 RVA: 0x0014E06B File Offset: 0x0014C26B
		private void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
		{
			this.AddGameMenus(campaignGameStarter);
			this.AddDialogs(campaignGameStarter);
		}

		// Token: 0x06004441 RID: 17473 RVA: 0x0014E07C File Offset: 0x0014C27C
		private void UpdateVolunteersOfNotablesInSettlement(Settlement settlement)
		{
			if ((settlement.IsTown && !settlement.Town.InRebelliousState) || (settlement.IsVillage && !settlement.Village.Bound.Town.InRebelliousState))
			{
				foreach (Hero hero in settlement.Notables)
				{
					if (hero.CanHaveRecruits && hero.IsAlive)
					{
						bool flag = false;
						CharacterObject basicVolunteer = Campaign.Current.Models.VolunteerModel.GetBasicVolunteer(hero);
						for (int i = 0; i < 6; i++)
						{
							if (MBRandom.RandomFloat < Campaign.Current.Models.VolunteerModel.GetDailyVolunteerProductionProbability(hero, i, settlement))
							{
								CharacterObject characterObject = hero.VolunteerTypes[i];
								if (characterObject == null)
								{
									hero.VolunteerTypes[i] = basicVolunteer;
									flag = true;
								}
								else if (characterObject.UpgradeTargets.Length != 0 && characterObject.Tier < Campaign.Current.Models.VolunteerModel.MaxVolunteerTier)
								{
									float num = MathF.Log(hero.Power / (float)characterObject.Tier, 2f) * 0.01f;
									if (MBRandom.RandomFloat < num)
									{
										hero.VolunteerTypes[i] = characterObject.UpgradeTargets[MBRandom.RandomInt(characterObject.UpgradeTargets.Length)];
										flag = true;
									}
								}
							}
						}
						if (flag)
						{
							CharacterObject[] volunteerTypes = hero.VolunteerTypes;
							for (int j = 1; j < 6; j++)
							{
								CharacterObject characterObject2 = volunteerTypes[j];
								if (characterObject2 != null)
								{
									int num2 = 0;
									int num3 = j - 1;
									CharacterObject characterObject3 = volunteerTypes[num3];
									while (num3 >= 0 && (characterObject3 == null || (float)characterObject2.Level + (characterObject2.IsMounted ? 0.5f : 0f) < (float)characterObject3.Level + (characterObject3.IsMounted ? 0.5f : 0f)))
									{
										if (characterObject3 == null)
										{
											num3--;
											num2++;
											if (num3 >= 0)
											{
												characterObject3 = volunteerTypes[num3];
											}
										}
										else
										{
											volunteerTypes[num3 + 1 + num2] = characterObject3;
											num3--;
											num2 = 0;
											if (num3 >= 0)
											{
												characterObject3 = volunteerTypes[num3];
											}
										}
									}
									volunteerTypes[num3 + 1 + num2] = characterObject2;
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06004442 RID: 17474 RVA: 0x0014E2E4 File Offset: 0x0014C4E4
		public void HourlyTickParty(MobileParty mobileParty)
		{
			if ((mobileParty.IsCaravan || mobileParty.IsLordParty) && mobileParty.MapEvent == null && mobileParty != MobileParty.MainParty)
			{
				Settlement currentSettlementOfMobilePartyForAICalculation = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(mobileParty);
				if (currentSettlementOfMobilePartyForAICalculation != null)
				{
					if ((currentSettlementOfMobilePartyForAICalculation.IsVillage && !currentSettlementOfMobilePartyForAICalculation.IsRaided && !currentSettlementOfMobilePartyForAICalculation.IsUnderRaid) || (currentSettlementOfMobilePartyForAICalculation.IsTown && !currentSettlementOfMobilePartyForAICalculation.IsUnderSiege))
					{
						this.CheckRecruiting(mobileParty, currentSettlementOfMobilePartyForAICalculation);
						return;
					}
				}
				else if (MBRandom.RandomFloat < 0.05f && mobileParty.LeaderHero != null && mobileParty.ActualClan != Clan.PlayerClan && !mobileParty.IsCaravan)
				{
					IFaction mapFaction = mobileParty.MapFaction;
					if (mapFaction != null && mapFaction.IsMinorFaction && MobileParty.MainParty.Position.DistanceSquared(mobileParty.Position) > (MobileParty.MainParty.SeeingRange + 5f) * (MobileParty.MainParty.SeeingRange + 5f))
					{
						int partySizeLimit = mobileParty.Party.PartySizeLimit;
						float num = (float)mobileParty.Party.NumberOfAllMembers / (float)partySizeLimit;
						float num2 = ((double)num < 0.2) ? 1000f : (((double)num < 0.3) ? 2000f : (((double)num < 0.4) ? 3000f : (((double)num < 0.55) ? 4000f : (((double)num < 0.7) ? 5000f : 7000f))));
						float num3 = ((float)mobileParty.PartyTradeGold > num2) ? 1f : MathF.Sqrt((float)mobileParty.PartyTradeGold / num2);
						if (MBRandom.RandomFloat < (1f - num) * num3)
						{
							CharacterObject basicTroop = mobileParty.ActualClan.BasicTroop;
							int num4 = MBRandom.RandomInt(3, 8);
							if (num4 + mobileParty.Party.NumberOfAllMembers > partySizeLimit)
							{
								num4 = partySizeLimit - mobileParty.Party.NumberOfAllMembers;
							}
							int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(basicTroop, mobileParty.LeaderHero, false).RoundedResultNumber;
							if (num4 * roundedResultNumber > mobileParty.PartyTradeGold)
							{
								num4 = mobileParty.PartyTradeGold / roundedResultNumber;
							}
							if (num4 > 0)
							{
								this.GetRecruitVolunteerFromMap(mobileParty, basicTroop, num4);
							}
						}
					}
				}
			}
		}

		// Token: 0x06004443 RID: 17475 RVA: 0x0014E52C File Offset: 0x0014C72C
		private void UpdateCurrentMercenaryTroopAndCount(Town town, bool forceUpdate = false)
		{
			RecruitmentCampaignBehavior.TownMercenaryData mercenaryData = this.GetMercenaryData(town);
			if (!forceUpdate && mercenaryData.HasAvailableMercenary(Occupation.NotAssigned))
			{
				int difference = this.FindNumberOfMercenariesWillBeAdded(mercenaryData.TroopType, true);
				mercenaryData.ChangeMercenaryCount(difference);
				return;
			}
			if (MBRandom.RandomFloat < Campaign.Current.Models.TavernMercenaryTroopsModel.RegularMercenariesSpawnChance)
			{
				CharacterObject randomElementInefficiently = town.Culture.BasicMercenaryTroops.GetRandomElementInefficiently<CharacterObject>();
				this._selectedTroop = null;
				float num = this.FindTotalMercenaryProbability(randomElementInefficiently, 1f);
				float randomValueRemaining = MBRandom.RandomFloat * num;
				this.FindRandomMercenaryTroop(randomElementInefficiently, 1f, randomValueRemaining);
				int number = this.FindNumberOfMercenariesWillBeAdded(this._selectedTroop, false);
				mercenaryData.ChangeMercenaryType(this._selectedTroop, number);
				return;
			}
			CharacterObject caravanGuard = town.Culture.CaravanGuard;
			if (caravanGuard != null)
			{
				this._selectedTroop = null;
				float num2 = this.FindTotalMercenaryProbability(caravanGuard, 1f);
				float randomValueRemaining2 = MBRandom.RandomFloat * num2;
				this.FindRandomMercenaryTroop(caravanGuard, 1f, randomValueRemaining2);
				int number2 = this.FindNumberOfMercenariesWillBeAdded(this._selectedTroop, false);
				mercenaryData.ChangeMercenaryType(this._selectedTroop, number2);
			}
		}

		// Token: 0x06004444 RID: 17476 RVA: 0x0014E638 File Offset: 0x0014C838
		private float FindTotalMercenaryProbability(CharacterObject mercenaryTroop, float probabilityOfTroop)
		{
			float num = probabilityOfTroop;
			foreach (CharacterObject mercenaryTroop2 in mercenaryTroop.UpgradeTargets)
			{
				num += this.FindTotalMercenaryProbability(mercenaryTroop2, probabilityOfTroop / 1.5f);
			}
			return num;
		}

		// Token: 0x06004445 RID: 17477 RVA: 0x0014E674 File Offset: 0x0014C874
		private float FindRandomMercenaryTroop(CharacterObject mercenaryTroop, float probabilityOfTroop, float randomValueRemaining)
		{
			randomValueRemaining -= probabilityOfTroop;
			if (randomValueRemaining <= 1E-05f && this._selectedTroop == null)
			{
				this._selectedTroop = mercenaryTroop;
				return 1f;
			}
			float num = probabilityOfTroop;
			foreach (CharacterObject mercenaryTroop2 in mercenaryTroop.UpgradeTargets)
			{
				float num2 = this.FindRandomMercenaryTroop(mercenaryTroop2, probabilityOfTroop / 1.5f, randomValueRemaining);
				randomValueRemaining -= num2;
				num += num2;
			}
			return num;
		}

		// Token: 0x06004446 RID: 17478 RVA: 0x0014E6DC File Offset: 0x0014C8DC
		private int FindNumberOfMercenariesWillBeAdded(CharacterObject character, bool dailyUpdate = false)
		{
			int tier = Campaign.Current.Models.CharacterStatsModel.GetTier(character);
			int maxCharacterTier = Campaign.Current.Models.CharacterStatsModel.MaxCharacterTier;
			int num = (maxCharacterTier - tier) * 2;
			int num2 = (maxCharacterTier - tier) * 5;
			float randomFloat = MBRandom.RandomFloat;
			float randomFloat2 = MBRandom.RandomFloat;
			return MBRandom.RoundRandomized(MBMath.ClampFloat((randomFloat * randomFloat2 * (float)(num2 - num) + (float)num) * (dailyUpdate ? 0.1f : 1f), 1f, (float)num2));
		}

		// Token: 0x06004447 RID: 17479 RVA: 0x0014E754 File Offset: 0x0014C954
		private void CheckRecruiting(MobileParty mobileParty, Settlement settlement)
		{
			if (settlement.IsTown && mobileParty.IsCaravan)
			{
				RecruitmentCampaignBehavior.TownMercenaryData mercenaryData = this.GetMercenaryData(settlement.Town);
				if (mercenaryData.HasAvailableMercenary(Occupation.CaravanGuard) || mercenaryData.HasAvailableMercenary(Occupation.Mercenary))
				{
					int partySizeLimit = mobileParty.Party.PartySizeLimit;
					if (mobileParty.Party.NumberOfAllMembers < partySizeLimit)
					{
						CharacterObject troopType = mercenaryData.TroopType;
						int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troopType, mobileParty.LeaderHero, false).RoundedResultNumber;
						int num = mobileParty.IsCaravan ? 2000 : 0;
						if (mobileParty.PartyTradeGold > roundedResultNumber + num)
						{
							bool flag = true;
							double num2 = 0.0;
							for (int i = 0; i < mercenaryData.Number; i++)
							{
								if (flag)
								{
									int num3 = mobileParty.PartyTradeGold - (roundedResultNumber + num);
									double num4 = (double)MathF.Min(1f, MathF.Sqrt((float)num3 / (100f * (float)roundedResultNumber)));
									float num5 = (float)mobileParty.Party.NumberOfAllMembers / (float)partySizeLimit;
									float num6 = (MathF.Min(10f, 1f / num5) * MathF.Min(10f, 1f / num5) - 1f) * ((mobileParty.IsCaravan && mobileParty.Party.Owner == Hero.MainHero) ? 0.4f : 0.1f);
									num2 = num4 * (double)num6;
								}
								if ((double)MBRandom.RandomFloat < num2)
								{
									this.ApplyRecruitMercenary(mobileParty, settlement, troopType, 1);
									flag = true;
								}
								else
								{
									flag = false;
								}
							}
							return;
						}
					}
				}
			}
			else if (mobileParty.IsLordParty && !mobileParty.IsDisbanding && mobileParty.LeaderHero != null && !mobileParty.Party.IsStarving && mobileParty.Party.LeaderHero.IsAlive && (float)mobileParty.PartyTradeGold > HeroHelper.StartRecruitingMoneyLimit(mobileParty.LeaderHero) && (mobileParty.LeaderHero == mobileParty.LeaderHero.Clan.Leader || (float)mobileParty.LeaderHero.Clan.Gold > HeroHelper.StartRecruitingMoneyLimitForClanLeader(mobileParty.LeaderHero)) && ((float)mobileParty.Party.NumberOfAllMembers + 0.5f) / (float)mobileParty.Party.PartySizeLimit <= 1f)
			{
				if (settlement.IsTown && this.GetMercenaryData(settlement.Town).HasAvailableMercenary(Occupation.Mercenary))
				{
					float num7 = (float)mobileParty.Party.NumberOfAllMembers / (float)mobileParty.Party.PartySizeLimit;
					CharacterObject troopType2 = this.GetMercenaryData(settlement.Town).TroopType;
					if (troopType2 != null)
					{
						int roundedResultNumber2 = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troopType2, mobileParty.LeaderHero, false).RoundedResultNumber;
						if (roundedResultNumber2 < 5000)
						{
							float num8 = MathF.Min(1f, (float)mobileParty.PartyTradeGold / ((roundedResultNumber2 <= 100) ? 100000f : ((float)((roundedResultNumber2 <= 200) ? 125000 : ((roundedResultNumber2 <= 400) ? 150000 : ((roundedResultNumber2 <= 700) ? 175000 : ((roundedResultNumber2 <= 1100) ? 200000 : ((roundedResultNumber2 <= 1600) ? 250000 : ((roundedResultNumber2 <= 2200) ? 300000 : 400000)))))))));
							float num9 = num8 * num8;
							float num10 = MathF.Max(1f, MathF.Min(10f, 1f / num7)) - 1f;
							float num11 = num9 * num10 * 0.25f;
							int number = this.GetMercenaryData(settlement.Town).Number;
							int num12 = 0;
							int characterWage = Campaign.Current.Models.PartyWageModel.GetCharacterWage(troopType2);
							for (int j = 0; j < number; j++)
							{
								if (MBRandom.RandomFloat < num11)
								{
									num12++;
								}
							}
							num12 = MathF.Min(num12, mobileParty.Party.PartySizeLimit - mobileParty.Party.NumberOfAllMembers);
							num12 = (((double)roundedResultNumber2 <= 0.1) ? num12 : MathF.Min(mobileParty.PartyTradeGold / roundedResultNumber2, num12));
							num12 = MathF.Min(num12, mobileParty.GetAvailableWageBudget() / characterWage);
							if (num12 > 0)
							{
								this.ApplyRecruitMercenary(mobileParty, settlement, troopType2, num12);
							}
						}
					}
				}
				if (mobileParty.Party.NumberOfAllMembers < mobileParty.Party.PartySizeLimit && !mobileParty.IsWageLimitExceeded())
				{
					this.RecruitVolunteersFromNotable(mobileParty, settlement);
				}
			}
		}

		// Token: 0x06004448 RID: 17480 RVA: 0x0014EBC4 File Offset: 0x0014CDC4
		private void RecruitVolunteersFromNotable(MobileParty mobileParty, Settlement settlement)
		{
			if (((float)mobileParty.Party.NumberOfAllMembers + 0.5f) / (float)mobileParty.Party.PartySizeLimit <= 1f)
			{
				foreach (Hero hero in settlement.Notables)
				{
					if (hero.IsAlive)
					{
						int num = hero.VolunteerTypes.FindIndexQ((CharacterObject x) => x != null);
						if (num >= 0)
						{
							int num2 = MBRandom.RandomInt(6);
							int num3 = Campaign.Current.Models.VolunteerModel.MaximumIndexHeroCanRecruitFromHero(mobileParty.IsGarrison ? mobileParty.Party.Owner : mobileParty.LeaderHero, hero, -101);
							if (num <= num3)
							{
								for (int i = num2; i < num2 + 6; i++)
								{
									int num4 = i % 6;
									if (num4 >= num3)
									{
										break;
									}
									int num5 = (mobileParty.LeaderHero != null) ? ((int)MathF.Sqrt((float)mobileParty.PartyTradeGold / 10000f)) : 0;
									float num6 = MBRandom.RandomFloat;
									for (int j = 0; j < num5; j++)
									{
										float randomFloat = MBRandom.RandomFloat;
										if (randomFloat > num6)
										{
											num6 = randomFloat;
										}
									}
									if (mobileParty.Army != null)
									{
										float y = (mobileParty.Army.LeaderParty == mobileParty) ? 0.5f : 0.67f;
										num6 = MathF.Pow(num6, y);
									}
									float num7 = (float)mobileParty.Party.NumberOfAllMembers / (float)mobileParty.Party.PartySizeLimit;
									if (num6 > num7 - 0.1f)
									{
										CharacterObject characterObject = hero.VolunteerTypes[num4];
										if (characterObject != null && mobileParty.PartyTradeGold > Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(characterObject, mobileParty.LeaderHero, false).RoundedResultNumber && mobileParty.GetAvailableWageBudget() >= Campaign.Current.Models.PartyWageModel.GetCharacterWage(characterObject))
										{
											this.GetRecruitVolunteerFromIndividual(mobileParty, characterObject, hero, num4);
											break;
										}
									}
								}
							}
						}
					}
				}
			}
		}

		// Token: 0x06004449 RID: 17481 RVA: 0x0014EDFC File Offset: 0x0014CFFC
		public void OnBeforeSettlementEntered(MobileParty mobileParty, Settlement settlement, Hero hero)
		{
			if (mobileParty != null && mobileParty.MapEvent == null)
			{
				if (!settlement.IsVillage)
				{
					IFaction mapFaction = settlement.MapFaction;
					if (mapFaction == null || mapFaction.IsAtWarWith(mobileParty.MapFaction))
					{
						return;
					}
				}
				if (!settlement.IsRaided && !settlement.IsUnderRaid)
				{
					int num = mobileParty.IsCaravan ? 1 : ((mobileParty.Army != null && mobileParty.Army == MobileParty.MainParty.Army) ? ((MobileParty.MainParty.PartySizeRatio < 0.6f) ? 1 : ((MobileParty.MainParty.PartySizeRatio < 0.9f) ? 2 : 3)) : 7);
					List<MobileParty> list = new List<MobileParty>();
					if (mobileParty.Army != null && mobileParty.Army.LeaderParty == mobileParty)
					{
						using (List<MobileParty>.Enumerator enumerator = mobileParty.Army.Parties.GetEnumerator())
						{
							while (enumerator.MoveNext())
							{
								MobileParty mobileParty2 = enumerator.Current;
								if ((mobileParty2 == mobileParty.Army.LeaderParty || mobileParty2.AttachedTo == mobileParty.Army.LeaderParty) && mobileParty2 != MobileParty.MainParty)
								{
									list.Add(mobileParty2);
								}
							}
							goto IL_138;
						}
					}
					if (mobileParty.AttachedTo == null && mobileParty != MobileParty.MainParty)
					{
						list.Add(mobileParty);
					}
					IL_138:
					for (int i = 0; i < num; i++)
					{
						foreach (MobileParty mobileParty3 in list)
						{
							this.CheckRecruiting(mobileParty3, settlement);
						}
					}
				}
			}
		}

		// Token: 0x0600444A RID: 17482 RVA: 0x0014EFA4 File Offset: 0x0014D1A4
		private void ApplyInternal(MobileParty side1Party, Settlement settlement, Hero individual, CharacterObject troop, int number, int bitCode, RecruitmentCampaignBehavior.RecruitingDetail detail)
		{
			int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, side1Party.LeaderHero, false).RoundedResultNumber;
			if (detail == RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern)
			{
				if (side1Party.IsCaravan)
				{
					side1Party.PartyTradeGold -= number * roundedResultNumber;
					this.GetMercenaryData(settlement.Town).ChangeMercenaryCount(-number);
				}
				else
				{
					GiveGoldAction.ApplyBetweenCharacters(side1Party.LeaderHero, null, number * roundedResultNumber, true);
					this.GetMercenaryData(settlement.Town).ChangeMercenaryCount(-number);
				}
				side1Party.AddElementToMemberRoster(troop, number, false);
			}
			else if (detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual)
			{
				GiveGoldAction.ApplyBetweenCharacters(side1Party.LeaderHero, null, roundedResultNumber, true);
				individual.VolunteerTypes[bitCode] = null;
				side1Party.AddElementToMemberRoster(troop, 1, false);
			}
			else if (detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromMap)
			{
				GiveGoldAction.ApplyBetweenCharacters(side1Party.LeaderHero, null, number * roundedResultNumber, true);
				side1Party.AddElementToMemberRoster(troop, number, false);
			}
			else if (detail == RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividualToGarrison)
			{
				individual.VolunteerTypes[bitCode] = null;
				side1Party.AddElementToMemberRoster(troop, 1, false);
			}
			CampaignEventDispatcher.Instance.OnTroopRecruited(side1Party.LeaderHero, settlement, individual, troop, number);
		}

		// Token: 0x0600444B RID: 17483 RVA: 0x0014F0BB File Offset: 0x0014D2BB
		private void ApplyRecruitMercenary(MobileParty side1Party, Settlement side2Party, CharacterObject subject, int number)
		{
			this.ApplyInternal(side1Party, side2Party, null, subject, number, -1, RecruitmentCampaignBehavior.RecruitingDetail.MercenaryFromTavern);
		}

		// Token: 0x0600444C RID: 17484 RVA: 0x0014F0CB File Offset: 0x0014D2CB
		private void GetRecruitVolunteerFromMap(MobileParty side1Party, CharacterObject subject, int number)
		{
			this.ApplyInternal(side1Party, null, null, subject, number, -1, RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromMap);
		}

		// Token: 0x0600444D RID: 17485 RVA: 0x0014F0DA File Offset: 0x0014D2DA
		private void GetRecruitVolunteerFromIndividual(MobileParty side1Party, CharacterObject subject, Hero individual, int bitCode)
		{
			this.ApplyInternal(side1Party, individual.CurrentSettlement, individual, subject, 1, bitCode, RecruitmentCampaignBehavior.RecruitingDetail.VolunteerFromIndividual);
		}

		// Token: 0x0600444E RID: 17486 RVA: 0x0014F0F0 File Offset: 0x0014D2F0
		protected void AddGameMenus(CampaignGameStarter campaignGameSystemStarter)
		{
			campaignGameSystemStarter.AddGameMenuOption("town_backstreet", "recruit_mercenaries", "{=NwO0CVzn}Recruit {MEN_COUNT} {MERCENARY_NAME} ({TOTAL_AMOUNT}{GOLD_ICON})", new GameMenuOption.OnConditionDelegate(this.buy_mercenaries_condition), delegate(MenuCallbackArgs x)
			{
				this.buy_mercenaries_on_consequence();
			}, false, 2, false, null);
		}

		// Token: 0x0600444F RID: 17487 RVA: 0x0014F130 File Offset: 0x0014D330
		protected void AddDialogs(CampaignGameStarter campaignGameStarter)
		{
			campaignGameStarter.AddDialogLine("mercenary_recruit_start", "start", "mercenary_tavern_talk", "{=I0StkXlK}Do you have a need for fighters, {?PLAYER.GENDER}madam{?}sir{\\?}? Me and {?PLURAL}{MERCENARY_COUNT} of my mates{?}one of my mates{\\?} are looking for a master. You might call us mercenaries, like. We'll join you for {GOLD_AMOUNT}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruit_plural_start_on_condition), null, 100, null);
			campaignGameStarter.AddDialogLine("mercenary_recruit_start_single", "start", "mercenary_tavern_talk", "{=rJwExPKb}Do you have a need for fighters, {?PLAYER.GENDER}madam{?}sir{\\?}? I am looking for a master. I'll join you for {GOLD_AMOUNT}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruit_single_start_on_condition), null, 100, null);
			campaignGameStarter.AddPlayerLine("mercenary_recruit_accept", "mercenary_tavern_talk", "mercenary_tavern_talk_hire", "{=PDLDvUfH}All right. I will hire {?PLURAL}all of you{?}you{\\?}. Here is {GOLD_AMOUNT}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruit_accept_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_mercenary_recruit_accept_on_consequence), 100, null, null);
			campaignGameStarter.AddPlayerLine("mercenary_recruit_accept_some", "mercenary_tavern_talk", "mercenary_tavern_talk_hire", "{=aTPc7AkY}All right. But I can only hire {MERCENARY_COUNT} of you. Here is {GOLD_AMOUNT}{GOLD_ICON}", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruit_accept_some_on_condition), new ConversationSentence.OnConsequenceDelegate(this.conversation_mercenary_recruit_accept_some_on_consequence), 100, null, null);
			campaignGameStarter.AddPlayerLine("mercenary_recruit_reject_gold", "mercenary_tavern_talk", "close_window", "{=n5BGNLrc}That sounds good. But I can't afford any more men right now.", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruit_reject_gold_on_condition), null, 100, null, null);
			campaignGameStarter.AddPlayerLine("mercenary_recruit_reject", "mercenary_tavern_talk", "close_window", "{=ZSWrAC7V}Sorry, I can't take on any more troops right now.", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruit_dont_need_men_on_condition), null, 100, null, null);
			campaignGameStarter.AddDialogLine("mercenary_recruit_end", "mercenary_tavern_talk_hire", "close_window", "{=vbxQoyN3}{RANDOM_HIRE_SENTENCE}", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruit_end_on_condition), null, 100, null);
			campaignGameStarter.AddDialogLine("mercenary_recruit_start_2", "start", "close_window", "{=Jhj437BV}Don't worry, I'll be ready. Just having a last drink for the road.", new ConversationSentence.OnConditionDelegate(this.conversation_mercenary_recruited_on_condition), null, 100, null);
		}

		// Token: 0x06004450 RID: 17488 RVA: 0x0014F2B0 File Offset: 0x0014D4B0
		private bool buy_mercenaries_condition(MenuCallbackArgs args)
		{
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && this.GetMercenaryData(MobileParty.MainParty.CurrentSettlement.Town).Number > 0)
			{
				RecruitmentCampaignBehavior.TownMercenaryData mercenaryData = this.GetMercenaryData(MobileParty.MainParty.CurrentSettlement.Town);
				int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero, false).RoundedResultNumber;
				if (Hero.MainHero.Gold >= roundedResultNumber)
				{
					int num = MathF.Min(mercenaryData.Number, Hero.MainHero.Gold / roundedResultNumber);
					MBTextManager.SetTextVariable("MEN_COUNT", num);
					MBTextManager.SetTextVariable("MERCENARY_NAME", mercenaryData.TroopType.Name, false);
					MBTextManager.SetTextVariable("TOTAL_AMOUNT", num * roundedResultNumber);
				}
				else
				{
					args.Tooltip = GameTexts.FindText("str_decision_not_enough_gold", null);
					args.IsEnabled = false;
					int number = mercenaryData.Number;
					MBTextManager.SetTextVariable("MEN_COUNT", number);
					MBTextManager.SetTextVariable("MERCENARY_NAME", mercenaryData.TroopType.Name, false);
					MBTextManager.SetTextVariable("TOTAL_AMOUNT", number * roundedResultNumber);
				}
				args.optionLeaveType = GameMenuOption.LeaveType.Bribe;
				return true;
			}
			return false;
		}

		// Token: 0x06004451 RID: 17489 RVA: 0x0014F3F0 File Offset: 0x0014D5F0
		private void buy_mercenaries_on_consequence()
		{
			if (MobileParty.MainParty.CurrentSettlement != null && MobileParty.MainParty.CurrentSettlement.IsTown && this.GetMercenaryData(MobileParty.MainParty.CurrentSettlement.Town).Number > 0)
			{
				RecruitmentCampaignBehavior.TownMercenaryData mercenaryData = this.GetMercenaryData(MobileParty.MainParty.CurrentSettlement.Town);
				int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero, false).RoundedResultNumber;
				if (Hero.MainHero.Gold >= roundedResultNumber)
				{
					int num = MathF.Min(mercenaryData.Number, Hero.MainHero.Gold / roundedResultNumber);
					MobileParty.MainParty.MemberRoster.AddToCounts(mercenaryData.TroopType, num, false, 0, 0, true, -1);
					GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, -(num * roundedResultNumber), false);
					mercenaryData.ChangeMercenaryCount(-num);
					GameMenu.SwitchToMenu("town_backstreet");
				}
			}
		}

		// Token: 0x06004452 RID: 17490 RVA: 0x0014F4E4 File Offset: 0x0014D6E4
		private bool conversation_mercenary_recruit_plural_start_on_condition()
		{
			if (PlayerEncounter.EncounterSettlement == null || !PlayerEncounter.EncounterSettlement.IsTown)
			{
				return false;
			}
			RecruitmentCampaignBehavior.TownMercenaryData mercenaryData = this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town);
			bool flag = (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Mercenary || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.CaravanGuard || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Gangster) && PlayerEncounter.EncounterSettlement != null && PlayerEncounter.EncounterSettlement.IsTown && mercenaryData.Number > 1;
			if (flag)
			{
				int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero, false).RoundedResultNumber;
				MBTextManager.SetTextVariable("PLURAL", (mercenaryData.Number - 1 > 1) ? 1 : 0);
				MBTextManager.SetTextVariable("MERCENARY_COUNT", mercenaryData.Number - 1);
				MBTextManager.SetTextVariable("GOLD_AMOUNT", roundedResultNumber * mercenaryData.Number);
			}
			return flag;
		}

		// Token: 0x06004453 RID: 17491 RVA: 0x0014F5C8 File Offset: 0x0014D7C8
		private bool conversation_mercenary_recruit_single_start_on_condition()
		{
			if (PlayerEncounter.EncounterSettlement == null || !PlayerEncounter.EncounterSettlement.IsTown)
			{
				return false;
			}
			RecruitmentCampaignBehavior.TownMercenaryData mercenaryData = this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town);
			bool flag = (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Mercenary || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.CaravanGuard || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Gangster) && PlayerEncounter.EncounterSettlement != null && PlayerEncounter.EncounterSettlement.IsTown && mercenaryData.Number == 1;
			if (flag)
			{
				int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero, false).RoundedResultNumber;
				MBTextManager.SetTextVariable("GOLD_AMOUNT", mercenaryData.Number * roundedResultNumber);
			}
			return flag;
		}

		// Token: 0x06004454 RID: 17492 RVA: 0x0014F684 File Offset: 0x0014D884
		private bool conversation_mercenary_recruit_accept_on_condition()
		{
			RecruitmentCampaignBehavior.TownMercenaryData mercenaryData = this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town);
			int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(mercenaryData.TroopType, Hero.MainHero, false).RoundedResultNumber;
			MBTextManager.SetTextVariable("PLURAL", (mercenaryData.Number > 1) ? 1 : 0);
			return Hero.MainHero.Gold >= mercenaryData.Number * roundedResultNumber;
		}

		// Token: 0x06004455 RID: 17493 RVA: 0x0014F6F9 File Offset: 0x0014D8F9
		private bool conversation_mercenary_recruited_on_condition()
		{
			return (CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Mercenary || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.CaravanGuard || CharacterObject.OneToOneConversationCharacter.Occupation == Occupation.Gangster) && PlayerEncounter.EncounterSettlement != null;
		}

		// Token: 0x06004456 RID: 17494 RVA: 0x0014F730 File Offset: 0x0014D930
		private void BuyMercenaries()
		{
			this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).ChangeMercenaryCount(-this._selectedMercenaryCount);
			int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).TroopType, Hero.MainHero, false).RoundedResultNumber;
			MobileParty.MainParty.AddElementToMemberRoster(CharacterObject.OneToOneConversationCharacter, this._selectedMercenaryCount, false);
			int amount = this._selectedMercenaryCount * roundedResultNumber;
			GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, false);
			CampaignEventDispatcher.Instance.OnUnitRecruited(CharacterObject.OneToOneConversationCharacter, this._selectedMercenaryCount);
		}

		// Token: 0x06004457 RID: 17495 RVA: 0x0014F7D3 File Offset: 0x0014D9D3
		private void conversation_mercenary_recruit_accept_on_consequence()
		{
			this._selectedMercenaryCount = this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).Number;
			this.BuyMercenaries();
		}

		// Token: 0x06004458 RID: 17496 RVA: 0x0014F7F8 File Offset: 0x0014D9F8
		private bool conversation_mercenary_recruit_accept_some_on_condition()
		{
			int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).TroopType, Hero.MainHero, false).RoundedResultNumber;
			if (Hero.MainHero.Gold >= roundedResultNumber && Hero.MainHero.Gold < this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).Number * roundedResultNumber)
			{
				this._selectedMercenaryCount = 0;
				while (Hero.MainHero.Gold >= roundedResultNumber * (this._selectedMercenaryCount + 1))
				{
					this._selectedMercenaryCount++;
				}
				MBTextManager.SetTextVariable("MERCENARY_COUNT", this._selectedMercenaryCount);
				MBTextManager.SetTextVariable("GOLD_AMOUNT", roundedResultNumber * this._selectedMercenaryCount);
				return true;
			}
			return false;
		}

		// Token: 0x06004459 RID: 17497 RVA: 0x0014F8BF File Offset: 0x0014DABF
		private void conversation_mercenary_recruit_accept_some_on_consequence()
		{
			this.BuyMercenaries();
		}

		// Token: 0x0600445A RID: 17498 RVA: 0x0014F8C8 File Offset: 0x0014DAC8
		private bool conversation_mercenary_recruit_reject_gold_on_condition()
		{
			int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).TroopType, Hero.MainHero, false).RoundedResultNumber;
			return Hero.MainHero.Gold < roundedResultNumber;
		}

		// Token: 0x0600445B RID: 17499 RVA: 0x0014F91C File Offset: 0x0014DB1C
		private bool conversation_mercenary_recruit_dont_need_men_on_condition()
		{
			int roundedResultNumber = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(this.GetMercenaryData(PlayerEncounter.EncounterSettlement.Town).TroopType, Hero.MainHero, false).RoundedResultNumber;
			return Hero.MainHero.Gold >= roundedResultNumber;
		}

		// Token: 0x0600445C RID: 17500 RVA: 0x0014F974 File Offset: 0x0014DB74
		private bool conversation_mercenary_recruit_end_on_condition()
		{
			MBTextManager.SetTextVariable("RANDOM_HIRE_SENTENCE", GameTexts.FindText("str_mercenary_tavern_talk_hire", MBRandom.RandomInt(4).ToString()), false);
			return true;
		}

		// Token: 0x0400132A RID: 4906
		private Dictionary<Town, RecruitmentCampaignBehavior.TownMercenaryData> _townMercenaryData = new Dictionary<Town, RecruitmentCampaignBehavior.TownMercenaryData>();

		// Token: 0x0400132B RID: 4907
		private int _selectedMercenaryCount;

		// Token: 0x0400132C RID: 4908
		private CharacterObject _selectedTroop;

		// Token: 0x02000823 RID: 2083
		public class RecruitmentCampaignBehaviorTypeDefiner : SaveableTypeDefiner
		{
			// Token: 0x060065EF RID: 26095 RVA: 0x001BF696 File Offset: 0x001BD896
			public RecruitmentCampaignBehaviorTypeDefiner() : base(881200)
			{
			}

			// Token: 0x060065F0 RID: 26096 RVA: 0x001BF6A3 File Offset: 0x001BD8A3
			protected override void DefineClassTypes()
			{
				base.AddClassDefinition(typeof(RecruitmentCampaignBehavior.TownMercenaryData), 1, null);
			}

			// Token: 0x060065F1 RID: 26097 RVA: 0x001BF6B7 File Offset: 0x001BD8B7
			protected override void DefineContainerDefinitions()
			{
				base.ConstructContainerDefinition(typeof(Dictionary<Town, RecruitmentCampaignBehavior.TownMercenaryData>));
			}
		}

		// Token: 0x02000824 RID: 2084
		public class TownMercenaryData
		{
			// Token: 0x1700151A RID: 5402
			// (get) Token: 0x060065F2 RID: 26098 RVA: 0x001BF6C9 File Offset: 0x001BD8C9
			// (set) Token: 0x060065F3 RID: 26099 RVA: 0x001BF6D1 File Offset: 0x001BD8D1
			[SaveableProperty(202)]
			public CharacterObject TroopType { get; private set; }

			// Token: 0x1700151B RID: 5403
			// (get) Token: 0x060065F4 RID: 26100 RVA: 0x001BF6DA File Offset: 0x001BD8DA
			// (set) Token: 0x060065F5 RID: 26101 RVA: 0x001BF6E2 File Offset: 0x001BD8E2
			[SaveableProperty(203)]
			public int Number { get; private set; }

			// Token: 0x060065F6 RID: 26102 RVA: 0x001BF6EB File Offset: 0x001BD8EB
			public TownMercenaryData(Town currentTown)
			{
				this._currentTown = currentTown;
			}

			// Token: 0x060065F7 RID: 26103 RVA: 0x001BF6FC File Offset: 0x001BD8FC
			public void ChangeMercenaryType(CharacterObject troopType, int number)
			{
				if (troopType != this.TroopType)
				{
					CharacterObject troopType2 = this.TroopType;
					this.TroopType = troopType;
					this.Number = number;
					CampaignEventDispatcher.Instance.OnMercenaryTroopChangedInTown(this._currentTown, troopType2, this.TroopType);
					return;
				}
				if (this.Number != number)
				{
					int difference = number - this.Number;
					this.ChangeMercenaryCount(difference);
				}
			}

			// Token: 0x060065F8 RID: 26104 RVA: 0x001BF758 File Offset: 0x001BD958
			public void ChangeMercenaryCount(int difference)
			{
				if (difference != 0)
				{
					int number = this.Number;
					this.Number += difference;
					CampaignEventDispatcher.Instance.OnMercenaryNumberChangedInTown(this._currentTown, number, this.Number);
				}
			}

			// Token: 0x060065F9 RID: 26105 RVA: 0x001BF794 File Offset: 0x001BD994
			public bool HasAvailableMercenary(Occupation occupation = Occupation.NotAssigned)
			{
				return this.TroopType != null && this.Number > 0 && (occupation == Occupation.NotAssigned || this.TroopType.Occupation == occupation);
			}

			// Token: 0x060065FA RID: 26106 RVA: 0x001BF7BC File Offset: 0x001BD9BC
			internal static void AutoGeneratedStaticCollectObjectsTownMercenaryData(object o, List<object> collectedObjects)
			{
				((RecruitmentCampaignBehavior.TownMercenaryData)o).AutoGeneratedInstanceCollectObjects(collectedObjects);
			}

			// Token: 0x060065FB RID: 26107 RVA: 0x001BF7CA File Offset: 0x001BD9CA
			protected virtual void AutoGeneratedInstanceCollectObjects(List<object> collectedObjects)
			{
				collectedObjects.Add(this._currentTown);
				collectedObjects.Add(this.TroopType);
			}

			// Token: 0x060065FC RID: 26108 RVA: 0x001BF7E4 File Offset: 0x001BD9E4
			internal static object AutoGeneratedGetMemberValueTroopType(object o)
			{
				return ((RecruitmentCampaignBehavior.TownMercenaryData)o).TroopType;
			}

			// Token: 0x060065FD RID: 26109 RVA: 0x001BF7F1 File Offset: 0x001BD9F1
			internal static object AutoGeneratedGetMemberValueNumber(object o)
			{
				return ((RecruitmentCampaignBehavior.TownMercenaryData)o).Number;
			}

			// Token: 0x060065FE RID: 26110 RVA: 0x001BF803 File Offset: 0x001BDA03
			internal static object AutoGeneratedGetMemberValue_currentTown(object o)
			{
				return ((RecruitmentCampaignBehavior.TownMercenaryData)o)._currentTown;
			}

			// Token: 0x0400229A RID: 8858
			[SaveableField(204)]
			private readonly Town _currentTown;
		}

		// Token: 0x02000825 RID: 2085
		public enum RecruitingDetail
		{
			// Token: 0x0400229C RID: 8860
			MercenaryFromTavern,
			// Token: 0x0400229D RID: 8861
			VolunteerFromIndividual,
			// Token: 0x0400229E RID: 8862
			VolunteerFromIndividualToGarrison,
			// Token: 0x0400229F RID: 8863
			VolunteerFromMap
		}
	}
}
