using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x02000138 RID: 312
	public class DefaultPartySizeLimitModel : PartySizeLimitModel
	{
		// Token: 0x1700069A RID: 1690
		// (get) Token: 0x0600194B RID: 6475 RVA: 0x0007D853 File Offset: 0x0007BA53
		public override int MinimumNumberOfVillagersAtVillagerParty
		{
			get
			{
				return 12;
			}
		}

		// Token: 0x0600194D RID: 6477 RVA: 0x0007D948 File Offset: 0x0007BB48
		public override ExplainedNumber GetPartyMemberSizeLimit(PartyBase party, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			if (!party.IsMobile)
			{
				return result;
			}
			if (party.MobileParty.IsGarrison)
			{
				return this.CalculateGarrisonPartySizeLimit(party.MobileParty.GarrisonPartyComponent.Settlement, includeDescriptions);
			}
			if (party.MobileParty.IsPatrolParty)
			{
				return this.CalculatePatrolPartySizeLimit(party.MobileParty, includeDescriptions);
			}
			return this.CalculateMobilePartyMemberSizeLimit(party.MobileParty, includeDescriptions);
		}

		// Token: 0x0600194E RID: 6478 RVA: 0x0007D9BC File Offset: 0x0007BBBC
		private ExplainedNumber CalculatePatrolPartySizeLimit(MobileParty mobileParty, bool includeDescriptions)
		{
			new ExplainedNumber(10f, includeDescriptions, null);
			foreach (Building building in mobileParty.HomeSettlement.Town.Buildings)
			{
				if (building.BuildingType == DefaultBuildingTypes.SettlementGuardHouse)
				{
					return new ExplainedNumber((float)this.GetPatrolPartySizeLimitFromGuardHouseLevel(building.CurrentLevel), includeDescriptions, null);
				}
			}
			return new ExplainedNumber(0f, includeDescriptions, null);
		}

		// Token: 0x0600194F RID: 6479 RVA: 0x0007DA54 File Offset: 0x0007BC54
		private int GetPatrolPartySizeLimitFromGuardHouseLevel(int level)
		{
			return 10 + 5 * level;
		}

		// Token: 0x06001950 RID: 6480 RVA: 0x0007DA5C File Offset: 0x0007BC5C
		public override ExplainedNumber GetPartyPrisonerSizeLimit(PartyBase party, bool includeDescriptions = false)
		{
			if (party.IsSettlement)
			{
				return this.CalculateSettlementPartyPrisonerSizeLimitInternal(party.Settlement, includeDescriptions);
			}
			return this.CalculateMobilePartyPrisonerSizeLimitInternal(party, includeDescriptions);
		}

		// Token: 0x06001951 RID: 6481 RVA: 0x0007DA7C File Offset: 0x0007BC7C
		private ExplainedNumber CalculateMobilePartyMemberSizeLimit(MobileParty party, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(20f, includeDescriptions, this._baseSizeText);
			if (party.LeaderHero != null && party.LeaderHero.Clan != null && !party.IsCaravan)
			{
				this.CalculateBaseMemberSize(party.LeaderHero, party.MapFaction, party.ActualClan, ref result);
				SkillHelper.AddSkillBonusForParty(DefaultSkillEffects.StewardPartySizeBonus, party, ref result);
				if (DefaultPartySizeLimitModel._addAdditionalPartySizeAsCheat && party.IsMainParty && Game.Current.CheatMode)
				{
					result.Add(5000f, new TextObject("{=!}Additional size from extra party cheat", null), null);
				}
			}
			else if (party.IsCaravan)
			{
				if (party.Party.Owner == Hero.MainHero)
				{
					int num = party.CaravanPartyComponent.IsElite ? 30 : 10;
					if (party.CaravanPartyComponent.CanHaveNavalNavigationCapability)
					{
						num = (party.CaravanPartyComponent.IsElite ? 46 : 33);
					}
					result.Add((float)num, this._randomSizeBonusTemporary, null);
				}
				else
				{
					Hero owner = party.Party.Owner;
					if (owner != null && owner.IsNotable)
					{
						result.Add((float)(10 * ((party.Party.Owner.Power < 100f) ? 1 : ((party.Party.Owner.Power < 200f) ? 2 : 3))), this._randomSizeBonusTemporary, null);
					}
				}
			}
			else if (party.IsVillager)
			{
				result.Add(40f, this._randomSizeBonusTemporary, null);
			}
			if (party.IsCurrentlyAtSea)
			{
				foreach (Ship ship in party.Ships)
				{
					result.AddFactor(ship.CrewCapacityBonusFactor, ship.Name);
				}
			}
			return result;
		}

		// Token: 0x06001952 RID: 6482 RVA: 0x0007DC60 File Offset: 0x0007BE60
		public override ExplainedNumber CalculateGarrisonPartySizeLimit(Settlement settlement, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(200f, includeDescriptions, this._baseSizeText);
			SkillHelper.AddSkillBonusForCharacter(DefaultSkillEffects.LeadershipGarrisonSizeBonus, settlement.OwnerClan.Leader.CharacterObject, ref result);
			if (settlement.IsTown)
			{
				result.Add(200f, this._townBonusText, null);
			}
			this.AddGarrisonOwnerPerkEffects(settlement, ref result);
			this.AddSettlementProjectBonuses(settlement, ref result);
			return result;
		}

		// Token: 0x06001953 RID: 6483 RVA: 0x0007DCCC File Offset: 0x0007BECC
		private ExplainedNumber CalculateSettlementPartyPrisonerSizeLimitInternal(Settlement settlement, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(60f, includeDescriptions, this._baseSizeText);
			Town town = settlement.Town;
			int num = (town != null) ? town.GetWallLevel() : 0;
			if (num > 0)
			{
				result.Add((float)(num * 40), this._wallLevelBonusText, null);
			}
			this.AddSettlementProjectPrisonerBonuses(settlement, ref result);
			return result;
		}

		// Token: 0x06001954 RID: 6484 RVA: 0x0007DD20 File Offset: 0x0007BF20
		private ExplainedNumber CalculateMobilePartyPrisonerSizeLimitInternal(PartyBase party, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(10f, includeDescriptions, this._baseSizeText);
			result.Add((float)this.GetCurrentPartySizeEffect(party), this._currentPartySizeBonusText, null);
			this.AddMobilePartyLeaderPrisonerSizePerkEffects(party, ref result);
			if (DefaultPartySizeLimitModel._addAdditionalPrisonerSizeAsCheat && party.IsMobile && party.MobileParty.IsMainParty && Game.Current.CheatMode)
			{
				result.Add(5000f, new TextObject("{=!}Additional size from extra prisoner cheat", null), null);
			}
			return result;
		}

		// Token: 0x06001955 RID: 6485 RVA: 0x0007DDA0 File Offset: 0x0007BFA0
		private void AddMobilePartyLeaderPrisonerSizePerkEffects(PartyBase party, ref ExplainedNumber result)
		{
			if (party.LeaderHero != null)
			{
				if (party.LeaderHero.GetPerkValue(DefaultPerks.TwoHanded.Terror))
				{
					result.Add(DefaultPerks.TwoHanded.Terror.SecondaryBonus, DefaultPerks.TwoHanded.Terror.Name, null);
				}
				if (!party.MobileParty.IsCurrentlyAtSea && party.LeaderHero.GetPerkValue(DefaultPerks.Athletics.Stamina))
				{
					result.Add(DefaultPerks.Athletics.Stamina.SecondaryBonus, DefaultPerks.Athletics.Stamina.Name, null);
				}
				if (party.LeaderHero.GetPerkValue(DefaultPerks.Roguery.Manhunter))
				{
					result.Add(DefaultPerks.Roguery.Manhunter.SecondaryBonus, DefaultPerks.Roguery.Manhunter.Name, null);
				}
				if (party.LeaderHero != null && party.LeaderHero.GetPerkValue(DefaultPerks.Scouting.VantagePoint))
				{
					result.Add(DefaultPerks.Scouting.VantagePoint.SecondaryBonus, DefaultPerks.Scouting.VantagePoint.Name, null);
				}
			}
		}

		// Token: 0x06001956 RID: 6486 RVA: 0x0007DE81 File Offset: 0x0007C081
		private void AddGarrisonOwnerPerkEffects(Settlement currentSettlement, ref ExplainedNumber result)
		{
			if (currentSettlement != null && currentSettlement.IsFortification)
			{
				PerkHelper.AddPerkBonusForTown(DefaultPerks.OneHanded.CorpsACorps, currentSettlement.Town, ref result);
				PerkHelper.AddPerkBonusForTown(DefaultPerks.Leadership.VeteransRespect, currentSettlement.Town, ref result);
			}
		}

		// Token: 0x06001957 RID: 6487 RVA: 0x0007DEB0 File Offset: 0x0007C0B0
		public override int GetNextClanTierPartySizeEffectChangeForHero(Hero hero)
		{
			int tierEffectInternal = this.GetTierEffectInternal(hero.Clan.Tier, hero.Clan.Leader == hero);
			return this.GetTierEffectInternal(hero.Clan.Tier + 1, hero.Clan.Leader == hero) - tierEffectInternal;
		}

		// Token: 0x06001958 RID: 6488 RVA: 0x0007DF00 File Offset: 0x0007C100
		private int GetTierEffectInternal(int tier, bool isHeroClanLeader)
		{
			if (tier < 1)
			{
				return 0;
			}
			if (isHeroClanLeader)
			{
				return 25 * tier;
			}
			return 15 * tier;
		}

		// Token: 0x06001959 RID: 6489 RVA: 0x0007DF14 File Offset: 0x0007C114
		public override int GetAssumedPartySizeForLordParty(Hero leaderHero, IFaction partyMapFaction, Clan actualClan)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(20f, false, this._baseSizeText);
			if (leaderHero != null && leaderHero.Clan != null)
			{
				this.CalculateBaseMemberSize(leaderHero, partyMapFaction, actualClan, ref explainedNumber);
				SkillHelper.AddSkillBonusForSkillLevel(DefaultSkillEffects.StewardPartySizeBonus, ref explainedNumber, leaderHero.GetSkillValue(DefaultSkills.Steward));
			}
			return (int)explainedNumber.ResultNumber;
		}

		// Token: 0x0600195A RID: 6490 RVA: 0x0007DF69 File Offset: 0x0007C169
		public override int GetClanTierPartySizeEffectForHero(Hero hero)
		{
			return this.GetTierEffectInternal(hero.Clan.Tier, hero.Clan.Leader == hero);
		}

		// Token: 0x0600195B RID: 6491 RVA: 0x0007DF8A File Offset: 0x0007C18A
		private void AddSettlementProjectBonuses(Settlement settlement, ref ExplainedNumber result)
		{
			if (settlement != null && settlement.IsFortification)
			{
				settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.GarrisonCapacity, ref result);
			}
		}

		// Token: 0x0600195C RID: 6492 RVA: 0x0007DFA4 File Offset: 0x0007C1A4
		private void AddSettlementProjectPrisonerBonuses(Settlement settlement, ref ExplainedNumber result)
		{
			if (settlement != null && settlement.IsFortification)
			{
				settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.PrisonCapacity, ref result);
			}
		}

		// Token: 0x0600195D RID: 6493 RVA: 0x0007DFBF File Offset: 0x0007C1BF
		private int GetCurrentPartySizeEffect(PartyBase party)
		{
			return party.NumberOfHealthyMembers / 2;
		}

		// Token: 0x0600195E RID: 6494 RVA: 0x0007DFCC File Offset: 0x0007C1CC
		private void CalculateBaseMemberSize(Hero partyLeader, IFaction partyMapFaction, Clan actualClan, ref ExplainedNumber result)
		{
			if (partyMapFaction != null && partyMapFaction.IsKingdomFaction && partyLeader.MapFaction.Leader == partyLeader)
			{
				result.Add(20f, this._factionLeaderText, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.OneHanded.Prestige))
			{
				result.Add(DefaultPerks.OneHanded.Prestige.SecondaryBonus, DefaultPerks.OneHanded.Prestige.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.TwoHanded.Hope))
			{
				result.Add(DefaultPerks.TwoHanded.Hope.SecondaryBonus, DefaultPerks.TwoHanded.Hope.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.Athletics.ImposingStature))
			{
				result.Add(DefaultPerks.Athletics.ImposingStature.SecondaryBonus, DefaultPerks.Athletics.ImposingStature.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.Bow.MerryMen))
			{
				result.Add(DefaultPerks.Bow.MerryMen.PrimaryBonus, DefaultPerks.Bow.MerryMen.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.Tactics.HordeLeader))
			{
				result.Add(DefaultPerks.Tactics.HordeLeader.PrimaryBonus, DefaultPerks.Tactics.HordeLeader.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.Scouting.MountedScouts))
			{
				result.Add(DefaultPerks.Scouting.MountedScouts.SecondaryBonus, DefaultPerks.Scouting.MountedScouts.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.Leadership.Authority))
			{
				result.Add(DefaultPerks.Leadership.Authority.SecondaryBonus, DefaultPerks.Leadership.Authority.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.Leadership.UpliftingSpirit))
			{
				result.Add(DefaultPerks.Leadership.UpliftingSpirit.SecondaryBonus, DefaultPerks.Leadership.UpliftingSpirit.Name, null);
			}
			if (partyLeader.GetPerkValue(DefaultPerks.Leadership.TalentMagnet))
			{
				result.Add(DefaultPerks.Leadership.TalentMagnet.PrimaryBonus, DefaultPerks.Leadership.TalentMagnet.Name, null);
			}
			if (partyLeader.GetSkillValue(DefaultSkills.Leadership) > Campaign.Current.Models.CharacterDevelopmentModel.MaxSkillRequiredForEpicPerkBonus && partyLeader.GetPerkValue(DefaultPerks.Leadership.UltimateLeader))
			{
				int num = partyLeader.GetSkillValue(DefaultSkills.Leadership) - Campaign.Current.Models.CharacterDevelopmentModel.MaxSkillRequiredForEpicPerkBonus;
				result.Add((float)num * DefaultPerks.Leadership.UltimateLeader.PrimaryBonus, this._leadershipPerkUltimateLeaderBonusText, null);
			}
			if (actualClan != null)
			{
				Hero leader = actualClan.Leader;
				bool? flag = (leader != null) ? new bool?(leader.GetPerkValue(DefaultPerks.Leadership.LeaderOfMasses)) : null;
				bool flag2 = true;
				if (flag.GetValueOrDefault() == flag2 & flag != null)
				{
					int num2 = 0;
					using (List<Settlement>.Enumerator enumerator = actualClan.Settlements.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							if (enumerator.Current.IsTown)
							{
								num2++;
							}
						}
					}
					float num3 = (float)num2 * DefaultPerks.Leadership.LeaderOfMasses.PrimaryBonus;
					if (num3 > 0f)
					{
						result.Add(num3, DefaultPerks.Leadership.LeaderOfMasses.Name, null);
					}
				}
			}
			if (partyLeader.Clan.Leader == partyLeader)
			{
				if (partyLeader.Clan.Tier >= 5 && partyMapFaction.IsKingdomFaction && ((Kingdom)partyMapFaction).ActivePolicies.Contains(DefaultPolicies.NobleRetinues))
				{
					result.Add(40f, DefaultPolicies.NobleRetinues.Name, null);
				}
				if (partyMapFaction.IsKingdomFaction && partyMapFaction.Leader == partyLeader && ((Kingdom)partyMapFaction).ActivePolicies.Contains(DefaultPolicies.RoyalGuard))
				{
					result.Add(60f, DefaultPolicies.RoyalGuard.Name, null);
				}
			}
			result.Add((float)Campaign.Current.Models.PartySizeLimitModel.GetClanTierPartySizeEffectForHero(partyLeader), this._clanTierText, null);
		}

		// Token: 0x0600195F RID: 6495 RVA: 0x0007E358 File Offset: 0x0007C558
		private float GetPartySizeRatioForSize(PartyTemplateObject partyTemplate, int desiredSize)
		{
			int num = partyTemplate.Stacks.Sum((PartyTemplateStack s) => s.MinValue);
			int num2 = partyTemplate.Stacks.Sum((PartyTemplateStack s) => s.MaxValue);
			float result;
			if (desiredSize < num)
			{
				result = (float)desiredSize / (float)num - 1f;
			}
			else if (num <= desiredSize && desiredSize <= num2)
			{
				result = (float)(desiredSize - num) / (float)(num2 - num);
			}
			else
			{
				result = (float)desiredSize / (float)num2;
			}
			return result;
		}

		// Token: 0x06001960 RID: 6496 RVA: 0x0007E3F0 File Offset: 0x0007C5F0
		private float GetInitialPartySizeRatioForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
		{
			float result;
			if (party.IsBandit)
			{
				if (!partyTemplate.ShipHulls.IsEmpty<ShipTemplateStack>())
				{
					result = ((MBRandom.RandomFloat < 0.4f) ? MBRandom.RandomFloatRanged(0f, 0.33f) : MBRandom.RandomFloatRanged(0.66f, 1f));
				}
				else
				{
					float playerProgress = Campaign.Current.PlayerProgress;
					float num = 0.4f + 0.8f * playerProgress;
					float num2 = MBRandom.RandomFloatRanged(0.2f, 0.8f);
					result = num * num2;
				}
			}
			else if (party.IsCaravan && party.Owner == Hero.MainHero)
			{
				result = 1f;
			}
			else if (party.IsPatrolParty)
			{
				result = 1f;
			}
			else
			{
				result = party.RandomFloat();
			}
			return result;
		}

		// Token: 0x06001961 RID: 6497 RVA: 0x0007E4AC File Offset: 0x0007C6AC
		public override int GetIdealVillagerPartySize(Village village)
		{
			float num = 0f;
			foreach (ValueTuple<ItemObject, float> valueTuple in village.VillageType.Productions)
			{
				float resultNumber = Campaign.Current.Models.VillageProductionCalculatorModel.CalculateDailyProductionAmount(village, valueTuple.Item1).ResultNumber;
				num += resultNumber;
			}
			float num2 = (num > 10f) ? (40f * (1f - (MathF.Min(40f, num) - 10f) / 60f)) : 40f;
			return this.MinimumNumberOfVillagersAtVillagerParty + (int)(village.Hearth / num2);
		}

		// Token: 0x06001962 RID: 6498 RVA: 0x0007E574 File Offset: 0x0007C774
		public override TroopRoster FindAppropriateInitialRosterForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
		{
			TroopRoster troopRoster = TroopRoster.CreateDummyTroopRoster();
			float initialPartySizeRatioForMobileParty = this.GetInitialPartySizeRatioForMobileParty(party, partyTemplate);
			for (int i = 0; i < partyTemplate.Stacks.Count; i++)
			{
				int minValue = partyTemplate.Stacks[i].MinValue;
				int maxValue = partyTemplate.Stacks[i].MaxValue;
				int num;
				if (initialPartySizeRatioForMobileParty <= 0f)
				{
					num = minValue;
				}
				else if (initialPartySizeRatioForMobileParty <= 1f)
				{
					num = MBRandom.RoundRandomized((float)minValue + (float)(maxValue - minValue) * initialPartySizeRatioForMobileParty);
				}
				else
				{
					Debug.FailedAssert("initialPartySizeRatio should not be above 1", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem\\GameComponents\\DefaultPartySizeLimitModel.cs", "FindAppropriateInitialRosterForMobileParty", 538);
					num = maxValue;
				}
				if (party.IsVillager)
				{
					Village village = party.VillagerPartyComponent.Village;
					Settlement bound = village.Bound;
					bool flag;
					if (bound == null)
					{
						flag = (null != null);
					}
					else
					{
						Town town = bound.Town;
						flag = (((town != null) ? town.Governor : null) != null);
					}
					if (flag && village.Bound.Town.Governor.GetPerkValue(DefaultPerks.Scouting.VillageNetwork))
					{
						num = MathF.Round((float)num * (1f + DefaultPerks.Scouting.VillageNetwork.SecondaryBonus));
					}
				}
				if (num > 0)
				{
					CharacterObject character = partyTemplate.Stacks[i].Character;
					troopRoster.AddToCounts(character, num, false, 0, 0, true, -1);
				}
			}
			return troopRoster;
		}

		// Token: 0x06001963 RID: 6499 RVA: 0x0007E6B0 File Offset: 0x0007C8B0
		public override List<Ship> FindAppropriateInitialShipsForMobileParty(MobileParty party, PartyTemplateObject partyTemplate)
		{
			List<Ship> list = new List<Ship>();
			float initialPartySizeRatioForMobileParty = this.GetInitialPartySizeRatioForMobileParty(party, partyTemplate);
			if (partyTemplate.ShipHulls != null && partyTemplate.ShipHulls.Count > 0)
			{
				foreach (ShipTemplateStack shipTemplateStack in partyTemplate.ShipHulls)
				{
					int minValue = shipTemplateStack.MinValue;
					int maxValue = shipTemplateStack.MaxValue;
					int num;
					if (initialPartySizeRatioForMobileParty <= 0f)
					{
						num = MBRandom.RoundRandomized(Math.Max(0f, (float)minValue + (float)minValue * initialPartySizeRatioForMobileParty));
					}
					else if (initialPartySizeRatioForMobileParty <= 1f)
					{
						num = MBRandom.RoundRandomized((float)minValue + (float)(maxValue - minValue) * initialPartySizeRatioForMobileParty);
					}
					else
					{
						num = MBRandom.RoundRandomized((float)maxValue * initialPartySizeRatioForMobileParty);
					}
					for (int i = 0; i < num; i++)
					{
						list.Add(new Ship(shipTemplateStack.ShipHull));
					}
				}
			}
			return list;
		}

		// Token: 0x04000844 RID: 2116
		private const int BaseMobilePartySize = 20;

		// Token: 0x04000845 RID: 2117
		private const int BaseMobilePartyPrisonerSize = 10;

		// Token: 0x04000846 RID: 2118
		private const int BaseSettlementPrisonerSize = 60;

		// Token: 0x04000847 RID: 2119
		private const int SettlementPrisonerSizeBonusPerWallLevel = 40;

		// Token: 0x04000848 RID: 2120
		private const int BaseGarrisonPartySize = 200;

		// Token: 0x04000849 RID: 2121
		private const int BasePatrolPartySize = 10;

		// Token: 0x0400084A RID: 2122
		private const int TownGarrisonSizeBonus = 200;

		// Token: 0x0400084B RID: 2123
		private const int AdditionalPartySizeForCheat = 5000;

		// Token: 0x0400084C RID: 2124
		private const int OneVillagerPerHearth = 40;

		// Token: 0x0400084D RID: 2125
		private const int AdditionalPartySizeLimitPerTier = 15;

		// Token: 0x0400084E RID: 2126
		private const int AdditionalPartySizeLimitForLeaderPerTier = 25;

		// Token: 0x0400084F RID: 2127
		private readonly TextObject _leadershipSkillLevelBonusText = GameTexts.FindText("str_leadership_skill_level_bonus", null);

		// Token: 0x04000850 RID: 2128
		private readonly TextObject _leadershipPerkUltimateLeaderBonusText = GameTexts.FindText("str_leadership_perk_bonus", null);

		// Token: 0x04000851 RID: 2129
		private readonly TextObject _wallLevelBonusText = GameTexts.FindText("str_map_tooltip_wall_level", null);

		// Token: 0x04000852 RID: 2130
		private readonly TextObject _baseSizeText = GameTexts.FindText("str_base_size", null);

		// Token: 0x04000853 RID: 2131
		private readonly TextObject _clanTierText = GameTexts.FindText("str_clan_tier_bonus", null);

		// Token: 0x04000854 RID: 2132
		private readonly TextObject _renownText = GameTexts.FindText("str_renown_bonus", null);

		// Token: 0x04000855 RID: 2133
		private readonly TextObject _clanLeaderText = GameTexts.FindText("str_clan_leader_bonus", null);

		// Token: 0x04000856 RID: 2134
		private readonly TextObject _factionLeaderText = GameTexts.FindText("str_faction_leader_bonus", null);

		// Token: 0x04000857 RID: 2135
		private readonly TextObject _leaderLevelText = GameTexts.FindText("str_leader_level_bonus", null);

		// Token: 0x04000858 RID: 2136
		private readonly TextObject _townBonusText = GameTexts.FindText("str_town_bonus", null);

		// Token: 0x04000859 RID: 2137
		private readonly TextObject _minorFactionText = GameTexts.FindText("str_minor_faction_bonus", null);

		// Token: 0x0400085A RID: 2138
		private readonly TextObject _currentPartySizeBonusText = GameTexts.FindText("str_current_party_size_bonus", null);

		// Token: 0x0400085B RID: 2139
		private readonly TextObject _randomSizeBonusTemporary = new TextObject("{=hynFV8jC}Extra size bonus (Perk-like Effect)", null);

		// Token: 0x0400085C RID: 2140
		private static bool _addAdditionalPartySizeAsCheat;

		// Token: 0x0400085D RID: 2141
		private static bool _addAdditionalPrisonerSizeAsCheat;

		// Token: 0x02000599 RID: 1433
		private enum LimitType
		{
			// Token: 0x040017DE RID: 6110
			MobilePartySizeLimit,
			// Token: 0x040017DF RID: 6111
			GarrisonPartySizeLimit,
			// Token: 0x040017E0 RID: 6112
			PrisonerSizeLimit
		}
	}
}
