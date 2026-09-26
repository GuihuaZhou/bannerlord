using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.GameComponents
{
	// Token: 0x020000F0 RID: 240
	public class DefaultAllianceModel : AllianceModel
	{
		// Token: 0x1700060F RID: 1551
		// (get) Token: 0x06001613 RID: 5651 RVA: 0x0006484A File Offset: 0x00062A4A
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

		// Token: 0x17000610 RID: 1552
		// (get) Token: 0x06001614 RID: 5652 RVA: 0x0006486A File Offset: 0x00062A6A
		public override CampaignTime MaxDurationOfAlliance
		{
			get
			{
				return CampaignTime.Days(84f);
			}
		}

		// Token: 0x17000611 RID: 1553
		// (get) Token: 0x06001615 RID: 5653 RVA: 0x00064876 File Offset: 0x00062A76
		public override CampaignTime MaxDurationOfWarParticipation
		{
			get
			{
				return CampaignTime.Days(42f);
			}
		}

		// Token: 0x17000612 RID: 1554
		// (get) Token: 0x06001616 RID: 5654 RVA: 0x00064882 File Offset: 0x00062A82
		public override int MaxNumberOfAlliances
		{
			get
			{
				return 2;
			}
		}

		// Token: 0x17000613 RID: 1555
		// (get) Token: 0x06001617 RID: 5655 RVA: 0x00064885 File Offset: 0x00062A85
		public override CampaignTime DurationForOffers
		{
			get
			{
				return CampaignTime.Hours(24f);
			}
		}

		// Token: 0x06001618 RID: 5656 RVA: 0x00064894 File Offset: 0x00062A94
		public override int GetCallToWarCost(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
		{
			int callToWarCostForCalledKingdom = this.GetCallToWarCostForCalledKingdom(calledKingdom, kingdomToCallToWarAgainst);
			int callToWarBudgetOfCallingKingdom = this.GetCallToWarBudgetOfCallingKingdom(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
			if (callingKingdom == Clan.PlayerClan.Kingdom && callToWarBudgetOfCallingKingdom < 0)
			{
				return callToWarCostForCalledKingdom;
			}
			return MathF.Min((int)((double)callToWarCostForCalledKingdom * 1.5), (callToWarCostForCalledKingdom + callToWarBudgetOfCallingKingdom) / 2);
		}

		// Token: 0x06001619 RID: 5657 RVA: 0x000648E0 File Offset: 0x00062AE0
		public override ExplainedNumber GetScoreOfStartingAlliance(Kingdom querierKingdom, Kingdom queriedKingdom, out TextObject explanationText, bool includeDescription = false)
		{
			explanationText = this._allianceNotFormedExplanationText;
			ExplainedNumber result = new ExplainedNumber(0f, includeDescription, null);
			List<ValueTuple<float, TextObject>> list = new List<ValueTuple<float, TextObject>>();
			if (this.AreKingdomsNeighbors(querierKingdom, queriedKingdom))
			{
				float num;
				float num2;
				ValueTuple<Kingdom, float> threateningNeighbor = this.GetThreateningNeighbor(querierKingdom, out num, out num2);
				Kingdom item = threateningNeighbor.Item1;
				float item2 = threateningNeighbor.Item2;
				if (item != null && item2 > 430f && item != queriedKingdom)
				{
					ValueTuple<Kingdom, float> threateningNeighbor2 = this.GetThreateningNeighbor(queriedKingdom, out num2, out num);
					Kingdom item3 = threateningNeighbor2.Item1;
					float item4 = threateningNeighbor2.Item2;
					if (item4 > 430f)
					{
						float alliancePenalty = this.GetAlliancePenalty(querierKingdom);
						TextObject alliancePenaltyText = this.GetAlliancePenaltyText(querierKingdom, includeDescription);
						result.Add(alliancePenalty, alliancePenaltyText, null);
						float alliancePenalty2 = this.GetAlliancePenalty(queriedKingdom);
						TextObject alliancePenaltyText2 = this.GetAlliancePenaltyText(queriedKingdom, includeDescription);
						result.Add(alliancePenalty2, alliancePenaltyText2, null);
						float threatEffect = this.GetThreatEffect(item4, item2);
						result.Add(threatEffect, this._threatEffect, null);
						float relationshipEffect = this.GetRelationshipEffect(querierKingdom, queriedKingdom);
						result.Add(relationshipEffect, this._relationshipText, null);
						float marriageEffect = this.GetMarriageEffect(querierKingdom, queriedKingdom);
						result.Add(marriageEffect, this._marriageEffect, null);
						float atWarWithAllyEffect = this.GetAtWarWithAllyEffect(querierKingdom, queriedKingdom);
						result.Add(atWarWithAllyEffect, this._atWarWithAllyText, null);
						float atWarWithEnemyEffect = this.GetAtWarWithEnemyEffect(querierKingdom, queriedKingdom);
						result.Add(atWarWithEnemyEffect, this._atWarWithEnemyEffect, null);
						float atWarOrPeaceEffect = this.GetAtWarOrPeaceEffect(queriedKingdom);
						result.Add(atWarOrPeaceEffect, this._atWarText, null);
						float fiefWithSameCultureEffect = this.GetFiefWithSameCultureEffect(querierKingdom, queriedKingdom);
						result.Add(fiefWithSameCultureEffect, this._sameCultureFiefsText, null);
						float honorableKingEffect = this.GetHonorableKingEffect(querierKingdom, queriedKingdom);
						if (includeDescription)
						{
							this._lowHonorText.SetCharacterProperties("RULER", querierKingdom.Leader.CharacterObject, false);
						}
						result.Add(honorableKingEffect, this._lowHonorText, null);
						float tradeAgreementEffect = this.GetTradeAgreementEffect(querierKingdom, queriedKingdom);
						result.Add(tradeAgreementEffect, this._tradeAgreementEffect, null);
						float commonThreatEffect = this.GetCommonThreatEffect(item, item3);
						result.Add(commonThreatEffect, this._commonThreatEffect, null);
						if (includeDescription)
						{
							if (alliancePenalty < 0f)
							{
								list.Add(new ValueTuple<float, TextObject>(alliancePenalty, alliancePenaltyText));
							}
							if (alliancePenalty2 < 0f)
							{
								list.Add(new ValueTuple<float, TextObject>(alliancePenalty2, alliancePenaltyText2));
							}
							if (relationshipEffect < 0f)
							{
								list.Add(new ValueTuple<float, TextObject>(relationshipEffect, this._relationshipText));
							}
							if (atWarWithAllyEffect < 0f)
							{
								list.Add(new ValueTuple<float, TextObject>(atWarWithAllyEffect, this._atWarWithAllyText));
							}
							if (atWarOrPeaceEffect < 0f)
							{
								list.Add(new ValueTuple<float, TextObject>(atWarOrPeaceEffect, this._atWarText));
							}
							if (atWarWithAllyEffect < 0f)
							{
								list.Add(new ValueTuple<float, TextObject>(fiefWithSameCultureEffect, this._sameCultureFiefsText));
							}
							if (honorableKingEffect < 0f)
							{
								list.Add(new ValueTuple<float, TextObject>(honorableKingEffect, this._lowHonorText));
							}
							if ((float)list.Count > 0f)
							{
								explanationText = this.BuildExplanationForAlliance(querierKingdom, list);
							}
							else
							{
								this._allianceScoreNotEnoughText.SetTextVariable("KINGDOM_NAME", querierKingdom.Name);
								explanationText.SetTextVariable("REASON", this._allianceScoreNotEnoughText);
							}
						}
					}
					else if (includeDescription)
					{
						this._allianceScoreNotEnoughText.SetTextVariable("KINGDOM_NAME", querierKingdom.Name);
						explanationText.SetTextVariable("REASON", this._allianceScoreNotEnoughText);
					}
				}
				else if (includeDescription)
				{
					this._kingdomsNotSeekingAllianceText.SetTextVariable("KINGDOM_NAME", querierKingdom.Name);
					explanationText.SetTextVariable("REASON", this._kingdomsNotSeekingAllianceText);
				}
			}
			else if (includeDescription)
			{
				explanationText.SetTextVariable("REASON", this._kingdomsNotNeighborsText);
			}
			return result;
		}

		// Token: 0x0600161A RID: 5658 RVA: 0x00064C58 File Offset: 0x00062E58
		public override float GetSupportScoreOfStartingAllianceForClan(Kingdom querierKingdom, Kingdom queriedKingdom, Clan evaluatingClan, out TextObject explanationText, bool includeDescriptions = false)
		{
			explanationText = (includeDescriptions ? TextObject.GetEmpty() : null);
			int influenceCostOfProposingStartingAlliance = Campaign.Current.Models.AllianceModel.GetInfluenceCostOfProposingStartingAlliance(evaluatingClan);
			if (evaluatingClan == Clan.PlayerClan)
			{
				return (float)influenceCostOfProposingStartingAlliance;
			}
			if (evaluatingClan.Kingdom != Clan.PlayerClan.Kingdom)
			{
				return (float)influenceCostOfProposingStartingAlliance;
			}
			float num = Campaign.Current.Models.AllianceModel.GetScoreOfStartingAlliance(querierKingdom, queriedKingdom, out explanationText, includeDescriptions).ResultNumber;
			if (evaluatingClan.Leader != null)
			{
				num += (float)evaluatingClan.Leader.GetRelation(queriedKingdom.Leader) * 0.25f;
			}
			if (this.IsThereMarriageBetweenClans(evaluatingClan, queriedKingdom.RulingClan))
			{
				num += 25f;
			}
			else if (queriedKingdom.Clans.AnyQ((Clan clan) => clan != queriedKingdom.RulingClan && this.IsThereMarriageBetweenClans(evaluatingClan, clan)))
			{
				num += 5f;
			}
			if (queriedKingdom.Leader != null)
			{
				num += (float)queriedKingdom.Leader.GetTraitLevel(DefaultTraits.Honor) * 6.25f;
			}
			if (evaluatingClan.Leader != null)
			{
				num += evaluatingClan.Leader.RandomFloatWithSeed((uint)CampaignTime.Now.ToDays) * 20f - 10f;
			}
			float result;
			if (num > 0f)
			{
				result = MBMath.Map(num, 0f, 195f, 0f, (float)influenceCostOfProposingStartingAlliance);
			}
			else
			{
				result = MBMath.Map(num, -135f, 0f, (float)(-(float)influenceCostOfProposingStartingAlliance), 0f);
			}
			return result;
		}

		// Token: 0x0600161B RID: 5659 RVA: 0x00064E1C File Offset: 0x0006301C
		public override bool CanMakeAlliance(Kingdom kingdom, Kingdom targetKingdom, IFaction evaluatingFaction, out TextObject reason, bool includeReason = false)
		{
			reason = (includeReason ? this._allianceNotFormedExplanationText : null);
			if (targetKingdom.IsEliminated || kingdom.IsEliminated)
			{
				if (includeReason)
				{
					reason.SetTextVariable("REASON", new TextObject("{=a5EAl1aW}That realm has been eliminated.", null));
				}
				return false;
			}
			if (targetKingdom == kingdom)
			{
				if (includeReason)
				{
					reason.SetTextVariable("REASON", new TextObject("{=zPoS5fIu}You are referring to your own realm.", null));
				}
				return false;
			}
			if (targetKingdom.IsAtWarWith(kingdom))
			{
				if (includeReason)
				{
					TextObject textObject = new TextObject("{=lseJ70y0}Your realm is at war with the {KINGDOM_NAME}.", null);
					textObject.SetTextVariable("KINGDOM_NAME", targetKingdom.Name);
					reason.SetTextVariable("REASON", textObject);
				}
				return false;
			}
			if (kingdom.AlliedKingdoms.Count >= Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances)
			{
				if (includeReason)
				{
					TextObject textObject2 = new TextObject("{=xOJtrGAR}Your realm's current number of allies: {NUMBER_OF_ALLIES}/{MAX_NUMBER_OF_ALLIES}", null);
					textObject2.SetTextVariable("NUMBER_OF_ALLIES", kingdom.AlliedKingdoms.Count);
					textObject2.SetTextVariable("MAX_NUMBER_OF_ALLIES", Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances);
					reason.SetTextVariable("REASON", textObject2);
				}
				return false;
			}
			if (targetKingdom.AlliedKingdoms.Count >= Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances)
			{
				if (includeReason)
				{
					TextObject textObject3 = new TextObject("{=rYssCdQb}{KINGDOM_NAME} cannot have any more allies.", null);
					textObject3.SetTextVariable("KINGDOM_NAME", targetKingdom.Name);
					reason.SetTextVariable("REASON", textObject3);
				}
				return false;
			}
			if (targetKingdom.IsAllyWith(kingdom))
			{
				if (includeReason)
				{
					TextObject textObject4 = new TextObject("{=zd9sawl9}You are already allied with the {KINGDOM_NAME}.", null);
					textObject4.SetTextVariable("KINGDOM_NAME", targetKingdom.Name);
					reason.SetTextVariable("REASON", textObject4);
				}
				return false;
			}
			if (kingdom == Clan.PlayerClan.Kingdom)
			{
				if (Campaign.Current.Models.AllianceModel.GetScoreOfStartingAlliance(targetKingdom, kingdom, out reason, includeReason).ResultNumber < 50f)
				{
					return false;
				}
				Clan evaluatingClan;
				if (evaluatingFaction != Clan.PlayerClan && (evaluatingClan = (evaluatingFaction as Clan)) != null && !this.CanMakeAllianceWithPlayerSupport(kingdom, targetKingdom, evaluatingClan))
				{
					return false;
				}
			}
			else
			{
				if (Campaign.Current.Models.AllianceModel.GetScoreOfStartingAlliance(kingdom, targetKingdom, out reason, includeReason).ResultNumber < 50f)
				{
					return false;
				}
				Clan playerClan = Clan.PlayerClan;
				if (((playerClan != null) ? playerClan.Kingdom : null) != null && Clan.PlayerClan.Kingdom == targetKingdom)
				{
					if (!this.CanMakeAllianceWithPlayerSupport(targetKingdom, kingdom, Campaign.Current.Models.AllianceModel.GetProposerClanForAllianceDecision(targetKingdom, kingdom)))
					{
						return false;
					}
				}
				else if (Campaign.Current.Models.AllianceModel.GetScoreOfStartingAlliance(targetKingdom, kingdom, out reason, includeReason).ResultNumber < 50f)
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x0600161C RID: 5660 RVA: 0x000650CE File Offset: 0x000632CE
		public override int GetInfluenceCostOfProposingStartingAlliance(Clan proposingClan)
		{
			return 200;
		}

		// Token: 0x0600161D RID: 5661 RVA: 0x000650D8 File Offset: 0x000632D8
		public override float GetScoreOfCallingToWar(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, IFaction evaluatingFaction, out TextObject reason)
		{
			float num = 60f;
			reason = TextObject.GetEmpty();
			int callToWarBudgetOfCallingKingdom = this.GetCallToWarBudgetOfCallingKingdom(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
			int callToWarCost = Campaign.Current.Models.AllianceModel.GetCallToWarCost(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
			Clan clan;
			Clan evaluatingClan = ((clan = (evaluatingFaction as Clan)) != null) ? clan : callingKingdom.RulingClan;
			TextObject textObject;
			float scoreOfDeclaringWar = Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringWar(callingKingdom, kingdomToCallToWarAgainst, evaluatingClan, out textObject, false);
			if (callToWarBudgetOfCallingKingdom < 0 || callingKingdom.CallToWarWallet < -100000 || (float)callToWarBudgetOfCallingKingdom * 1.5f < (float)callToWarCost || scoreOfDeclaringWar > 0f)
			{
				return -100f;
			}
			if (callToWarCost == 0)
			{
				return 100f;
			}
			float num2 = (float)callToWarBudgetOfCallingKingdom / (float)callToWarCost;
			num *= num2;
			return num + ((float)evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Calculating) * 2.5f - (float)evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Valor) * 2.5f);
		}

		// Token: 0x0600161E RID: 5662 RVA: 0x000651C0 File Offset: 0x000633C0
		public override float GetScoreOfJoiningWar(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, IFaction evaluatingFaction, out TextObject reason)
		{
			float num = 70f;
			reason = TextObject.GetEmpty();
			int callToWarCostForCalledKingdom = this.GetCallToWarCostForCalledKingdom(calledKingdom, kingdomToCallToWarAgainst);
			int callToWarCost = Campaign.Current.Models.AllianceModel.GetCallToWarCost(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
			if (callToWarCostForCalledKingdom == 0)
			{
				return 100f;
			}
			float num2 = (float)callToWarCost / (float)callToWarCostForCalledKingdom;
			num2 = MathF.Clamp(num2, 1E-05f, 2f);
			num *= num2;
			return num + ((float)evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Valor) * 2.5f + (float)evaluatingFaction.Leader.GetTraitLevel(DefaultTraits.Calculating) * 2.5f);
		}

		// Token: 0x0600161F RID: 5663 RVA: 0x00065255 File Offset: 0x00063455
		public override int GetInfluenceCostOfCallingToWar(Clan proposingClan)
		{
			return 200;
		}

		// Token: 0x06001620 RID: 5664 RVA: 0x0006525C File Offset: 0x0006345C
		public override float GetAllianceFactorForDeclaringWar(IFaction factionDeclaresWar, IFaction factionDeclaredWar)
		{
			if (factionDeclaresWar.IsKingdomFaction && factionDeclaredWar.IsKingdomFaction)
			{
				bool flag = false;
				float num = 1f;
				Kingdom kingdom = (Kingdom)factionDeclaresWar;
				Kingdom kingdom2 = (Kingdom)factionDeclaredWar;
				foreach (Kingdom kingdom3 in kingdom.AlliedKingdoms)
				{
					if (kingdom3 == kingdom2)
					{
						num *= 0.5f;
						break;
					}
					if (!flag && kingdom3.IsAtWarWith(kingdom2))
					{
						float num2;
						float num3;
						if (this.GetThreateningNeighbor(kingdom, out num2, out num3).Item1 == kingdom2)
						{
							num *= 1.5f;
						}
						else
						{
							num *= 1.3f;
						}
						flag = true;
					}
				}
				return num;
			}
			return 1f;
		}

		// Token: 0x06001621 RID: 5665 RVA: 0x00065320 File Offset: 0x00063520
		public override float GetAllianceFactorForDeclaringPeace(IFaction factionDeclaresPeace, IFaction factionDeclaredPeace)
		{
			if (factionDeclaresPeace.IsKingdomFaction && factionDeclaredPeace.IsKingdomFaction)
			{
				float num = 1f;
				Kingdom kingdom = (Kingdom)factionDeclaresPeace;
				Kingdom other = (Kingdom)factionDeclaredPeace;
				using (List<Kingdom>.Enumerator enumerator = kingdom.AlliedKingdoms.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						if (enumerator.Current.IsAtWarWith(other))
						{
							num *= 0.7f;
							break;
						}
					}
				}
				return num;
			}
			return 1f;
		}

		// Token: 0x06001622 RID: 5666 RVA: 0x000653A8 File Offset: 0x000635A8
		public override Clan GetProposerClanForAllianceDecision(Kingdom proposerKingdom, Kingdom proposedKingdom)
		{
			TextObject textObject;
			Clan clan = (proposerKingdom.RulingClan != Clan.PlayerClan && Campaign.Current.Models.AllianceModel.GetSupportScoreOfStartingAllianceForClan(proposerKingdom, proposedKingdom, proposerKingdom.RulingClan, out textObject, false) > 0f) ? proposerKingdom.RulingClan : null;
			if (clan == null)
			{
				List<ValueTuple<Clan, float>> list = new List<ValueTuple<Clan, float>>(proposerKingdom.Clans.Count);
				foreach (Clan clan2 in proposerKingdom.Clans)
				{
					if (!clan2.IsUnderMercenaryService && clan2 != Clan.PlayerClan)
					{
						float supportScoreOfStartingAllianceForClan = Campaign.Current.Models.AllianceModel.GetSupportScoreOfStartingAllianceForClan(proposerKingdom, proposedKingdom, clan2, out textObject, false);
						if (supportScoreOfStartingAllianceForClan > 0f)
						{
							list.Add(new ValueTuple<Clan, float>(clan2, supportScoreOfStartingAllianceForClan));
						}
					}
				}
				if (list.Count > 0)
				{
					clan = list.MaxBy(([TupleElementNames(new string[]
					{
						"Clan",
						"Score"
					})] ValueTuple<Clan, float> x) => x.Item2).Item1;
				}
				else
				{
					clan = ((proposerKingdom == Clan.PlayerClan.Kingdom) ? Clan.PlayerClan : proposerKingdom.RulingClan);
				}
			}
			return clan;
		}

		// Token: 0x06001623 RID: 5667 RVA: 0x000654E4 File Offset: 0x000636E4
		[return: TupleElementNames(new string[]
		{
			"threateningKingdom",
			"threatScore"
		})]
		private ValueTuple<Kingdom, float> GetThreateningNeighbor(Kingdom querierKingdom, out float exposureScore, out float powerRatio)
		{
			HashSet<Settlement> hashSet = new HashSet<Settlement>();
			Dictionary<Kingdom, float> dictionary = new Dictionary<Kingdom, float>();
			float num = 0f;
			foreach (Town town in querierKingdom.Fiefs)
			{
				foreach (Settlement settlement in town.GetNeighborFortifications(MobileParty.NavigationType.All))
				{
					if (settlement.MapFaction != querierKingdom && settlement.MapFaction.IsKingdomFaction && !hashSet.Contains(settlement))
					{
						Kingdom kingdom = (Kingdom)settlement.MapFaction;
						if (dictionary.ContainsKey(kingdom))
						{
							Dictionary<Kingdom, float> dictionary2 = dictionary;
							Kingdom key = kingdom;
							dictionary2[key] += 1f;
						}
						else
						{
							dictionary.Add(kingdom, 1f);
						}
						num += 1f;
						hashSet.Add(settlement);
					}
				}
			}
			float num2 = 0f;
			Kingdom item = null;
			exposureScore = 0f;
			powerRatio = 0f;
			foreach (KeyValuePair<Kingdom, float> keyValuePair in dictionary)
			{
				float num4;
				float num5;
				float num3 = this.CalculateThreatScore(keyValuePair.Value, num, this.CalculateKingdomStrength(keyValuePair.Key), this.CalculateKingdomStrength(querierKingdom), out num4, out num5);
				if (num2 < num3)
				{
					item = keyValuePair.Key;
					num2 = num3;
					exposureScore = num4;
					powerRatio = num5;
				}
			}
			return new ValueTuple<Kingdom, float>(item, num2);
		}

		// Token: 0x06001624 RID: 5668 RVA: 0x0006569C File Offset: 0x0006389C
		private bool IsThereMarriageBetweenClans(Clan clan1, Clan clan2)
		{
			return clan1.AliveLords.AnyQ(delegate(Hero x)
			{
				Hero spouse = x.Spouse;
				Clan clan3;
				if (spouse == null)
				{
					clan3 = null;
				}
				else
				{
					Hero father = spouse.Father;
					clan3 = ((father != null) ? father.Clan : null);
				}
				return clan3 == clan2;
			}) || clan2.AliveLords.AnyQ(delegate(Hero x)
			{
				Hero spouse = x.Spouse;
				Clan clan3;
				if (spouse == null)
				{
					clan3 = null;
				}
				else
				{
					Hero father = spouse.Father;
					clan3 = ((father != null) ? father.Clan : null);
				}
				return clan3 == clan1;
			});
		}

		// Token: 0x06001625 RID: 5669 RVA: 0x000656FC File Offset: 0x000638FC
		private TextObject BuildExplanationForAlliance(Kingdom other, List<ValueTuple<float, TextObject>> explanationList)
		{
			TextObject textObject = null;
			textObject = this._kingdomNotConsederingAllianceText;
			List<TextObject> list = new List<TextObject>();
			foreach (ValueTuple<float, TextObject> valueTuple in from x in explanationList
			orderby x.Item1
			select x)
			{
				TextObject item = valueTuple.Item2;
				list.Add(item);
				if (list.Count >= 3)
				{
					break;
				}
			}
			TextObject variable = GameTexts.GameTextHelper.MergeTextObjectsWithSymbol(list, new TextObject("{=!}{newline}", null), null);
			textObject.SetTextVariable("REASONS_BY_LINE", variable);
			textObject.SetTextVariable("KINGDOM", other.Name);
			return textObject;
		}

		// Token: 0x06001626 RID: 5670 RVA: 0x000657BC File Offset: 0x000639BC
		private int GetCallToWarCostForCalledKingdom(Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
		{
			TextObject textObject;
			float scoreOfDeclaringWar = Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringWar(calledKingdom, kingdomToCallToWarAgainst, calledKingdom.RulingClan, out textObject, false);
			float num = Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(kingdomToCallToWarAgainst) - scoreOfDeclaringWar;
			if (num <= 0f)
			{
				return 0;
			}
			float valueOfSettlementsForFaction = Campaign.Current.Models.DiplomacyModel.GetValueOfSettlementsForFaction(calledKingdom);
			double num2 = (double)(num / (valueOfSettlementsForFaction + 1f));
			double num3 = (double)calledKingdom.Fiefs.SumQ((Town x) => x.Prosperity) * 0.35;
			return (int)(num2 * num3 * Campaign.Current.Models.AllianceModel.MaxDurationOfWarParticipation.ToDays);
		}

		// Token: 0x06001627 RID: 5671 RVA: 0x00065884 File Offset: 0x00063A84
		private int GetCallToWarBudgetOfCallingKingdom(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst)
		{
			float num = this.CalculateKingdomStrength(callingKingdom);
			float num2 = this.CalculateKingdomStrength(calledKingdom);
			float num3 = this.CalculateKingdomStrength(kingdomToCallToWarAgainst);
			double num4 = (double)callingKingdom.Fiefs.SumQ((Town x) => x.Prosperity) * 0.35;
			float num5 = num - num3;
			if (num5.ApproximatelyEqualsTo(0f, 1E-05f))
			{
				return int.MinValue;
			}
			return (int)((double)MathF.Clamp(-(num2 / num5), float.MinValue, 1f) * num4 * Campaign.Current.Models.AllianceModel.MaxDurationOfWarParticipation.ToDays);
		}

		// Token: 0x06001628 RID: 5672 RVA: 0x00065930 File Offset: 0x00063B30
		private bool AreKingdomsNeighbors(Kingdom kingdom1, Kingdom kingdom2)
		{
			if (kingdom1 == kingdom2 || kingdom1.Fiefs.Count == 0 || kingdom2.Fiefs.Count == 0)
			{
				return false;
			}
			foreach (Town town in kingdom1.Fiefs)
			{
				using (List<Settlement>.Enumerator enumerator2 = town.GetNeighborFortifications(MobileParty.NavigationType.All).GetEnumerator())
				{
					while (enumerator2.MoveNext())
					{
						if (enumerator2.Current.MapFaction == kingdom2)
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		// Token: 0x06001629 RID: 5673 RVA: 0x000659E4 File Offset: 0x00063BE4
		private float CalculateThreatScore(float neighborScore, float totalNeighborScore, float powerOfThreat, float powerOfQuerier, out float exposureScore, out float powerRatio)
		{
			if (powerOfQuerier <= 0f || totalNeighborScore <= 0f)
			{
				exposureScore = 0f;
				powerRatio = 0f;
				return 0f;
			}
			exposureScore = MBMath.Map(neighborScore / totalNeighborScore, 0f, 1f, 1f, 2f);
			powerRatio = MathF.Clamp(powerOfThreat / powerOfQuerier, 0f, 3f);
			return (MathF.Min(exposureScore, 1.7f) + 0.4f + powerRatio) * 130f;
		}

		// Token: 0x0600162A RID: 5674 RVA: 0x00065A6A File Offset: 0x00063C6A
		private float GetThreatEffect(float threatScoreForQuerier, float threatScoreForQueried)
		{
			return 0.08f * (threatScoreForQueried + threatScoreForQuerier) * 0.66f;
		}

		// Token: 0x0600162B RID: 5675 RVA: 0x00065A7C File Offset: 0x00063C7C
		private float GetRelationshipEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			if (querierKingdom.Leader != null && queriedKingdom.Leader != null)
			{
				int relation = querierKingdom.Leader.GetRelation(queriedKingdom.Leader);
				int traitLevel = querierKingdom.Leader.GetTraitLevel(DefaultTraits.Calculating);
				if (relation > 0 || traitLevel <= 0)
				{
					return MathF.Clamp((float)relation, -100f, 100f) * 0.08f;
				}
			}
			return 0f;
		}

		// Token: 0x0600162C RID: 5676 RVA: 0x00065AE4 File Offset: 0x00063CE4
		private float GetMarriageEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			if (this.IsThereMarriageBetweenClans(querierKingdom.RulingClan, queriedKingdom.RulingClan))
			{
				return 8f;
			}
			if (querierKingdom.Leader != null && queriedKingdom.RulingClan.AliveLords.AnyQ(delegate(Hero x)
			{
				Hero spouse = x.Spouse;
				Kingdom kingdom;
				if (spouse == null)
				{
					kingdom = null;
				}
				else
				{
					Hero father = spouse.Father;
					kingdom = ((father != null) ? father.MapFaction : null);
				}
				return kingdom == queriedKingdom;
			}))
			{
				return 4f;
			}
			return 0f;
		}

		// Token: 0x0600162D RID: 5677 RVA: 0x00065B54 File Offset: 0x00063D54
		private float GetAtWarWithAllyEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			return (queriedKingdom.FactionsAtWarWith.AnyQ((IFaction x) => x.IsKingdomFaction && querierKingdom.IsAllyWith((Kingdom)x)) ? -100f : 0f) * 0.08f;
		}

		// Token: 0x0600162E RID: 5678 RVA: 0x00065B9C File Offset: 0x00063D9C
		private float GetAtWarWithEnemyEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			return (queriedKingdom.FactionsAtWarWith.AnyQ((IFaction x) => x.IsKingdomFaction && querierKingdom.IsAtWarWith((Kingdom)x)) ? 100f : 0f) * 0.08f;
		}

		// Token: 0x0600162F RID: 5679 RVA: 0x00065BE1 File Offset: 0x00063DE1
		private float GetAtWarOrPeaceEffect(Kingdom queriedKingdom)
		{
			return (queriedKingdom.FactionsAtWarWith.AnyQ((IFaction x) => x.IsKingdomFaction) ? -5f : 25f) * 0.08f;
		}

		// Token: 0x06001630 RID: 5680 RVA: 0x00065C24 File Offset: 0x00063E24
		private float GetFiefWithSameCultureEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			float num = (float)queriedKingdom.Fiefs.Count;
			if (num > 0f)
			{
				return MathF.Clamp((float)queriedKingdom.Fiefs.Count((Town fief) => fief.Culture == querierKingdom.Culture) / num * -200f, -200f, 0f) * 0.08f;
			}
			return 0f;
		}

		// Token: 0x06001631 RID: 5681 RVA: 0x00065C90 File Offset: 0x00063E90
		private float GetHonorableKingEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			if (querierKingdom.Leader != null)
			{
				int traitLevel = querierKingdom.Leader.GetTraitLevel(DefaultTraits.Honor);
				int traitLevel2 = queriedKingdom.Leader.GetTraitLevel(DefaultTraits.Honor);
				if (traitLevel > 0)
				{
					return 4f;
				}
				if (traitLevel < 0 && traitLevel2 > 0)
				{
					return -4f;
				}
			}
			return 0f;
		}

		// Token: 0x06001632 RID: 5682 RVA: 0x00065CE4 File Offset: 0x00063EE4
		private float GetTradeAgreementEffect(Kingdom querierKingdom, Kingdom queriedKingdom)
		{
			ITradeAgreementsCampaignBehavior tradeAgreementsCampaignBehavior = this.TradeAgreementsCampaignBehavior;
			TradeAgreementsCampaignBehavior.TradeAgreement tradeAgreement;
			if (tradeAgreementsCampaignBehavior != null && tradeAgreementsCampaignBehavior.HasTradeAgreement(querierKingdom, queriedKingdom, out tradeAgreement))
			{
				return 4f;
			}
			return 0f;
		}

		// Token: 0x06001633 RID: 5683 RVA: 0x00065D14 File Offset: 0x00063F14
		private float GetCommonThreatEffect(Kingdom threateningKingdomForQuerier, Kingdom threateningKingdomForQueried)
		{
			if (threateningKingdomForQuerier != null && threateningKingdomForQueried != null && threateningKingdomForQuerier == threateningKingdomForQueried)
			{
				return 8f;
			}
			return 0f;
		}

		// Token: 0x06001634 RID: 5684 RVA: 0x00065D2C File Offset: 0x00063F2C
		private float GetAlliancePenalty(Kingdom kingdom)
		{
			if (kingdom.AlliedKingdoms.Count > 0)
			{
				float num = -48f;
				if (kingdom == Clan.PlayerClan.Kingdom)
				{
					num *= 0.5f;
				}
				return num;
			}
			return 0f;
		}

		// Token: 0x06001635 RID: 5685 RVA: 0x00065D6C File Offset: 0x00063F6C
		private TextObject GetAlliancePenaltyText(Kingdom kingdom, bool includeDescription)
		{
			TextObject textObject = (kingdom == Clan.PlayerClan.Kingdom) ? this._tooManyAlliancePlayerPenaltyText : this._tooManyAllianceAIPenaltyText;
			if (includeDescription)
			{
				textObject.SetTextVariable("NUMBER_OF_ALLIES", kingdom.AlliedKingdoms.Count);
				textObject.SetTextVariable("KINGDOM_NAME", kingdom.Name);
				textObject.SetTextVariable("MAX_NUMBER_OF_ALLIES", Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances);
			}
			return textObject;
		}

		// Token: 0x06001636 RID: 5686 RVA: 0x00065DE4 File Offset: 0x00063FE4
		private bool CanMakeAllianceWithPlayerSupport(Kingdom proposingKingdom, Kingdom proposedKingdom, Clan evaluatingClan)
		{
			if (proposingKingdom == Clan.PlayerClan.Kingdom && evaluatingClan == Clan.PlayerClan)
			{
				return true;
			}
			KingdomElection kingdomElection = new KingdomElection(new StartAllianceDecision(evaluatingClan, proposedKingdom));
			DecisionOutcome supportedOutcome = kingdomElection.PossibleOutcomes.FirstOrDefault(delegate(DecisionOutcome x)
			{
				StartAllianceDecision.StartAllianceDecisionOutcome startAllianceDecisionOutcome;
				return (startAllianceDecisionOutcome = (x as StartAllianceDecision.StartAllianceDecisionOutcome)) != null && startAllianceDecisionOutcome.ShouldAllianceBeStarted;
			});
			kingdomElection.SetupResultWithoutPlayerSupport();
			return kingdomElection.GetWinChanceWithPlayerSupport(supportedOutcome, Supporter.SupportWeights.FullyPush) > 0.5f;
		}

		// Token: 0x06001637 RID: 5687 RVA: 0x00065E54 File Offset: 0x00064054
		private float CalculateKingdomStrength(Kingdom kingdom)
		{
			float num = 0f;
			foreach (Clan clan in kingdom.Clans)
			{
				if (!clan.IsUnderMercenaryService)
				{
					num += clan.CurrentTotalStrength;
				}
			}
			return num;
		}

		// Token: 0x04000751 RID: 1873
		private const int ThresholdForCallToWarWallet = 100000;

		// Token: 0x04000752 RID: 1874
		private const float FirstDegreeNeighborScore = 1f;

		// Token: 0x04000753 RID: 1875
		private const int ThreatScoreCoefficient = 130;

		// Token: 0x04000754 RID: 1876
		private const float AllianceScoreNormalizationFactor = 0.08f;

		// Token: 0x04000755 RID: 1877
		private const float MarriageEffect = 50f;

		// Token: 0x04000756 RID: 1878
		private const float AtWarEffect = -5f;

		// Token: 0x04000757 RID: 1879
		private const float AtPeaceEffect = 25f;

		// Token: 0x04000758 RID: 1880
		private const float AtWarWithAllyEffect = -100f;

		// Token: 0x04000759 RID: 1881
		private const float AtWarWithEnemyEffect = 100f;

		// Token: 0x0400075A RID: 1882
		private const float HonorableRulerEffect = 50f;

		// Token: 0x0400075B RID: 1883
		private const float DishonorableRulerEffect = -50f;

		// Token: 0x0400075C RID: 1884
		private const float TradeAgreementEffect = 50f;

		// Token: 0x0400075D RID: 1885
		private const float CommonThreatEffect = 100f;

		// Token: 0x0400075E RID: 1886
		private const float ThresholdForThreatScoreForQuerier = 430f;

		// Token: 0x0400075F RID: 1887
		private const float ThresholdForThreatScoreForQueried = 430f;

		// Token: 0x04000760 RID: 1888
		private const float SecondAlliancePenaltyForAllianceScore = -600f;

		// Token: 0x04000761 RID: 1889
		private const float WarDeclarationScorePenaltyAgainstAllies = 0.5f;

		// Token: 0x04000762 RID: 1890
		private const float WarDeclarationScoreBonusAgainstEnemiesOfAllies = 0.3f;

		// Token: 0x04000763 RID: 1891
		private const float WarDeclarationScoreBonusAgainstBiggestThreat = 0.5f;

		// Token: 0x04000764 RID: 1892
		private const float PeaceDeclarationScorePenaltyAgainstEnemiesOfAllies = 0.7f;

		// Token: 0x04000765 RID: 1893
		private const float AllianceScoreThreshold = 50f;

		// Token: 0x04000766 RID: 1894
		private readonly TextObject _relationshipText = new TextObject("{=3YVDMg5X}Low relations between rulers.", null);

		// Token: 0x04000767 RID: 1895
		private readonly TextObject _kingdomsNotNeighborsText = new TextObject("{=Bu6YdMme}Kingdoms aren't neighbors.", null);

		// Token: 0x04000768 RID: 1896
		private readonly TextObject _kingdomsNotSeekingAllianceText = new TextObject("{=ml9bhOka}{KINGDOM_NAME} is not seeking alliances at the moment.", null);

		// Token: 0x04000769 RID: 1897
		private readonly TextObject _atWarWithAllyText = new TextObject("{=tT91z3AL}Your realm is at war with their ally.", null);

		// Token: 0x0400076A RID: 1898
		private readonly TextObject _sameCultureFiefsText = new TextObject("{=S5lz4aHk}Your realm is occupying fiefs belonging to their culture.", null);

		// Token: 0x0400076B RID: 1899
		private readonly TextObject _atWarText = new TextObject("{=aUStIIWw}Your realm is participating in a war.", null);

		// Token: 0x0400076C RID: 1900
		private readonly TextObject _lowHonorText = new TextObject("{=VIkVcmaE}{RULER.NAME} has low honor.", null);

		// Token: 0x0400076D RID: 1901
		private readonly TextObject _kingdomNotConsederingAllianceText = new TextObject("{=tbH04aAX}{KINGDOM} is not considering an alliance with your realm due to:{newline}{newline}{REASONS_BY_LINE}", null);

		// Token: 0x0400076E RID: 1902
		private readonly TextObject _allianceNotFormedExplanationText = new TextObject("{=Y20TbMLR}An alliance cannot be formed due to:{newline}{newline}{REASON}", null);

		// Token: 0x0400076F RID: 1903
		private readonly TextObject _tooManyAlliancePlayerPenaltyText = new TextObject("{=2RiYKRM8}Number of alliances your realm's already in: {NUMBER_OF_ALLIES}/{MAX_NUMBER_OF_ALLIES}", null);

		// Token: 0x04000770 RID: 1904
		private readonly TextObject _tooManyAllianceAIPenaltyText = new TextObject("{=cFYMdTcr}Number of alliances {KINGDOM_NAME} is already in: {NUMBER_OF_ALLIES}/{MAX_NUMBER_OF_ALLIES}", null);

		// Token: 0x04000771 RID: 1905
		private readonly TextObject _allianceScoreNotEnoughText = new TextObject("{=XnwKgWab}{KINGDOM_NAME} currently does not consider your realm to be a possible ally.", null);

		// Token: 0x04000772 RID: 1906
		private readonly TextObject _threatEffect = new TextObject("{=!}Threat Effect", null);

		// Token: 0x04000773 RID: 1907
		private readonly TextObject _marriageEffect = new TextObject("{=!}Marriage Effect", null);

		// Token: 0x04000774 RID: 1908
		private readonly TextObject _atWarWithEnemyEffect = new TextObject("{=!}At war with enemy Effect", null);

		// Token: 0x04000775 RID: 1909
		private readonly TextObject _tradeAgreementEffect = new TextObject("{=!}Trade Agreement Effect", null);

		// Token: 0x04000776 RID: 1910
		private readonly TextObject _commonThreatEffect = new TextObject("{=!}Common Threat Effect", null);

		// Token: 0x04000777 RID: 1911
		private const int MaxReasonsInExplanation = 3;

		// Token: 0x04000778 RID: 1912
		private ITradeAgreementsCampaignBehavior _tradeAgreementsBehavior;
	}
}
