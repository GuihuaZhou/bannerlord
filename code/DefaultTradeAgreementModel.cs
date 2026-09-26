using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x0200015E RID: 350
	public class DefaultTradeAgreementModel : TradeAgreementModel
	{
		// Token: 0x170006E3 RID: 1763
		// (get) Token: 0x06001ADE RID: 6878 RVA: 0x0008A3DF File Offset: 0x000885DF
		private ITradeAgreementsCampaignBehavior TradeAgreementsCampaignBehavior
		{
			get
			{
				if (this._tradeAgreementsBehavior == null)
				{
					this._tradeAgreementsBehavior = Campaign.Current.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
				}
				return this._tradeAgreementsBehavior;
			}
		}

		// Token: 0x06001ADF RID: 6879 RVA: 0x0008A3FF File Offset: 0x000885FF
		public override int GetInfluenceCostOfProposingTradeAgreement(Clan proposerClan)
		{
			return 200;
		}

		// Token: 0x06001AE0 RID: 6880 RVA: 0x0008A406 File Offset: 0x00088606
		public override int GetMaximumTradeAgreementCount(Kingdom kingdom)
		{
			return 2;
		}

		// Token: 0x06001AE1 RID: 6881 RVA: 0x0008A40C File Offset: 0x0008860C
		public override bool CanMakeTradeAgreement(Kingdom querierKingdom, Kingdom queriedKingdom, bool checkOtherSideSupport, out TextObject reason, bool includeReason = false)
		{
			reason = (includeReason ? TextObject.GetEmpty() : null);
			if (querierKingdom.IsAtWarWith(queriedKingdom))
			{
				reason = DefaultTradeAgreementModel._kingdomsAtWarText;
				return false;
			}
			if (queriedKingdom.IsEliminated)
			{
				reason = DefaultTradeAgreementModel._eliminatedKingdomText;
				return false;
			}
			ITradeAgreementsCampaignBehavior tradeAgreementsCampaignBehavior = this.TradeAgreementsCampaignBehavior;
			TradeAgreementsCampaignBehavior.TradeAgreement tradeAgreement;
			if (tradeAgreementsCampaignBehavior != null && tradeAgreementsCampaignBehavior.HasTradeAgreement(querierKingdom, queriedKingdom, out tradeAgreement))
			{
				reason = DefaultTradeAgreementModel._existingTradeAgreementText;
				return false;
			}
			if (Kingdom.All.Count(delegate(Kingdom x)
			{
				if (x != querierKingdom && !x.IsEliminated)
				{
					ITradeAgreementsCampaignBehavior tradeAgreementsCampaignBehavior2 = this.TradeAgreementsCampaignBehavior;
					TradeAgreementsCampaignBehavior.TradeAgreement tradeAgreement2;
					return tradeAgreementsCampaignBehavior2 != null && tradeAgreementsCampaignBehavior2.HasTradeAgreement(querierKingdom, x, out tradeAgreement2);
				}
				return false;
			}) >= Campaign.Current.Models.TradeAgreementModel.GetMaximumTradeAgreementCount(querierKingdom))
			{
				reason = DefaultTradeAgreementModel._maximumNumberOfTradeAgreementsText;
				return false;
			}
			if (Kingdom.All.Count(delegate(Kingdom x)
			{
				if (x != queriedKingdom && !x.IsEliminated)
				{
					ITradeAgreementsCampaignBehavior tradeAgreementsCampaignBehavior2 = this.TradeAgreementsCampaignBehavior;
					TradeAgreementsCampaignBehavior.TradeAgreement tradeAgreement2;
					return tradeAgreementsCampaignBehavior2 != null && tradeAgreementsCampaignBehavior2.HasTradeAgreement(queriedKingdom, x, out tradeAgreement2);
				}
				return false;
			}) >= Campaign.Current.Models.TradeAgreementModel.GetMaximumTradeAgreementCount(querierKingdom))
			{
				if (includeReason)
				{
					reason = new TextObject("{=O6zpuLGa}{OTHER_KINGDOM} already has maximum number of trade agreements.", null);
					reason.SetTextVariable("OTHER_KINGDOM", queriedKingdom.Name);
				}
				return false;
			}
			if (querierKingdom.Towns.Count == 0)
			{
				reason = DefaultTradeAgreementModel._noTownText;
				return false;
			}
			if (queriedKingdom.Towns.Count == 0)
			{
				if (includeReason)
				{
					reason = new TextObject("{=XkbKOO3v}{OTHER_KINGDOM} does not own any towns.", null);
					reason.SetTextVariable("OTHER_KINGDOM", queriedKingdom.Name);
				}
				return false;
			}
			Func<Settlement, bool> <>9__7;
			if (!querierKingdom.Fiefs.Any(delegate(Town x)
			{
				IEnumerable<Settlement> neighborFortifications = x.GetNeighborFortifications(MobileParty.NavigationType.All);
				Func<Settlement, bool> predicate;
				if ((predicate = <>9__7) == null)
				{
					predicate = (<>9__7 = ((Settlement y) => y.MapFaction == queriedKingdom));
				}
				return neighborFortifications.Any(predicate);
			}))
			{
				reason = DefaultTradeAgreementModel._kingdomsNotNeighborsText;
				return false;
			}
			if (querierKingdom.Towns.All((Town x) => x.Settlement.HasPort))
			{
				if (queriedKingdom.Towns.All((Town x) => !x.Settlement.HasPort))
				{
					goto IL_26C;
				}
			}
			if (querierKingdom.Towns.All((Town x) => !x.Settlement.HasPort))
			{
				if (queriedKingdom.Towns.All((Town x) => x.Settlement.HasPort))
				{
					goto IL_26C;
				}
			}
			if (checkOtherSideSupport)
			{
				TradeAgreementDecision tradeAgreementDecision = new TradeAgreementDecision(queriedKingdom.RulingClan, querierKingdom);
				KingdomElection kingdomElection = new KingdomElection(tradeAgreementDecision);
				kingdomElection.SetupResultWithoutPlayerSupport();
				if (queriedKingdom == Clan.PlayerClan.Kingdom)
				{
					DecisionOutcome supportedOutcome = kingdomElection.PossibleOutcomes.FirstOrDefault(delegate(DecisionOutcome x)
					{
						TradeAgreementDecision.TradeAgreementDecisionOutcome tradeAgreementDecisionOutcome;
						return (tradeAgreementDecisionOutcome = (x as TradeAgreementDecision.TradeAgreementDecisionOutcome)) != null && tradeAgreementDecisionOutcome.ShouldTradeAgreementStart;
					});
					return kingdomElection.GetWinChanceWithPlayerSupport(supportedOutcome, Supporter.SupportWeights.FullyPush) > 0.5f;
				}
				if (kingdomElection.GetWinChanceForSponsor(queriedKingdom.RulingClan) < 0.5f)
				{
					tradeAgreementDecision.CalculateSupport(queriedKingdom.RulingClan, out reason);
					return false;
				}
			}
			return true;
			IL_26C:
			reason = DefaultTradeAgreementModel._landlockedText;
			return false;
		}

		// Token: 0x06001AE2 RID: 6882 RVA: 0x0008A738 File Offset: 0x00088938
		public override float GetScoreOfStartingTradeAgreement(Kingdom querierKingdom, Kingdom queriedKingdom, Clan clan, out TextObject detailedBreakdownTooltip, bool includeExplanation = false)
		{
			detailedBreakdownTooltip = null;
			float securityEffect = this.GetSecurityEffect(queriedKingdom);
			float relationEffectBetweenRulers = this.GetRelationEffectBetweenRulers(querierKingdom, queriedKingdom);
			float allianceEffect = this.GetAllianceEffect(querierKingdom, queriedKingdom);
			bool flag;
			bool flag2;
			float diplomacyEffect = this.GetDiplomacyEffect(querierKingdom, queriedKingdom, out flag, out flag2);
			float marriageEffect = this.GetMarriageEffect(querierKingdom, queriedKingdom);
			float num;
			float num2;
			float prosperityEffect = this.GetProsperityEffect(querierKingdom, queriedKingdom, out num, out num2);
			float exposureEffect = this.GetExposureEffect(querierKingdom, queriedKingdom);
			float num3 = 0f;
			if (querierKingdom == Clan.PlayerClan.Kingdom && !Clan.PlayerClan.IsUnderMercenaryService)
			{
				num3 += this.GetSubjectiveEffect(queriedKingdom, clan);
			}
			float value = securityEffect + prosperityEffect + relationEffectBetweenRulers + allianceEffect + diplomacyEffect + exposureEffect + num3 + marriageEffect;
			if (includeExplanation)
			{
				List<ValueTuple<float, TextObject>> list = new List<ValueTuple<float, TextObject>>();
				if (securityEffect < 0f)
				{
					list.Add(new ValueTuple<float, TextObject>(MathF.Abs(securityEffect), DefaultTradeAgreementModel._lowSecurityText));
				}
				if (relationEffectBetweenRulers < 0f)
				{
					list.Add(new ValueTuple<float, TextObject>((float)Campaign.Current.Models.DiplomacyModel.MaxRelationLimit * 0.1f - relationEffectBetweenRulers, DefaultTradeAgreementModel._relationsText));
				}
				if (flag)
				{
					list.Add(new ValueTuple<float, TextObject>(MathF.Abs(-15f), DefaultTradeAgreementModel._warWithAlliedText));
				}
				else if (flag2)
				{
					list.Add(new ValueTuple<float, TextObject>(MathF.Abs(-1.5f), DefaultTradeAgreementModel._warText));
				}
				if (list.Sum((ValueTuple<float, TextObject> x) => x.Item1).ApproximatelyEqualsTo(0f, 1E-05f))
				{
					if (num > num2)
					{
						list.Add(new ValueTuple<float, TextObject>(1f, DefaultTradeAgreementModel._higherQuerierProsperityText));
					}
					else
					{
						list.Add(new ValueTuple<float, TextObject>(1f, DefaultTradeAgreementModel._higherQueriedProsperityText));
					}
					if (exposureEffect < 30f)
					{
						list.Add(new ValueTuple<float, TextObject>(1f, DefaultTradeAgreementModel._limitedSharedBordersText));
					}
				}
				list = (from x in list
				orderby x.Item1 descending
				select x).ToList<ValueTuple<float, TextObject>>();
				List<TextObject> list2 = new List<TextObject>();
				foreach (ValueTuple<float, TextObject> valueTuple in list)
				{
					list2.Add(valueTuple.Item2);
					if (list2.Count >= 3)
					{
						break;
					}
				}
				TextObject variable = GameTexts.GameTextHelper.MergeTextObjectsWithSymbol(list2, new TextObject("{=!}{newline}", null), null);
				detailedBreakdownTooltip = new TextObject("{=jXcb9oHi}{KINGDOM} is not considering a trade agreement with your realm due to:{newline}{newline}{REASONS_BY_LINE}", null);
				detailedBreakdownTooltip.SetTextVariable("KINGDOM", querierKingdom.Name);
				detailedBreakdownTooltip.SetTextVariable("REASONS_BY_LINE", variable);
			}
			return MBMath.ClampFloat(value, 0f, 100f);
		}

		// Token: 0x06001AE3 RID: 6883 RVA: 0x0008A9E4 File Offset: 0x00088BE4
		private float GetMarriageEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			if (this.IsThereMarriageBetweenClans(queriedKingdom.RulingClan, querierKingdom.RulingClan))
			{
				return 10f;
			}
			if (queriedKingdom.Clans.AnyQ((Clan clan) => clan != queriedKingdom.RulingClan && this.IsThereMarriageBetweenClans(querierKingdom.RulingClan, clan)))
			{
				return 5f;
			}
			return 0f;
		}

		// Token: 0x06001AE4 RID: 6884 RVA: 0x0008AA5C File Offset: 0x00088C5C
		private bool IsThereMarriageBetweenClans(Clan clan1, Clan clan2)
		{
			return clan1.AliveLords.Any((Hero x) => x.OriginClan == clan2) || clan2.AliveLords.Any((Hero x) => x.OriginClan == clan1);
		}

		// Token: 0x06001AE5 RID: 6885 RVA: 0x0008AABC File Offset: 0x00088CBC
		private float GetSubjectiveEffect(Kingdom queriedKingdom, Clan clan)
		{
			return (float)clan.Leader.GetRelation(queriedKingdom.Leader) * 0.25f + clan.Leader.RandomFloatWithSeed((uint)CampaignTime.Now.ToWeeks, -10f, 10f);
		}

		// Token: 0x06001AE6 RID: 6886 RVA: 0x0008AB05 File Offset: 0x00088D05
		private float GetAllianceEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			if (queriedKingdom.IsAllyWith(querierKingdom))
			{
				return 5f;
			}
			return 0f;
		}

		// Token: 0x06001AE7 RID: 6887 RVA: 0x0008AB1C File Offset: 0x00088D1C
		private float GetDiplomacyEffect(Kingdom querierKingdom, Kingdom queriedKingdom, out bool isAtWarWithAlliedKingdom, out bool isAtWarWithAnyKingdom)
		{
			isAtWarWithAlliedKingdom = querierKingdom.AlliedKingdoms.Any((Kingdom x) => x.IsAtWarWith(queriedKingdom));
			isAtWarWithAnyKingdom = (isAtWarWithAlliedKingdom || Kingdom.All.Any((Kingdom x) => !x.IsEliminated && x != queriedKingdom && x != querierKingdom && x.IsAtWarWith(queriedKingdom)));
			return 2.5f;
		}

		// Token: 0x06001AE8 RID: 6888 RVA: 0x0008AB80 File Offset: 0x00088D80
		private float GetRelationEffectBetweenRulers(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			return (float)querierKingdom.Leader.GetRelation(queriedKingdom.Leader) * 0.1f;
		}

		// Token: 0x06001AE9 RID: 6889 RVA: 0x0008AB9C File Offset: 0x00088D9C
		private float GetProsperityEffect(Kingdom querierKingdom, Kingdom queriedKingdom, out float querierKingdomAverageProsperity, out float queriedKingdomAverageProsperity)
		{
			querierKingdomAverageProsperity = this.GetAverageProsperityOfTownsInKingdom(querierKingdom);
			queriedKingdomAverageProsperity = this.GetAverageProsperityOfTownsInKingdom(queriedKingdom);
			return (2500f - MathF.Clamp(MathF.Abs(queriedKingdomAverageProsperity - querierKingdomAverageProsperity), 0.1f, 2500f)) / 2500f * 45f;
		}

		// Token: 0x06001AEA RID: 6890 RVA: 0x0008ABE8 File Offset: 0x00088DE8
		private float GetAverageProsperityOfTownsInKingdom(Kingdom kingdom)
		{
			if (kingdom.Towns.Count > 0)
			{
				float num = 0f;
				for (int i = 0; i < kingdom.Towns.Count; i++)
				{
					num += kingdom.Towns[i].Prosperity;
				}
				return num / (float)kingdom.Towns.Count;
			}
			return 0f;
		}

		// Token: 0x06001AEB RID: 6891 RVA: 0x0008AC48 File Offset: 0x00088E48
		private float GetSecurityEffect(Kingdom queriedKingdom)
		{
			if (queriedKingdom.Towns.Count > 0)
			{
				float num = 0f;
				for (int i = 0; i < queriedKingdom.Towns.Count; i++)
				{
					num += queriedKingdom.Towns[i].Security;
				}
				return MathF.Clamp((num / (float)queriedKingdom.Towns.Count - 85f) * 0.4f, -5f, 0f);
			}
			return 0f;
		}

		// Token: 0x06001AEC RID: 6892 RVA: 0x0008ACC2 File Offset: 0x00088EC2
		public override CampaignTime GetTradeAgreementDurationInYears(Kingdom iniatatingKingdom, Kingdom otherKingdom)
		{
			return CampaignTime.Years(1f);
		}

		// Token: 0x06001AED RID: 6893 RVA: 0x0008ACD0 File Offset: 0x00088ED0
		private float GetExposureEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			float result = 0f;
			if (queriedKingdom.Fiefs.Count > 0 && querierKingdom.Fiefs.Count > 0)
			{
				HashSet<Settlement> hashSet = new HashSet<Settlement>();
				int num = 0;
				int num2 = 0;
				foreach (Town town in querierKingdom.Fiefs)
				{
					foreach (Settlement settlement in town.GetNeighborFortifications(MobileParty.NavigationType.All))
					{
						if (settlement.IsFortification && !hashSet.Contains(settlement) && settlement.MapFaction != querierKingdom)
						{
							if (settlement.MapFaction == queriedKingdom)
							{
								num2++;
							}
							num++;
							hashSet.Add(settlement);
						}
					}
				}
				result = Math.Min((float)num2 / (float)num * 50f, 30f);
			}
			return result;
		}

		// Token: 0x06001AEE RID: 6894 RVA: 0x0008ADDC File Offset: 0x00088FDC
		public override int GetProfitPerCaravanVisit(MobileParty mobileParty)
		{
			return 500;
		}

		// Token: 0x040008FE RID: 2302
		private const float MaxExposureEffect = 30f;

		// Token: 0x040008FF RID: 2303
		private const float MarriageBetweenRulingClansBonus = 10f;

		// Token: 0x04000900 RID: 2304
		private const float MarriageBetweenKingdomsBonus = 5f;

		// Token: 0x04000901 RID: 2305
		private const float AlliedScoreBonus = 5f;

		// Token: 0x04000902 RID: 2306
		private const float WarWithAllyPenalty = -15f;

		// Token: 0x04000903 RID: 2307
		private const float WarWithAnyKingdomPenalty = -1.5f;

		// Token: 0x04000904 RID: 2308
		private const float NoWarBonus = 2.5f;

		// Token: 0x04000905 RID: 2309
		private const float ProsperityCheckRange = 2500f;

		// Token: 0x04000906 RID: 2310
		private static readonly TextObject _kingdomsAtWarText = new TextObject("{=vo7kAlkR}The kingdoms are at war.", null);

		// Token: 0x04000907 RID: 2311
		private static readonly TextObject _eliminatedKingdomText = new TextObject("{=ZeNt57yM}The kingdom is eliminated.", null);

		// Token: 0x04000908 RID: 2312
		private static readonly TextObject _existingTradeAgreementText = new TextObject("{=8HXcla1b}These kingdoms already have a trade agreement.", null);

		// Token: 0x04000909 RID: 2313
		private static readonly TextObject _maximumNumberOfTradeAgreementsText = new TextObject("{=DJ51OJWj}You already have maximum number of trade agreements.", null);

		// Token: 0x0400090A RID: 2314
		private static readonly TextObject _noTownText = new TextObject("{=QQ4bi6Zr}You don't own any towns.", null);

		// Token: 0x0400090B RID: 2315
		private static readonly TextObject _landlockedText = new TextObject("{=Ig8l75Rg}One of the kingdoms is landlocked.", null);

		// Token: 0x0400090C RID: 2316
		private static readonly TextObject _kingdomsNotNeighborsText = new TextObject("{=Bu6YdMme}Kingdoms aren't neighbors.", null);

		// Token: 0x0400090D RID: 2317
		private static readonly TextObject _limitedSharedBordersText = new TextObject("{=EapZFDGF}Limited shared borders", null);

		// Token: 0x0400090E RID: 2318
		private static readonly TextObject _relationsText = new TextObject("{=3YVDMg5X}Low relations between rulers", null);

		// Token: 0x0400090F RID: 2319
		private static readonly TextObject _warWithAlliedText = new TextObject("{=tT91z3AL}Your realm is at war with their ally.", null);

		// Token: 0x04000910 RID: 2320
		private static readonly TextObject _warText = new TextObject("{=FaEOnF8q}Your realm is a participant in a war.", null);

		// Token: 0x04000911 RID: 2321
		private static readonly TextObject _lowSecurityText = new TextObject("{=aTuRql06}Target faction is concerned by security of towns in your realm.", null);

		// Token: 0x04000912 RID: 2322
		private static readonly TextObject _higherQuerierProsperityText = new TextObject("{=ji8oPOXU}Your realm is not open to negotiation as target faction’s prosperity is too low.", null);

		// Token: 0x04000913 RID: 2323
		private static readonly TextObject _higherQueriedProsperityText = new TextObject("{=lYaumdUj}Your realm has lower prosperity then target faction.", null);

		// Token: 0x04000914 RID: 2324
		private static readonly TextObject _recentWarText = new TextObject("{=lDIz0nEY}Recent war", null);

		// Token: 0x04000915 RID: 2325
		private const int MaxReasonsInExplanation = 3;

		// Token: 0x04000916 RID: 2326
		private ITradeAgreementsCampaignBehavior _tradeAgreementsBehavior;
	}
}
