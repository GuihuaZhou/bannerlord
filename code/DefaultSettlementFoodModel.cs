using System;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x0200014B RID: 331
	public class DefaultSettlementFoodModel : SettlementFoodModel
	{
		// Token: 0x170006A7 RID: 1703
		// (get) Token: 0x060019E7 RID: 6631 RVA: 0x00082E3F File Offset: 0x0008103F
		public override int FoodStocksUpperLimit
		{
			get
			{
				return 300;
			}
		}

		// Token: 0x170006A8 RID: 1704
		// (get) Token: 0x060019E8 RID: 6632 RVA: 0x00082E46 File Offset: 0x00081046
		public override int NumberOfProsperityToEatOneFood
		{
			get
			{
				return 40;
			}
		}

		// Token: 0x170006A9 RID: 1705
		// (get) Token: 0x060019E9 RID: 6633 RVA: 0x00082E4A File Offset: 0x0008104A
		public override int NumberOfMenOnGarrisonToEatOneFood
		{
			get
			{
				return 20;
			}
		}

		// Token: 0x170006AA RID: 1706
		// (get) Token: 0x060019EA RID: 6634 RVA: 0x00082E4E File Offset: 0x0008104E
		public override int CastleFoodStockUpperLimitBonus
		{
			get
			{
				return 150;
			}
		}

		// Token: 0x060019EB RID: 6635 RVA: 0x00082E55 File Offset: 0x00081055
		public override ExplainedNumber CalculateTownFoodStocksChange(Town town, bool includeMarketStocks = true, bool includeDescriptions = false)
		{
			return this.CalculateTownFoodChangeInternal(town, includeMarketStocks, includeDescriptions);
		}

		// Token: 0x060019EC RID: 6636 RVA: 0x00082E60 File Offset: 0x00081060
		private ExplainedNumber CalculateTownFoodChangeInternal(Town town, bool includeMarketStocks, bool includeDescriptions)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, includeDescriptions, null);
			ExplainedNumber explainedNumber2 = new ExplainedNumber(0f, includeDescriptions, null);
			ExplainedNumber explainedNumber3 = new ExplainedNumber(town.Prosperity / (float)this.NumberOfProsperityToEatOneFood, false, null);
			MobileParty garrisonParty = town.GarrisonParty;
			int? num = (garrisonParty != null) ? new int?(garrisonParty.Party.NumberOfAllMembers) : null;
			ExplainedNumber explainedNumber4 = new ExplainedNumber(((num != null) ? ((float)num.GetValueOrDefault()) : 0f) / (float)this.NumberOfMenOnGarrisonToEatOneFood, false, null);
			if (town.IsUnderSiege)
			{
				PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.Gourmet, town, ref explainedNumber4);
				PerkHelper.AddPerkBonusForTown(DefaultPerks.Medicine.TriageTent, town, ref explainedNumber2);
			}
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.MasterOfWarcraft, town, ref explainedNumber3);
			explainedNumber2.Add(explainedNumber3.ResultNumber, this.ProsperityText, null);
			explainedNumber2.Add(explainedNumber4.ResultNumber, this.GarrisonText, null);
			town.AddEffectOfBuildings(BuildingEffectEnum.FoodConsumption, ref explainedNumber2);
			Clan ownerClan = town.Settlement.OwnerClan;
			Kingdom kingdom = (ownerClan != null) ? ownerClan.Kingdom : null;
			if (kingdom != null && kingdom.HasPolicy(DefaultPolicies.HuntingRights))
			{
				explainedNumber.Add(2f, DefaultPolicies.HuntingRights.Name, null);
			}
			if (!town.IsUnderSiege)
			{
				int num2 = town.IsTown ? 15 : 10;
				explainedNumber.Add((float)num2, this.LandsAroundSettlementText, null);
				foreach (Village village in town.Owner.Settlement.BoundVillages)
				{
					float value = 0f;
					if (village.VillageState == Village.VillageStates.Normal)
					{
						value = (float)((village.GetHearthLevel() + 1) * 6);
					}
					explainedNumber.Add(value, village.Name, null);
				}
				town.AddEffectOfBuildings(BuildingEffectEnum.FoodProduction, ref explainedNumber);
			}
			else
			{
				PerkHelper.AddPerkBonusForTown(DefaultPerks.Roguery.DirtyFighting, town, ref explainedNumber);
			}
			if (includeMarketStocks)
			{
				foreach (Town.SellLog sellLog in town.SoldItems)
				{
					if (sellLog.Category.Properties == ItemCategory.Property.BonusToFoodStores)
					{
						explainedNumber.Add((float)sellLog.Number, includeDescriptions ? sellLog.Category.GetName() : null, null);
					}
				}
			}
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			result.AddFromExplainedNumber(explainedNumber, null);
			result.SubtractFromExplainedNumber(explainedNumber2, null);
			DefaultSettlementFoodModel.GetSettlementFoodChangeDueToIssues(town, ref result);
			return result;
		}

		// Token: 0x060019ED RID: 6637 RVA: 0x000830F0 File Offset: 0x000812F0
		private static void GetSettlementFoodChangeDueToIssues(Town town, ref ExplainedNumber explainedNumber)
		{
			Campaign.Current.Models.IssueModel.GetIssueEffectsOfSettlement(DefaultIssueEffects.SettlementFood, town.Settlement, ref explainedNumber);
		}

		// Token: 0x04000892 RID: 2194
		private readonly TextObject ProsperityText = GameTexts.FindText("str_prosperity", null);

		// Token: 0x04000893 RID: 2195
		private readonly TextObject GarrisonText = GameTexts.FindText("str_garrison", null);

		// Token: 0x04000894 RID: 2196
		private readonly TextObject LandsAroundSettlementText = GameTexts.FindText("str_lands_around_settlement", null);

		// Token: 0x04000895 RID: 2197
		private readonly TextObject NormalVillagesText = GameTexts.FindText("str_normal_villages", null);

		// Token: 0x04000896 RID: 2198
		private readonly TextObject RaidedVillagesText = GameTexts.FindText("str_raided_villages", null);

		// Token: 0x04000897 RID: 2199
		private readonly TextObject VillagesUnderSiegeText = GameTexts.FindText("str_villages_under_siege", null);

		// Token: 0x04000898 RID: 2200
		private readonly TextObject FoodBoughtByCiviliansText = GameTexts.FindText("str_food_bought_by_civilians", null);

		// Token: 0x04000899 RID: 2201
		private const int FoodProductionPerVillage = 10;
	}
}
