using System;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Diplomacy
{
	// Token: 0x0200006F RID: 111
	public class KingdomDiplomacyVM : KingdomCategoryVM
	{
		// Token: 0x060008D6 RID: 2262 RVA: 0x000275C8 File Offset: 0x000257C8
		public KingdomDiplomacyVM(Action<KingdomDecision> forceDecision)
		{
			this._forceDecision = forceDecision;
			this._playerKingdom = (Hero.MainHero.MapFaction as Kingdom);
			this.PlayerWars = new MBBindingList<KingdomWarItemVM>();
			this.PlayerTruces = new MBBindingList<KingdomTruceItemVM>();
			this.WarsSortController = new KingdomWarSortControllerVM(ref this._playerWars);
			this.Actions = new MBBindingList<KingdomDiplomacyProposalActionItemVM>();
			this.ExecuteShowStatComparisons();
			this.RefreshValues();
			this.SetDefaultSelectedItem();
		}

		// Token: 0x060008D7 RID: 2263 RVA: 0x0002763C File Offset: 0x0002583C
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.BehaviorSelection = new SelectorVM<SelectorItemVM>(0, new Action<SelectorVM<SelectorItemVM>>(this.OnBehaviorSelectionChanged));
			this.BehaviorSelection.AddItem(new SelectorItemVM(GameTexts.FindText("str_kingdom_war_strategy_balanced", null), GameTexts.FindText("str_kingdom_war_strategy_balanced_desc", null)));
			this.BehaviorSelection.AddItem(new SelectorItemVM(GameTexts.FindText("str_kingdom_war_strategy_defensive", null), GameTexts.FindText("str_kingdom_war_strategy_defensive_desc", null)));
			this.BehaviorSelection.AddItem(new SelectorItemVM(GameTexts.FindText("str_kingdom_war_strategy_offensive", null), GameTexts.FindText("str_kingdom_war_strategy_offensive_desc", null)));
			this.RefreshDiplomacyList();
			this.BehaviorSelectionTitle = GameTexts.FindText("str_kingdom_war_strategy", null).ToString();
			base.NoItemSelectedText = GameTexts.FindText("str_kingdom_no_war_selected", null).ToString();
			this.PlayerWarsText = GameTexts.FindText("str_kingdom_at_war", null).ToString();
			this.PlayerTrucesText = GameTexts.FindText("str_kingdom_at_peace", null).ToString();
			this.WarsText = GameTexts.FindText("str_diplomatic_group", null).ToString();
			this.ShowStatBarsHint = new HintViewModel(GameTexts.FindText("str_kingdom_war_show_comparison_bars", null), null);
			this.ShowWarLogsHint = new HintViewModel(GameTexts.FindText("str_kingdom_war_show_war_logs", null), null);
			this.PlayerWars.ApplyActionOnAllItems(delegate(KingdomWarItemVM x)
			{
				x.RefreshValues();
			});
			this.PlayerTruces.ApplyActionOnAllItems(delegate(KingdomTruceItemVM x)
			{
				x.RefreshValues();
			});
			KingdomDiplomacyItemVM currentSelectedDiplomacyItem = this.CurrentSelectedDiplomacyItem;
			if (currentSelectedDiplomacyItem != null)
			{
				currentSelectedDiplomacyItem.RefreshValues();
			}
			this.Actions.ApplyActionOnAllItems(delegate(KingdomDiplomacyProposalActionItemVM x)
			{
				x.RefreshValues();
			});
		}

		// Token: 0x060008D8 RID: 2264 RVA: 0x0002780C File Offset: 0x00025A0C
		public void RefreshDiplomacyList()
		{
			Kingdom kingdom = Clan.PlayerClan.Kingdom;
			int notificationCount;
			if (kingdom == null)
			{
				notificationCount = 0;
			}
			else
			{
				notificationCount = kingdom.UnresolvedDecisions.Count((KingdomDecision d) => !d.ShouldBeCancelled());
			}
			base.NotificationCount = notificationCount;
			this.PlayerWars.Clear();
			this.PlayerTruces.Clear();
			foreach (StanceLink stanceLink in from x in this._playerKingdom.FactionsAtWarWith
			select this._playerKingdom.GetStanceWith(x) into w
			orderby w.Faction1.Name.ToString() + w.Faction2.Name.ToString()
			select w)
			{
				if (stanceLink.Faction1.IsKingdomFaction && stanceLink.Faction2.IsKingdomFaction)
				{
					this.PlayerWars.Add(new KingdomWarItemVM(stanceLink, new Action<KingdomWarItemVM>(this.OnDiplomacyItemSelection)));
				}
			}
			foreach (Kingdom kingdom2 in Kingdom.All)
			{
				if (kingdom2 != this._playerKingdom && !kingdom2.IsEliminated && (DiplomacyHelper.IsSameFactionAndNotEliminated(kingdom2, this._playerKingdom) || FactionManager.IsNeutralWithFaction(kingdom2, this._playerKingdom)))
				{
					this.PlayerTruces.Add(new KingdomTruceItemVM(this._playerKingdom, kingdom2, new Action<KingdomDiplomacyItemVM>(this.OnDiplomacyItemSelection)));
				}
			}
			GameTexts.SetVariable("STR", this.PlayerWars.Count);
			this.NumOfPlayerWarsText = GameTexts.FindText("str_STR_in_parentheses", null).ToString();
			GameTexts.SetVariable("STR", this.PlayerTruces.Count);
			this.NumOfPlayerTrucesText = GameTexts.FindText("str_STR_in_parentheses", null).ToString();
			this.SetDefaultSelectedItem();
		}

		// Token: 0x060008D9 RID: 2265 RVA: 0x00027A00 File Offset: 0x00025C00
		public void SelectKingdom(Kingdom kingdom)
		{
			bool flag = false;
			foreach (KingdomWarItemVM kingdomWarItemVM in this.PlayerWars)
			{
				if (kingdomWarItemVM.Faction1 == kingdom || kingdomWarItemVM.Faction2 == kingdom)
				{
					this.OnSetCurrentDiplomacyItem(kingdomWarItemVM);
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				foreach (KingdomTruceItemVM kingdomTruceItemVM in this.PlayerTruces)
				{
					if (kingdomTruceItemVM.Faction1 == kingdom || kingdomTruceItemVM.Faction2 == kingdom)
					{
						this.OnSetCurrentDiplomacyItem(kingdomTruceItemVM);
						flag = true;
						break;
					}
				}
			}
		}

		// Token: 0x060008DA RID: 2266 RVA: 0x00027AC0 File Offset: 0x00025CC0
		private void OnSetCurrentDiplomacyItem(KingdomDiplomacyItemVM item)
		{
			this.Actions.Clear();
			if (item is KingdomWarItemVM)
			{
				this.OnSetWarItem(item as KingdomWarItemVM);
			}
			else if (item is KingdomTruceItemVM)
			{
				this.OnSetPeaceItem(item as KingdomTruceItemVM);
			}
			this.RefreshCurrentWarVisuals(item);
			this.UpdateBehaviorSelection();
		}

		// Token: 0x060008DB RID: 2267 RVA: 0x00027B10 File Offset: 0x00025D10
		private void OnSetWarItem(KingdomWarItemVM item)
		{
			KingdomDiplomacyVM.<>c__DisplayClass8_0 CS$<>8__locals1 = new KingdomDiplomacyVM.<>c__DisplayClass8_0();
			CS$<>8__locals1.item = item;
			CS$<>8__locals1.<>4__this = this;
			CS$<>8__locals1.unresolvedPeaceDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault(delegate(KingdomDecision d)
			{
				MakePeaceKingdomDecision makePeaceKingdomDecision;
				return (makePeaceKingdomDecision = (d as MakePeaceKingdomDecision)) != null && makePeaceKingdomDecision.FactionToMakePeaceWith == CS$<>8__locals1.item.Faction2 && !d.ShouldBeCancelled();
			});
			if (CS$<>8__locals1.unresolvedPeaceDecision != null)
			{
				TextObject hintText;
				this.Actions.Add(new KingdomDiplomacyProposalActionItemVM(GameTexts.FindText("str_resolve", null), GameTexts.FindText("str_resolve_explanation", null), 0, this.GetAreProposalActionsEnabledWithReason(0f, out hintText), hintText, delegate()
				{
					CS$<>8__locals1.<>4__this._forceDecision(CS$<>8__locals1.unresolvedPeaceDecision);
				}));
				return;
			}
			int durationInDays;
			int dailyPeaceTributeToPay = Campaign.Current.Models.DiplomacyModel.GetDailyTributeToPay(Clan.PlayerClan, CS$<>8__locals1.item.Faction2.Leader.Clan, out durationInDays);
			dailyPeaceTributeToPay = 10 * (dailyPeaceTributeToPay / 10);
			TextObject textObject = (dailyPeaceTributeToPay == 0) ? GameTexts.FindText("str_propose_peace_explanation", null) : ((dailyPeaceTributeToPay > 0) ? GameTexts.FindText("str_propose_peace_explanation_pay_tribute", null) : GameTexts.FindText("str_propose_peace_explanation_get_tribute", null));
			textObject.SetTextVariable("SUPPORT", this.CalculatePeaceSupport(CS$<>8__locals1.item.Faction2, dailyPeaceTributeToPay, durationInDays)).SetTextVariable("TRIBUTE_AMOUNT", MathF.Abs(dailyPeaceTributeToPay)).SetTextVariable("TRIBUTE_DURATION", durationInDays);
			int influenceCostOfProposingPeace = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingPeace(Clan.PlayerClan);
			TextObject hintText2;
			this.Actions.Add(new KingdomDiplomacyProposalActionItemVM((this._playerKingdom.Clans.Count > 1) ? GameTexts.FindText("str_policy_propose", null) : GameTexts.FindText("str_policy_enact", null), textObject, influenceCostOfProposingPeace, this.GetIsProposingPeaceEnabledWithReason(CS$<>8__locals1.item, (float)influenceCostOfProposingPeace, out hintText2), hintText2, delegate()
			{
				CS$<>8__locals1.<>4__this.OnDeclarePeace(CS$<>8__locals1.item, dailyPeaceTributeToPay, durationInDays);
			}));
		}

		// Token: 0x060008DC RID: 2268 RVA: 0x00027D08 File Offset: 0x00025F08
		private void OnSetPeaceItem(KingdomTruceItemVM item)
		{
			KingdomDecision unresolvedAllianceDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault(delegate(KingdomDecision d)
			{
				StartAllianceDecision startAllianceDecision;
				return (startAllianceDecision = (d as StartAllianceDecision)) != null && startAllianceDecision.KingdomToStartAllianceWith == item.Faction2 && !d.ShouldBeCancelled();
			});
			if (unresolvedAllianceDecision != null)
			{
				TextObject hintText;
				this.Actions.Add(new KingdomDiplomacyProposalActionItemVM(GameTexts.FindText("str_resolve", null), GameTexts.FindText("str_resolve_explanation", null), 0, this.GetAreProposalActionsEnabledWithReason(0f, out hintText), hintText, delegate()
				{
					this._forceDecision(unresolvedAllianceDecision);
				}));
			}
			else if (!Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>().IsAllyWithKingdom(item.Faction1 as Kingdom, item.Faction2 as Kingdom))
			{
				int influenceCostOfProposingStartingAlliance = Campaign.Current.Models.AllianceModel.GetInfluenceCostOfProposingStartingAlliance(Clan.PlayerClan);
				TextObject hintText2;
				this.Actions.Add(new KingdomDiplomacyProposalActionItemVM((this._playerKingdom.Clans.Count > 1) ? GameTexts.FindText("str_policy_propose", null) : GameTexts.FindText("str_policy_enact", null), GameTexts.FindText("str_propose_alliance_explanation", null).SetTextVariable("SUPPORT", this.CalculateAllianceSupport(item.Faction2)), influenceCostOfProposingStartingAlliance, this.GetIsProposingAllianceEnabledWithReason(item, (float)influenceCostOfProposingStartingAlliance, out hintText2), hintText2, delegate()
				{
					this.OnStartAlliance(item);
				}));
			}
			KingdomDecision unresolvedWarDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault(delegate(KingdomDecision d)
			{
				DeclareWarDecision declareWarDecision;
				return (declareWarDecision = (d as DeclareWarDecision)) != null && declareWarDecision.FactionToDeclareWarOn == item.Faction2 && !d.ShouldBeCancelled();
			});
			if (unresolvedWarDecision != null)
			{
				TextObject hintText3;
				this.Actions.Add(new KingdomDiplomacyProposalActionItemVM(GameTexts.FindText("str_resolve", null), GameTexts.FindText("str_resolve_explanation", null), 0, this.GetAreProposalActionsEnabledWithReason(0f, out hintText3), hintText3, delegate()
				{
					this._forceDecision(unresolvedWarDecision);
				}));
			}
			else
			{
				int influenceCostOfProposingWar = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(Clan.PlayerClan);
				TextObject hintText4;
				this.Actions.Add(new KingdomDiplomacyProposalActionItemVM((this._playerKingdom.Clans.Count > 1) ? GameTexts.FindText("str_policy_propose", null) : GameTexts.FindText("str_policy_enact", null), GameTexts.FindText("str_propose_war_explanation", null).SetTextVariable("SUPPORT", this.CalculateWarSupport(item.Faction2)), influenceCostOfProposingWar, this.GetIsProposingWarEnabledWithReason(item, (float)influenceCostOfProposingWar, out hintText4), hintText4, delegate()
				{
					this.OnDeclareWar(item);
				}));
			}
			KingdomDecision unresolvedTradeAgreementDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault(delegate(KingdomDecision d)
			{
				TradeAgreementDecision tradeAgreementDecision;
				return (tradeAgreementDecision = (d as TradeAgreementDecision)) != null && tradeAgreementDecision.TargetKingdom == item.Faction2 && !d.ShouldBeCancelled();
			});
			if (unresolvedTradeAgreementDecision != null)
			{
				TextObject hintText5;
				this.Actions.Add(new KingdomDiplomacyProposalActionItemVM(GameTexts.FindText("str_resolve", null), GameTexts.FindText("str_resolve_explanation", null), 0, this.GetAreProposalActionsEnabledWithReason(0f, out hintText5), hintText5, delegate()
				{
					this._forceDecision(unresolvedTradeAgreementDecision);
				}));
				return;
			}
			if (!Campaign.Current.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>().HasTradeAgreement(item.Faction1 as Kingdom, item.Faction2 as Kingdom))
			{
				int influenceCostOfProposingTradeAgreement = Campaign.Current.Models.TradeAgreementModel.GetInfluenceCostOfProposingTradeAgreement(Clan.PlayerClan);
				TextObject hintText6;
				this.Actions.Add(new KingdomDiplomacyProposalActionItemVM((this._playerKingdom.Clans.Count > 1) ? GameTexts.FindText("str_policy_propose", null) : GameTexts.FindText("str_policy_enact", null), GameTexts.FindText("str_propose_trade_agreement_explanation", null).SetTextVariable("SUPPORT", this.CalculateTradeAgreementSupport(item.Faction2)), influenceCostOfProposingTradeAgreement, this.GetIsProposingTradeAgreementEnabledWithReason(item, (float)influenceCostOfProposingTradeAgreement, out hintText6), hintText6, delegate()
				{
					this.OnStartTradeAgreement(item);
				}));
			}
		}

		// Token: 0x060008DD RID: 2269 RVA: 0x000280C0 File Offset: 0x000262C0
		private bool GetIsProposingWarEnabledWithReason(KingdomTruceItemVM item, float actionInfluenceCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!this.GetAreProposalActionsEnabledWithReason(actionInfluenceCost, out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			TextObject textObject2;
			if (!Campaign.Current.Models.KingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms(item.Faction1 as Kingdom, item.Faction2 as Kingdom, out textObject2))
			{
				disabledReason = textObject2;
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x060008DE RID: 2270 RVA: 0x00028118 File Offset: 0x00026318
		private bool GetIsProposingPeaceEnabledWithReason(KingdomWarItemVM item, float actionInfluenceCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!this.GetAreProposalActionsEnabledWithReason(actionInfluenceCost, out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			TextObject textObject2;
			if (!Campaign.Current.Models.KingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms(item.Faction1 as Kingdom, item.Faction2 as Kingdom, out textObject2))
			{
				disabledReason = textObject2;
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x060008DF RID: 2271 RVA: 0x00028170 File Offset: 0x00026370
		private bool GetIsProposingAllianceEnabledWithReason(KingdomTruceItemVM item, float actionInfluenceCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!this.GetAreProposalActionsEnabledWithReason(actionInfluenceCost, out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			TextObject textObject2;
			if (!new StartAllianceDecision(Clan.PlayerClan, item.Faction2 as Kingdom).CanMakeDecision(out textObject2))
			{
				disabledReason = textObject2;
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x060008E0 RID: 2272 RVA: 0x000281B8 File Offset: 0x000263B8
		private bool GetIsProposingTradeAgreementEnabledWithReason(KingdomTruceItemVM item, float actionInfluenceCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!this.GetAreProposalActionsEnabledWithReason(actionInfluenceCost, out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			TextObject textObject2;
			if (!new TradeAgreementDecision(Clan.PlayerClan, item.Faction2 as Kingdom).CanMakeDecision(out textObject2))
			{
				disabledReason = textObject2;
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x060008E1 RID: 2273 RVA: 0x00028200 File Offset: 0x00026400
		private bool GetAreProposalActionsEnabledWithReason(float actionInfluenceCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			if (actionInfluenceCost > 0f && Clan.PlayerClan.Influence < actionInfluenceCost)
			{
				disabledReason = GameTexts.FindText("str_warning_you_dont_have_enough_influence", null);
				return false;
			}
			if (Clan.PlayerClan.IsUnderMercenaryService)
			{
				disabledReason = GameTexts.FindText("str_cannot_propose_war_truce_while_mercenary", null);
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x060008E2 RID: 2274 RVA: 0x00028262 File Offset: 0x00026462
		private void RefreshCurrentWarVisuals(KingdomDiplomacyItemVM item)
		{
			if (item != null)
			{
				if (this.CurrentSelectedDiplomacyItem != null)
				{
					this.CurrentSelectedDiplomacyItem.IsSelected = false;
				}
				this.CurrentSelectedDiplomacyItem = item;
				if (this.CurrentSelectedDiplomacyItem != null)
				{
					this.CurrentSelectedDiplomacyItem.IsSelected = true;
				}
			}
		}

		// Token: 0x060008E3 RID: 2275 RVA: 0x00028296 File Offset: 0x00026496
		private void OnDiplomacyItemSelection(KingdomDiplomacyItemVM item)
		{
			if (this.CurrentSelectedDiplomacyItem != item)
			{
				if (this.CurrentSelectedDiplomacyItem != null)
				{
					this.CurrentSelectedDiplomacyItem.IsSelected = false;
				}
				this.CurrentSelectedDiplomacyItem = item;
				base.IsAcceptableItemSelected = (item != null);
				this.OnSetCurrentDiplomacyItem(item);
			}
		}

		// Token: 0x060008E4 RID: 2276 RVA: 0x000282D0 File Offset: 0x000264D0
		private void OnDeclareWar(KingdomTruceItemVM item)
		{
			DeclareWarDecision declareWarDecision = new DeclareWarDecision(Clan.PlayerClan, item.Faction2);
			Clan.PlayerClan.Kingdom.AddDecision(declareWarDecision, false);
			this._forceDecision(declareWarDecision);
		}

		// Token: 0x060008E5 RID: 2277 RVA: 0x0002830C File Offset: 0x0002650C
		private void OnDeclarePeace(KingdomWarItemVM item, int tributeToPay, int tributeDurationInDays)
		{
			MakePeaceKingdomDecision makePeaceKingdomDecision = new MakePeaceKingdomDecision(Clan.PlayerClan, item.Faction2 as Kingdom, tributeToPay, tributeDurationInDays, true, false);
			Clan.PlayerClan.Kingdom.AddDecision(makePeaceKingdomDecision, false);
			this._forceDecision(makePeaceKingdomDecision);
		}

		// Token: 0x060008E6 RID: 2278 RVA: 0x00028350 File Offset: 0x00026550
		private void OnStartAlliance(KingdomTruceItemVM item)
		{
			if (item.Faction2.IsKingdomFaction)
			{
				StartAllianceDecision startAllianceDecision = new StartAllianceDecision(Clan.PlayerClan, (Kingdom)item.Faction2);
				Clan.PlayerClan.Kingdom.AddDecision(startAllianceDecision, false);
				this._forceDecision(startAllianceDecision);
			}
		}

		// Token: 0x060008E7 RID: 2279 RVA: 0x000283A0 File Offset: 0x000265A0
		private void OnStartTradeAgreement(KingdomTruceItemVM item)
		{
			if (item.Faction2.IsKingdomFaction)
			{
				TradeAgreementDecision tradeAgreementDecision = new TradeAgreementDecision(Clan.PlayerClan, (Kingdom)item.Faction2);
				Clan.PlayerClan.Kingdom.AddDecision(tradeAgreementDecision, false);
				this._forceDecision(tradeAgreementDecision);
			}
		}

		// Token: 0x060008E8 RID: 2280 RVA: 0x000283ED File Offset: 0x000265ED
		private void ExecuteShowWarLogs()
		{
			this.IsDisplayingWarLogs = true;
			this.IsDisplayingStatComparisons = false;
		}

		// Token: 0x060008E9 RID: 2281 RVA: 0x000283FD File Offset: 0x000265FD
		private void ExecuteShowStatComparisons()
		{
			this.IsDisplayingWarLogs = false;
			this.IsDisplayingStatComparisons = true;
		}

		// Token: 0x060008EA RID: 2282 RVA: 0x00028410 File Offset: 0x00026610
		private void SetDefaultSelectedItem()
		{
			KingdomDiplomacyItemVM kingdomDiplomacyItemVM = this.PlayerWars.FirstOrDefault<KingdomWarItemVM>();
			KingdomDiplomacyItemVM kingdomDiplomacyItemVM2 = this.PlayerTruces.FirstOrDefault<KingdomTruceItemVM>();
			this.OnDiplomacyItemSelection(kingdomDiplomacyItemVM ?? kingdomDiplomacyItemVM2);
		}

		// Token: 0x060008EB RID: 2283 RVA: 0x00028444 File Offset: 0x00026644
		private void UpdateBehaviorSelection()
		{
			if (Hero.MainHero.MapFaction.IsKingdomFaction && Hero.MainHero.MapFaction.Leader == Hero.MainHero && this.CurrentSelectedDiplomacyItem != null)
			{
				StanceLink stanceWith = Hero.MainHero.MapFaction.GetStanceWith(this.CurrentSelectedDiplomacyItem.Faction2);
				this.BehaviorSelection.SelectedIndex = stanceWith.BehaviorPriority;
			}
		}

		// Token: 0x060008EC RID: 2284 RVA: 0x000284AC File Offset: 0x000266AC
		private void OnBehaviorSelectionChanged(SelectorVM<SelectorItemVM> s)
		{
			if (!this._isChangingDiplomacyItem && Hero.MainHero.MapFaction.IsKingdomFaction && Hero.MainHero.MapFaction.Leader == Hero.MainHero && this.CurrentSelectedDiplomacyItem != null)
			{
				Hero.MainHero.MapFaction.GetStanceWith(this.CurrentSelectedDiplomacyItem.Faction2).BehaviorPriority = s.SelectedIndex;
			}
		}

		// Token: 0x060008ED RID: 2285 RVA: 0x00028515 File Offset: 0x00026715
		private int CalculateWarSupport(IFaction faction)
		{
			return MathF.Round(new KingdomElection(new DeclareWarDecision(Clan.PlayerClan, faction)).GetLikelihoodForSponsor(Clan.PlayerClan) * 100f);
		}

		// Token: 0x060008EE RID: 2286 RVA: 0x0002853C File Offset: 0x0002673C
		private int CalculateAllianceSupport(IFaction faction)
		{
			return MathF.Round(new KingdomElection(new StartAllianceDecision(Clan.PlayerClan, faction as Kingdom)).GetLikelihoodForSponsor(Clan.PlayerClan) * 100f);
		}

		// Token: 0x060008EF RID: 2287 RVA: 0x00028568 File Offset: 0x00026768
		private int CalculatePeaceSupport(IFaction faction, int dailyTributeToBePaid, int durationInDays)
		{
			return MathF.Round(new KingdomElection(new MakePeaceKingdomDecision(Clan.PlayerClan, faction, dailyTributeToBePaid, durationInDays, true, false)).GetLikelihoodForSponsor(Clan.PlayerClan) * 100f);
		}

		// Token: 0x060008F0 RID: 2288 RVA: 0x00028593 File Offset: 0x00026793
		private int CalculateTradeAgreementSupport(IFaction faction)
		{
			return MathF.Round(new KingdomElection(new TradeAgreementDecision(Clan.PlayerClan, faction as Kingdom)).GetLikelihoodForSponsor(Clan.PlayerClan) * 100f);
		}

		// Token: 0x1700029A RID: 666
		// (get) Token: 0x060008F1 RID: 2289 RVA: 0x000285BF File Offset: 0x000267BF
		// (set) Token: 0x060008F2 RID: 2290 RVA: 0x000285C7 File Offset: 0x000267C7
		[DataSourceProperty]
		public MBBindingList<KingdomWarItemVM> PlayerWars
		{
			get
			{
				return this._playerWars;
			}
			set
			{
				if (value != this._playerWars)
				{
					this._playerWars = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomWarItemVM>>(value, "PlayerWars");
				}
			}
		}

		// Token: 0x1700029B RID: 667
		// (get) Token: 0x060008F3 RID: 2291 RVA: 0x000285E5 File Offset: 0x000267E5
		// (set) Token: 0x060008F4 RID: 2292 RVA: 0x000285ED File Offset: 0x000267ED
		[DataSourceProperty]
		public bool IsDisplayingWarLogs
		{
			get
			{
				return this._isDisplayingWarLogs;
			}
			set
			{
				if (value != this._isDisplayingWarLogs)
				{
					this._isDisplayingWarLogs = value;
					base.OnPropertyChangedWithValue(value, "IsDisplayingWarLogs");
				}
			}
		}

		// Token: 0x1700029C RID: 668
		// (get) Token: 0x060008F5 RID: 2293 RVA: 0x0002860B File Offset: 0x0002680B
		// (set) Token: 0x060008F6 RID: 2294 RVA: 0x00028613 File Offset: 0x00026813
		[DataSourceProperty]
		public bool IsDisplayingStatComparisons
		{
			get
			{
				return this._isDisplayingStatComparisons;
			}
			set
			{
				if (value != this._isDisplayingStatComparisons)
				{
					this._isDisplayingStatComparisons = value;
					base.OnPropertyChangedWithValue(value, "IsDisplayingStatComparisons");
				}
			}
		}

		// Token: 0x1700029D RID: 669
		// (get) Token: 0x060008F7 RID: 2295 RVA: 0x00028631 File Offset: 0x00026831
		// (set) Token: 0x060008F8 RID: 2296 RVA: 0x00028639 File Offset: 0x00026839
		[DataSourceProperty]
		public bool IsWar
		{
			get
			{
				return this._isWar;
			}
			set
			{
				if (value != this._isWar)
				{
					this._isWar = value;
					if (!value)
					{
						this.ExecuteShowStatComparisons();
					}
					base.OnPropertyChangedWithValue(value, "IsWar");
				}
			}
		}

		// Token: 0x1700029E RID: 670
		// (get) Token: 0x060008F9 RID: 2297 RVA: 0x00028660 File Offset: 0x00026860
		// (set) Token: 0x060008FA RID: 2298 RVA: 0x00028668 File Offset: 0x00026868
		[DataSourceProperty]
		public string BehaviorSelectionTitle
		{
			get
			{
				return this._behaviorSelectionTitle;
			}
			set
			{
				if (value != this._behaviorSelectionTitle)
				{
					this._behaviorSelectionTitle = value;
					base.OnPropertyChangedWithValue<string>(value, "BehaviorSelectionTitle");
				}
			}
		}

		// Token: 0x1700029F RID: 671
		// (get) Token: 0x060008FB RID: 2299 RVA: 0x0002868B File Offset: 0x0002688B
		// (set) Token: 0x060008FC RID: 2300 RVA: 0x00028693 File Offset: 0x00026893
		[DataSourceProperty]
		public MBBindingList<KingdomTruceItemVM> PlayerTruces
		{
			get
			{
				return this._playerTruces;
			}
			set
			{
				if (value != this._playerTruces)
				{
					this._playerTruces = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomTruceItemVM>>(value, "PlayerTruces");
				}
			}
		}

		// Token: 0x170002A0 RID: 672
		// (get) Token: 0x060008FD RID: 2301 RVA: 0x000286B1 File Offset: 0x000268B1
		// (set) Token: 0x060008FE RID: 2302 RVA: 0x000286B9 File Offset: 0x000268B9
		[DataSourceProperty]
		public KingdomDiplomacyItemVM CurrentSelectedDiplomacyItem
		{
			get
			{
				return this._currentSelectedItem;
			}
			set
			{
				if (value != this._currentSelectedItem)
				{
					this._isChangingDiplomacyItem = true;
					this._currentSelectedItem = value;
					this.IsWar = (value is KingdomWarItemVM);
					base.OnPropertyChangedWithValue<KingdomDiplomacyItemVM>(value, "CurrentSelectedDiplomacyItem");
					this._isChangingDiplomacyItem = false;
				}
			}
		}

		// Token: 0x170002A1 RID: 673
		// (get) Token: 0x060008FF RID: 2303 RVA: 0x000286F4 File Offset: 0x000268F4
		// (set) Token: 0x06000900 RID: 2304 RVA: 0x000286FC File Offset: 0x000268FC
		[DataSourceProperty]
		public KingdomWarSortControllerVM WarsSortController
		{
			get
			{
				return this._warsSortController;
			}
			set
			{
				if (value != this._warsSortController)
				{
					this._warsSortController = value;
					base.OnPropertyChangedWithValue<KingdomWarSortControllerVM>(value, "WarsSortController");
				}
			}
		}

		// Token: 0x170002A2 RID: 674
		// (get) Token: 0x06000901 RID: 2305 RVA: 0x0002871A File Offset: 0x0002691A
		// (set) Token: 0x06000902 RID: 2306 RVA: 0x00028722 File Offset: 0x00026922
		[DataSourceProperty]
		public string PlayerWarsText
		{
			get
			{
				return this._playerWarsText;
			}
			set
			{
				if (value != this._playerWarsText)
				{
					this._playerWarsText = value;
					base.OnPropertyChangedWithValue<string>(value, "PlayerWarsText");
				}
			}
		}

		// Token: 0x170002A3 RID: 675
		// (get) Token: 0x06000903 RID: 2307 RVA: 0x00028745 File Offset: 0x00026945
		// (set) Token: 0x06000904 RID: 2308 RVA: 0x0002874D File Offset: 0x0002694D
		[DataSourceProperty]
		public string WarsText
		{
			get
			{
				return this._warsText;
			}
			set
			{
				if (value != this._warsText)
				{
					this._warsText = value;
					base.OnPropertyChangedWithValue<string>(value, "WarsText");
				}
			}
		}

		// Token: 0x170002A4 RID: 676
		// (get) Token: 0x06000905 RID: 2309 RVA: 0x00028770 File Offset: 0x00026970
		// (set) Token: 0x06000906 RID: 2310 RVA: 0x00028778 File Offset: 0x00026978
		[DataSourceProperty]
		public string NumOfPlayerWarsText
		{
			get
			{
				return this._numOfPlayerWarsText;
			}
			set
			{
				if (value != this._numOfPlayerWarsText)
				{
					this._numOfPlayerWarsText = value;
					base.OnPropertyChangedWithValue<string>(value, "NumOfPlayerWarsText");
				}
			}
		}

		// Token: 0x170002A5 RID: 677
		// (get) Token: 0x06000907 RID: 2311 RVA: 0x0002879B File Offset: 0x0002699B
		// (set) Token: 0x06000908 RID: 2312 RVA: 0x000287A3 File Offset: 0x000269A3
		[DataSourceProperty]
		public string PlayerTrucesText
		{
			get
			{
				return this._otherWarsText;
			}
			set
			{
				if (value != this._otherWarsText)
				{
					this._otherWarsText = value;
					base.OnPropertyChangedWithValue<string>(value, "PlayerTrucesText");
				}
			}
		}

		// Token: 0x170002A6 RID: 678
		// (get) Token: 0x06000909 RID: 2313 RVA: 0x000287C6 File Offset: 0x000269C6
		// (set) Token: 0x0600090A RID: 2314 RVA: 0x000287CE File Offset: 0x000269CE
		[DataSourceProperty]
		public string NumOfPlayerTrucesText
		{
			get
			{
				return this._numOfOtherWarsText;
			}
			set
			{
				if (value != this._numOfOtherWarsText)
				{
					this._numOfOtherWarsText = value;
					base.OnPropertyChangedWithValue<string>(value, "NumOfPlayerTrucesText");
				}
			}
		}

		// Token: 0x170002A7 RID: 679
		// (get) Token: 0x0600090B RID: 2315 RVA: 0x000287F1 File Offset: 0x000269F1
		// (set) Token: 0x0600090C RID: 2316 RVA: 0x000287F9 File Offset: 0x000269F9
		[DataSourceProperty]
		public SelectorVM<SelectorItemVM> BehaviorSelection
		{
			get
			{
				return this._behaviorSelection;
			}
			set
			{
				if (value != this._behaviorSelection)
				{
					this._behaviorSelection = value;
					base.OnPropertyChangedWithValue<SelectorVM<SelectorItemVM>>(value, "BehaviorSelection");
				}
			}
		}

		// Token: 0x170002A8 RID: 680
		// (get) Token: 0x0600090D RID: 2317 RVA: 0x00028817 File Offset: 0x00026A17
		// (set) Token: 0x0600090E RID: 2318 RVA: 0x0002881F File Offset: 0x00026A1F
		[DataSourceProperty]
		public HintViewModel ShowStatBarsHint
		{
			get
			{
				return this._showStatBarsHint;
			}
			set
			{
				if (value != this._showStatBarsHint)
				{
					this._showStatBarsHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "ShowStatBarsHint");
				}
			}
		}

		// Token: 0x170002A9 RID: 681
		// (get) Token: 0x0600090F RID: 2319 RVA: 0x0002883D File Offset: 0x00026A3D
		// (set) Token: 0x06000910 RID: 2320 RVA: 0x00028845 File Offset: 0x00026A45
		[DataSourceProperty]
		public HintViewModel ShowWarLogsHint
		{
			get
			{
				return this._showWarLogsHint;
			}
			set
			{
				if (value != this._showWarLogsHint)
				{
					this._showWarLogsHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "ShowWarLogsHint");
				}
			}
		}

		// Token: 0x170002AA RID: 682
		// (get) Token: 0x06000911 RID: 2321 RVA: 0x00028863 File Offset: 0x00026A63
		// (set) Token: 0x06000912 RID: 2322 RVA: 0x0002886B File Offset: 0x00026A6B
		[DataSourceProperty]
		public MBBindingList<KingdomDiplomacyProposalActionItemVM> Actions
		{
			get
			{
				return this._actions;
			}
			set
			{
				if (value != this._actions)
				{
					this._actions = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomDiplomacyProposalActionItemVM>>(value, "Actions");
				}
			}
		}

		// Token: 0x040003E5 RID: 997
		private readonly Action<KingdomDecision> _forceDecision;

		// Token: 0x040003E6 RID: 998
		private readonly Kingdom _playerKingdom;

		// Token: 0x040003E7 RID: 999
		private bool _isChangingDiplomacyItem;

		// Token: 0x040003E8 RID: 1000
		private MBBindingList<KingdomWarItemVM> _playerWars;

		// Token: 0x040003E9 RID: 1001
		private MBBindingList<KingdomTruceItemVM> _playerTruces;

		// Token: 0x040003EA RID: 1002
		private KingdomWarSortControllerVM _warsSortController;

		// Token: 0x040003EB RID: 1003
		private KingdomDiplomacyItemVM _currentSelectedItem;

		// Token: 0x040003EC RID: 1004
		private SelectorVM<SelectorItemVM> _behaviorSelection;

		// Token: 0x040003ED RID: 1005
		private HintViewModel _showStatBarsHint;

		// Token: 0x040003EE RID: 1006
		private HintViewModel _showWarLogsHint;

		// Token: 0x040003EF RID: 1007
		private string _playerWarsText;

		// Token: 0x040003F0 RID: 1008
		private string _numOfPlayerWarsText;

		// Token: 0x040003F1 RID: 1009
		private string _otherWarsText;

		// Token: 0x040003F2 RID: 1010
		private string _numOfOtherWarsText;

		// Token: 0x040003F3 RID: 1011
		private string _warsText;

		// Token: 0x040003F4 RID: 1012
		private string _behaviorSelectionTitle;

		// Token: 0x040003F5 RID: 1013
		private bool _isDisplayingWarLogs;

		// Token: 0x040003F6 RID: 1014
		private bool _isDisplayingStatComparisons;

		// Token: 0x040003F7 RID: 1015
		private bool _isWar;

		// Token: 0x040003F8 RID: 1016
		private MBBindingList<KingdomDiplomacyProposalActionItemVM> _actions;
	}
}
