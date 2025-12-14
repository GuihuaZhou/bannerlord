using System;
using Helpers;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x0200014B RID: 331
	public class DefaultSettlementLoyaltyModel : SettlementLoyaltyModel
	{
		// Token: 0x17000699 RID: 1689
		// (get) Token: 0x060019AD RID: 6573 RVA: 0x0008105D File Offset: 0x0007F25D
		public override float HighLoyaltyProsperityEffect
		{
			get
			{
				return 0.5f;
			}
		}

		// Token: 0x1700069A RID: 1690
		// (get) Token: 0x060019AE RID: 6574 RVA: 0x00081064 File Offset: 0x0007F264
		public override int LowLoyaltyProsperityEffect
		{
			get
			{
				return -1;
			}
		}

		// Token: 0x1700069B RID: 1691
		// (get) Token: 0x060019AF RID: 6575 RVA: 0x00081067 File Offset: 0x0007F267
		public override int ThresholdForTaxBoost
		{
			get
			{
				return 75;
			}
		}

		// Token: 0x1700069C RID: 1692
		// (get) Token: 0x060019B0 RID: 6576 RVA: 0x0008106B File Offset: 0x0007F26B
		public override int ThresholdForTaxCorruption
		{
			get
			{
				return 50;
			}
		}

		// Token: 0x1700069D RID: 1693
		// (get) Token: 0x060019B1 RID: 6577 RVA: 0x0008106F File Offset: 0x0007F26F
		public override int ThresholdForHigherTaxCorruption
		{
			get
			{
				return 25;
			}
		}

		// Token: 0x1700069E RID: 1694
		// (get) Token: 0x060019B2 RID: 6578 RVA: 0x00081073 File Offset: 0x0007F273
		public override int ThresholdForProsperityBoost
		{
			get
			{
				return 75;
			}
		}

		// Token: 0x1700069F RID: 1695
		// (get) Token: 0x060019B3 RID: 6579 RVA: 0x00081077 File Offset: 0x0007F277
		public override int ThresholdForProsperityPenalty
		{
			get
			{
				return 25;
			}
		}

		// Token: 0x170006A0 RID: 1696
		// (get) Token: 0x060019B4 RID: 6580 RVA: 0x0008107B File Offset: 0x0007F27B
		public override int AdditionalStarvationPenaltyStartDay
		{
			get
			{
				return 14;
			}
		}

		// Token: 0x170006A1 RID: 1697
		// (get) Token: 0x060019B5 RID: 6581 RVA: 0x0008107F File Offset: 0x0007F27F
		public override int AdditionalStarvationLoyaltyEffect
		{
			get
			{
				return -1;
			}
		}

		// Token: 0x170006A2 RID: 1698
		// (get) Token: 0x060019B6 RID: 6582 RVA: 0x00081082 File Offset: 0x0007F282
		public override int RebellionStartLoyaltyThreshold
		{
			get
			{
				return 15;
			}
		}

		// Token: 0x170006A3 RID: 1699
		// (get) Token: 0x060019B7 RID: 6583 RVA: 0x00081086 File Offset: 0x0007F286
		public override int RebelliousStateStartLoyaltyThreshold
		{
			get
			{
				return 25;
			}
		}

		// Token: 0x170006A4 RID: 1700
		// (get) Token: 0x060019B8 RID: 6584 RVA: 0x0008108A File Offset: 0x0007F28A
		public override int LoyaltyBoostAfterRebellionStartValue
		{
			get
			{
				return 5;
			}
		}

		// Token: 0x170006A5 RID: 1701
		// (get) Token: 0x060019B9 RID: 6585 RVA: 0x0008108D File Offset: 0x0007F28D
		public override int MilitiaBoostPercentage
		{
			get
			{
				return 200;
			}
		}

		// Token: 0x170006A6 RID: 1702
		// (get) Token: 0x060019BA RID: 6586 RVA: 0x00081094 File Offset: 0x0007F294
		public override float ThresholdForNotableRelationBonus
		{
			get
			{
				return 75f;
			}
		}

		// Token: 0x170006A7 RID: 1703
		// (get) Token: 0x060019BB RID: 6587 RVA: 0x0008109B File Offset: 0x0007F29B
		public override int DailyNotableRelationBonus
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x170006A8 RID: 1704
		// (get) Token: 0x060019BC RID: 6588 RVA: 0x0008109E File Offset: 0x0007F29E
		public override int SettlementLoyaltyChangeDueToSecurityThreshold
		{
			get
			{
				return 50;
			}
		}

		// Token: 0x170006A9 RID: 1705
		// (get) Token: 0x060019BD RID: 6589 RVA: 0x000810A2 File Offset: 0x0007F2A2
		public override int MaximumLoyaltyInSettlement
		{
			get
			{
				return 100;
			}
		}

		// Token: 0x170006AA RID: 1706
		// (get) Token: 0x060019BE RID: 6590 RVA: 0x000810A6 File Offset: 0x0007F2A6
		public override int LoyaltyDriftMedium
		{
			get
			{
				return 50;
			}
		}

		// Token: 0x170006AB RID: 1707
		// (get) Token: 0x060019BF RID: 6591 RVA: 0x000810AA File Offset: 0x0007F2AA
		public override float HighSecurityLoyaltyEffect
		{
			get
			{
				return 1f;
			}
		}

		// Token: 0x170006AC RID: 1708
		// (get) Token: 0x060019C0 RID: 6592 RVA: 0x000810B1 File Offset: 0x0007F2B1
		public override float LowSecurityLoyaltyEffect
		{
			get
			{
				return -2f;
			}
		}

		// Token: 0x170006AD RID: 1709
		// (get) Token: 0x060019C1 RID: 6593 RVA: 0x000810B8 File Offset: 0x0007F2B8
		public override float GovernorSameCultureLoyaltyEffect
		{
			get
			{
				return 1f;
			}
		}

		// Token: 0x170006AE RID: 1710
		// (get) Token: 0x060019C2 RID: 6594 RVA: 0x000810BF File Offset: 0x0007F2BF
		public override float GovernorDifferentCultureLoyaltyEffect
		{
			get
			{
				return -1f;
			}
		}

		// Token: 0x170006AF RID: 1711
		// (get) Token: 0x060019C3 RID: 6595 RVA: 0x000810C6 File Offset: 0x0007F2C6
		public override float SettlementOwnerDifferentCultureLoyaltyEffect
		{
			get
			{
				return -3f;
			}
		}

		// Token: 0x060019C4 RID: 6596 RVA: 0x000810CD File Offset: 0x0007F2CD
		public override ExplainedNumber CalculateLoyaltyChange(Town town, bool includeDescriptions = false)
		{
			return this.CalculateLoyaltyChangeInternal(town, includeDescriptions);
		}

		// Token: 0x060019C5 RID: 6597 RVA: 0x000810D8 File Offset: 0x0007F2D8
		public override void CalculateGoldGainDueToHighLoyalty(Town town, ref ExplainedNumber explainedNumber)
		{
			float value = MBMath.Map(town.Loyalty, (float)this.ThresholdForTaxBoost, 100f, 0f, 0.2f);
			explainedNumber.AddFactor(value, DefaultSettlementLoyaltyModel.LoyaltyText);
		}

		// Token: 0x060019C6 RID: 6598 RVA: 0x00081114 File Offset: 0x0007F314
		public override void CalculateGoldCutDueToLowLoyalty(Town town, ref ExplainedNumber explainedNumber)
		{
			float value = MBMath.Map(town.Loyalty, (float)this.ThresholdForHigherTaxCorruption, (float)this.ThresholdForTaxCorruption, -0.5f, 0f);
			explainedNumber.AddFactor(value, DefaultSettlementLoyaltyModel.CorruptionText);
		}

		// Token: 0x060019C7 RID: 6599 RVA: 0x00081154 File Offset: 0x0007F354
		private ExplainedNumber CalculateLoyaltyChangeInternal(Town town, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			this.GetSettlementLoyaltyChangeDueToFoodStocks(town, ref result);
			this.GetSettlementLoyaltyChangeDueToGovernorCulture(town, ref result);
			this.GetSettlementLoyaltyChangeDueToOwnerCulture(town, ref result);
			this.GetSettlementLoyaltyChangeDueToPolicies(town, ref result);
			this.GetSettlementLoyaltyChangeDueToProjects(town, ref result);
			this.GetSettlementLoyaltyChangeDueToIssues(town, ref result);
			this.GetSettlementLoyaltyChangeDueToSecurity(town, ref result);
			this.GetSettlementLoyaltyChangeDueToNotableRelations(town, ref result);
			this.GetSettlementLoyaltyChangeDueToGovernorPerks(town, ref result);
			this.GetSettlementLoyaltyChangeDueToLoyaltyDrift(town, ref result);
			return result;
		}

		// Token: 0x060019C8 RID: 6600 RVA: 0x000811CC File Offset: 0x0007F3CC
		private void GetSettlementLoyaltyChangeDueToGovernorPerks(Town town, ref ExplainedNumber explainedNumber)
		{
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Leadership.HeroicLeader, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Medicine.PhysicianOfPeople, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Athletics.Durable, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Bow.Discipline, town, ref explainedNumber);
			PerkHelper.AddPerkBonusForTown(DefaultPerks.Riding.WellStraped, town, ref explainedNumber);
			float num = 0f;
			for (int i = 0; i < town.Settlement.Parties.Count; i++)
			{
				MobileParty mobileParty = town.Settlement.Parties[i];
				if (mobileParty.ActualClan == town.OwnerClan)
				{
					if (mobileParty.IsMainParty)
					{
						for (int j = 0; j < mobileParty.MemberRoster.Count; j++)
						{
							CharacterObject characterAtIndex = mobileParty.MemberRoster.GetCharacterAtIndex(j);
							if (characterAtIndex.IsHero && characterAtIndex.HeroObject.GetPerkValue(DefaultPerks.Charm.Parade))
							{
								num += DefaultPerks.Charm.Parade.PrimaryBonus;
							}
						}
					}
					else if (mobileParty.LeaderHero != null && mobileParty.LeaderHero.GetPerkValue(DefaultPerks.Charm.Parade))
					{
						num += DefaultPerks.Charm.Parade.PrimaryBonus;
					}
				}
			}
			foreach (Hero hero in town.Settlement.HeroesWithoutParty)
			{
				if (hero.Clan == town.OwnerClan && hero.GetPerkValue(DefaultPerks.Charm.Parade))
				{
					num += DefaultPerks.Charm.Parade.PrimaryBonus;
				}
			}
			if (num > 0f)
			{
				explainedNumber.Add(num, DefaultPerks.Charm.Parade.Name, null);
			}
		}

		// Token: 0x060019C9 RID: 6601 RVA: 0x00081364 File Offset: 0x0007F564
		private void GetSettlementLoyaltyChangeDueToNotableRelations(Town town, ref ExplainedNumber explainedNumber)
		{
			float num = 0f;
			foreach (Hero hero in town.Settlement.Notables)
			{
				if (hero.SupporterOf != null)
				{
					if (hero.SupporterOf == town.Settlement.OwnerClan)
					{
						num += 0.5f;
					}
					else if (town.MapFaction.IsAtWarWith(hero.SupporterOf.MapFaction))
					{
						num += -0.5f;
					}
				}
			}
			if (!num.ApproximatelyEqualsTo(0f, 1E-05f))
			{
				explainedNumber.Add(num, DefaultSettlementLoyaltyModel.NotableText, null);
			}
		}

		// Token: 0x060019CA RID: 6602 RVA: 0x00081420 File Offset: 0x0007F620
		private void GetSettlementLoyaltyChangeDueToOwnerCulture(Town town, ref ExplainedNumber explainedNumber)
		{
			if (town.Settlement.OwnerClan.Culture != town.Settlement.Culture)
			{
				explainedNumber.Add(this.SettlementOwnerDifferentCultureLoyaltyEffect, DefaultSettlementLoyaltyModel.CultureText, null);
			}
		}

		// Token: 0x060019CB RID: 6603 RVA: 0x00081454 File Offset: 0x0007F654
		private void GetSettlementLoyaltyChangeDueToPolicies(Town town, ref ExplainedNumber explainedNumber)
		{
			Kingdom kingdom = town.Owner.Settlement.OwnerClan.Kingdom;
			if (kingdom != null)
			{
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.Citizenship))
				{
					if (town.Settlement.OwnerClan.Culture == town.Settlement.Culture)
					{
						explainedNumber.Add(0.5f, DefaultPolicies.Citizenship.Name, null);
					}
					else
					{
						explainedNumber.Add(-0.5f, DefaultPolicies.Citizenship.Name, null);
					}
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.HuntingRights))
				{
					explainedNumber.Add(-0.2f, DefaultPolicies.HuntingRights.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.GrazingRights))
				{
					explainedNumber.Add(0.5f, DefaultPolicies.GrazingRights.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.TrialByJury))
				{
					explainedNumber.Add(0.5f, DefaultPolicies.TrialByJury.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.ImperialTowns))
				{
					if (kingdom.RulingClan == town.Settlement.OwnerClan)
					{
						explainedNumber.Add(1f, DefaultPolicies.ImperialTowns.Name, null);
					}
					else
					{
						explainedNumber.Add(-0.3f, DefaultPolicies.ImperialTowns.Name, null);
					}
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.ForgivenessOfDebts))
				{
					explainedNumber.Add(2f, DefaultPolicies.ForgivenessOfDebts.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.TribunesOfThePeople) && town.IsTown)
				{
					explainedNumber.Add(1f, DefaultPolicies.TribunesOfThePeople.Name, null);
				}
				if (kingdom.ActivePolicies.Contains(DefaultPolicies.DebasementOfTheCurrency))
				{
					explainedNumber.Add(-1f, DefaultPolicies.DebasementOfTheCurrency.Name, null);
				}
			}
		}

		// Token: 0x060019CC RID: 6604 RVA: 0x00081625 File Offset: 0x0007F825
		private void GetSettlementLoyaltyChangeDueToGovernorCulture(Town town, ref ExplainedNumber explainedNumber)
		{
			if (town.Governor != null)
			{
				explainedNumber.Add((town.Governor.Culture == town.Culture) ? this.GovernorSameCultureLoyaltyEffect : this.GovernorDifferentCultureLoyaltyEffect, DefaultSettlementLoyaltyModel.GovernorCultureText, null);
			}
		}

		// Token: 0x060019CD RID: 6605 RVA: 0x0008165C File Offset: 0x0007F85C
		private void GetSettlementLoyaltyChangeDueToFoodStocks(Town town, ref ExplainedNumber explainedNumber)
		{
			if (town.Settlement.IsStarving)
			{
				float num = -1f;
				if (town.Settlement.Party.DaysStarving > 14f)
				{
					num += -1f;
				}
				explainedNumber.Add(num, DefaultSettlementLoyaltyModel.StarvingText, null);
			}
		}

		// Token: 0x060019CE RID: 6606 RVA: 0x000816A8 File Offset: 0x0007F8A8
		private void GetSettlementLoyaltyChangeDueToSecurity(Town town, ref ExplainedNumber explainedNumber)
		{
			float value = (town.Security > (float)this.SettlementLoyaltyChangeDueToSecurityThreshold) ? MBMath.Map(town.Security, (float)this.SettlementLoyaltyChangeDueToSecurityThreshold, (float)this.MaximumLoyaltyInSettlement, 0f, this.HighSecurityLoyaltyEffect) : MBMath.Map(town.Security, 0f, (float)this.SettlementLoyaltyChangeDueToSecurityThreshold, this.LowSecurityLoyaltyEffect, 0f);
			explainedNumber.Add(value, DefaultSettlementLoyaltyModel.SecurityText, null);
		}

		// Token: 0x060019CF RID: 6607 RVA: 0x0008171A File Offset: 0x0007F91A
		private void GetSettlementLoyaltyChangeDueToProjects(Town town, ref ExplainedNumber explainedNumber)
		{
			town.AddEffectOfBuildings(BuildingEffectEnum.Loyalty, ref explainedNumber);
		}

		// Token: 0x060019D0 RID: 6608 RVA: 0x00081724 File Offset: 0x0007F924
		private void GetSettlementLoyaltyChangeDueToIssues(Town town, ref ExplainedNumber explainedNumber)
		{
			Campaign.Current.Models.IssueModel.GetIssueEffectsOfSettlement(DefaultIssueEffects.SettlementLoyalty, town.Settlement, ref explainedNumber);
		}

		// Token: 0x060019D1 RID: 6609 RVA: 0x00081746 File Offset: 0x0007F946
		private void GetSettlementLoyaltyChangeDueToLoyaltyDrift(Town town, ref ExplainedNumber explainedNumber)
		{
			explainedNumber.Add(-0.1f * (town.Loyalty - (float)this.LoyaltyDriftMedium), DefaultSettlementLoyaltyModel.LoyaltyDriftText, null);
		}

		// Token: 0x04000875 RID: 2165
		private const float StarvationLoyaltyEffect = -1f;

		// Token: 0x04000876 RID: 2166
		private const int AdditionalStarvationLoyaltyEffectAfterDays = 14;

		// Token: 0x04000877 RID: 2167
		private const float NotableSupportsOwnerLoyaltyEffect = 0.5f;

		// Token: 0x04000878 RID: 2168
		private const float NotableSupportsEnemyLoyaltyEffect = -0.5f;

		// Token: 0x04000879 RID: 2169
		private static readonly TextObject StarvingText = GameTexts.FindText("str_starving", null);

		// Token: 0x0400087A RID: 2170
		private static readonly TextObject CultureText = new TextObject("{=YjoXyFDX}Owner Culture", null);

		// Token: 0x0400087B RID: 2171
		private static readonly TextObject NotableText = GameTexts.FindText("str_notable_relations", null);

		// Token: 0x0400087C RID: 2172
		private static readonly TextObject CrimeText = GameTexts.FindText("str_governor_criminal", null);

		// Token: 0x0400087D RID: 2173
		private static readonly TextObject GovernorText = GameTexts.FindText("str_notable_governor", null);

		// Token: 0x0400087E RID: 2174
		private static readonly TextObject GovernorCultureText = new TextObject("{=5Vo8dJub}Governor's Culture", null);

		// Token: 0x0400087F RID: 2175
		private static readonly TextObject NoGovernorText = new TextObject("{=NH5N3kP5}No governor", null);

		// Token: 0x04000880 RID: 2176
		private static readonly TextObject SecurityText = GameTexts.FindText("str_security", null);

		// Token: 0x04000881 RID: 2177
		private static readonly TextObject LoyaltyText = GameTexts.FindText("str_loyalty", null);

		// Token: 0x04000882 RID: 2178
		private static readonly TextObject LoyaltyDriftText = GameTexts.FindText("str_loyalty_drift", null);

		// Token: 0x04000883 RID: 2179
		private static readonly TextObject CorruptionText = GameTexts.FindText("str_corruption", null);
	}
}
