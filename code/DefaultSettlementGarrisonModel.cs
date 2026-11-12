using System;
using System.Linq;
using Helpers;
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
	// Token: 0x02000153 RID: 339
	public class DefaultSettlementGarrisonModel : SettlementGarrisonModel
	{
		// Token: 0x06001A80 RID: 6784 RVA: 0x00086708 File Offset: 0x00084908
		public override ExplainedNumber GetMaximumDailyAutoRecruitmentCount(Town town, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(1f, includeDescriptions, null);
			town.AddEffectOfBuildings(BuildingEffectEnum.GarrisonAutoRecruitment, ref result);
			return result;
		}

		// Token: 0x06001A81 RID: 6785 RVA: 0x00086730 File Offset: 0x00084930
		public override ExplainedNumber CalculateBaseGarrisonChange(Settlement settlement, bool includeDescriptions = false)
		{
			ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions, null);
			if ((settlement.IsTown || settlement.IsCastle) && settlement.OwnerClan.IsRebelClan && (settlement.OwnerClan.MapFaction == null || !settlement.OwnerClan.MapFaction.IsKingdomFaction))
			{
				result.Add(2f, this.RebellionText, null);
			}
			Campaign.Current.Models.IssueModel.GetIssueEffectsOfSettlement(DefaultIssueEffects.SettlementGarrison, settlement, ref result);
			return result;
		}

		// Token: 0x06001A82 RID: 6786 RVA: 0x000867B8 File Offset: 0x000849B8
		public override int FindNumberOfTroopsToTakeFromGarrison(MobileParty mobileParty, Settlement settlement, float defaultIdealGarrisonStrengthPerWalledCenter = 0f)
		{
			MobileParty garrisonParty = settlement.Town.GarrisonParty;
			if (garrisonParty == null)
			{
				return 0;
			}
			float num = garrisonParty.Party.CalculateCurrentStrength();
			float num2;
			if (garrisonParty.HasLimitedWage())
			{
				num2 = (float)garrisonParty.PaymentLimit / Campaign.Current.AverageWage;
				num2 /= 1.5f;
			}
			else
			{
				num2 = ((defaultIdealGarrisonStrengthPerWalledCenter > 0.1f) ? defaultIdealGarrisonStrengthPerWalledCenter : FactionHelper.FindIdealGarrisonStrengthPerWalledCenter(mobileParty.MapFaction as Kingdom, settlement.OwnerClan));
				float num3 = FactionHelper.OwnerClanEconomyEffectOnGarrisonSizeConstant(settlement.OwnerClan);
				num2 *= num3;
				num2 *= (settlement.IsTown ? 2f : 1f);
			}
			float partySizeLimit = (float)mobileParty.Party.PartySizeLimit;
			int numberOfAllMembers = mobileParty.Party.NumberOfAllMembers;
			float num4 = partySizeLimit / (float)numberOfAllMembers;
			float num5 = MathF.Min(11f, num4 * MathF.Sqrt(num4)) - 1f;
			float num6 = MathF.Pow(num / num2, 1.5f);
			float num7 = (mobileParty.LeaderHero.Clan.Leader == mobileParty.LeaderHero) ? 2f : 1f;
			int num8 = 0;
			if (num5 * num6 * num7 > 1f)
			{
				num8 = MBRandom.RoundRandomized(num5 * num6 * num7);
			}
			int num9 = 25;
			num9 *= (settlement.IsTown ? 2 : 1);
			if (num8 > garrisonParty.Party.MemberRoster.TotalRegulars - num9)
			{
				num8 = garrisonParty.Party.MemberRoster.TotalRegulars - num9;
			}
			return num8;
		}

		// Token: 0x06001A83 RID: 6787 RVA: 0x00086920 File Offset: 0x00084B20
		public override float GetMaximumDailyRepairAmount(Settlement settlement)
		{
			if (!settlement.IsUnderSiege)
			{
				if (!settlement.SettlementWallSectionHitPointsRatioList.All((float ratio) => ratio >= 1f))
				{
					ExplainedNumber explainedNumber = new ExplainedNumber(settlement.MaxHitPointsOfOneWallSection * (float)settlement.WallSectionCount * 0.04f, false, null);
					if (settlement.IsFortification)
					{
						settlement.Town.AddEffectOfBuildings(BuildingEffectEnum.WallRepairSpeed, ref explainedNumber);
					}
					return explainedNumber.ResultNumber;
				}
			}
			return 0f;
		}

		// Token: 0x040008C1 RID: 2241
		private readonly TextObject RebellionText = GameTexts.FindText("str_rebel_settlement", null);

		// Token: 0x040008C2 RID: 2242
		private const int MaximumDailyAutoRecruitmentCount = 1;
	}
}
