using System;
using System.Linq;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.KingdomManagement.Settlements
{
	// Token: 0x02000069 RID: 105
	public class KingdomSettlementVM : KingdomCategoryVM
	{
		// Token: 0x0600081B RID: 2075 RVA: 0x0002512C File Offset: 0x0002332C
		public KingdomSettlementVM(Action<KingdomDecision> forceDecision, Action<Settlement> onGrantFief)
		{
			this._forceDecision = forceDecision;
			this._onGrantFief = onGrantFief;
			this._kingdom = (Hero.MainHero.MapFaction as Kingdom);
			this.AnnexCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfAnnexation(Clan.PlayerClan);
			this.AnnexHint = new HintViewModel();
			base.IsAcceptableItemSelected = false;
			this.Settlements = new MBBindingList<KingdomSettlementItemVM>();
			this.RefreshSettlementList();
			base.NotificationCount = 0;
			this.SettlementSortController = new KingdomSettlementSortControllerVM(this.Settlements);
			this.RefreshValues();
		}

		// Token: 0x0600081C RID: 2076 RVA: 0x000251C2 File Offset: 0x000233C2
		protected virtual KingdomSettlementItemVM CreateSettlementItemVM(Settlement settlement, Action<KingdomSettlementItemVM> onSelect)
		{
			return new KingdomSettlementItemVM(settlement, onSelect);
		}

		// Token: 0x0600081D RID: 2077 RVA: 0x000251CC File Offset: 0x000233CC
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.OwnerText = GameTexts.FindText("str_owner", null).ToString();
			this.NameText = GameTexts.FindText("str_scoreboard_header", "name").ToString();
			this.TypeText = GameTexts.FindText("str_sort_by_type_label", null).ToString();
			this.ProsperityText = GameTexts.FindText("str_prosperity_abbr", null).ToString();
			this.FoodText = GameTexts.FindText("str_inventory_category_tooltip", "6").ToString();
			this.GarrisonText = GameTexts.FindText("str_map_tooltip_garrison", null).ToString();
			this.MilitiaText = GameTexts.FindText("str_militia", null).ToString();
			this.ClanText = GameTexts.FindText("str_clans", null).ToString();
			this.VillagesText = GameTexts.FindText("str_villages", null).ToString();
			base.NoItemSelectedText = GameTexts.FindText("str_kingdom_no_settlement_selected", null).ToString();
			this.ProposeText = GameTexts.FindText("str_policy_propose", null).ToString();
			this.DefendersText = GameTexts.FindText("str_sort_by_defenders_label", null).ToString();
			base.CategoryNameText = new TextObject("{=qKUjgS6r}Settlement", null).ToString();
			this.Settlements.ApplyActionOnAllItems(delegate(KingdomSettlementItemVM x)
			{
				x.RefreshValues();
			});
			KingdomSettlementItemVM currentSelectedSettlement = this.CurrentSelectedSettlement;
			if (currentSelectedSettlement == null)
			{
				return;
			}
			currentSelectedSettlement.RefreshValues();
		}

		// Token: 0x0600081E RID: 2078 RVA: 0x00025340 File Offset: 0x00023540
		public void RefreshSettlementList()
		{
			this.Settlements.Clear();
			if (this._kingdom != null)
			{
				foreach (Settlement settlement in from S in this._kingdom.Settlements
				where S.IsCastle || S.IsTown
				select S)
				{
					KingdomSettlementItemVM item = this.CreateSettlementItemVM(settlement, new Action<KingdomSettlementItemVM>(this.OnSettlementSelection));
					this.Settlements.Add(item);
				}
			}
			if (this.Settlements.Count > 0)
			{
				this.SetCurrentSelectedSettlement(this.Settlements.FirstOrDefault<KingdomSettlementItemVM>());
			}
		}

		// Token: 0x0600081F RID: 2079 RVA: 0x00025404 File Offset: 0x00023604
		private void SetCurrentSelectedSettlement(KingdomSettlementItemVM settlementItem)
		{
			if (this.CurrentSelectedSettlement != settlementItem)
			{
				if (this.CurrentSelectedSettlement != null)
				{
					this.CurrentSelectedSettlement.IsSelected = false;
				}
				this.CurrentSelectedSettlement = settlementItem;
				this.CurrentSelectedSettlement.IsSelected = true;
				if (settlementItem != null)
				{
					this._currenItemsUnresolvedDecision = this.GetSettlementsAnyWaitingDecision(settlementItem.Settlement);
					if (this._currenItemsUnresolvedDecision != null)
					{
						base.IsAcceptableItemSelected = true;
						this.AnnexCost = 0;
						this.AnnexText = GameTexts.FindText("str_resolve", null).ToString();
						this.AnnexActionExplanationText = GameTexts.FindText("str_resolve_explanation", null).ToString();
						this.AnnexHint.HintText = TextObject.GetEmpty();
					}
					else if (settlementItem.Owner.Hero == Hero.MainHero)
					{
						if (Hero.MainHero.IsKingdomLeader)
						{
							this.AnnexActionExplanationText = new TextObject("{=G2h0V10w}Gift this settlement to a clan in your kingdom.", null).ToString();
							this.AnnexText = new TextObject("{=sffGeQ1g}Gift", null).ToString();
						}
						else
						{
							this.AnnexActionExplanationText = new TextObject("{=1UbocG5B}Denounce your rights and responsibilities from this fief by giving it back to the realm.", null).ToString();
							this.AnnexText = new TextObject("{=U3ksQXD3}Give Away", null).ToString();
						}
						if (Hero.MainHero.IsPrisoner)
						{
							this.CanAnnexCurrentSettlement = false;
							this.HasCost = true;
							this.AnnexHint.HintText = GameTexts.FindText("str_action_disabled_reason_prisoner", null);
						}
						else if (!Campaign.Current.Models.DiplomacyModel.CanSettlementBeGifted(this._currentSelectedSettlement.Settlement))
						{
							this.CanAnnexCurrentSettlement = false;
							this.HasCost = true;
							this.AnnexHint.HintText = GameTexts.FindText("str_cannot_annex_waiting_for_ruler_decision", null);
						}
						else if (PlayerEncounter.Current != null && PlayerEncounter.EncounterSettlement == null)
						{
							this.CanAnnexCurrentSettlement = false;
							this.HasCost = true;
							this.AnnexHint.HintText = GameTexts.FindText("str_action_disabled_reason_encounter", null);
						}
						else if (PlayerSiege.PlayerSiegeEvent != null)
						{
							this.CanAnnexCurrentSettlement = false;
							this.HasCost = true;
							this.AnnexHint.HintText = GameTexts.FindText("str_action_disabled_reason_siege", null);
						}
						else
						{
							this.CanAnnexCurrentSettlement = true;
							this.HasCost = false;
							this.AnnexHint.HintText = TextObject.GetEmpty();
						}
					}
					else
					{
						this.AnnexText = GameTexts.FindText("str_policy_propose", null).ToString();
						this.AnnexCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfAnnexation(Clan.PlayerClan);
						this.AnnexActionExplanationText = GameTexts.FindText("str_annex_fief_action_explanation", null).SetTextVariable("SUPPORT", KingdomSettlementVM.CalculateLikelihood(settlementItem.Settlement)).ToString();
						TextObject hintText;
						this.CanAnnexCurrentSettlement = this.GetCanAnnexSettlementWithReason(this.AnnexCost, out hintText);
						this.AnnexHint.HintText = hintText;
						this.HasCost = true;
					}
				}
				base.IsAcceptableItemSelected = (this.CurrentSelectedSettlement != null);
			}
		}

		// Token: 0x06000820 RID: 2080 RVA: 0x000256D0 File Offset: 0x000238D0
		private bool GetCanAnnexSettlementWithReason(int annexCost, out TextObject disabledReason)
		{
			TextObject textObject;
			if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out textObject))
			{
				disabledReason = textObject;
				return false;
			}
			if (Hero.MainHero.Clan.Influence < (float)annexCost)
			{
				disabledReason = GameTexts.FindText("str_warning_you_dont_have_enough_influence", null);
				return false;
			}
			if (this.CurrentSelectedSettlement.Settlement.OwnerClan == this._kingdom.RulingClan)
			{
				disabledReason = GameTexts.FindText("str_cannot_annex_ruling_clan_settlement", null);
				return false;
			}
			if (Clan.PlayerClan.IsUnderMercenaryService)
			{
				disabledReason = GameTexts.FindText("str_cannot_annex_while_mercenary", null);
				return false;
			}
			disabledReason = TextObject.GetEmpty();
			return true;
		}

		// Token: 0x06000821 RID: 2081 RVA: 0x0002575C File Offset: 0x0002395C
		public void SelectSettlement(Settlement settlement)
		{
			foreach (KingdomSettlementItemVM kingdomSettlementItemVM in this.Settlements)
			{
				if (kingdomSettlementItemVM.Settlement == settlement)
				{
					this.OnSettlementSelection(kingdomSettlementItemVM);
					break;
				}
			}
		}

		// Token: 0x06000822 RID: 2082 RVA: 0x000257B4 File Offset: 0x000239B4
		private void OnSettlementSelection(KingdomSettlementItemVM settlement)
		{
			if (this._currentSelectedSettlement != settlement)
			{
				this.SetCurrentSelectedSettlement(settlement);
			}
		}

		// Token: 0x06000823 RID: 2083 RVA: 0x000257C8 File Offset: 0x000239C8
		private void ExecuteAnnex()
		{
			if (this._currentSelectedSettlement != null)
			{
				if (this._currenItemsUnresolvedDecision != null)
				{
					this._forceDecision(this._currenItemsUnresolvedDecision);
					return;
				}
				Settlement settlement = this._currentSelectedSettlement.Settlement;
				if (settlement.OwnerClan.Leader == Hero.MainHero)
				{
					this._onGrantFief(settlement);
					return;
				}
				if (Hero.MainHero.Clan.Influence >= (float)this.AnnexCost)
				{
					SettlementClaimantPreliminaryDecision settlementClaimantPreliminaryDecision = new SettlementClaimantPreliminaryDecision(Clan.PlayerClan, settlement);
					Clan.PlayerClan.Kingdom.AddDecision(settlementClaimantPreliminaryDecision, false);
					this._forceDecision(settlementClaimantPreliminaryDecision);
				}
			}
		}

		// Token: 0x06000824 RID: 2084 RVA: 0x00025868 File Offset: 0x00023A68
		private KingdomDecision GetSettlementsAnyWaitingDecision(Settlement settlement)
		{
			KingdomDecision kingdomDecision = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault(delegate(KingdomDecision d)
			{
				SettlementClaimantDecision settlementClaimantDecision;
				return (settlementClaimantDecision = (d as SettlementClaimantDecision)) != null && settlementClaimantDecision.Settlement == settlement && !d.ShouldBeCancelled();
			});
			KingdomDecision kingdomDecision2 = Clan.PlayerClan.Kingdom.UnresolvedDecisions.FirstOrDefault(delegate(KingdomDecision d)
			{
				SettlementClaimantPreliminaryDecision settlementClaimantPreliminaryDecision;
				return (settlementClaimantPreliminaryDecision = (d as SettlementClaimantPreliminaryDecision)) != null && settlementClaimantPreliminaryDecision.Settlement == settlement && !d.ShouldBeCancelled();
			});
			return kingdomDecision ?? kingdomDecision2;
		}

		// Token: 0x1700024E RID: 590
		// (get) Token: 0x06000825 RID: 2085 RVA: 0x000258C8 File Offset: 0x00023AC8
		// (set) Token: 0x06000826 RID: 2086 RVA: 0x000258D0 File Offset: 0x00023AD0
		[DataSourceProperty]
		public KingdomSettlementItemVM CurrentSelectedSettlement
		{
			get
			{
				return this._currentSelectedSettlement;
			}
			set
			{
				if (value != this._currentSelectedSettlement)
				{
					this._currentSelectedSettlement = value;
					base.OnPropertyChangedWithValue<KingdomSettlementItemVM>(value, "CurrentSelectedSettlement");
				}
			}
		}

		// Token: 0x1700024F RID: 591
		// (get) Token: 0x06000827 RID: 2087 RVA: 0x000258EE File Offset: 0x00023AEE
		// (set) Token: 0x06000828 RID: 2088 RVA: 0x000258F6 File Offset: 0x00023AF6
		[DataSourceProperty]
		public KingdomSettlementSortControllerVM SettlementSortController
		{
			get
			{
				return this._settlementSortController;
			}
			set
			{
				if (value != this._settlementSortController)
				{
					this._settlementSortController = value;
					base.OnPropertyChangedWithValue<KingdomSettlementSortControllerVM>(value, "SettlementSortController");
				}
			}
		}

		// Token: 0x17000250 RID: 592
		// (get) Token: 0x06000829 RID: 2089 RVA: 0x00025914 File Offset: 0x00023B14
		// (set) Token: 0x0600082A RID: 2090 RVA: 0x0002591C File Offset: 0x00023B1C
		[DataSourceProperty]
		public HintViewModel AnnexHint
		{
			get
			{
				return this._annexHint;
			}
			set
			{
				if (value != this._annexHint)
				{
					this._annexHint = value;
					base.OnPropertyChangedWithValue<HintViewModel>(value, "AnnexHint");
				}
			}
		}

		// Token: 0x17000251 RID: 593
		// (get) Token: 0x0600082B RID: 2091 RVA: 0x0002593A File Offset: 0x00023B3A
		// (set) Token: 0x0600082C RID: 2092 RVA: 0x00025942 File Offset: 0x00023B42
		[DataSourceProperty]
		public string ProposeText
		{
			get
			{
				return this._proposeText;
			}
			set
			{
				if (value != this._proposeText)
				{
					this._proposeText = value;
					base.OnPropertyChangedWithValue<string>(value, "ProposeText");
				}
			}
		}

		// Token: 0x17000252 RID: 594
		// (get) Token: 0x0600082D RID: 2093 RVA: 0x00025965 File Offset: 0x00023B65
		// (set) Token: 0x0600082E RID: 2094 RVA: 0x0002596D File Offset: 0x00023B6D
		[DataSourceProperty]
		public string AnnexActionExplanationText
		{
			get
			{
				return this._annexActionExplanationText;
			}
			set
			{
				if (value != this._annexActionExplanationText)
				{
					this._annexActionExplanationText = value;
					base.OnPropertyChangedWithValue<string>(value, "AnnexActionExplanationText");
				}
			}
		}

		// Token: 0x17000253 RID: 595
		// (get) Token: 0x0600082F RID: 2095 RVA: 0x00025990 File Offset: 0x00023B90
		// (set) Token: 0x06000830 RID: 2096 RVA: 0x00025998 File Offset: 0x00023B98
		[DataSourceProperty]
		public string ProsperityText
		{
			get
			{
				return this._prosperityText;
			}
			set
			{
				if (value != this._prosperityText)
				{
					this._prosperityText = value;
					base.OnPropertyChangedWithValue<string>(value, "ProsperityText");
				}
			}
		}

		// Token: 0x17000254 RID: 596
		// (get) Token: 0x06000831 RID: 2097 RVA: 0x000259BB File Offset: 0x00023BBB
		// (set) Token: 0x06000832 RID: 2098 RVA: 0x000259C3 File Offset: 0x00023BC3
		[DataSourceProperty]
		public string VillagesText
		{
			get
			{
				return this._villagesText;
			}
			set
			{
				if (value != this._villagesText)
				{
					this._villagesText = value;
					base.OnPropertyChangedWithValue<string>(value, "VillagesText");
				}
			}
		}

		// Token: 0x17000255 RID: 597
		// (get) Token: 0x06000833 RID: 2099 RVA: 0x000259E6 File Offset: 0x00023BE6
		// (set) Token: 0x06000834 RID: 2100 RVA: 0x000259EE File Offset: 0x00023BEE
		[DataSourceProperty]
		public string OwnerText
		{
			get
			{
				return this._ownerText;
			}
			set
			{
				if (value != this._ownerText)
				{
					this._ownerText = value;
					base.OnPropertyChangedWithValue<string>(value, "OwnerText");
				}
			}
		}

		// Token: 0x17000256 RID: 598
		// (get) Token: 0x06000835 RID: 2101 RVA: 0x00025A11 File Offset: 0x00023C11
		// (set) Token: 0x06000836 RID: 2102 RVA: 0x00025A19 File Offset: 0x00023C19
		[DataSourceProperty]
		public string NameText
		{
			get
			{
				return this._nameText;
			}
			set
			{
				if (value != this._nameText)
				{
					this._nameText = value;
					base.OnPropertyChangedWithValue<string>(value, "NameText");
				}
			}
		}

		// Token: 0x17000257 RID: 599
		// (get) Token: 0x06000837 RID: 2103 RVA: 0x00025A3C File Offset: 0x00023C3C
		// (set) Token: 0x06000838 RID: 2104 RVA: 0x00025A44 File Offset: 0x00023C44
		[DataSourceProperty]
		public string ClanText
		{
			get
			{
				return this._clanText;
			}
			set
			{
				if (value != this._clanText)
				{
					this._clanText = value;
					base.OnPropertyChangedWithValue<string>(value, "ClanText");
				}
			}
		}

		// Token: 0x17000258 RID: 600
		// (get) Token: 0x06000839 RID: 2105 RVA: 0x00025A67 File Offset: 0x00023C67
		// (set) Token: 0x0600083A RID: 2106 RVA: 0x00025A6F File Offset: 0x00023C6F
		[DataSourceProperty]
		public string FoodText
		{
			get
			{
				return this._foodText;
			}
			set
			{
				if (value != this._foodText)
				{
					this._foodText = value;
					base.OnPropertyChangedWithValue<string>(value, "FoodText");
				}
			}
		}

		// Token: 0x17000259 RID: 601
		// (get) Token: 0x0600083B RID: 2107 RVA: 0x00025A92 File Offset: 0x00023C92
		// (set) Token: 0x0600083C RID: 2108 RVA: 0x00025A9A File Offset: 0x00023C9A
		[DataSourceProperty]
		public string GarrisonText
		{
			get
			{
				return this._garrisonText;
			}
			set
			{
				if (value != this._garrisonText)
				{
					this._garrisonText = value;
					base.OnPropertyChangedWithValue<string>(value, "GarrisonText");
				}
			}
		}

		// Token: 0x1700025A RID: 602
		// (get) Token: 0x0600083D RID: 2109 RVA: 0x00025ABD File Offset: 0x00023CBD
		// (set) Token: 0x0600083E RID: 2110 RVA: 0x00025AC5 File Offset: 0x00023CC5
		[DataSourceProperty]
		public string MilitiaText
		{
			get
			{
				return this._militiaText;
			}
			set
			{
				if (value != this._militiaText)
				{
					this._militiaText = value;
					base.OnPropertyChangedWithValue<string>(value, "MilitiaText");
				}
			}
		}

		// Token: 0x1700025B RID: 603
		// (get) Token: 0x0600083F RID: 2111 RVA: 0x00025AE8 File Offset: 0x00023CE8
		// (set) Token: 0x06000840 RID: 2112 RVA: 0x00025AF0 File Offset: 0x00023CF0
		[DataSourceProperty]
		public string AnnexText
		{
			get
			{
				return this._annexText;
			}
			set
			{
				if (value != this._annexText)
				{
					this._annexText = value;
					base.OnPropertyChangedWithValue<string>(value, "AnnexText");
				}
			}
		}

		// Token: 0x1700025C RID: 604
		// (get) Token: 0x06000841 RID: 2113 RVA: 0x00025B13 File Offset: 0x00023D13
		// (set) Token: 0x06000842 RID: 2114 RVA: 0x00025B1B File Offset: 0x00023D1B
		[DataSourceProperty]
		public string TypeText
		{
			get
			{
				return this._typeText;
			}
			set
			{
				if (value != this._typeText)
				{
					this._typeText = value;
					base.OnPropertyChangedWithValue<string>(value, "TypeText");
				}
			}
		}

		// Token: 0x1700025D RID: 605
		// (get) Token: 0x06000843 RID: 2115 RVA: 0x00025B3E File Offset: 0x00023D3E
		// (set) Token: 0x06000844 RID: 2116 RVA: 0x00025B46 File Offset: 0x00023D46
		[DataSourceProperty]
		public int AnnexCost
		{
			get
			{
				return this._annexCost;
			}
			set
			{
				if (value != this._annexCost)
				{
					this._annexCost = value;
					base.OnPropertyChangedWithValue(value, "AnnexCost");
				}
			}
		}

		// Token: 0x1700025E RID: 606
		// (get) Token: 0x06000845 RID: 2117 RVA: 0x00025B64 File Offset: 0x00023D64
		// (set) Token: 0x06000846 RID: 2118 RVA: 0x00025B6C File Offset: 0x00023D6C
		[DataSourceProperty]
		public string DefendersText
		{
			get
			{
				return this._defendersText;
			}
			set
			{
				if (value != this._defendersText)
				{
					this._defendersText = value;
					base.OnPropertyChangedWithValue<string>(value, "DefendersText");
				}
			}
		}

		// Token: 0x1700025F RID: 607
		// (get) Token: 0x06000847 RID: 2119 RVA: 0x00025B8F File Offset: 0x00023D8F
		// (set) Token: 0x06000848 RID: 2120 RVA: 0x00025B97 File Offset: 0x00023D97
		[DataSourceProperty]
		public MBBindingList<KingdomSettlementItemVM> Settlements
		{
			get
			{
				return this._settlements;
			}
			set
			{
				if (value != this._settlements)
				{
					this._settlements = value;
					base.OnPropertyChangedWithValue<MBBindingList<KingdomSettlementItemVM>>(value, "Settlements");
				}
			}
		}

		// Token: 0x17000260 RID: 608
		// (get) Token: 0x06000849 RID: 2121 RVA: 0x00025BB5 File Offset: 0x00023DB5
		// (set) Token: 0x0600084A RID: 2122 RVA: 0x00025BBD File Offset: 0x00023DBD
		[DataSourceProperty]
		public bool CanAnnexCurrentSettlement
		{
			get
			{
				return this._canAnnexCurrentSettlement;
			}
			set
			{
				if (value != this._canAnnexCurrentSettlement)
				{
					this._canAnnexCurrentSettlement = value;
					base.OnPropertyChangedWithValue(value, "CanAnnexCurrentSettlement");
				}
			}
		}

		// Token: 0x17000261 RID: 609
		// (get) Token: 0x0600084B RID: 2123 RVA: 0x00025BDB File Offset: 0x00023DDB
		// (set) Token: 0x0600084C RID: 2124 RVA: 0x00025BE3 File Offset: 0x00023DE3
		[DataSourceProperty]
		public bool HasCost
		{
			get
			{
				return this._hasCost;
			}
			set
			{
				if (value != this._hasCost)
				{
					this._hasCost = value;
					base.OnPropertyChangedWithValue(value, "HasCost");
				}
			}
		}

		// Token: 0x0600084D RID: 2125 RVA: 0x00025C01 File Offset: 0x00023E01
		private static int CalculateLikelihood(Settlement settlement)
		{
			return MathF.Round(new KingdomElection(new SettlementClaimantPreliminaryDecision(Clan.PlayerClan, settlement)).GetLikelihoodForSponsor(Clan.PlayerClan) * 100f);
		}

		// Token: 0x04000383 RID: 899
		private readonly Action<KingdomDecision> _forceDecision;

		// Token: 0x04000384 RID: 900
		private readonly Action<Settlement> _onGrantFief;

		// Token: 0x04000385 RID: 901
		private readonly Kingdom _kingdom;

		// Token: 0x04000386 RID: 902
		private KingdomDecision _currenItemsUnresolvedDecision;

		// Token: 0x04000387 RID: 903
		private MBBindingList<KingdomSettlementItemVM> _settlements;

		// Token: 0x04000388 RID: 904
		private KingdomSettlementItemVM _currentSelectedSettlement;

		// Token: 0x04000389 RID: 905
		private HintViewModel _annexHint;

		// Token: 0x0400038A RID: 906
		private string _ownerText;

		// Token: 0x0400038B RID: 907
		private string _nameText;

		// Token: 0x0400038C RID: 908
		private string _typeText;

		// Token: 0x0400038D RID: 909
		private string _prosperityText;

		// Token: 0x0400038E RID: 910
		private string _foodText;

		// Token: 0x0400038F RID: 911
		private string _garrisonText;

		// Token: 0x04000390 RID: 912
		private string _militiaText;

		// Token: 0x04000391 RID: 913
		private string _annexText;

		// Token: 0x04000392 RID: 914
		private string _clanText;

		// Token: 0x04000393 RID: 915
		private string _villagesText;

		// Token: 0x04000394 RID: 916
		private string _annexActionExplanationText;

		// Token: 0x04000395 RID: 917
		private string _proposeText;

		// Token: 0x04000396 RID: 918
		private string _defendersText;

		// Token: 0x04000397 RID: 919
		private int _annexCost;

		// Token: 0x04000398 RID: 920
		private bool _canAnnexCurrentSettlement;

		// Token: 0x04000399 RID: 921
		private bool _hasCost;

		// Token: 0x0400039A RID: 922
		private KingdomSettlementSortControllerVM _settlementSortController;
	}
}
