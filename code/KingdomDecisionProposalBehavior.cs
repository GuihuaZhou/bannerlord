using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.LinQuick;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.CampaignBehaviors
{
	// Token: 0x02000411 RID: 1041
	public class KingdomDecisionProposalBehavior : CampaignBehaviorBase
	{
		// Token: 0x17000E16 RID: 3606
		// (get) Token: 0x060040B5 RID: 16565 RVA: 0x0012D4C1 File Offset: 0x0012B6C1
		public ITradeAgreementsCampaignBehavior TradeAgreementsCampaignBehavior
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

		// Token: 0x060040B6 RID: 16566 RVA: 0x0012D4E4 File Offset: 0x0012B6E4
		public override void RegisterEvents()
		{
			CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, new Action<Clan>(this.DailyTickClan));
			CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, new Action(this.HourlyTick));
			CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, new Action(this.DailyTick));
			CampaignEvents.MakePeace.AddNonSerializedListener(this, new Action<IFaction, IFaction, MakePeaceAction.MakePeaceDetail>(this.OnPeaceMade));
			CampaignEvents.WarDeclared.AddNonSerializedListener(this, new Action<IFaction, IFaction, DeclareWarAction.DeclareWarDetail>(this.OnWarDeclared));
			CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, new Action<Kingdom>(this.OnKingdomDestroyed));
			CampaignEvents.OnClanChangedKingdomEvent.AddNonSerializedListener(this, new Action<Clan, Kingdom, Kingdom, ChangeKingdomAction.ChangeKingdomActionDetail, bool>(this.OnClanChangedKingdom));
			CampaignEvents.KingdomDecisionAdded.AddNonSerializedListener(this, new Action<KingdomDecision, bool>(this.OnKingdomDecisionAdded));
		}

		// Token: 0x060040B7 RID: 16567 RVA: 0x0012D5A9 File Offset: 0x0012B7A9
		private void OnKingdomDestroyed(Kingdom kingdom)
		{
			this.UpdateKingdomDecisions(kingdom);
		}

		// Token: 0x060040B8 RID: 16568 RVA: 0x0012D5B4 File Offset: 0x0012B7B4
		private void DailyTickClan(Clan clan)
		{
			if ((float)((int)Campaign.Current.Models.CampaignTimeModel.CampaignStartTime.ElapsedDaysUntilNow) < 5f)
			{
				return;
			}
			if (clan.IsEliminated)
			{
				return;
			}
			if (clan == Clan.PlayerClan || clan.CurrentTotalStrength <= 0f)
			{
				return;
			}
			if (clan.IsBanditFaction)
			{
				return;
			}
			if (clan.Kingdom == null)
			{
				return;
			}
			if (clan.Influence < 100f)
			{
				return;
			}
			KingdomDecision kingdomDecision = null;
			float randomFloat = MBRandom.RandomFloat;
			int num = ((Kingdom)clan.MapFaction).Clans.Count((Clan x) => x.Influence > 100f);
			float num2 = MathF.Min(0.33f, 1f / ((float)num + 2f));
			num2 *= ((clan.Kingdom == Hero.MainHero.MapFaction && !Hero.MainHero.Clan.IsUnderMercenaryService) ? ((clan.Kingdom.Leader == Hero.MainHero) ? 0.5f : 0.75f) : 1f);
			DiplomacyModel diplomacyModel = Campaign.Current.Models.DiplomacyModel;
			AllianceModel allianceModel = Campaign.Current.Models.AllianceModel;
			if (randomFloat < num2 && clan.Influence > (float)diplomacyModel.GetInfluenceCostOfProposingPeace(clan))
			{
				kingdomDecision = KingdomDecisionProposalBehavior.GetRandomPeaceDecision(clan);
			}
			else if (randomFloat < num2 * 2f && clan.Influence > (float)diplomacyModel.GetInfluenceCostOfProposingWar(clan))
			{
				kingdomDecision = this.GetRandomWarDecision(clan);
			}
			else if (randomFloat < num2 * 2.5f)
			{
				kingdomDecision = ((MBRandom.RandomFloat < 0.5f) ? this.GetRandomTradeAgreementDecision(clan) : this.GetRandomStartingAllianceDecision(clan));
			}
			else if (randomFloat < num2 * 2.75f && clan.Influence > (float)(diplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(clan) * 4))
			{
				kingdomDecision = this.GetRandomPolicyDecision(clan);
			}
			else if (randomFloat < num2 * 3f && clan.Influence > 700f)
			{
				kingdomDecision = this.GetRandomAnnexationDecision(clan);
			}
			if (kingdomDecision != null)
			{
				bool flag = false;
				if (kingdomDecision is MakePeaceKingdomDecision && ((MakePeaceKingdomDecision)kingdomDecision).FactionToMakePeaceWith == Hero.MainHero.MapFaction)
				{
					foreach (KingdomDecision kingdomDecision2 in this._kingdomDecisionsList)
					{
						if (kingdomDecision2 is MakePeaceKingdomDecision && kingdomDecision2.Kingdom == Hero.MainHero.MapFaction && ((MakePeaceKingdomDecision)kingdomDecision2).FactionToMakePeaceWith == clan.Kingdom && kingdomDecision2.TriggerTime.IsFuture)
						{
							flag = true;
							break;
						}
						if (kingdomDecision2 is MakePeaceKingdomDecision && kingdomDecision2.Kingdom == clan.Kingdom && ((MakePeaceKingdomDecision)kingdomDecision2).FactionToMakePeaceWith == Hero.MainHero.MapFaction && kingdomDecision2.TriggerTime.IsFuture)
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					bool flag2 = false;
					foreach (KingdomDecision kingdomDecision3 in this._kingdomDecisionsList)
					{
						DeclareWarDecision declareWarDecision;
						DeclareWarDecision declareWarDecision2;
						if ((declareWarDecision = (kingdomDecision3 as DeclareWarDecision)) != null && (declareWarDecision2 = (kingdomDecision as DeclareWarDecision)) != null && declareWarDecision.FactionToDeclareWarOn == declareWarDecision2.FactionToDeclareWarOn && declareWarDecision.ProposerClan.MapFaction == declareWarDecision2.ProposerClan.MapFaction)
						{
							flag2 = true;
							break;
						}
						MakePeaceKingdomDecision makePeaceKingdomDecision;
						MakePeaceKingdomDecision makePeaceKingdomDecision2;
						if ((makePeaceKingdomDecision = (kingdomDecision3 as MakePeaceKingdomDecision)) != null && (makePeaceKingdomDecision2 = (kingdomDecision as MakePeaceKingdomDecision)) != null && makePeaceKingdomDecision.FactionToMakePeaceWith == makePeaceKingdomDecision2.FactionToMakePeaceWith && makePeaceKingdomDecision.ProposerClan.MapFaction == makePeaceKingdomDecision2.ProposerClan.MapFaction)
						{
							flag2 = true;
							break;
						}
					}
					if (!flag2)
					{
						clan.Kingdom.AddDecision(kingdomDecision, false);
						return;
					}
				}
			}
			else
			{
				this.UpdateKingdomDecisions(clan.Kingdom);
			}
		}

		// Token: 0x060040B9 RID: 16569 RVA: 0x0012D9A0 File Offset: 0x0012BBA0
		private void HourlyTick()
		{
			if (Clan.PlayerClan.Kingdom != null)
			{
				this.UpdateKingdomDecisions(Clan.PlayerClan.Kingdom);
			}
		}

		// Token: 0x060040BA RID: 16570 RVA: 0x0012D9C0 File Offset: 0x0012BBC0
		private void DailyTick()
		{
			for (int i = this._kingdomDecisionsList.Count - 1; i >= 0; i--)
			{
				if (this._kingdomDecisionsList[i].TriggerTime.ElapsedDaysUntilNow > 5f)
				{
					this._kingdomDecisionsList.RemoveAt(i);
				}
			}
		}

		// Token: 0x060040BB RID: 16571 RVA: 0x0012DA14 File Offset: 0x0012BC14
		public void UpdateKingdomDecisions(Kingdom kingdom)
		{
			List<KingdomDecision> list = new List<KingdomDecision>();
			List<KingdomDecision> list2 = new List<KingdomDecision>();
			foreach (KingdomDecision kingdomDecision in kingdom.UnresolvedDecisions)
			{
				if (kingdomDecision.ShouldBeCancelled())
				{
					list.Add(kingdomDecision);
				}
				else if (!kingdomDecision.IsPlayerParticipant || (kingdomDecision.TriggerTime.IsPast && !kingdomDecision.NeedsPlayerResolution))
				{
					list2.Add(kingdomDecision);
				}
			}
			foreach (KingdomDecision kingdomDecision2 in list)
			{
				kingdom.RemoveDecision(kingdomDecision2);
				bool flag;
				if (!kingdomDecision2.DetermineChooser().Leader.IsHumanPlayerCharacter)
				{
					flag = kingdomDecision2.DetermineSupporters().Any((Supporter x) => x.IsPlayer);
				}
				else
				{
					flag = true;
				}
				bool isPlayerInvolved = flag;
				CampaignEventDispatcher.Instance.OnKingdomDecisionCancelled(kingdomDecision2, isPlayerInvolved);
			}
			foreach (KingdomDecision decision in list2)
			{
				new KingdomElection(decision).StartElectionWithoutPlayer();
			}
		}

		// Token: 0x060040BC RID: 16572 RVA: 0x0012DB74 File Offset: 0x0012BD74
		private void OnPeaceMade(IFaction side1Faction, IFaction side2Faction, MakePeaceAction.MakePeaceDetail detail)
		{
			this.HandleDiplomaticChangeBetweenFactions(side1Faction, side2Faction);
		}

		// Token: 0x060040BD RID: 16573 RVA: 0x0012DB7E File Offset: 0x0012BD7E
		private void OnWarDeclared(IFaction side1Faction, IFaction side2Faction, DeclareWarAction.DeclareWarDetail detail)
		{
			this.HandleDiplomaticChangeBetweenFactions(side1Faction, side2Faction);
		}

		// Token: 0x060040BE RID: 16574 RVA: 0x0012DB88 File Offset: 0x0012BD88
		private void HandleDiplomaticChangeBetweenFactions(IFaction side1Faction, IFaction side2Faction)
		{
			if (side1Faction.IsKingdomFaction && side2Faction.IsKingdomFaction)
			{
				this.UpdateKingdomDecisions((Kingdom)side1Faction);
				this.UpdateKingdomDecisions((Kingdom)side2Faction);
			}
		}

		// Token: 0x060040BF RID: 16575 RVA: 0x0012DBB4 File Offset: 0x0012BDB4
		private KingdomDecision GetRandomStartingAllianceDecision(Clan clan)
		{
			Kingdom kingdom = clan.Kingdom;
			KingdomDecision kingdomDecision = null;
			if (kingdom.UnresolvedDecisions.AnyQ((KingdomDecision x) => x is StartAllianceDecision) || clan.Influence < (float)Campaign.Current.Models.AllianceModel.GetInfluenceCostOfProposingStartingAlliance(clan))
			{
				return null;
			}
			Kingdom randomElementWithPredicate = Kingdom.All.GetRandomElementWithPredicate((Kingdom x) => !x.IsEliminated && x != kingdom);
			if (randomElementWithPredicate != null)
			{
				kingdomDecision = new StartAllianceDecision(clan, randomElementWithPredicate);
				TextObject textObject;
				if (!kingdomDecision.CanMakeDecision(out textObject))
				{
					kingdomDecision = null;
				}
			}
			return kingdomDecision;
		}

		// Token: 0x060040C0 RID: 16576 RVA: 0x0012DC58 File Offset: 0x0012BE58
		private KingdomDecision GetRandomWarDecision(Clan clan)
		{
			KingdomDecision result = null;
			Kingdom kingdom = clan.Kingdom;
			if (kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision x) => x is DeclareWarDecision) != null)
			{
				return null;
			}
			Kingdom randomElementWithPredicate = Kingdom.All.GetRandomElementWithPredicate((Kingdom x) => !x.IsEliminated && x != kingdom && !x.IsAtWarWith(kingdom) && x.GetStanceWith(kingdom).PeaceDeclarationDate.ElapsedDaysUntilNow > 20f);
			if (randomElementWithPredicate != null)
			{
				if ((float)new DeclareWarBarterable(kingdom, randomElementWithPredicate).GetValueForFaction(clan) < Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(randomElementWithPredicate))
				{
					return null;
				}
				if (this.ConsiderWar(clan, kingdom, randomElementWithPredicate))
				{
					result = new DeclareWarDecision(clan, randomElementWithPredicate);
				}
			}
			return result;
		}

		// Token: 0x060040C1 RID: 16577 RVA: 0x0012DD0C File Offset: 0x0012BF0C
		private static KingdomDecision GetRandomPeaceDecision(Clan clan)
		{
			KingdomDecision result = null;
			Kingdom kingdom = clan.Kingdom;
			if (kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision x) => x is MakePeaceKingdomDecision) != null)
			{
				return null;
			}
			IAllianceCampaignBehavior allianceCampaignBehavior = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
			Kingdom randomElementWithPredicate = Kingdom.All.GetRandomElementWithPredicate(delegate(Kingdom x)
			{
				if (x.IsAtWarWith(kingdom) && !x.IsAtConstantWarWith(kingdom))
				{
					IAllianceCampaignBehavior allianceCampaignBehavior = allianceCampaignBehavior;
					if (allianceCampaignBehavior == null || !allianceCampaignBehavior.IsAtWarByCallToWarAgreement(kingdom, x))
					{
						IAllianceCampaignBehavior allianceCampaignBehavior2 = allianceCampaignBehavior;
						return allianceCampaignBehavior2 == null || !allianceCampaignBehavior2.IsAtWarByCallToWarAgreement(x, kingdom);
					}
				}
				return false;
			});
			MakePeaceKingdomDecision makePeaceKingdomDecision;
			if (randomElementWithPredicate != null && KingdomDecisionProposalBehavior.ConsiderPeace(clan, randomElementWithPredicate.RulingClan, randomElementWithPredicate, out makePeaceKingdomDecision))
			{
				result = makePeaceKingdomDecision;
			}
			return result;
		}

		// Token: 0x060040C2 RID: 16578 RVA: 0x0012DDA0 File Offset: 0x0012BFA0
		private bool ConsiderWar(Clan clan, Kingdom kingdom, IFaction otherFaction)
		{
			int num = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(clan) / 2;
			if (clan.Influence < (float)num)
			{
				return false;
			}
			DeclareWarDecision declareWarDecision = new DeclareWarDecision(clan, otherFaction);
			if (declareWarDecision.CalculateSupport(clan) > 50f)
			{
				KingdomElection kingdomElection = new KingdomElection(declareWarDecision);
				float num2 = 0f;
				using (List<DecisionOutcome>.Enumerator enumerator = kingdomElection.PossibleOutcomes.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						DeclareWarDecision.DeclareWarDecisionOutcome declareWarDecisionOutcome;
						if ((declareWarDecisionOutcome = (enumerator.Current as DeclareWarDecision.DeclareWarDecisionOutcome)) != null && declareWarDecisionOutcome.ShouldWarBeDeclared)
						{
							num2 = declareWarDecisionOutcome.Likelihood;
							break;
						}
					}
				}
				if (MBRandom.RandomFloat < 1.4f * num2 - 0.55f)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x060040C3 RID: 16579 RVA: 0x0012DE64 File Offset: 0x0012C064
		private float GetKingdomSupportForWar(Clan clan, Kingdom kingdom, IFaction otherFaction)
		{
			return new KingdomElection(new DeclareWarDecision(clan, otherFaction)).GetLikelihoodForSponsor(clan);
		}

		// Token: 0x060040C4 RID: 16580 RVA: 0x0012DE78 File Offset: 0x0012C078
		private static bool ConsiderPeace(Clan clan, Clan otherClan, IFaction otherFaction, out MakePeaceKingdomDecision decision)
		{
			if (!Campaign.Current.Models.DiplomacyModel.IsPeaceSuitable(clan.MapFaction, otherFaction))
			{
				decision = null;
				return false;
			}
			if (Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringPeace(clan.MapFaction, otherFaction) < Campaign.Current.Models.DiplomacyModel.GetDecisionMakingThreshold(clan.Kingdom))
			{
				decision = null;
				return false;
			}
			int dailyTributeDurationInDays;
			int dailyTributeToPay = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(clan, otherClan, out dailyTributeDurationInDays);
			if (dailyTributeToPay < 0)
			{
				decision = null;
				return false;
			}
			MakePeaceKingdomDecision makePeaceKingdomDecision = new MakePeaceKingdomDecision(clan, otherFaction, dailyTributeToPay, dailyTributeDurationInDays, true, false);
			DecisionOutcome possibleOutcome = makePeaceKingdomDecision.DetermineInitialCandidates().First(delegate(DecisionOutcome x)
			{
				MakePeaceKingdomDecision.MakePeaceDecisionOutcome makePeaceDecisionOutcome;
				return (makePeaceDecisionOutcome = (x as MakePeaceKingdomDecision.MakePeaceDecisionOutcome)) != null && makePeaceDecisionOutcome.ShouldPeaceBeDeclared;
			});
			if (makePeaceKingdomDecision.DetermineSupport(clan, possibleOutcome) <= 0f)
			{
				decision = null;
				return false;
			}
			decision = makePeaceKingdomDecision;
			return true;
		}

		// Token: 0x060040C5 RID: 16581 RVA: 0x0012DF54 File Offset: 0x0012C154
		private KingdomDecision GetRandomPolicyDecision(Clan clan)
		{
			KingdomDecision result = null;
			Kingdom kingdom = clan.Kingdom;
			if (kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision x) => x is KingdomPolicyDecision) != null)
			{
				return null;
			}
			if (clan.Influence < 200f)
			{
				return null;
			}
			PolicyObject randomElement = PolicyObject.All.GetRandomElement<PolicyObject>();
			bool flag = kingdom.ActivePolicies.Contains(randomElement);
			if (this.ConsiderPolicy(clan, kingdom, randomElement, flag))
			{
				result = new KingdomPolicyDecision(clan, randomElement, flag);
			}
			return result;
		}

		// Token: 0x060040C6 RID: 16582 RVA: 0x0012DFD8 File Offset: 0x0012C1D8
		private bool ConsiderPolicy(Clan clan, Kingdom kingdom, PolicyObject policy, bool invert)
		{
			int influenceCostOfPolicyProposalAndDisavowal = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(clan);
			if (clan.Influence < (float)influenceCostOfPolicyProposalAndDisavowal)
			{
				return false;
			}
			KingdomPolicyDecision kingdomPolicyDecision = new KingdomPolicyDecision(clan, policy, invert);
			if (kingdomPolicyDecision.CalculateSupport(clan) > 50f)
			{
				KingdomElection kingdomElection = new KingdomElection(kingdomPolicyDecision);
				float num = 0f;
				using (List<DecisionOutcome>.Enumerator enumerator = kingdomElection.PossibleOutcomes.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						KingdomPolicyDecision.PolicyDecisionOutcome policyDecisionOutcome;
						if ((policyDecisionOutcome = (enumerator.Current as KingdomPolicyDecision.PolicyDecisionOutcome)) != null && policyDecisionOutcome.ShouldDecisionBeEnforced)
						{
							num = policyDecisionOutcome.Likelihood;
							break;
						}
					}
				}
				if ((double)MBRandom.RandomFloat < (double)num - 0.55)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x060040C7 RID: 16583 RVA: 0x0012E09C File Offset: 0x0012C29C
		private float GetKingdomSupportForPolicy(Clan clan, Kingdom kingdom, PolicyObject policy, bool invert)
		{
			Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(clan);
			return new KingdomElection(new KingdomPolicyDecision(clan, policy, invert)).GetLikelihoodForSponsor(clan);
		}

		// Token: 0x060040C8 RID: 16584 RVA: 0x0012E0C8 File Offset: 0x0012C2C8
		private KingdomDecision GetRandomAnnexationDecision(Clan clan)
		{
			KingdomDecision result = null;
			Kingdom kingdom = clan.Kingdom;
			if (kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision x) => x is KingdomPolicyDecision) != null)
			{
				return null;
			}
			if (clan.Influence < 300f)
			{
				return null;
			}
			Clan randomElement = kingdom.Clans.GetRandomElement<Clan>();
			if (randomElement != null && randomElement != clan && randomElement.GetRelationWithClan(clan) < -25)
			{
				if (randomElement.Fiefs.Count == 0)
				{
					return null;
				}
				Town randomElement2 = randomElement.Fiefs.GetRandomElement<Town>();
				if (this.ConsiderAnnex(clan, randomElement2))
				{
					result = new SettlementClaimantPreliminaryDecision(clan, randomElement2.Settlement);
				}
			}
			return result;
		}

		// Token: 0x060040C9 RID: 16585 RVA: 0x0012E16C File Offset: 0x0012C36C
		private bool ConsiderAnnex(Clan clan, Town targetSettlement)
		{
			int influenceCostOfAnnexation = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfAnnexation(clan);
			if (clan.Influence < (float)influenceCostOfAnnexation)
			{
				return false;
			}
			SettlementClaimantPreliminaryDecision settlementClaimantPreliminaryDecision = new SettlementClaimantPreliminaryDecision(clan, targetSettlement.Settlement);
			if (settlementClaimantPreliminaryDecision.CalculateSupport(clan) > 50f)
			{
				float num = 0f;
				using (List<DecisionOutcome>.Enumerator enumerator = new KingdomElection(settlementClaimantPreliminaryDecision).PossibleOutcomes.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						SettlementClaimantPreliminaryDecision.SettlementClaimantPreliminaryOutcome settlementClaimantPreliminaryOutcome;
						if ((settlementClaimantPreliminaryOutcome = (enumerator.Current as SettlementClaimantPreliminaryDecision.SettlementClaimantPreliminaryOutcome)) != null && settlementClaimantPreliminaryOutcome.ShouldSettlementOwnerChange)
						{
							num = settlementClaimantPreliminaryOutcome.Likelihood;
							break;
						}
					}
				}
				if ((double)MBRandom.RandomFloat < (double)num - 0.6)
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x060040CA RID: 16586 RVA: 0x0012E234 File Offset: 0x0012C434
		private KingdomDecision GetRandomTradeAgreementDecision(Clan clan)
		{
			KingdomDecision result = null;
			Kingdom kingdom = clan.Kingdom;
			if (kingdom.UnresolvedDecisions.FirstOrDefault((KingdomDecision x) => x is TradeAgreementDecision) != null || clan.Influence < (float)Campaign.Current.Models.TradeAgreementModel.GetInfluenceCostOfProposingTradeAgreement(clan))
			{
				return null;
			}
			Kingdom randomElementWithPredicate = Kingdom.All.GetRandomElementWithPredicate((Kingdom x) => x != kingdom);
			if (randomElementWithPredicate != null && this.ConsiderTradeAgreement(clan, kingdom, randomElementWithPredicate))
			{
				result = new TradeAgreementDecision(clan, randomElementWithPredicate);
			}
			return result;
		}

		// Token: 0x060040CB RID: 16587 RVA: 0x0012E2DC File Offset: 0x0012C4DC
		private bool ConsiderTradeAgreement(Clan clan, Kingdom kingdom, Kingdom otherKingdom)
		{
			TextObject textObject;
			if (Campaign.Current.Models.TradeAgreementModel.CanMakeTradeAgreement(kingdom, otherKingdom, Clan.PlayerClan.Kingdom != otherKingdom, out textObject))
			{
				TradeAgreementDecision tradeAgreementDecision = new TradeAgreementDecision(clan, otherKingdom);
				if (tradeAgreementDecision.CalculateSupport(clan) > 50f)
				{
					KingdomElection kingdomElection = new KingdomElection(tradeAgreementDecision);
					float num = 0f;
					using (List<DecisionOutcome>.Enumerator enumerator = kingdomElection.PossibleOutcomes.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							TradeAgreementDecision.TradeAgreementDecisionOutcome tradeAgreementDecisionOutcome;
							if ((tradeAgreementDecisionOutcome = (enumerator.Current as TradeAgreementDecision.TradeAgreementDecisionOutcome)) != null && tradeAgreementDecisionOutcome.ShouldTradeAgreementStart)
							{
								num = tradeAgreementDecisionOutcome.Likelihood;
								break;
							}
						}
					}
					if (MBRandom.RandomFloat < num)
					{
						return true;
					}
				}
			}
			return false;
		}

		// Token: 0x060040CC RID: 16588 RVA: 0x0012E39C File Offset: 0x0012C59C
		public override void SyncData(IDataStore dataStore)
		{
			dataStore.SyncData<List<KingdomDecision>>("_kingdomDecisionsList", ref this._kingdomDecisionsList);
			if (dataStore.IsLoading && MBSaveLoad.LastLoadedGameVersion.IsOlderThan(ApplicationVersion.FromString("v1.3.0", 0)) && this._kingdomDecisionsList == null)
			{
				this._kingdomDecisionsList = new List<KingdomDecision>();
			}
		}

		// Token: 0x060040CD RID: 16589 RVA: 0x0012E3F0 File Offset: 0x0012C5F0
		private void OnClanChangedKingdom(Clan clan, Kingdom oldKingdom, Kingdom newKingdom, ChangeKingdomAction.ChangeKingdomActionDetail detail, bool showNotification = true)
		{
			if (clan == Clan.PlayerClan && oldKingdom != null && detail != ChangeKingdomAction.ChangeKingdomActionDetail.LeaveByKingdomDestruction)
			{
				this.UpdateKingdomDecisions(oldKingdom);
			}
		}

		// Token: 0x060040CE RID: 16590 RVA: 0x0012E409 File Offset: 0x0012C609
		private void OnKingdomDecisionAdded(KingdomDecision decision, bool isPlayerInvolved)
		{
			this._kingdomDecisionsList.Add(decision);
		}

		// Token: 0x040012BE RID: 4798
		private const float DaysBetweenSameProposal = 5f;

		// Token: 0x040012BF RID: 4799
		private List<KingdomDecision> _kingdomDecisionsList = new List<KingdomDecision>();

		// Token: 0x040012C0 RID: 4800
		private ITradeAgreementsCampaignBehavior _tradeAgreementsBehavior;

		// Token: 0x02000803 RID: 2051
		// (Invoke) Token: 0x06006515 RID: 25877
		private delegate KingdomDecision KingdomDecisionCreatorDelegate(Clan sponsorClan);
	}
}
