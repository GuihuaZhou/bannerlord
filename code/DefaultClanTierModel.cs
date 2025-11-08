using System;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x02000104 RID: 260
	public class DefaultClanTierModel : ClanTierModel
	{
		// Token: 0x17000636 RID: 1590
		// (get) Token: 0x060016CC RID: 5836 RVA: 0x0006A2B5 File Offset: 0x000684B5
		public override int MinClanTier
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000637 RID: 1591
		// (get) Token: 0x060016CD RID: 5837 RVA: 0x0006A2B8 File Offset: 0x000684B8
		public override int MaxClanTier
		{
			get
			{
				return 6;
			}
		}

		// Token: 0x17000638 RID: 1592
		// (get) Token: 0x060016CE RID: 5838 RVA: 0x0006A2BB File Offset: 0x000684BB
		public override int MercenaryEligibleTier
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x17000639 RID: 1593
		// (get) Token: 0x060016CF RID: 5839 RVA: 0x0006A2BE File Offset: 0x000684BE
		public override int VassalEligibleTier
		{
			get
			{
				return 2;
			}
		}

		// Token: 0x1700063A RID: 1594
		// (get) Token: 0x060016D0 RID: 5840 RVA: 0x0006A2C1 File Offset: 0x000684C1
		public override int BannerEligibleTier
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x1700063B RID: 1595
		// (get) Token: 0x060016D1 RID: 5841 RVA: 0x0006A2C4 File Offset: 0x000684C4
		public override int RebelClanStartingTier
		{
			get
			{
				return 3;
			}
		}

		// Token: 0x1700063C RID: 1596
		// (get) Token: 0x060016D2 RID: 5842 RVA: 0x0006A2C7 File Offset: 0x000684C7
		public override int CompanionToLordClanStartingTier
		{
			get
			{
				return 2;
			}
		}

		// Token: 0x1700063D RID: 1597
		// (get) Token: 0x060016D3 RID: 5843 RVA: 0x0006A2CA File Offset: 0x000684CA
		private int KingdomEligibleTier
		{
			get
			{
				return Campaign.Current.Models.KingdomCreationModel.MinimumClanTierToCreateKingdom;
			}
		}

		// Token: 0x060016D4 RID: 5844 RVA: 0x0006A2E0 File Offset: 0x000684E0
		public override int CalculateInitialRenown(Clan clan)
		{
			int num = DefaultClanTierModel.TierLowerRenownLimits[clan.Tier];
			int num2 = (clan.Tier >= this.MaxClanTier) ? (DefaultClanTierModel.TierLowerRenownLimits[this.MaxClanTier] + 1500) : DefaultClanTierModel.TierLowerRenownLimits[clan.Tier + 1];
			int maxValue = (int)((float)num2 - (float)(num2 - num) * 0.4f);
			return MBRandom.RandomInt(num, maxValue);
		}

		// Token: 0x060016D5 RID: 5845 RVA: 0x0006A341 File Offset: 0x00068541
		public override int CalculateInitialInfluence(Clan clan)
		{
			return (int)(150f + (float)MBRandom.RandomInt((int)((float)this.CalculateInitialRenown(clan) / 15f)) + (float)MBRandom.RandomInt(MBRandom.RandomInt(MBRandom.RandomInt(400))));
		}

		// Token: 0x060016D6 RID: 5846 RVA: 0x0006A378 File Offset: 0x00068578
		public override int CalculateTier(Clan clan)
		{
			int result = this.MinClanTier;
			for (int i = this.MinClanTier + 1; i <= this.MaxClanTier; i++)
			{
				if (clan.Renown >= (float)DefaultClanTierModel.TierLowerRenownLimits[i])
				{
					result = i;
				}
			}
			return result;
		}

		// Token: 0x060016D7 RID: 5847 RVA: 0x0006A3B8 File Offset: 0x000685B8
		public override ValueTuple<ExplainedNumber, bool> HasUpcomingTier(Clan clan, out TextObject extraExplanation, bool includeDescriptions = false)
		{
			bool flag = clan.Tier < this.MaxClanTier;
			ExplainedNumber item = new ExplainedNumber(0f, includeDescriptions, null);
			extraExplanation = null;
			if (flag)
			{
				int num = this.GetPartyLimitForTier(clan, clan.Tier + 1) - this.GetPartyLimitForTier(clan, clan.Tier);
				if (num != 0)
				{
					item.Add((float)num, this._partyLimitBonusText, null);
				}
				int num2 = this.GetCompanionLimitFromTier(clan.Tier + 1) - this.GetCompanionLimitFromTier(clan.Tier);
				if (num2 != 0)
				{
					item.Add((float)num2, this._companionLimitBonusText, null);
				}
				int nextClanTierPartySizeEffectChangeForHero = Campaign.Current.Models.PartySizeLimitModel.GetNextClanTierPartySizeEffectChangeForHero(clan.Leader);
				if (nextClanTierPartySizeEffectChangeForHero > 0)
				{
					item.Add((float)nextClanTierPartySizeEffectChangeForHero, this._additionalCurrentPartySizeBonus, null);
				}
				int num3 = Campaign.Current.Models.WorkshopModel.GetMaxWorkshopCountForClanTier(clan.Tier + 1) - Campaign.Current.Models.WorkshopModel.GetMaxWorkshopCountForClanTier(clan.Tier);
				if (num3 > 0)
				{
					item.Add((float)num3, this._additionalWorkshopCountBonus, null);
				}
				if (clan.Tier + 1 == this.MercenaryEligibleTier)
				{
					extraExplanation = this._mercenaryEligibleText;
				}
				else if (clan.Tier + 1 == this.VassalEligibleTier)
				{
					extraExplanation = this._vassalEligibleText;
				}
				else if (clan.Tier + 1 == this.KingdomEligibleTier)
				{
					extraExplanation = this._kingdomEligibleText;
				}
			}
			return new ValueTuple<ExplainedNumber, bool>(item, flag);
		}

		// Token: 0x060016D8 RID: 5848 RVA: 0x0006A51F File Offset: 0x0006871F
		public override int GetRequiredRenownForTier(int tier)
		{
			return DefaultClanTierModel.TierLowerRenownLimits[tier];
		}

		// Token: 0x060016D9 RID: 5849 RVA: 0x0006A528 File Offset: 0x00068728
		public override int GetPartyLimitForTier(Clan clan, int clanTierToCheck)
		{
			ExplainedNumber explainedNumber = new ExplainedNumber(0f, false, null);
			if (!clan.IsMinorFaction)
			{
				if (clanTierToCheck < 3)
				{
					explainedNumber.Add(1f, null, null);
				}
				else if (clanTierToCheck < 5)
				{
					explainedNumber.Add(2f, null, null);
				}
				else
				{
					explainedNumber.Add(3f, null, null);
				}
			}
			else
			{
				explainedNumber.Add(MathF.Clamp((float)clanTierToCheck, 1f, 4f), null, null);
			}
			this.AddPartyLimitPerkEffects(clan, ref explainedNumber);
			return MathF.Round(explainedNumber.ResultNumber);
		}

		// Token: 0x060016DA RID: 5850 RVA: 0x0006A5B2 File Offset: 0x000687B2
		private void AddPartyLimitPerkEffects(Clan clan, ref ExplainedNumber result)
		{
			if (clan.Leader != null && clan.Leader.GetPerkValue(DefaultPerks.Leadership.TalentMagnet))
			{
				result.Add(DefaultPerks.Leadership.TalentMagnet.SecondaryBonus, DefaultPerks.Leadership.TalentMagnet.Name, null);
			}
		}

		// Token: 0x060016DB RID: 5851 RVA: 0x0006A5EC File Offset: 0x000687EC
		public override int GetCompanionLimit(Clan clan)
		{
			int num = this.GetCompanionLimitFromTier(clan.Tier);
			if (clan.Leader.GetPerkValue(DefaultPerks.Leadership.WePledgeOurSwords))
			{
				num += (int)DefaultPerks.Leadership.WePledgeOurSwords.PrimaryBonus;
			}
			if (clan.Leader.GetPerkValue(DefaultPerks.Charm.Camaraderie))
			{
				num += (int)DefaultPerks.Charm.Camaraderie.SecondaryBonus;
			}
			return num;
		}

		// Token: 0x060016DC RID: 5852 RVA: 0x0006A647 File Offset: 0x00068847
		private int GetCompanionLimitFromTier(int clanTier)
		{
			return clanTier + 3;
		}

		// Token: 0x04000791 RID: 1937
		private static readonly int[] TierLowerRenownLimits = new int[]
		{
			0,
			50,
			150,
			350,
			900,
			2350,
			6150
		};

		// Token: 0x04000792 RID: 1938
		private readonly TextObject _partyLimitBonusText = GameTexts.FindText("str_clan_tier_party_limit_bonus", null);

		// Token: 0x04000793 RID: 1939
		private readonly TextObject _companionLimitBonusText = GameTexts.FindText("str_clan_tier_companion_limit_bonus", null);

		// Token: 0x04000794 RID: 1940
		private readonly TextObject _mercenaryEligibleText = GameTexts.FindText("str_clan_tier_mercenary_eligible", null);

		// Token: 0x04000795 RID: 1941
		private readonly TextObject _vassalEligibleText = GameTexts.FindText("str_clan_tier_vassal_eligible", null);

		// Token: 0x04000796 RID: 1942
		private readonly TextObject _additionalCurrentPartySizeBonus = GameTexts.FindText("str_clan_tier_party_size_bonus", null);

		// Token: 0x04000797 RID: 1943
		private readonly TextObject _additionalWorkshopCountBonus = GameTexts.FindText("str_clan_tier_workshop_count_bonus", null);

		// Token: 0x04000798 RID: 1944
		private readonly TextObject _kingdomEligibleText = GameTexts.FindText("str_clan_tier_kingdom_eligible", null);
	}
}
