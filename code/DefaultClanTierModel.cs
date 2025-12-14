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
		// Token: 0x17000637 RID: 1591
		// (get) Token: 0x060016EE RID: 5870 RVA: 0x0006A9AD File Offset: 0x00068BAD
		public override int MinClanTier
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x17000638 RID: 1592
		// (get) Token: 0x060016EF RID: 5871 RVA: 0x0006A9B0 File Offset: 0x00068BB0
		public override int MaxClanTier
		{
			get
			{
				return 6;
			}
		}

		// Token: 0x17000639 RID: 1593
		// (get) Token: 0x060016F0 RID: 5872 RVA: 0x0006A9B3 File Offset: 0x00068BB3
		public override int MercenaryEligibleTier
		{
			get
			{
				return 1;
			}
		}

		// Token: 0x1700063A RID: 1594
		// (get) Token: 0x060016F1 RID: 5873 RVA: 0x0006A9B6 File Offset: 0x00068BB6
		public override int VassalEligibleTier
		{
			get
			{
				return 2;
			}
		}

		// Token: 0x1700063B RID: 1595
		// (get) Token: 0x060016F2 RID: 5874 RVA: 0x0006A9B9 File Offset: 0x00068BB9
		public override int BannerEligibleTier
		{
			get
			{
				return 0;
			}
		}

		// Token: 0x1700063C RID: 1596
		// (get) Token: 0x060016F3 RID: 5875 RVA: 0x0006A9BC File Offset: 0x00068BBC
		public override int RebelClanStartingTier
		{
			get
			{
				return 3;
			}
		}

		// Token: 0x1700063D RID: 1597
		// (get) Token: 0x060016F4 RID: 5876 RVA: 0x0006A9BF File Offset: 0x00068BBF
		public override int CompanionToLordClanStartingTier
		{
			get
			{
				return 2;
			}
		}

		// Token: 0x1700063E RID: 1598
		// (get) Token: 0x060016F5 RID: 5877 RVA: 0x0006A9C2 File Offset: 0x00068BC2
		private int KingdomEligibleTier
		{
			get
			{
				return Campaign.Current.Models.KingdomCreationModel.MinimumClanTierToCreateKingdom;
			}
		}

		// Token: 0x060016F6 RID: 5878 RVA: 0x0006A9D8 File Offset: 0x00068BD8
		public override int CalculateInitialRenown(Clan clan)
		{
			int num = DefaultClanTierModel.TierLowerRenownLimits[clan.Tier];
			int num2 = (clan.Tier >= this.MaxClanTier) ? (DefaultClanTierModel.TierLowerRenownLimits[this.MaxClanTier] + 1500) : DefaultClanTierModel.TierLowerRenownLimits[clan.Tier + 1];
			int maxValue = (int)((float)num2 - (float)(num2 - num) * 0.4f);
			return MBRandom.RandomInt(num, maxValue);
		}

		// Token: 0x060016F7 RID: 5879 RVA: 0x0006AA39 File Offset: 0x00068C39
		public override int CalculateInitialInfluence(Clan clan)
		{
			return (int)(150f + (float)MBRandom.RandomInt((int)((float)this.CalculateInitialRenown(clan) / 15f)) + (float)MBRandom.RandomInt(MBRandom.RandomInt(MBRandom.RandomInt(400))));
		}

		// Token: 0x060016F8 RID: 5880 RVA: 0x0006AA70 File Offset: 0x00068C70
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

		// Token: 0x060016F9 RID: 5881 RVA: 0x0006AAB0 File Offset: 0x00068CB0
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

		// Token: 0x060016FA RID: 5882 RVA: 0x0006AC17 File Offset: 0x00068E17
		public override int GetRequiredRenownForTier(int tier)
		{
			return DefaultClanTierModel.TierLowerRenownLimits[tier];
		}

		// Token: 0x060016FB RID: 5883 RVA: 0x0006AC20 File Offset: 0x00068E20
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

		// Token: 0x060016FC RID: 5884 RVA: 0x0006ACAA File Offset: 0x00068EAA
		private void AddPartyLimitPerkEffects(Clan clan, ref ExplainedNumber result)
		{
			if (clan.Leader != null && clan.Leader.GetPerkValue(DefaultPerks.Leadership.TalentMagnet))
			{
				result.Add(DefaultPerks.Leadership.TalentMagnet.SecondaryBonus, DefaultPerks.Leadership.TalentMagnet.Name, null);
			}
		}

		// Token: 0x060016FD RID: 5885 RVA: 0x0006ACE4 File Offset: 0x00068EE4
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

		// Token: 0x060016FE RID: 5886 RVA: 0x0006AD3F File Offset: 0x00068F3F
		private int GetCompanionLimitFromTier(int clanTier)
		{
			return clanTier + 3;
		}

		// Token: 0x0400079D RID: 1949
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

		// Token: 0x0400079E RID: 1950
		private readonly TextObject _partyLimitBonusText = GameTexts.FindText("str_clan_tier_party_limit_bonus", null);

		// Token: 0x0400079F RID: 1951
		private readonly TextObject _companionLimitBonusText = GameTexts.FindText("str_clan_tier_companion_limit_bonus", null);

		// Token: 0x040007A0 RID: 1952
		private readonly TextObject _mercenaryEligibleText = GameTexts.FindText("str_clan_tier_mercenary_eligible", null);

		// Token: 0x040007A1 RID: 1953
		private readonly TextObject _vassalEligibleText = GameTexts.FindText("str_clan_tier_vassal_eligible", null);

		// Token: 0x040007A2 RID: 1954
		private readonly TextObject _additionalCurrentPartySizeBonus = GameTexts.FindText("str_clan_tier_party_size_bonus", null);

		// Token: 0x040007A3 RID: 1955
		private readonly TextObject _additionalWorkshopCountBonus = GameTexts.FindText("str_clan_tier_workshop_count_bonus", null);

		// Token: 0x040007A4 RID: 1956
		private readonly TextObject _kingdomEligibleText = GameTexts.FindText("str_clan_tier_kingdom_eligible", null);
	}
}
