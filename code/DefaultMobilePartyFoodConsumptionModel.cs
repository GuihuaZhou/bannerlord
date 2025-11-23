using System;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x02000116 RID: 278
	public class DefaultMobilePartyFoodConsumptionModel : MobilePartyFoodConsumptionModel
	{
		// Token: 0x17000661 RID: 1633
		// (get) Token: 0x060017D2 RID: 6098 RVA: 0x000716AB File Offset: 0x0006F8AB
		public override int NumberOfMenOnMapToEatOneFood
		{
			get
			{
				return 20;
			}
		}

		// Token: 0x060017D3 RID: 6099 RVA: 0x000716B0 File Offset: 0x0006F8B0
		public override ExplainedNumber CalculateDailyBaseFoodConsumptionf(MobileParty party, bool includeDescription = false)
		{
			int num = party.Party.NumberOfAllMembers + party.Party.NumberOfPrisoners / 2;
			num = ((num < 1) ? 1 : num);
			return new ExplainedNumber(-(float)num / (float)this.NumberOfMenOnMapToEatOneFood, includeDescription, null);
		}

		// Token: 0x060017D4 RID: 6100 RVA: 0x000716F2 File Offset: 0x0006F8F2
		public override ExplainedNumber CalculateDailyFoodConsumptionf(MobileParty party, ExplainedNumber baseConsumption)
		{
			this.CalculatePerkEffects(party, ref baseConsumption);
			baseConsumption.LimitMax(-0.01f, null);
			return baseConsumption;
		}

		// Token: 0x060017D5 RID: 6101 RVA: 0x0007170C File Offset: 0x0006F90C
		private void CalculatePerkEffects(MobileParty party, ref ExplainedNumber result)
		{
			int num = 0;
			for (int i = 0; i < party.MemberRoster.Count; i++)
			{
				if (party.MemberRoster.GetCharacterAtIndex(i).Culture.IsBandit)
				{
					num += party.MemberRoster.GetElementNumber(i);
				}
			}
			for (int j = 0; j < party.PrisonRoster.Count; j++)
			{
				if (party.PrisonRoster.GetCharacterAtIndex(j).Culture.IsBandit)
				{
					num += party.PrisonRoster.GetElementNumber(j);
				}
			}
			if (party.LeaderHero != null && party.LeaderHero.GetPerkValue(DefaultPerks.Roguery.Promises) && num > 0)
			{
				float value = (float)num / (float)party.MemberRoster.TotalManCount * DefaultPerks.Roguery.Promises.PrimaryBonus;
				result.AddFactor(value, DefaultPerks.Roguery.Promises.Name);
			}
			PerkHelper.AddPerkBonusForParty(DefaultPerks.Athletics.Spartan, party, false, ref result, party.IsCurrentlyAtSea);
			if (!party.IsCurrentlyAtSea)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.WarriorsDiet, party, true, ref result, false);
			}
			if (party.EffectiveQuartermaster != null)
			{
				PerkHelper.AddEpicPerkBonusForCharacter(DefaultPerks.Steward.PriceOfLoyalty, party.EffectiveQuartermaster.CharacterObject, DefaultSkills.Steward, true, ref result, Campaign.Current.Models.CharacterDevelopmentModel.MaxSkillRequiredForEpicPerkBonus, false);
			}
			TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(party.CurrentNavigationFace);
			if (faceTerrainType == TerrainType.Forest || faceTerrainType == TerrainType.Steppe)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.Foragers, party, true, ref result, false);
			}
			if (party.IsGarrison && party.CurrentSettlement != null && party.CurrentSettlement.Town.IsUnderSiege)
			{
				PerkHelper.AddPerkBonusForTown(DefaultPerks.Athletics.StrongLegs, party.CurrentSettlement.Town, ref result);
			}
			if (party.Army != null)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.StiffUpperLip, party, true, ref result, party.IsCurrentlyAtSea);
			}
			SiegeEvent siegeEvent = party.SiegeEvent;
			if (((siegeEvent != null) ? siegeEvent.BesiegerCamp : null) != null)
			{
				if (party.HasPerk(DefaultPerks.Steward.SoundReserves, true))
				{
					PerkHelper.AddPerkBonusForParty(DefaultPerks.Steward.SoundReserves, party, false, ref result, false);
				}
				if (party.SiegeEvent.BesiegerCamp.HasInvolvedPartyForEventType(party.Party, MapEvent.BattleTypes.Siege) && party.HasPerk(DefaultPerks.Steward.MasterOfPlanning, false))
				{
					result.AddFactor(DefaultPerks.Steward.MasterOfPlanning.PrimaryBonus, DefaultPerks.Steward.MasterOfPlanning.Name);
				}
			}
		}

		// Token: 0x060017D6 RID: 6102 RVA: 0x00071934 File Offset: 0x0006FB34
		public override bool DoesPartyConsumeFood(MobileParty mobileParty)
		{
			return mobileParty.IsActive && (mobileParty.LeaderHero == null || mobileParty.LeaderHero.IsLord || mobileParty.LeaderHero.Clan == Clan.PlayerClan || mobileParty.LeaderHero.IsMinorFactionHero) && !mobileParty.IsGarrison && !mobileParty.IsCaravan && !mobileParty.IsBandit && !mobileParty.IsMilitia && !mobileParty.IsPatrolParty && !mobileParty.IsVillager;
		}

		// Token: 0x040007B7 RID: 1975
		private const float MinimumDailyFoodConsumption = 0.01f;

		// Token: 0x040007B8 RID: 1976
		private static readonly TextObject _partyConsumption = new TextObject("{=UrFzdy4z}Daily Consumption", null);
	}
}
