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
	// Token: 0x02000136 RID: 310
	public class DefaultPartySizeLimitModel : PartySizeLimitModel
	{
		// Token: 0x17000689 RID: 1673
		// (get) Token: 0x06001905 RID: 6405 RVA: 0x0007B263 File Offset: 0x00079463
		public override int MinimumNumberOfVillagersAtVillagerParty
		{
			get
			{
				return 12;
			}
		}

		// Token: 0x06001907 RID: 6407 RVA: 0x0007B358 File Offset: 0x00079558
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

		// Token: 0x06001908 RID: 6408 RVA: 0x0007B3CC File Offset: 0x000795CC
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

		// Token: 0x06001909 RID: 6409 RVA: 0x0007B464 File Offset: 0x00079664
		private int GetPatrolPartySizeLimitFromGuardHouseLevel(int level)
		{
			return 10 + 5 * level;
		}

		// Token: 0x0600190A RID: 6410 RVA: 0x0007B46C File Offset: 0x0007966C
		public override ExplainedNumber GetPartyPrisonerSizeLimit(PartyBase party, bool includeDescriptions = false)
		{
			if (party.IsSettlement)
			{
				return this.CalculateSettlementPartyPrisonerSizeLimitInternal(party.Settlement, includeDescriptions);
			}
			return this.CalculateMobilePartyPrisonerSizeLimitInternal(party, includeDescriptions);
		}

		// Token: 0x0600190B RID: 6411 RVA: 0x0007B48C File Offset: 0x0007968C
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

		// Token: 0x0600190C RID: 6412 RVA: 0x0007B670 File Offset: 0x00079870
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

		// Token: 0x0600190D RID: 6413 RVA: 0x0007B6DC File Offset: 0x000798DC
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

		// Token: 0x0600190E RID: 6414 RVA: 0x0007B730 File Offset: 0x00079930
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

		// Token: 0x0600190F RID: 6415 RVA: 0x0007B7B0 File Offset: 0x000799B0
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

		// Token: 0x06001910 RID: 6416 RVA: 0x0007B891 File Offset: 0x00079A91
		private void AddGarrisonOwnerPerkEffects(Settlement currentSettlement, ref ExplainedNumber result)
		{
			if (currentSettlement != null && currentSettlement.IsFortification)
			{
				PerkHelper.AddPerkBonusForTown(DefaultPerks.OneHanded.CorpsACorps, currentSettlement.Town, ref result);
				PerkHelper.AddPerkBonusForTown(DefaultPerks.Leadership.VeteransRespect, currentSettlement.Town, ref result);
			}
		}

		// Token: 0x06001911 RID: 6417 RVA: 0x0007B8C0 File Offset: 0x00079AC0
		public override int GetNextClanTierPartySizeEffectChangeForHero(Hero hero)
		{
			int tierEffectInternal = this.GetTierEffectInternal(hero.Clan.Tier, hero.Clan.Leader == hero);
			return this.GetTierEffectInternal(hero.Clan.Tier + 1, hero.Clan.Leader == hero) - tierEffectInternal;
		}

		// Token: 0x06001912 RID: 6418 RVA: 0x0007B910 File Offset: 0x00079B10
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

		// Token: 0x06001913 RID: 6419 RVA: 0x0007B924 File Offset: 0x00079B24
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

		// Token: 0x06001914 RID: 6420 RVA: 0x0007B979 File Offset: 0x00079B79
		public override int GetClanTierPartySizeEffectForHero(Hero hero)
		{
			return this.GetTierEffectInternal(hero.Clan.Tier, hero.Clan.Leader == hero);
		}

		// Token: 0x06001915 RID: 6421 RVA: 0x0007B99A File Offset: 0x00079B9A
		private void AddSettlementProjectBonuses(Settlement settlement, ref ExplainedNumber result)
		{
			if (settlement != null && settlement.IsFortification)
			{
				settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.GarrisonCapacity, ref result);
			}
		}

		// Token: 0x06001916 RID: 6422 RVA: 0x0007B9B4 File Offset: 0x00079BB4
		private void AddSettlementProjectPrisonerBonuses(Settlement settlement, ref ExplainedNumber result)
		{
			if (settlement != null && settlement.IsFortification)
			{
				settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.PrisonCapacity, ref result);
			}
		}

		// Token: 0x06001917 RID: 6423 RVA: 0x0007B9CF File Offset: 0x00079BCF
		private int GetCurrentPartySizeEffect(PartyBase party)
		{
			return party.NumberOfHealthyMembers / 2;
		}

		// Token: 0x06001918 RID: 6424 RVA: 0x0007B9DC File Offset: 0x00079BDC
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

		// Token: 0x06001919 RID: 6425 RVA: 0x0007BD68 File Offset: 0x00079F68
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

		// Token: 0x0600191A RID: 6426 RVA: 0x0007BE00 File Offset: 0x0007A000
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

		// Token: 0x0600191B RID: 6427 RVA: 0x0007BEBC File Offset: 0x0007A0BC
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

		// Token: 0x0600191C RID: 6428 RVA: 0x0007BF84 File Offset: 0x0007A184
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

		// Token: 0x0600191D RID: 6429 RVA: 0x0007C0C0 File Offset: 0x0007A2C0
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

		// Token: 0x04000810 RID: 2064
		private const int BaseMobilePartySize = 20;

		// Token: 0x04000811 RID: 2065
		private const int BaseMobilePartyPrisonerSize = 10;

		// Token: 0x04000812 RID: 2066
		private const int BaseSettlementPrisonerSize = 60;

		// Token: 0x04000813 RID: 2067
		private const int SettlementPrisonerSizeBonusPerWallLevel = 40;

		// Token: 0x04000814 RID: 2068
		private const int BaseGarrisonPartySize = 200;

		// Token: 0x04000815 RID: 2069
		private const int BasePatrolPartySize = 10;

		// Token: 0x04000816 RID: 2070
		private const int TownGarrisonSizeBonus = 200;

		// Token: 0x04000817 RID: 2071
		private const int AdditionalPartySizeForCheat = 5000;

		// Token: 0x04000818 RID: 2072
		private const int OneVillagerPerHearth = 40;

		// Token: 0x04000819 RID: 2073
		private const int AdditionalPartySizeLimitPerTier = 15;

		// Token: 0x0400081A RID: 2074
		private const int AdditionalPartySizeLimitForLeaderPerTier = 25;

		// Token: 0x0400081B RID: 2075
		private readonly TextObject _leadershipSkillLevelBonusText = GameTexts.FindText("str_leadership_skill_level_bonus", null);

		// Token: 0x0400081C RID: 2076
		private readonly TextObject _leadershipPerkUltimateLeaderBonusText = GameTexts.FindText("str_leadership_perk_bonus", null);

		// Token: 0x0400081D RID: 2077
		private readonly TextObject _wallLevelBonusText = GameTexts.FindText("str_map_tooltip_wall_level", null);

		// Token: 0x0400081E RID: 2078
		private readonly TextObject _baseSizeText = GameTexts.FindText("str_base_size", null);

		// Token: 0x0400081F RID: 2079
		private readonly TextObject _clanTierText = GameTexts.FindText("str_clan_tier_bonus", null);

		// Token: 0x04000820 RID: 2080
		private readonly TextObject _renownText = GameTexts.FindText("str_renown_bonus", null);

		// Token: 0x04000821 RID: 2081
		private readonly TextObject _clanLeaderText = GameTexts.FindText("str_clan_leader_bonus", null);

		// Token: 0x04000822 RID: 2082
		private readonly TextObject _factionLeaderText = GameTexts.FindText("str_faction_leader_bonus", null);

		// Token: 0x04000823 RID: 2083
		private readonly TextObject _leaderLevelText = GameTexts.FindText("str_leader_level_bonus", null);

		// Token: 0x04000824 RID: 2084
		private readonly TextObject _townBonusText = GameTexts.FindText("str_town_bonus", null);

		// Token: 0x04000825 RID: 2085
		private readonly TextObject _minorFactionText = GameTexts.FindText("str_minor_faction_bonus", null);

		// Token: 0x04000826 RID: 2086
		private readonly TextObject _currentPartySizeBonusText = GameTexts.FindText("str_current_party_size_bonus", null);

		// Token: 0x04000827 RID: 2087
		private readonly TextObject _randomSizeBonusTemporary = new TextObject("{=hynFV8jC}Extra size bonus (Perk-like Effect)", null);

		// Token: 0x04000828 RID: 2088
		private static bool _addAdditionalPartySizeAsCheat;

		// Token: 0x04000829 RID: 2089
		private static bool _addAdditionalPrisonerSizeAsCheat;

		// Token: 0x0200058E RID: 1422
		private enum LimitType
		{
			// Token: 0x04001764 RID: 5988
			MobilePartySizeLimit,
			// Token: 0x04001765 RID: 5989
			GarrisonPartySizeLimit,
			// Token: 0x04001766 RID: 5990
			PrisonerSizeLimit
		}
	}
}
