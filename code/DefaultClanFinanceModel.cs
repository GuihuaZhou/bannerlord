using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x02000102 RID: 258
	public class DefaultClanFinanceModel : ClanFinanceModel
	{
		// Token: 0x17000637 RID: 1591
		// (get) Token: 0x060016BE RID: 5822 RVA: 0x00067E70 File Offset: 0x00066070
		public override int PartyGoldLowerThreshold
		{
			get
			{
				return 5000;
			}
		}

		// Token: 0x060016BF RID: 5823 RVA: 0x00067E78 File Offset: 0x00066078
		public override ExplainedNumber CalculateClanGoldChange(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
		{
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			this.CalculateClanIncomeInternal(clan, ref result, applyWithdrawals, includeDetails);
			this.CalculateClanExpensesInternal(clan, ref result, applyWithdrawals, includeDetails);
			return result;
		}

		// Token: 0x060016C0 RID: 5824 RVA: 0x00067EAC File Offset: 0x000660AC
		public override ExplainedNumber CalculateClanIncome(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
		{
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			this.CalculateClanIncomeInternal(clan, ref result, applyWithdrawals, includeDetails);
			return result;
		}

		// Token: 0x060016C1 RID: 5825 RVA: 0x00067ED4 File Offset: 0x000660D4
		private void CalculateClanIncomeInternal(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
		{
			if (clan.IsEliminated)
			{
				return;
			}
			Kingdom kingdom = clan.Kingdom;
			if (((kingdom != null) ? kingdom.RulingClan : null) == clan)
			{
				this.AddRulingClanIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
			}
			if (clan != Clan.PlayerClan && (!clan.MapFaction.IsKingdomFaction || clan.IsUnderMercenaryService) && clan.Fiefs.Count == 0)
			{
				int num = clan.Tier * (80 + (clan.IsUnderMercenaryService ? 40 : 0));
				goldChange.Add((float)num, null, null);
			}
			this.AddMercenaryIncome(clan, ref goldChange, applyWithdrawals);
			this.AddSettlementIncome(clan, ref goldChange, applyWithdrawals, includeDetails);
			this.CalculateHeroIncomeFromWorkshops(clan.Leader, ref goldChange, applyWithdrawals);
			this.AddIncomeFromParties(clan, ref goldChange, applyWithdrawals, includeDetails);
			if (clan == Clan.PlayerClan)
			{
				this.AddPlayerClanIncomeFromOwnedAlleys(ref goldChange);
			}
			if (!clan.IsUnderMercenaryService)
			{
				this.AddIncomeFromTribute(clan, ref goldChange, applyWithdrawals, includeDetails);
				this.AddIncomeFromCallToWarAgrements(clan, ref goldChange, applyWithdrawals);
			}
			if (clan.Gold < 30000 && clan.Kingdom != null && clan.Leader != Hero.MainHero && !clan.IsUnderMercenaryService)
			{
				this.AddIncomeFromKingdomBudget(clan, ref goldChange, applyWithdrawals);
			}
			Hero leader = clan.Leader;
			if (leader != null && leader.GetPerkValue(DefaultPerks.Trade.SpringOfGold))
			{
				int num2 = MathF.Min(1000, MathF.Round((float)clan.Leader.Gold * DefaultPerks.Trade.SpringOfGold.PrimaryBonus));
				goldChange.Add((float)num2, DefaultPerks.Trade.SpringOfGold.Name, null);
			}
		}

		// Token: 0x060016C2 RID: 5826 RVA: 0x00068034 File Offset: 0x00066234
		public void CalculateClanExpensesInternal(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false, bool includeDetails = false)
		{
			this.AddExpensesFromPartiesAndGarrisons(clan, ref goldChange, applyWithdrawals, includeDetails);
			if (!clan.IsUnderMercenaryService)
			{
				this.AddExpensesForHiredMercenaries(clan, ref goldChange, applyWithdrawals);
				this.AddExpensesForTributes(clan, ref goldChange, applyWithdrawals);
			}
			this.AddExpensesForAutoRecruitment(clan, ref goldChange, applyWithdrawals);
			if (clan.Gold > 100000 && clan.Kingdom != null && clan.Leader != Hero.MainHero && !clan.IsUnderMercenaryService)
			{
				int num = (int)(((float)clan.Gold - 100000f) * 0.01f);
				if (applyWithdrawals)
				{
					clan.Kingdom.KingdomBudgetWallet += num;
				}
				goldChange.Add((float)(-(float)num), Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._kingdomBudgetStr, null), null);
			}
			if (clan.DebtToKingdom > 0)
			{
				this.AddPaymentForDebts(clan, ref goldChange, applyWithdrawals);
			}
			if (Clan.PlayerClan == clan)
			{
				this.AddPlayerExpenseForWorkshops(ref goldChange);
			}
			if (!clan.IsUnderMercenaryService)
			{
				this.AddExpensesForCallToWarAgreements(clan, ref goldChange, applyWithdrawals);
			}
		}

		// Token: 0x060016C3 RID: 5827 RVA: 0x00068118 File Offset: 0x00066318
		private void AddPlayerExpenseForWorkshops(ref ExplainedNumber goldChange)
		{
			int num = 0;
			foreach (Workshop workshop in Hero.MainHero.OwnedWorkshops)
			{
				if (workshop.Capital < Campaign.Current.Models.WorkshopModel.CapitalLowLimit)
				{
					num -= workshop.Expense;
				}
			}
			goldChange.Add((float)num, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._shopExpenseStr, null), null);
		}

		// Token: 0x060016C4 RID: 5828 RVA: 0x000681B0 File Offset: 0x000663B0
		public override ExplainedNumber CalculateClanExpenses(Clan clan, bool includeDescriptions = false, bool applyWithdrawals = false, bool includeDetails = false)
		{
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			this.CalculateClanExpensesInternal(clan, ref result, applyWithdrawals, includeDetails);
			return result;
		}

		// Token: 0x060016C5 RID: 5829 RVA: 0x000681D8 File Offset: 0x000663D8
		private void AddPaymentForDebts(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			if (clan.Kingdom != null && clan.DebtToKingdom > 0)
			{
				int num = clan.DebtToKingdom;
				if (applyWithdrawals)
				{
					num = MathF.Min(num, (int)((float)clan.Gold + goldChange.ResultNumber));
					clan.DebtToKingdom -= num;
				}
				goldChange.Add((float)(-(float)num), Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._debtStr, null), null);
			}
		}

		// Token: 0x060016C6 RID: 5830 RVA: 0x00068244 File Offset: 0x00066444
		private void AddRulingClanIncome(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, goldChange.IncludeDescriptions, null);
			int num = 0;
			int num2 = 0;
			bool flag = clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.LandTax);
			float num3 = 0f;
			foreach (Town town in clan.Fiefs)
			{
				num += (int)Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(town, false).ResultNumber;
				num2++;
			}
			if (flag)
			{
				foreach (Village village in clan.Kingdom.Villages)
				{
					if (!village.IsOwnerUnassigned && village.Settlement.OwnerClan != clan && village.VillageState != Village.VillageStates.Looted && village.VillageState != Village.VillageStates.BeingRaided)
					{
						int num4 = (int)((float)village.TradeTaxAccumulated / this.RevenueSmoothenFraction());
						num3 += (float)num4 * 0.05f;
					}
				}
				if (num3 > 1E-05f)
				{
					explainedNumber.Add((float)((int)num3), DefaultPolicies.LandTax.Name, null);
				}
			}
			Kingdom kingdom = clan.Kingdom;
			if (kingdom.RulingClan == clan)
			{
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.WarTax))
				{
					int num5 = (int)((float)num * 0.05f);
					explainedNumber.Add((float)num5, DefaultPolicies.WarTax.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.DebasementOfTheCurrency))
				{
					explainedNumber.Add((float)(num2 * 100), DefaultPolicies.DebasementOfTheCurrency.Name, null);
				}
			}
			int num6 = 0;
			int num7 = 0;
			foreach (Settlement settlement in clan.Settlements)
			{
				if (settlement.IsTown)
				{
					if (kingdom.ActivePolicies.Contains(DefaultPolicies.RoadTolls))
					{
						int num8 = settlement.Town.TradeTaxAccumulated / 30;
						if (applyWithdrawals)
						{
							settlement.Town.TradeTaxAccumulated -= num8;
						}
						num6 += num8;
					}
					if (kingdom.ActivePolicies.Contains(DefaultPolicies.StateMonopolies))
					{
						num7 += (int)((float)settlement.Town.Workshops.Sum((Workshop t) => t.ProfitMade) * 0.05f);
					}
					if (num6 > 0)
					{
						explainedNumber.Add((float)num6, DefaultPolicies.RoadTolls.Name, null);
					}
					if (num7 > 0)
					{
						explainedNumber.Add((float)num7, DefaultPolicies.StateMonopolies.Name, null);
					}
				}
			}
			if (!explainedNumber.ResultNumber.ApproximatelyEqualsTo(0f, 1E-05f))
			{
				if (!includeDetails)
				{
					goldChange.Add(explainedNumber.ResultNumber, GameTexts.FindText("str_policies", null), null);
					return;
				}
				goldChange.AddFromExplainedNumber(explainedNumber, GameTexts.FindText("str_policies", null));
			}
		}

		// Token: 0x060016C7 RID: 5831 RVA: 0x00068574 File Offset: 0x00066774
		private void AddExpensesForHiredMercenaries(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			Kingdom kingdom = clan.Kingdom;
			if (kingdom != null)
			{
				float num = DefaultClanFinanceModel.CalculateShareFactor(clan);
				if (kingdom.MercenaryWallet < 0)
				{
					int num2 = (int)((float)(-(float)kingdom.MercenaryWallet) * num);
					DefaultClanFinanceModel.ApplyShareForExpenses(clan, ref goldChange, applyWithdrawals, num2, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._mercenaryExpensesStr, null));
					if (applyWithdrawals)
					{
						kingdom.MercenaryWallet += num2;
					}
				}
			}
		}

		// Token: 0x060016C8 RID: 5832 RVA: 0x000685D8 File Offset: 0x000667D8
		private void AddExpensesForTributes(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			Kingdom kingdom = clan.Kingdom;
			if (kingdom != null)
			{
				float num = DefaultClanFinanceModel.CalculateShareFactor(clan);
				if (kingdom.TributeWallet < 0)
				{
					int num2 = (int)((float)(-(float)kingdom.TributeWallet) * num);
					DefaultClanFinanceModel.ApplyShareForExpenses(clan, ref goldChange, applyWithdrawals, num2, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._tributeExpensesStr, null));
					if (applyWithdrawals)
					{
						kingdom.TributeWallet += num2;
					}
				}
			}
		}

		// Token: 0x060016C9 RID: 5833 RVA: 0x0006863C File Offset: 0x0006683C
		private void AddExpensesForCallToWarAgreements(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			Kingdom kingdom = clan.Kingdom;
			if (kingdom != null && kingdom.CallToWarWallet < 0)
			{
				float num = DefaultClanFinanceModel.CalculateShareFactor(clan);
				int num2 = (int)((float)(-(float)kingdom.CallToWarWallet) * num);
				int num3 = num2;
				int num4 = (int)((float)clan.Gold + goldChange.ResultNumber);
				if (applyWithdrawals && num4 - num3 < 5000)
				{
					num3 = MathF.Max(0, num4 - 5000);
					clan.DebtToKingdom += num2 - num3;
				}
				DefaultClanFinanceModel.ApplyShareForExpenses(clan, ref goldChange, applyWithdrawals, num3, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._callToWarExpenses, null));
				if (applyWithdrawals)
				{
					kingdom.CallToWarWallet += num2;
				}
			}
		}

		// Token: 0x060016CA RID: 5834 RVA: 0x000686E4 File Offset: 0x000668E4
		private static void ApplyShareForExpenses(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, int expenseShare, TextObject mercenaryExpensesStr)
		{
			if (applyWithdrawals)
			{
				int num = (int)((float)clan.Gold + goldChange.ResultNumber);
				if (expenseShare > num)
				{
					int num2 = expenseShare - num;
					expenseShare = num;
					clan.DebtToKingdom += num2;
				}
			}
			goldChange.Add((float)(-(float)expenseShare), mercenaryExpensesStr, null);
		}

		// Token: 0x060016CB RID: 5835 RVA: 0x0006872C File Offset: 0x0006692C
		private void AddSettlementIncome(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, goldChange.IncludeDescriptions, null);
			foreach (Town town in clan.Fiefs)
			{
				ExplainedNumber explainedNumber2 = Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(town, false);
				ExplainedNumber explainedNumber3 = Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromTariffs(clan, town, applyWithdrawals);
				int num = Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromProjects(town);
				explainedNumber.Add((float)((int)explainedNumber2.ResultNumber), Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._townTaxStr, null), town.Name);
				explainedNumber.Add((float)((int)explainedNumber3.ResultNumber), Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._tariffTaxStr, null), town.Name);
				explainedNumber.Add((float)num, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._projectsIncomeStr, null), null);
				foreach (Village village in town.Villages)
				{
					int num2 = this.CalculateVillageIncome(clan, village, applyWithdrawals);
					explainedNumber.Add((float)num2, village.Name, null);
				}
			}
			if (!includeDetails)
			{
				goldChange.Add(explainedNumber.ResultNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._settlementIncome, null), null);
				return;
			}
			goldChange.AddFromExplainedNumber(explainedNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._settlementIncome, null));
		}

		// Token: 0x060016CC RID: 5836 RVA: 0x00068908 File Offset: 0x00066B08
		public override ExplainedNumber CalculateTownIncomeFromTariffs(Clan clan, Town town, bool applyWithdrawals = false)
		{
			ExplainedNumber result = new ExplainedNumber((float)((int)((float)town.TradeTaxAccumulated / this.RevenueSmoothenFraction())), false, null);
			int num = MathF.Round(result.ResultNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Trade.ContentTrades, town, ref result);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Crossbow.Steady, town, ref result);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Roguery.SaltTheEarth, town, ref result);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Steward.GivingHands, town, ref result);
			this.CalculateSettlementProjectTariffBonuses(town, ref result);
			if (applyWithdrawals)
			{
				town.TradeTaxAccumulated -= num;
				if (clan == Clan.PlayerClan)
				{
					CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType.Taxes, (int)result.ResultNumber);
				}
			}
			return result;
		}

		// Token: 0x060016CD RID: 5837 RVA: 0x000689A5 File Offset: 0x00066BA5
		private void CalculateSettlementProjectTariffBonuses(Town town, ref ExplainedNumber result)
		{
			town.AddEffectOfBuildings(BuildingEffectEnum.TariffIncome, ref result);
		}

		// Token: 0x060016CE RID: 5838 RVA: 0x000689B0 File Offset: 0x00066BB0
		public override int CalculateTownIncomeFromProjects(Town town)
		{
			ExplainedNumber explainedNumber = default(ExplainedNumber);
			if (town.CurrentDefaultBuilding != null && town.Governor != null && town.Governor.GetPerkValue(DefaultPerks.Engineering.ArchitecturalCommisions))
			{
				explainedNumber.Add((float)((int)DefaultPerks.Engineering.ArchitecturalCommisions.SecondaryBonus), null, null);
			}
			town.AddEffectOfBuildings(BuildingEffectEnum.DenarByBoundVillageHeartPerDay, ref explainedNumber);
			return (int)explainedNumber.ResultNumber;
		}

		// Token: 0x060016CF RID: 5839 RVA: 0x00068A10 File Offset: 0x00066C10
		public override int CalculateVillageIncome(Clan clan, Village village, bool applyWithdrawals = false)
		{
			int num = (village.VillageState == Village.VillageStates.Looted || village.VillageState == Village.VillageStates.BeingRaided) ? 0 : ((int)((float)village.TradeTaxAccumulated / this.RevenueSmoothenFraction()));
			int num2 = num;
			if (clan.Kingdom != null && clan.Kingdom.RulingClan != clan && clan.Kingdom.ActivePolicies.Contains(DefaultPolicies.LandTax))
			{
				num -= (int)(0.05f * (float)num);
			}
			if (village.Bound.Town != null && village.Bound.Town.Governor != null && village.Bound.Town.Governor.GetPerkValue(DefaultPerks.Scouting.ForestKin))
			{
				num += MathF.Round((float)num * DefaultPerks.Scouting.ForestKin.SecondaryBonus);
			}
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
			if (flag && village.Bound.Town.Governor.GetPerkValue(DefaultPerks.Steward.Logistician))
			{
				num += MathF.Round((float)num * DefaultPerks.Steward.Logistician.SecondaryBonus);
			}
			if (applyWithdrawals)
			{
				village.TradeTaxAccumulated -= num2;
				if (clan == Clan.PlayerClan)
				{
					CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType.Taxes, num);
				}
			}
			return num;
		}

		// Token: 0x060016D0 RID: 5840 RVA: 0x00068B44 File Offset: 0x00066D44
		private static float CalculateShareFactor(Clan clan)
		{
			Kingdom kingdom = clan.Kingdom;
			int num = kingdom.Fiefs.Sum(delegate(Town x)
			{
				if (!x.IsCastle)
				{
					return 3;
				}
				return 1;
			}) + 1 + kingdom.Clans.Count;
			return (float)(clan.Fiefs.Sum(delegate(Town x)
			{
				if (!x.IsCastle)
				{
					return 3;
				}
				return 1;
			}) + ((clan == kingdom.RulingClan) ? 1 : 0) + 1) / (float)num;
		}

		// Token: 0x060016D1 RID: 5841 RVA: 0x00068BD0 File Offset: 0x00066DD0
		private void AddMercenaryIncome(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			if (clan.IsUnderMercenaryService && clan.Leader != null && clan.Kingdom != null)
			{
				int num = MathF.Ceiling(clan.Influence * (1f / Campaign.Current.Models.ClanFinanceModel.RevenueSmoothenFraction())) * clan.MercenaryAwardMultiplier;
				if (applyWithdrawals)
				{
					clan.Kingdom.MercenaryWallet -= num;
				}
				goldChange.Add((float)num, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._mercenaryStr, null), null);
			}
		}

		// Token: 0x060016D2 RID: 5842 RVA: 0x00068C58 File Offset: 0x00066E58
		private void AddIncomeFromKingdomBudget(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			int num = (clan.Gold < 5000) ? 2000 : ((clan.Gold < 10000) ? 1500 : ((clan.Gold < 20000) ? 1000 : 500));
			num *= ((clan.Kingdom.KingdomBudgetWallet > 1000000) ? 2 : 1);
			num *= ((clan.Leader == clan.Kingdom.Leader) ? 2 : 1);
			int num2 = MathF.Min(clan.Kingdom.KingdomBudgetWallet, num);
			if (applyWithdrawals)
			{
				clan.Kingdom.KingdomBudgetWallet -= num2;
			}
			goldChange.Add((float)num2, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._kingdomSupport, null), null);
		}

		// Token: 0x060016D3 RID: 5843 RVA: 0x00068D20 File Offset: 0x00066F20
		private void AddPlayerClanIncomeFromOwnedAlleys(ref ExplainedNumber goldChange)
		{
			int num = 0;
			foreach (Alley alley in Hero.MainHero.OwnedAlleys)
			{
				num += Campaign.Current.Models.AlleyModel.GetDailyIncomeOfAlley(alley);
			}
			goldChange.Add((float)num, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._alley, null), null);
		}

		// Token: 0x060016D4 RID: 5844 RVA: 0x00068DA8 File Offset: 0x00066FA8
		private void AddIncomeFromTribute(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, goldChange.IncludeDescriptions, null);
			IFaction mapFaction = clan.MapFaction;
			float num = 1f;
			if (clan.Kingdom != null)
			{
				num = DefaultClanFinanceModel.CalculateShareFactor(clan);
			}
			foreach (StanceLink stanceLink in FactionHelper.GetStances(mapFaction))
			{
				IFaction faction = (stanceLink.Faction1 == mapFaction) ? stanceLink.Faction2 : stanceLink.Faction1;
				int dailyTributeToPay = stanceLink.GetDailyTributeToPay(mapFaction);
				if (!mapFaction.IsAtWarWith(faction) && dailyTributeToPay < 0)
				{
					int num2 = (int)((float)dailyTributeToPay * num);
					if (applyWithdrawals)
					{
						faction.TributeWallet += num2;
						if (stanceLink.Faction1 == mapFaction)
						{
							stanceLink.TotalTributePaidFrom2To1 += -num2;
						}
						if (stanceLink.Faction2 == mapFaction)
						{
							stanceLink.TotalTributePaidFrom1To2 += -num2;
						}
						CampaignEventDispatcher.Instance.OnClanEarnedGoldFromTribute(clan, faction);
						if (clan == Clan.PlayerClan)
						{
							CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType.TributesEarned, -num2);
						}
					}
					explainedNumber.Add((float)(-(float)num2), Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._tributeIncomeStr, null), faction.InformalName);
				}
			}
			if (!includeDetails)
			{
				goldChange.Add(explainedNumber.ResultNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._tributeIncomes, null), null);
				return;
			}
			goldChange.AddFromExplainedNumber(explainedNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._tributeIncomes, null));
		}

		// Token: 0x060016D5 RID: 5845 RVA: 0x00068F40 File Offset: 0x00067140
		private void AddIncomeFromCallToWarAgrements(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			if (clan.Kingdom != null && clan.Kingdom.CallToWarWallet > 0)
			{
				float num = DefaultClanFinanceModel.CalculateShareFactor(clan);
				int num2 = (int)((float)clan.Kingdom.CallToWarWallet * num);
				if (applyWithdrawals)
				{
					clan.Kingdom.CallToWarWallet -= num2;
					if (clan == Clan.PlayerClan)
					{
						CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType.TributesEarned, num2);
					}
				}
				goldChange.Add((float)num2, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._callToWarIncomes, null), null);
			}
		}

		// Token: 0x060016D6 RID: 5846 RVA: 0x00068FC4 File Offset: 0x000671C4
		private void AddIncomeFromParties(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, goldChange.IncludeDescriptions, null);
			foreach (Hero hero in clan.AliveLords)
			{
				foreach (CaravanPartyComponent caravanPartyComponent in hero.OwnedCaravans)
				{
					if (caravanPartyComponent.MobileParty.IsActive && caravanPartyComponent.MobileParty.LeaderHero != clan.Leader && (caravanPartyComponent.MobileParty.IsLordParty || caravanPartyComponent.MobileParty.IsGarrison || caravanPartyComponent.MobileParty.IsCaravan))
					{
						int num = this.AddIncomeFromParty(caravanPartyComponent.MobileParty, clan, ref goldChange, applyWithdrawals);
						explainedNumber.Add((float)num, Game.Current.GameTextManager.FindText(caravanPartyComponent.MobileParty.CaravanPartyComponent.CanHaveNavalNavigationCapability ? DefaultClanFinanceModel._convoyIncomeStr : DefaultClanFinanceModel._caravanIncomeStr, null), (caravanPartyComponent.Leader != null) ? caravanPartyComponent.Leader.Name : caravanPartyComponent.Name);
					}
				}
			}
			foreach (Hero hero2 in clan.Companions)
			{
				foreach (CaravanPartyComponent caravanPartyComponent2 in hero2.OwnedCaravans)
				{
					if (caravanPartyComponent2.MobileParty.IsActive && caravanPartyComponent2.MobileParty.LeaderHero != clan.Leader && (caravanPartyComponent2.MobileParty.IsLordParty || caravanPartyComponent2.MobileParty.IsGarrison || caravanPartyComponent2.MobileParty.IsCaravan))
					{
						int num2 = this.AddIncomeFromParty(caravanPartyComponent2.MobileParty, clan, ref goldChange, applyWithdrawals);
						explainedNumber.Add((float)num2, Game.Current.GameTextManager.FindText(caravanPartyComponent2.MobileParty.CaravanPartyComponent.CanHaveNavalNavigationCapability ? DefaultClanFinanceModel._convoyIncomeStr : DefaultClanFinanceModel._caravanIncomeStr, null), (caravanPartyComponent2.Leader != null) ? caravanPartyComponent2.Leader.Name : caravanPartyComponent2.Name);
					}
				}
			}
			foreach (WarPartyComponent warPartyComponent in clan.WarPartyComponents)
			{
				if (warPartyComponent.MobileParty.IsActive && warPartyComponent.MobileParty.LeaderHero != clan.Leader && (warPartyComponent.MobileParty.IsLordParty || warPartyComponent.MobileParty.IsGarrison || warPartyComponent.MobileParty.IsCaravan))
				{
					int num3 = this.AddIncomeFromParty(warPartyComponent.MobileParty, clan, ref goldChange, applyWithdrawals);
					explainedNumber.Add((float)num3, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._partyIncomeStr, null), warPartyComponent.MobileParty.Name);
				}
			}
			if (!includeDetails)
			{
				goldChange.Add(explainedNumber.ResultNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._caravanAndPartyIncome, null), null);
				return;
			}
			goldChange.AddFromExplainedNumber(explainedNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._caravanAndPartyIncome, null));
		}

		// Token: 0x060016D7 RID: 5847 RVA: 0x000693A4 File Offset: 0x000675A4
		private int AddIncomeFromParty(MobileParty party, Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			int num = 0;
			if (party.IsActive && party.LeaderHero != clan.Leader && (party.IsLordParty || party.IsGarrison || party.IsCaravan))
			{
				int partyTradeGold = party.PartyTradeGold;
				if (partyTradeGold > 10000)
				{
					num = (partyTradeGold - 10000) / 10;
					if (applyWithdrawals)
					{
						party.PartyTradeGold -= num;
						if (party.LeaderHero != null && num > 0)
						{
							SkillLevelingManager.OnTradeProfitMade(party.LeaderHero, num);
						}
						Hero owner = party.Party.Owner;
						bool flag;
						if (owner == null)
						{
							flag = (null != null);
						}
						else
						{
							Clan clan2 = owner.Clan;
							flag = (((clan2 != null) ? clan2.Leader : null) != null);
						}
						if (flag && party.IsCaravan && party.Party.Owner.Clan.Leader.GetPerkValue(DefaultPerks.Trade.GreatInvestor) && num > 0)
						{
							party.Party.Owner.Clan.AddRenown(DefaultPerks.Trade.GreatInvestor.PrimaryBonus, true);
						}
						if (clan == Clan.PlayerClan && party.IsCaravan)
						{
							CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType.Caravan, num);
						}
					}
				}
			}
			return num;
		}

		// Token: 0x060016D8 RID: 5848 RVA: 0x000694C4 File Offset: 0x000676C4
		private void AddExpensesFromPartiesAndGarrisons(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals, bool includeDetails)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, goldChange.IncludeDescriptions, null);
			int num = this.AddExpenseFromLeaderParty(clan, goldChange, applyWithdrawals);
			explainedNumber.Add((float)num, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._mainPartywageStr, null), null);
			foreach (Hero hero in clan.AliveLords)
			{
				foreach (CaravanPartyComponent caravanPartyComponent in hero.OwnedCaravans)
				{
					if (caravanPartyComponent.MobileParty.IsActive && caravanPartyComponent.MobileParty.LeaderHero != clan.Leader)
					{
						int num2 = this.AddPartyExpense(caravanPartyComponent.MobileParty, clan, goldChange, applyWithdrawals);
						explainedNumber.Add((float)num2, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._partyExpensesStr, null), caravanPartyComponent.Name);
					}
				}
			}
			foreach (Hero hero2 in clan.Companions)
			{
				foreach (CaravanPartyComponent caravanPartyComponent2 in hero2.OwnedCaravans)
				{
					int num3 = this.AddPartyExpense(caravanPartyComponent2.MobileParty, clan, goldChange, applyWithdrawals);
					explainedNumber.Add((float)num3, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._partyExpensesStr, null), caravanPartyComponent2.Name);
				}
			}
			foreach (WarPartyComponent warPartyComponent in clan.WarPartyComponents)
			{
				if (warPartyComponent.MobileParty.IsActive && warPartyComponent.MobileParty.LeaderHero != clan.Leader)
				{
					int num4 = this.AddPartyExpense(warPartyComponent.MobileParty, clan, goldChange, applyWithdrawals);
					explainedNumber.Add((float)num4, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._partyExpensesStr, null), warPartyComponent.Name);
				}
			}
			foreach (Town town in clan.Fiefs)
			{
				if (town.GarrisonParty != null && town.GarrisonParty.IsActive)
				{
					int num5 = this.AddPartyExpense(town.GarrisonParty, clan, goldChange, applyWithdrawals);
					TextObject textObject = new TextObject("{=fsTBcLvA}{SETTLEMENT} Garrison", null);
					textObject.SetTextVariable("SETTLEMENT", town.Name);
					explainedNumber.Add((float)num5, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._partyExpensesStr, null), textObject);
				}
			}
			if (!includeDetails)
			{
				goldChange.Add(explainedNumber.ResultNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._garrisonAndPartyExpenses, null), null);
				return;
			}
			goldChange.AddFromExplainedNumber(explainedNumber, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._garrisonAndPartyExpenses, null));
		}

		// Token: 0x060016D9 RID: 5849 RVA: 0x00069834 File Offset: 0x00067A34
		private void AddExpensesForAutoRecruitment(Clan clan, ref ExplainedNumber goldChange, bool applyWithdrawals = false)
		{
			int num = clan.AutoRecruitmentExpenses / 5;
			if (applyWithdrawals)
			{
				clan.AutoRecruitmentExpenses -= num;
			}
			goldChange.Add((float)(-(float)num), Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._autoRecruitmentStr, null), null);
		}

		// Token: 0x060016DA RID: 5850 RVA: 0x0006987C File Offset: 0x00067A7C
		private int AddExpenseFromLeaderParty(Clan clan, ExplainedNumber goldChange, bool applyWithdrawals)
		{
			Hero leader = clan.Leader;
			MobileParty mobileParty = (leader != null) ? leader.PartyBelongedTo : null;
			if (mobileParty != null)
			{
				int num = clan.Gold + (int)goldChange.ResultNumber;
				if (num < 2000 && applyWithdrawals && clan != Clan.PlayerClan)
				{
					num = 0;
				}
				return -this.CalculatePartyWage(mobileParty, num, applyWithdrawals);
			}
			return 0;
		}

		// Token: 0x060016DB RID: 5851 RVA: 0x000698D4 File Offset: 0x00067AD4
		private int AddPartyExpense(MobileParty party, Clan clan, ExplainedNumber goldChange, bool applyWithdrawals)
		{
			int num = clan.Gold + (int)goldChange.ResultNumber;
			int num2 = num;
			if (num < (party.IsGarrison ? 8000 : 4000) && applyWithdrawals && clan != Clan.PlayerClan)
			{
				num2 = ((party.LeaderHero != null && party.PartyTradeGold < 500) ? MathF.Min(num, 250) : 0);
			}
			int num3 = this.CalculatePartyWage(party, num2, applyWithdrawals);
			int num4 = party.PartyTradeGold;
			if (applyWithdrawals)
			{
				if (party.IsLordParty && party.LeaderHero == null)
				{
					party.ActualClan.Leader.Gold -= num3;
				}
				else
				{
					party.PartyTradeGold -= num3;
				}
			}
			num4 -= num3;
			if (num4 < this.PartyGoldLowerThreshold)
			{
				int num5 = this.PartyGoldLowerThreshold - num4;
				if (party.IsLordParty && party.LeaderHero == null)
				{
					num5 = num3;
				}
				if (applyWithdrawals)
				{
					num5 = MathF.Min(num5, num2);
					party.PartyTradeGold += num5;
				}
				return -num5;
			}
			return 0;
		}

		// Token: 0x060016DC RID: 5852 RVA: 0x000699D5 File Offset: 0x00067BD5
		public override int CalculateOwnerIncomeFromCaravan(MobileParty caravan)
		{
			return (int)((float)MathF.Max(0, caravan.PartyTradeGold - Campaign.Current.Models.CaravanModel.GetInitialTradeGold(caravan.Owner, caravan.CaravanPartyComponent.CanHaveNavalNavigationCapability, false)) / this.RevenueSmoothenFraction());
		}

		// Token: 0x060016DD RID: 5853 RVA: 0x00069A13 File Offset: 0x00067C13
		public override int CalculateOwnerIncomeFromWorkshop(Workshop workshop)
		{
			return (int)((float)MathF.Max(0, workshop.ProfitMade) / this.RevenueSmoothenFraction());
		}

		// Token: 0x060016DE RID: 5854 RVA: 0x00069A2C File Offset: 0x00067C2C
		private void CalculateHeroIncomeFromAssets(Hero hero, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			int num = 0;
			foreach (CaravanPartyComponent caravanPartyComponent in hero.OwnedCaravans)
			{
				if (caravanPartyComponent.MobileParty.PartyTradeGold > Campaign.Current.Models.CaravanModel.GetInitialTradeGold(caravanPartyComponent.Owner, caravanPartyComponent.CanHaveNavalNavigationCapability, false))
				{
					int num2 = Campaign.Current.Models.ClanFinanceModel.CalculateOwnerIncomeFromCaravan(caravanPartyComponent.MobileParty);
					if (applyWithdrawals)
					{
						caravanPartyComponent.MobileParty.PartyTradeGold -= num2;
						SkillLevelingManager.OnTradeProfitMade(hero, num2);
					}
					if (num2 > 0)
					{
						num += num2;
					}
				}
			}
			goldChange.Add((float)num, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._caravanIncomeStr, null), null);
			this.CalculateHeroIncomeFromWorkshops(hero, ref goldChange, applyWithdrawals);
			if (hero.CurrentSettlement != null)
			{
				foreach (Alley alley in hero.CurrentSettlement.Alleys)
				{
					if (alley.Owner == hero)
					{
						goldChange.Add(30f, alley.Name, null);
					}
				}
			}
		}

		// Token: 0x060016DF RID: 5855 RVA: 0x00069B78 File Offset: 0x00067D78
		private void CalculateHeroIncomeFromWorkshops(Hero hero, ref ExplainedNumber goldChange, bool applyWithdrawals)
		{
			int num = 0;
			int num2 = 0;
			foreach (Workshop workshop in hero.OwnedWorkshops)
			{
				int num3 = Campaign.Current.Models.ClanFinanceModel.CalculateOwnerIncomeFromWorkshop(workshop);
				num += num3;
				if (applyWithdrawals && num3 > 0)
				{
					workshop.ChangeGold(-num3);
					if (hero == Hero.MainHero)
					{
						CampaignEventDispatcher.Instance.OnPlayerEarnedGoldFromAsset(DefaultClanFinanceModel.AssetIncomeType.Workshop, num3);
					}
				}
				if (num3 > 0)
				{
					num2++;
				}
			}
			goldChange.Add((float)num, Game.Current.GameTextManager.FindText(DefaultClanFinanceModel._shopIncomeStr, null), null);
			bool flag;
			if (hero.Clan != null)
			{
				Hero leader = hero.Clan.Leader;
				flag = (leader != null && leader.GetPerkValue(DefaultPerks.Trade.ArtisanCommunity));
			}
			else
			{
				flag = false;
			}
			if (flag && applyWithdrawals && num2 > 0)
			{
				hero.Clan.AddRenown((float)num2 * DefaultPerks.Trade.ArtisanCommunity.PrimaryBonus, true);
			}
		}

		// Token: 0x060016E0 RID: 5856 RVA: 0x00069C7C File Offset: 0x00067E7C
		public override float RevenueSmoothenFraction()
		{
			return 5f;
		}

		// Token: 0x060016E1 RID: 5857 RVA: 0x00069C84 File Offset: 0x00067E84
		private int CalculatePartyWage(MobileParty mobileParty, int budget, bool applyWithdrawals)
		{
			int totalWage = mobileParty.TotalWage;
			int num = totalWage;
			if (applyWithdrawals)
			{
				num = MathF.Min(totalWage, budget);
				DefaultClanFinanceModel.ApplyMoraleEffect(mobileParty, totalWage, num);
			}
			return num;
		}

		// Token: 0x060016E2 RID: 5858 RVA: 0x00069CB0 File Offset: 0x00067EB0
		public override int CalculateNotableDailyGoldChange(Hero hero, bool applyWithdrawals)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, false, null);
			this.CalculateHeroIncomeFromAssets(hero, ref explainedNumber, applyWithdrawals);
			return (int)explainedNumber.ResultNumber;
		}

		// Token: 0x060016E3 RID: 5859 RVA: 0x00069CE0 File Offset: 0x00067EE0
		private static void ApplyMoraleEffect(MobileParty mobileParty, int wage, int paymentAmount)
		{
			if (paymentAmount < wage && wage > 0)
			{
				float num = 1f - (float)paymentAmount / (float)wage;
				float num2 = (float)Campaign.Current.Models.PartyMoraleModel.GetDailyNoWageMoralePenalty(mobileParty) * num;
				if (mobileParty.HasUnpaidWages < num)
				{
					num2 += (float)Campaign.Current.Models.PartyMoraleModel.GetDailyNoWageMoralePenalty(mobileParty) * (num - mobileParty.HasUnpaidWages);
				}
				mobileParty.RecentEventsMorale += num2;
				mobileParty.HasUnpaidWages = num;
				MBTextManager.SetTextVariable("reg1", MathF.Round(MathF.Abs(num2), 1), 2);
				if (mobileParty == MobileParty.MainParty)
				{
					MBInformationManager.AddQuickInformation(GameTexts.FindText("str_party_loses_moral_due_to_insufficent_funds", null), 0, null, null, "");
					return;
				}
			}
			else
			{
				mobileParty.HasUnpaidWages = 0f;
			}
		}

		// Token: 0x04000779 RID: 1913
		private static readonly string _townTaxStr = "str_finance_town_tax";

		// Token: 0x0400077A RID: 1914
		private static readonly string _partyIncomeStr = "str_finance_party_income";

		// Token: 0x0400077B RID: 1915
		private static readonly string _caravanIncomeStr = "str_finance_caravan_income";

		// Token: 0x0400077C RID: 1916
		private static readonly string _convoyIncomeStr = "str_finance_convoy_income";

		// Token: 0x0400077D RID: 1917
		private static readonly string _projectsIncomeStr = "str_finance_projects_income";

		// Token: 0x0400077E RID: 1918
		private static readonly string _partyExpensesStr = "str_finance_party_expenses";

		// Token: 0x0400077F RID: 1919
		private static readonly string _shopIncomeStr = "str_finance_shop_income";

		// Token: 0x04000780 RID: 1920
		private static readonly string _shopExpenseStr = "str_finance_shop_expense";

		// Token: 0x04000781 RID: 1921
		private static readonly string _mercenaryStr = "str_finance_mercenary";

		// Token: 0x04000782 RID: 1922
		private static readonly string _mercenaryExpensesStr = "str_finance_mercenary_expenses";

		// Token: 0x04000783 RID: 1923
		private static readonly string _tributeExpensesStr = "str_finance_tribute_expenses";

		// Token: 0x04000784 RID: 1924
		private static readonly string _tributeIncomeStr = "str_finance_tribute_income";

		// Token: 0x04000785 RID: 1925
		private static readonly string _tributeIncomes = "str_finance_tribute_incomes";

		// Token: 0x04000786 RID: 1926
		private static readonly string _callToWarExpenses = "str_finance_call_to_war_expenses";

		// Token: 0x04000787 RID: 1927
		private static readonly string _callToWarIncomes = "str_finance_call_to_war_incomes";

		// Token: 0x04000788 RID: 1928
		private static readonly string _settlementIncome = "str_finance_settlement_income";

		// Token: 0x04000789 RID: 1929
		private static readonly string _mainPartywageStr = "str_finance_main_party_wage";

		// Token: 0x0400078A RID: 1930
		private static readonly string _caravanAndPartyIncome = "str_finance_caravan_and_party_income";

		// Token: 0x0400078B RID: 1931
		private static readonly string _garrisonAndPartyExpenses = "str_finance_garrison_and_party_expenses";

		// Token: 0x0400078C RID: 1932
		private static readonly string _debtStr = "str_finance_debt";

		// Token: 0x0400078D RID: 1933
		private static readonly string _kingdomSupport = "str_finance_kingdom_support";

		// Token: 0x0400078E RID: 1934
		private static readonly string _kingdomBudgetStr = "str_finance_kingdom_budget";

		// Token: 0x0400078F RID: 1935
		private static readonly string _tariffTaxStr = "str_finance_tariff_tax";

		// Token: 0x04000790 RID: 1936
		private static readonly string _autoRecruitmentStr = "str_finance_auto_recruitment";

		// Token: 0x04000791 RID: 1937
		private static readonly string _alley = "str_finance_alley";

		// Token: 0x04000792 RID: 1938
		private const int PartyGoldIncomeThreshold = 10000;

		// Token: 0x04000793 RID: 1939
		private const int payGarrisonWagesTreshold = 8000;

		// Token: 0x04000794 RID: 1940
		private const int payClanPartiesTreshold = 4000;

		// Token: 0x04000795 RID: 1941
		private const int payLeaderPartyWageTreshold = 2000;

		// Token: 0x02000573 RID: 1395
		private enum TransactionType
		{
			// Token: 0x04001706 RID: 5894
			Income = 1,
			// Token: 0x04001707 RID: 5895
			Both = 0,
			// Token: 0x04001708 RID: 5896
			Expense = -1
		}

		// Token: 0x02000574 RID: 1396
		public enum AssetIncomeType
		{
			// Token: 0x0400170A RID: 5898
			Workshop,
			// Token: 0x0400170B RID: 5899
			Caravan,
			// Token: 0x0400170C RID: 5900
			Taxes,
			// Token: 0x0400170D RID: 5901
			TributesEarned
		}
	}
}
