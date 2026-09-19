using System;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x02000139 RID: 313
	public class DefaultPartySpeedCalculatingModel : PartySpeedModel
	{
		// Token: 0x1700069B RID: 1691
		// (get) Token: 0x06001965 RID: 6501 RVA: 0x0007E7B6 File Offset: 0x0007C9B6
		public override float BaseSpeed
		{
			get
			{
				return 4f;
			}
		}

		// Token: 0x1700069C RID: 1692
		// (get) Token: 0x06001966 RID: 6502 RVA: 0x0007E7BD File Offset: 0x0007C9BD
		public override float MinimumSpeed
		{
			get
			{
				return 1f;
			}
		}

		// Token: 0x06001967 RID: 6503 RVA: 0x0007E7C4 File Offset: 0x0007C9C4
		private ExplainedNumber CalculateLandBaseSpeed(MobileParty mobileParty, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0)
		{
			PartyBase party = mobileParty.Party;
			int num = 0;
			float num2 = 0f;
			int num3 = 0;
			int num4 = mobileParty.MemberRoster.TotalManCount + additionalTroopOnFootCount + additionalTroopOnHorseCount;
			this.AddCargoStats(mobileParty, ref num, ref num2, ref num3);
			float num5 = mobileParty.TotalWeightCarried;
			int num6 = (int)Campaign.Current.Models.InventoryCapacityModel.CalculateInventoryCapacity(mobileParty, mobileParty.IsCurrentlyAtSea, false, additionalTroopOnFootCount, additionalTroopOnHorseCount, 0, false).ResultNumber;
			int num7 = party.NumberOfMenWithHorse + additionalTroopOnHorseCount;
			int num8 = party.NumberOfMenWithoutHorse + additionalTroopOnFootCount;
			int num9 = party.MemberRoster.TotalWounded;
			int num10 = party.PrisonRoster.TotalManCount;
			int num11 = party.PartySizeLimit;
			float morale = mobileParty.Morale;
			if (mobileParty.AttachedParties.Count != 0)
			{
				foreach (MobileParty mobileParty2 in mobileParty.AttachedParties)
				{
					this.AddCargoStats(mobileParty2, ref num, ref num2, ref num3);
					num4 += mobileParty2.MemberRoster.TotalManCount;
					num5 += mobileParty2.TotalWeightCarried;
					num6 += mobileParty2.InventoryCapacity;
					num7 += mobileParty2.Party.NumberOfMenWithHorse;
					num8 += mobileParty2.Party.NumberOfMenWithoutHorse;
					num9 += mobileParty2.MemberRoster.TotalWounded;
					num10 += mobileParty2.PrisonRoster.TotalManCount;
					num11 += mobileParty2.Party.PartySizeLimit;
				}
			}
			float baseNumber = this.CalculateBaseSpeedForParty(num4);
			ExplainedNumber result = new ExplainedNumber(baseNumber, includeDescriptions, null);
			bool flag = Campaign.Current.Models.MapWeatherModel.GetWeatherEffectOnTerrainForPosition(mobileParty.Position.ToVec2()) == MapWeatherModel.WeatherEventEffectOnTerrain.Wet;
			this.GetFootmenPerkBonus(mobileParty, num4, num8, ref result);
			float cavalryRatioModifier = this.GetCavalryRatioModifier(num4, num7);
			int num12 = MathF.Min(num8, num);
			float mountedFootmenRatioModifier = this.GetMountedFootmenRatioModifier(num4, num12);
			result.AddFactor(cavalryRatioModifier, DefaultPartySpeedCalculatingModel._textCavalry);
			result.AddFactor(mountedFootmenRatioModifier, DefaultPartySpeedCalculatingModel._textMountedFootmen);
			if (flag)
			{
				float num13 = cavalryRatioModifier * 0.3f;
				float num14 = mountedFootmenRatioModifier * 0.3f;
				result.AddFactor(-num13, DefaultPartySpeedCalculatingModel._textCavalryWeatherPenalty);
				result.AddFactor(-num14, DefaultPartySpeedCalculatingModel._textMountedFootmenWeatherPenalty);
			}
			if (mountedFootmenRatioModifier > 0f && mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Riding.NomadicTraditions))
			{
				result.AddFactor(mountedFootmenRatioModifier * DefaultPerks.Riding.NomadicTraditions.PrimaryBonus, DefaultPerks.Riding.NomadicTraditions.Name);
			}
			float num15 = MathF.Min(num5, (float)num6);
			if (num15 > 0f)
			{
				float cargoEffect = this.GetCargoEffect(num15, num6);
				result.AddFactor(cargoEffect, DefaultPartySpeedCalculatingModel._textCargo);
			}
			if (num2 > (float)num6)
			{
				ExplainedNumber overburdenedEffect = this.GetOverburdenedEffect(mobileParty, num2 - (float)num6, num6, includeDescriptions);
				result.AddFromExplainedNumber(overburdenedEffect, DefaultPartySpeedCalculatingModel._textOverburdened);
			}
			if (mobileParty.HasPerk(DefaultPerks.Riding.SweepingWind, true))
			{
				result.AddFactor(DefaultPerks.Riding.SweepingWind.SecondaryBonus, DefaultPerks.Riding.SweepingWind.Name);
			}
			if (num4 > num11)
			{
				float overPartySizeEffect = this.GetOverPartySizeEffect(num4, num11);
				Clan actualClan = mobileParty.ActualClan;
				if (((actualClan != null) ? actualClan.StringId : null) == "deserters")
				{
					result.AddFactor(overPartySizeEffect * 0.5f, DefaultPartySpeedCalculatingModel._textOverPartySize);
				}
				else
				{
					result.AddFactor(overPartySizeEffect, DefaultPartySpeedCalculatingModel._textOverPartySize);
				}
			}
			num3 += MathF.Max(0, num - num12);
			if (!mobileParty.IsVillager)
			{
				float herdingModifier = this.GetHerdingModifier(num4, num3);
				result.AddFactor(herdingModifier, DefaultPartySpeedCalculatingModel._textHerd);
				if (mobileParty.HasPerk(DefaultPerks.Riding.Shepherd, false))
				{
					result.AddFactor(herdingModifier * DefaultPerks.Riding.Shepherd.PrimaryBonus, DefaultPerks.Riding.Shepherd.Name);
				}
			}
			float woundedModifier = this.GetWoundedModifier(num4, num9, mobileParty);
			result.AddFactor(woundedModifier, DefaultPartySpeedCalculatingModel._textWounded);
			if (!mobileParty.IsCaravan)
			{
				if (mobileParty.Party.NumberOfPrisoners > mobileParty.Party.PrisonerSizeLimit)
				{
					float overPrisonerSizeEffect = this.GetOverPrisonerSizeEffect(mobileParty);
					result.AddFactor(overPrisonerSizeEffect, DefaultPartySpeedCalculatingModel._textOverPrisonerSize);
				}
				float sizeModifierPrisoner = this.GetSizeModifierPrisoner(num4, num10);
				result.AddFactor(1f / sizeModifierPrisoner - 1f, DefaultPartySpeedCalculatingModel._textPrisoners);
			}
			if (morale > 70f)
			{
				result.AddFactor(0.05f * ((morale - 70f) / 30f), DefaultPartySpeedCalculatingModel._textHighMorale);
			}
			if (morale < 30f)
			{
				result.AddFactor(-0.1f * (1f - mobileParty.Morale / 30f), DefaultPartySpeedCalculatingModel._textLowMorale);
			}
			if (mobileParty == MobileParty.MainParty)
			{
				float playerMapMovementSpeedBonusMultiplier = Campaign.Current.Models.DifficultyModel.GetPlayerMapMovementSpeedBonusMultiplier();
				if (playerMapMovementSpeedBonusMultiplier > 0f)
				{
					result.AddFactor(playerMapMovementSpeedBonusMultiplier, GameTexts.FindText("str_game_difficulty", null));
				}
			}
			if (mobileParty.IsCaravan)
			{
				result.AddFactor(0.1f, DefaultPartySpeedCalculatingModel._textCaravan);
			}
			if (mobileParty.IsDisorganized)
			{
				result.AddFactor(-0.4f, DefaultPartySpeedCalculatingModel._textDisorganized);
			}
			result.LimitMin(this.MinimumSpeed);
			return result;
		}

		// Token: 0x06001968 RID: 6504 RVA: 0x0007ECBC File Offset: 0x0007CEBC
		public override ExplainedNumber CalculateBaseSpeed(MobileParty mobileParty, bool includeDescriptions = false, int additionalTroopOnFootCount = 0, int additionalTroopOnHorseCount = 0)
		{
			return this.CalculateLandBaseSpeed(mobileParty, includeDescriptions, additionalTroopOnFootCount, additionalTroopOnHorseCount);
		}

		// Token: 0x06001969 RID: 6505 RVA: 0x0007ECCC File Offset: 0x0007CECC
		private void AddCargoStats(MobileParty mobileParty, ref int numberOfAvailableMounts, ref float totalWeightCarried, ref int herdSize)
		{
			ItemRoster itemRoster = mobileParty.ItemRoster;
			int numberOfPackAnimals = itemRoster.NumberOfPackAnimals;
			int numberOfLivestockAnimals = itemRoster.NumberOfLivestockAnimals;
			herdSize += numberOfPackAnimals + numberOfLivestockAnimals;
			numberOfAvailableMounts += itemRoster.NumberOfMounts;
			totalWeightCarried += mobileParty.TotalWeightCarried;
		}

		// Token: 0x0600196A RID: 6506 RVA: 0x0007ED0E File Offset: 0x0007CF0E
		private float CalculateBaseSpeedForParty(int menCount)
		{
			return this.BaseSpeed * MathF.Pow(200f / (200f + (float)menCount), 0.4f);
		}

		// Token: 0x0600196B RID: 6507 RVA: 0x0007ED30 File Offset: 0x0007CF30
		private ExplainedNumber GetOverburdenedEffect(MobileParty party, float totalWeightCarried, int partyCapacity, bool includeDescriptions)
		{
			ExplainedNumber result = new ExplainedNumber(-0.4f * (totalWeightCarried / (float)partyCapacity), includeDescriptions, null);
			if (!party.IsCurrentlyAtSea)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Athletics.Energetic, party, true, ref result, false);
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Scouting.Unburdened, party, true, ref result, false);
			}
			return result;
		}

		// Token: 0x0600196C RID: 6508 RVA: 0x0007ED78 File Offset: 0x0007CF78
		public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
		{
			if (mobileParty.IsCustomParty && !((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed.ApproximatelyEqualsTo(0f, 1E-05f))
			{
				finalSpeed = new ExplainedNumber(((CustomPartyComponent)mobileParty.PartyComponent).BaseSpeed, false, null);
			}
			TerrainType faceTerrainType = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
			Hero effectiveScout = mobileParty.EffectiveScout;
			if (faceTerrainType == TerrainType.Forest)
			{
				float num = 0f;
				if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForestKin))
				{
					for (int i = 0; i < mobileParty.MemberRoster.Count; i++)
					{
						if (!mobileParty.MemberRoster.GetCharacterAtIndex(i).IsMounted)
						{
							num += (float)mobileParty.MemberRoster.GetElementNumber(i);
						}
					}
				}
				float value = (num / (float)mobileParty.MemberRoster.TotalManCount >= 0.75f) ? (-0.3f * -DefaultPerks.Scouting.ForestKin.PrimaryBonus) : -0.3f;
				finalSpeed.AddFactor(value, DefaultPartySpeedCalculatingModel._movingInForest);
				if (PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.BattanianForestSpeedFeat))
				{
					float value2 = DefaultCulturalFeats.BattanianForestSpeedFeat.EffectBonus * 0.3f;
					finalSpeed.AddFactor(value2, this._culture);
				}
			}
			else if (!mobileParty.IsCurrentlyAtSea && (faceTerrainType == TerrainType.Water || faceTerrainType == TerrainType.River || faceTerrainType == TerrainType.UnderBridge || faceTerrainType == TerrainType.Bridge || faceTerrainType == TerrainType.Fording))
			{
				finalSpeed.AddFactor(-0.3f, DefaultPartySpeedCalculatingModel._fordEffect);
			}
			else if (faceTerrainType == TerrainType.Desert || faceTerrainType == TerrainType.Dune)
			{
				if (!PartyBaseHelper.HasFeat(mobileParty.Party, DefaultCulturalFeats.AseraiDesertFeat))
				{
					finalSpeed.AddFactor(-0.1f, DefaultPartySpeedCalculatingModel._desert);
				}
				if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DesertBorn))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.DesertBorn.PrimaryBonus, DefaultPerks.Scouting.DesertBorn.Name);
				}
			}
			else if ((faceTerrainType == TerrainType.Plain || faceTerrainType == TerrainType.Steppe) && effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Pathfinder))
			{
				finalSpeed.AddFactor(DefaultPerks.Scouting.Pathfinder.PrimaryBonus, DefaultPerks.Scouting.Pathfinder.Name);
			}
			MapWeatherModel.WeatherEvent weatherEventInPosition = Campaign.Current.Models.MapWeatherModel.GetWeatherEventInPosition(mobileParty.Position.ToVec2());
			if (weatherEventInPosition == MapWeatherModel.WeatherEvent.Snowy || weatherEventInPosition == MapWeatherModel.WeatherEvent.Blizzard)
			{
				finalSpeed.AddFactor(-0.1f, DefaultPartySpeedCalculatingModel._snow);
			}
			if (!mobileParty.IsCurrentlyAtSea)
			{
				if (Campaign.Current.IsNight)
				{
					finalSpeed.AddFactor(-0.25f, DefaultPartySpeedCalculatingModel._night);
					if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.NightRunner))
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.NightRunner.PrimaryBonus, DefaultPerks.Scouting.NightRunner.Name);
					}
				}
				else if (effectiveScout != null && effectiveScout.GetPerkValue(DefaultPerks.Scouting.DayTraveler))
				{
					finalSpeed.AddFactor(DefaultPerks.Scouting.DayTraveler.PrimaryBonus, DefaultPerks.Scouting.DayTraveler.Name);
				}
			}
			if (effectiveScout != null)
			{
				if (!mobileParty.IsCurrentlyAtSea)
				{
					PerkHelper.AddEpicPerkBonusForCharacter(DefaultPerks.Scouting.UncannyInsight, effectiveScout.CharacterObject, DefaultSkills.Scouting, true, ref finalSpeed, Campaign.Current.Models.CharacterDevelopmentModel.MinSkillRequiredForEpicPerkBonus, false);
					if (effectiveScout.GetPerkValue(DefaultPerks.Scouting.ForcedMarch) && mobileParty.Morale > 75f)
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.ForcedMarch.PrimaryBonus, DefaultPerks.Scouting.ForcedMarch.Name);
					}
				}
				if (mobileParty.DefaultBehavior == AiBehavior.EngageParty)
				{
					MobileParty targetParty = mobileParty.TargetParty;
					if (targetParty != null && !targetParty.IsCurrentlyAtSea && targetParty.MapFaction.IsAtWarWith(mobileParty.MapFaction) && effectiveScout.GetPerkValue(DefaultPerks.Scouting.Tracker))
					{
						finalSpeed.AddFactor(DefaultPerks.Scouting.Tracker.SecondaryBonus, DefaultPerks.Scouting.Tracker.Name);
					}
				}
			}
			Army army = mobileParty.Army;
			if (((army != null) ? army.LeaderParty : null) != null && mobileParty.Army.LeaderParty != mobileParty && mobileParty.AttachedTo != mobileParty.Army.LeaderParty && !mobileParty.IsCurrentlyAtSea && mobileParty.Army.LeaderParty.HasPerk(DefaultPerks.Tactics.CallToArms, false))
			{
				finalSpeed.AddFactor(DefaultPerks.Tactics.CallToArms.PrimaryBonus, DefaultPerks.Tactics.CallToArms.Name);
			}
			finalSpeed.LimitMin(this.MinimumSpeed);
			return finalSpeed;
		}

		// Token: 0x0600196D RID: 6509 RVA: 0x0007F17E File Offset: 0x0007D37E
		private float GetCargoEffect(float weightCarried, int partyCapacity)
		{
			return -0.02f * weightCarried / (float)partyCapacity;
		}

		// Token: 0x0600196E RID: 6510 RVA: 0x0007F18B File Offset: 0x0007D38B
		private float GetOverPartySizeEffect(int totalMenCount, int partySize)
		{
			return 1f / ((float)totalMenCount / (float)partySize) - 1f;
		}

		// Token: 0x0600196F RID: 6511 RVA: 0x0007F1A0 File Offset: 0x0007D3A0
		private float GetOverPrisonerSizeEffect(MobileParty mobileParty)
		{
			int prisonerSizeLimit = mobileParty.Party.PrisonerSizeLimit;
			int numberOfPrisoners = mobileParty.Party.NumberOfPrisoners;
			return 1f / ((float)numberOfPrisoners / (float)prisonerSizeLimit) - 1f;
		}

		// Token: 0x06001970 RID: 6512 RVA: 0x0007F1D6 File Offset: 0x0007D3D6
		private float GetHerdingModifier(int totalMenCount, int herdSize)
		{
			herdSize -= totalMenCount;
			if (herdSize <= 0)
			{
				return 0f;
			}
			if (totalMenCount == 0)
			{
				return -0.8f;
			}
			return MathF.Max(-0.8f, -0.3f * ((float)herdSize / (float)totalMenCount));
		}

		// Token: 0x06001971 RID: 6513 RVA: 0x0007F208 File Offset: 0x0007D408
		private float GetWoundedModifier(int totalMenCount, int numWounded, MobileParty party)
		{
			if (numWounded <= totalMenCount / 4)
			{
				return 0f;
			}
			if (totalMenCount == 0)
			{
				return -0.5f;
			}
			float baseNumber = MathF.Max(-0.8f, -0.05f * (float)numWounded / (float)totalMenCount);
			ExplainedNumber explainedNumber = new ExplainedNumber(baseNumber, false, null);
			if (!party.IsCurrentlyAtSea)
			{
				PerkHelper.AddPerkBonusForParty(DefaultPerks.Medicine.Sledges, party, true, ref explainedNumber, false);
			}
			return explainedNumber.ResultNumber;
		}

		// Token: 0x06001972 RID: 6514 RVA: 0x0007F268 File Offset: 0x0007D468
		private float GetCavalryRatioModifier(int totalMenCount, int totalCavalryCount)
		{
			if (totalMenCount == 0 || totalCavalryCount == 0)
			{
				return 0f;
			}
			return 0.3f * (float)totalCavalryCount / (float)totalMenCount;
		}

		// Token: 0x06001973 RID: 6515 RVA: 0x0007F281 File Offset: 0x0007D481
		private float GetMountedFootmenRatioModifier(int totalMenCount, int totalMountedFootmenCount)
		{
			if (totalMenCount == 0 || totalMountedFootmenCount == 0)
			{
				return 0f;
			}
			return 0.15f * (float)totalMountedFootmenCount / (float)totalMenCount;
		}

		// Token: 0x06001974 RID: 6516 RVA: 0x0007F29C File Offset: 0x0007D49C
		private void GetFootmenPerkBonus(MobileParty party, int totalMenCount, int totalFootmenCount, ref ExplainedNumber result)
		{
			if (totalMenCount == 0)
			{
				return;
			}
			float num = (float)totalFootmenCount / (float)totalMenCount;
			if (party.HasPerk(DefaultPerks.Athletics.Strong, true) && !num.ApproximatelyEqualsTo(0f, 1E-05f))
			{
				result.AddFactor(num * DefaultPerks.Athletics.Strong.SecondaryBonus, DefaultPerks.Athletics.Strong.Name);
			}
		}

		// Token: 0x06001975 RID: 6517 RVA: 0x0007F2F0 File Offset: 0x0007D4F0
		private float GetSizeModifierWounded(int totalMenCount, int totalWoundedMenCount)
		{
			return MathF.Pow((10f + (float)totalMenCount) / (10f + (float)totalMenCount - (float)totalWoundedMenCount), 0.33f);
		}

		// Token: 0x06001976 RID: 6518 RVA: 0x0007F310 File Offset: 0x0007D510
		private float GetSizeModifierPrisoner(int totalMenCount, int totalPrisonerCount)
		{
			return MathF.Pow((10f + (float)totalMenCount + (float)totalPrisonerCount) / (10f + (float)totalMenCount), 0.33f);
		}

		// Token: 0x0400085E RID: 2142
		private static readonly TextObject _textCargo = new TextObject("{=fSGY71wd}Cargo within capacity", null);

		// Token: 0x0400085F RID: 2143
		private static readonly TextObject _textOverburdened = new TextObject("{=xgO3cCgR}Overburdened", null);

		// Token: 0x04000860 RID: 2144
		private static readonly TextObject _textOverPartySize = new TextObject("{=bO5gL3FI}Men within party size", null);

		// Token: 0x04000861 RID: 2145
		private static readonly TextObject _textOverPrisonerSize = new TextObject("{=Ix8YjLPD}Men within prisoner size", null);

		// Token: 0x04000862 RID: 2146
		private static readonly TextObject _textCavalry = new TextObject("{=YVGtcLHF}Cavalry", null);

		// Token: 0x04000863 RID: 2147
		private static readonly TextObject _textCavalryWeatherPenalty = new TextObject("{=Cb0k9KM8}Cavalry weather penalty", null);

		// Token: 0x04000864 RID: 2148
		private static readonly TextObject _textKhuzaitCavalryBonus = new TextObject("{=yi07dBks}Khuzait cavalry bonus", null);

		// Token: 0x04000865 RID: 2149
		private static readonly TextObject _textMountedFootmen = new TextObject("{=5bSWSaPl}Footmen on horses", null);

		// Token: 0x04000866 RID: 2150
		private static readonly TextObject _textMountedFootmenWeatherPenalty = new TextObject("{=JAKoFNgt}Footmen on horses weather penalty", null);

		// Token: 0x04000867 RID: 2151
		private static readonly TextObject _textWounded = new TextObject("{=aLsVKIRy}Wounded members", null);

		// Token: 0x04000868 RID: 2152
		private static readonly TextObject _textPrisoners = new TextObject("{=N6QTvjMf}Prisoners", null);

		// Token: 0x04000869 RID: 2153
		private static readonly TextObject _textHerd = new TextObject("{=NhAMSaWU}Herding", null);

		// Token: 0x0400086A RID: 2154
		private static readonly TextObject _textHighMorale = new TextObject("{=aDQcIGfH}High morale", null);

		// Token: 0x0400086B RID: 2155
		private static readonly TextObject _textLowMorale = new TextObject("{=ydspCDIy}Low morale", null);

		// Token: 0x0400086C RID: 2156
		private static readonly TextObject _textCaravan = new TextObject("{=vvabqi2w}Caravan", null);

		// Token: 0x0400086D RID: 2157
		private static readonly TextObject _textDisorganized = new TextObject("{=JuwBb2Yg}Disorganized", null);

		// Token: 0x0400086E RID: 2158
		private static readonly TextObject _movingInForest = new TextObject("{=rTFaZCdY}Forest", null);

		// Token: 0x0400086F RID: 2159
		private static readonly TextObject _fordEffect = new TextObject("{=NT5fwUuJ}Fording", null);

		// Token: 0x04000870 RID: 2160
		private static readonly TextObject _night = new TextObject("{=fAxjyMt5}Night", null);

		// Token: 0x04000871 RID: 2161
		private static readonly TextObject _snow = new TextObject("{=vLjgcdgB}Snow", null);

		// Token: 0x04000872 RID: 2162
		private static readonly TextObject _desert = new TextObject("{=ecUwABe2}Desert", null);

		// Token: 0x04000873 RID: 2163
		private static readonly TextObject _sturgiaSnowBonus = new TextObject("{=0VfEGekD}Sturgia snow bonus", null);

		// Token: 0x04000874 RID: 2164
		private readonly TextObject _culture = GameTexts.FindText("str_culture", null);

		// Token: 0x04000875 RID: 2165
		private const float MovingAtForestEffect = -0.3f;

		// Token: 0x04000876 RID: 2166
		private const float MovingAtWaterEffect = -0.3f;

		// Token: 0x04000877 RID: 2167
		private const float MovingAtNightEffect = -0.25f;

		// Token: 0x04000878 RID: 2168
		private const float MovingOnSnowEffect = -0.1f;

		// Token: 0x04000879 RID: 2169
		private const float MovingInDesertEffect = -0.1f;

		// Token: 0x0400087A RID: 2170
		private const float CavalryEffect = 0.3f;

		// Token: 0x0400087B RID: 2171
		private const float MountedFootMenEffect = 0.15f;

		// Token: 0x0400087C RID: 2172
		private const float HerdEffect = -0.4f;

		// Token: 0x0400087D RID: 2173
		private const float WoundedEffect = -0.05f;

		// Token: 0x0400087E RID: 2174
		private const float CargoEffect = -0.02f;

		// Token: 0x0400087F RID: 2175
		private const float OverburdenedEffect = -0.4f;

		// Token: 0x04000880 RID: 2176
		private const float HighMoraleThreshold = 70f;

		// Token: 0x04000881 RID: 2177
		private const float LowMoraleThreshold = 30f;

		// Token: 0x04000882 RID: 2178
		private const float HighMoraleEffect = 0.05f;

		// Token: 0x04000883 RID: 2179
		private const float LowMoraleEffect = -0.1f;

		// Token: 0x04000884 RID: 2180
		private const float DisorganizedEffect = -0.4f;
	}
}
