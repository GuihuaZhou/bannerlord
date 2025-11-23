using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.ClanFinance;
using TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Supporters;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TaleWorlds.CampaignSystem.ViewModelCollection.ClanManagement.Categories
{
	// Token: 0x02000137 RID: 311
	public class ClanIncomeVM : ViewModel
	{
		// Token: 0x170009DF RID: 2527
		// (get) Token: 0x06001D05 RID: 7429 RVA: 0x0006A996 File Offset: 0x00068B96
		// (set) Token: 0x06001D06 RID: 7430 RVA: 0x0006A99E File Offset: 0x00068B9E
		public int TotalIncome { get; private set; }

		// Token: 0x06001D07 RID: 7431 RVA: 0x0006A9A8 File Offset: 0x00068BA8
		public ClanIncomeVM(Action onRefresh, Action<ClanCardSelectionInfo> openCardSelectionPopup)
		{
			this._onRefresh = onRefresh;
			this._openCardSelectionPopup = openCardSelectionPopup;
			this.Incomes = new MBBindingList<ClanFinanceWorkshopItemVM>();
			this.SupporterGroups = new MBBindingList<ClanSupporterGroupVM>();
			this.Alleys = new MBBindingList<ClanFinanceAlleyItemVM>();
			this.SortController = new ClanIncomeSortControllerVM(this._incomes, this._supporterGroups, this._alleys);
			this.RefreshList();
		}

		// Token: 0x06001D08 RID: 7432 RVA: 0x0006AA10 File Offset: 0x00068C10
		public override void RefreshValues()
		{
			base.RefreshValues();
			this.NameText = GameTexts.FindText("str_sort_by_name_label", null).ToString();
			this.IncomeText = GameTexts.FindText("str_income", null).ToString();
			this.LocationText = GameTexts.FindText("str_tooltip_label_location", null).ToString();
			this.NoAdditionalIncomesText = GameTexts.FindText("str_clan_no_additional_incomes", null).ToString();
			this.Incomes.ApplyActionOnAllItems(delegate(ClanFinanceWorkshopItemVM x)
			{
				x.RefreshValues();
			});
			ClanFinanceWorkshopItemVM currentSelectedIncome = this.CurrentSelectedIncome;
			if (currentSelectedIncome != null)
			{
				currentSelectedIncome.RefreshValues();
			}
			this.SortController.RefreshValues();
		}

		// Token: 0x06001D09 RID: 7433 RVA: 0x0006AAC4 File Offset: 0x00068CC4
		public void RefreshList()
		{
			this.Incomes.Clear();
			foreach (Settlement settlement in Settlement.All)
			{
				if (settlement.IsTown)
				{
					foreach (Workshop workshop in settlement.Town.Workshops)
					{
						if (workshop.Owner == Hero.MainHero)
						{
							this.Incomes.Add(new ClanFinanceWorkshopItemVM(workshop, new Action<ClanFinanceWorkshopItemVM>(this.OnIncomeSelection), new Action(this.OnRefresh), this._openCardSelectionPopup));
						}
					}
				}
			}
			this.RefreshSupporters();
			this.RefreshAlleys();
			this.SortController.ResetAllStates();
			GameTexts.SetVariable("STR1", GameTexts.FindText("str_clan_workshops", null));
			GameTexts.SetVariable("LEFT", Hero.MainHero.OwnedWorkshops.Count);
			GameTexts.SetVariable("RIGHT", Campaign.Current.Models.WorkshopModel.GetMaxWorkshopCountForClanTier(Clan.PlayerClan.Tier));
			GameTexts.SetVariable("STR2", GameTexts.FindText("str_LEFT_over_RIGHT_in_paranthesis", null));
			this.WorkshopText = GameTexts.FindText("str_STR1_space_STR2", null).ToString();
			int num = 0;
			foreach (ClanSupporterGroupVM clanSupporterGroupVM in this.SupporterGroups)
			{
				num += clanSupporterGroupVM.Supporters.Count;
			}
			GameTexts.SetVariable("RANK", new TextObject("{=RzFyGnWJ}Supporters", null).ToString());
			GameTexts.SetVariable("NUMBER", num);
			this.SupportersText = GameTexts.FindText("str_RANK_with_NUM_between_parenthesis", null).ToString();
			GameTexts.SetVariable("RANK", new TextObject("{=7tKjfMSb}Alleys", null).ToString());
			GameTexts.SetVariable("NUMBER", this.Alleys.Count);
			this.AlleysText = GameTexts.FindText("str_RANK_with_NUM_between_parenthesis", null).ToString();
			this.RefreshTotalIncome();
			this.OnIncomeSelection(this.GetDefaultIncome());
			this.RefreshValues();
		}

		// Token: 0x06001D0A RID: 7434 RVA: 0x0006AD00 File Offset: 0x00068F00
		private void RefreshSupporters()
		{
			foreach (ClanSupporterGroupVM clanSupporterGroupVM in this.SupporterGroups)
			{
				clanSupporterGroupVM.Supporters.Clear();
			}
			this.SupporterGroups.Clear();
			Dictionary<float, List<Hero>> dictionary = new Dictionary<float, List<Hero>>();
			NotablePowerModel notablePowerModel = Campaign.Current.Models.NotablePowerModel;
			foreach (Hero hero in from x in Clan.PlayerClan.SupporterNotables
			orderby x.Power
			select x)
			{
				if (hero.CurrentSettlement != null)
				{
					float influenceBonusToClan = notablePowerModel.GetInfluenceBonusToClan(hero);
					List<Hero> list;
					if (dictionary.TryGetValue(influenceBonusToClan, out list))
					{
						list.Add(hero);
					}
					else
					{
						dictionary.Add(influenceBonusToClan, new List<Hero>
						{
							hero
						});
					}
				}
			}
			foreach (KeyValuePair<float, List<Hero>> keyValuePair in dictionary)
			{
				if (keyValuePair.Value.Count > 0)
				{
					ClanSupporterGroupVM clanSupporterGroupVM2 = new ClanSupporterGroupVM(notablePowerModel.GetPowerRankName(keyValuePair.Value.FirstOrDefault<Hero>()), keyValuePair.Key, new Action<ClanSupporterGroupVM>(this.OnSupporterSelection));
					foreach (Hero hero2 in keyValuePair.Value)
					{
						clanSupporterGroupVM2.AddSupporter(hero2);
					}
					this.SupporterGroups.Add(clanSupporterGroupVM2);
				}
			}
			foreach (ClanSupporterGroupVM clanSupporterGroupVM3 in this.SupporterGroups)
			{
				clanSupporterGroupVM3.Refresh();
			}
		}

		// Token: 0x06001D0B RID: 7435 RVA: 0x0006AF14 File Offset: 0x00069114
		private void RefreshAlleys()
		{
			this.Alleys.Clear();
			foreach (Alley alley in Hero.MainHero.OwnedAlleys)
			{
				this.Alleys.Add(new ClanFinanceAlleyItemVM(alley, this._openCardSelectionPopup, new Action<ClanFinanceAlleyItemVM>(this.OnAlleySelection), new Action(this.OnRefresh)));
			}
		}

		// Token: 0x06001D0C RID: 7436 RVA: 0x0006AFA0 File Offset: 0x000691A0
		private ClanFinanceWorkshopItemVM GetDefaultIncome()
		{
			return this.Incomes.FirstOrDefault<ClanFinanceWorkshopItemVM>();
		}

		// Token: 0x06001D0D RID: 7437 RVA: 0x0006AFB0 File Offset: 0x000691B0
		public void SelectWorkshop(Workshop workshop)
		{
			foreach (ClanFinanceWorkshopItemVM clanFinanceWorkshopItemVM in this.Incomes)
			{
				if (clanFinanceWorkshopItemVM != null)
				{
					ClanFinanceWorkshopItemVM clanFinanceWorkshopItemVM2 = clanFinanceWorkshopItemVM;
					if (clanFinanceWorkshopItemVM2.Workshop == workshop)
					{
						this.OnIncomeSelection(clanFinanceWorkshopItemVM2);
						break;
					}
				}
			}
		}

		// Token: 0x06001D0E RID: 7438 RVA: 0x0006B010 File Offset: 0x00069210
		public void SelectAlley(Alley alley)
		{
			for (int i = 0; i < this.Alleys.Count; i++)
			{
				if (this.Alleys[i].Alley == alley)
				{
					this.OnAlleySelection(this.Alleys[i]);
					return;
				}
			}
		}

		// Token: 0x06001D0F RID: 7439 RVA: 0x0006B05C File Offset: 0x0006925C
		private void OnAlleySelection(ClanFinanceAlleyItemVM alley)
		{
			if (alley == null)
			{
				if (this.CurrentSelectedAlley != null)
				{
					this.CurrentSelectedAlley.IsSelected = false;
				}
				this.CurrentSelectedAlley = null;
				return;
			}
			this.OnIncomeSelection(null);
			this.OnSupporterSelection(null);
			if (this.CurrentSelectedAlley != null)
			{
				this.CurrentSelectedAlley.IsSelected = false;
			}
			this.CurrentSelectedAlley = alley;
			if (alley != null)
			{
				alley.IsSelected = true;
			}
		}

		// Token: 0x06001D10 RID: 7440 RVA: 0x0006B0BC File Offset: 0x000692BC
		private void OnIncomeSelection(ClanFinanceWorkshopItemVM income)
		{
			if (income == null)
			{
				if (this.CurrentSelectedIncome != null)
				{
					this.CurrentSelectedIncome.IsSelected = false;
				}
				this.CurrentSelectedIncome = null;
				return;
			}
			this.OnSupporterSelection(null);
			this.OnAlleySelection(null);
			if (this.CurrentSelectedIncome != null)
			{
				this.CurrentSelectedIncome.IsSelected = false;
			}
			this.CurrentSelectedIncome = income;
			if (income != null)
			{
				income.IsSelected = true;
			}
		}

		// Token: 0x06001D11 RID: 7441 RVA: 0x0006B11C File Offset: 0x0006931C
		private void OnSupporterSelection(ClanSupporterGroupVM supporter)
		{
			if (supporter == null)
			{
				if (this.CurrentSelectedSupporterGroup != null)
				{
					this.CurrentSelectedSupporterGroup.IsSelected = false;
				}
				this.CurrentSelectedSupporterGroup = null;
				return;
			}
			this.OnIncomeSelection(null);
			this.OnAlleySelection(null);
			if (this.CurrentSelectedSupporterGroup != null)
			{
				this.CurrentSelectedSupporterGroup.IsSelected = false;
			}
			this.CurrentSelectedSupporterGroup = supporter;
			if (this.CurrentSelectedSupporterGroup != null)
			{
				this.CurrentSelectedSupporterGroup.IsSelected = true;
			}
		}

		// Token: 0x06001D12 RID: 7442 RVA: 0x0006B185 File Offset: 0x00069385
		public void RefreshTotalIncome()
		{
			this.TotalIncome = this.Incomes.Sum((ClanFinanceWorkshopItemVM i) => i.Income);
		}

		// Token: 0x06001D13 RID: 7443 RVA: 0x0006B1B7 File Offset: 0x000693B7
		public void OnRefresh()
		{
			Action onRefresh = this._onRefresh;
			if (onRefresh == null)
			{
				return;
			}
			onRefresh();
		}

		// Token: 0x170009E0 RID: 2528
		// (get) Token: 0x06001D14 RID: 7444 RVA: 0x0006B1C9 File Offset: 0x000693C9
		// (set) Token: 0x06001D15 RID: 7445 RVA: 0x0006B1D1 File Offset: 0x000693D1
		[DataSourceProperty]
		public ClanFinanceAlleyItemVM CurrentSelectedAlley
		{
			get
			{
				return this._currentSelectedAlley;
			}
			set
			{
				if (value != this._currentSelectedAlley)
				{
					this._currentSelectedAlley = value;
					base.OnPropertyChangedWithValue<ClanFinanceAlleyItemVM>(value, "CurrentSelectedAlley");
					this.IsAnyValidAlleySelected = (value != null);
					this.IsAnyValidIncomeSelected = false;
					this.IsAnyValidSupporterSelected = false;
				}
			}
		}

		// Token: 0x170009E1 RID: 2529
		// (get) Token: 0x06001D16 RID: 7446 RVA: 0x0006B207 File Offset: 0x00069407
		// (set) Token: 0x06001D17 RID: 7447 RVA: 0x0006B20F File Offset: 0x0006940F
		[DataSourceProperty]
		public ClanFinanceWorkshopItemVM CurrentSelectedIncome
		{
			get
			{
				return this._currentSelectedIncome;
			}
			set
			{
				if (value != this._currentSelectedIncome)
				{
					this._currentSelectedIncome = value;
					base.OnPropertyChangedWithValue<ClanFinanceWorkshopItemVM>(value, "CurrentSelectedIncome");
					this.IsAnyValidIncomeSelected = (value != null);
					this.IsAnyValidSupporterSelected = false;
					this.IsAnyValidAlleySelected = false;
				}
			}
		}

		// Token: 0x170009E2 RID: 2530
		// (get) Token: 0x06001D18 RID: 7448 RVA: 0x0006B245 File Offset: 0x00069445
		// (set) Token: 0x06001D19 RID: 7449 RVA: 0x0006B24D File Offset: 0x0006944D
		[DataSourceProperty]
		public ClanSupporterGroupVM CurrentSelectedSupporterGroup
		{
			get
			{
				return this._currentSelectedSupporterGroup;
			}
			set
			{
				if (value != this._currentSelectedSupporterGroup)
				{
					this._currentSelectedSupporterGroup = value;
					base.OnPropertyChangedWithValue<ClanSupporterGroupVM>(value, "CurrentSelectedSupporterGroup");
					this.IsAnyValidSupporterSelected = (value != null);
					this.IsAnyValidIncomeSelected = false;
					this.IsAnyValidAlleySelected = false;
				}
			}
		}

		// Token: 0x170009E3 RID: 2531
		// (get) Token: 0x06001D1A RID: 7450 RVA: 0x0006B283 File Offset: 0x00069483
		// (set) Token: 0x06001D1B RID: 7451 RVA: 0x0006B28B File Offset: 0x0006948B
		[DataSourceProperty]
		public bool IsAnyValidAlleySelected
		{
			get
			{
				return this._isAnyValidAlleySelected;
			}
			set
			{
				if (value != this._isAnyValidAlleySelected)
				{
					this._isAnyValidAlleySelected = value;
					base.OnPropertyChangedWithValue(value, "IsAnyValidAlleySelected");
				}
			}
		}

		// Token: 0x170009E4 RID: 2532
		// (get) Token: 0x06001D1C RID: 7452 RVA: 0x0006B2A9 File Offset: 0x000694A9
		// (set) Token: 0x06001D1D RID: 7453 RVA: 0x0006B2B1 File Offset: 0x000694B1
		[DataSourceProperty]
		public bool IsAnyValidIncomeSelected
		{
			get
			{
				return this._isAnyValidIncomeSelected;
			}
			set
			{
				if (value != this._isAnyValidIncomeSelected)
				{
					this._isAnyValidIncomeSelected = value;
					base.OnPropertyChangedWithValue(value, "IsAnyValidIncomeSelected");
				}
			}
		}

		// Token: 0x170009E5 RID: 2533
		// (get) Token: 0x06001D1E RID: 7454 RVA: 0x0006B2CF File Offset: 0x000694CF
		// (set) Token: 0x06001D1F RID: 7455 RVA: 0x0006B2D7 File Offset: 0x000694D7
		[DataSourceProperty]
		public bool IsAnyValidSupporterSelected
		{
			get
			{
				return this._isAnyValidSupporterSelected;
			}
			set
			{
				if (value != this._isAnyValidSupporterSelected)
				{
					this._isAnyValidSupporterSelected = value;
					base.OnPropertyChangedWithValue(value, "IsAnyValidSupporterSelected");
				}
			}
		}

		// Token: 0x170009E6 RID: 2534
		// (get) Token: 0x06001D20 RID: 7456 RVA: 0x0006B2F5 File Offset: 0x000694F5
		// (set) Token: 0x06001D21 RID: 7457 RVA: 0x0006B2FD File Offset: 0x000694FD
		[DataSourceProperty]
		public string IncomeText
		{
			get
			{
				return this._incomeText;
			}
			set
			{
				if (value != this._incomeText)
				{
					this._incomeText = value;
					base.OnPropertyChangedWithValue<string>(value, "IncomeText");
				}
			}
		}

		// Token: 0x170009E7 RID: 2535
		// (get) Token: 0x06001D22 RID: 7458 RVA: 0x0006B320 File Offset: 0x00069520
		// (set) Token: 0x06001D23 RID: 7459 RVA: 0x0006B328 File Offset: 0x00069528
		[DataSourceProperty]
		public bool IsSelected
		{
			get
			{
				return this._isSelected;
			}
			set
			{
				if (value != this._isSelected)
				{
					this._isSelected = value;
					base.OnPropertyChangedWithValue(value, "IsSelected");
				}
			}
		}

		// Token: 0x170009E8 RID: 2536
		// (get) Token: 0x06001D24 RID: 7460 RVA: 0x0006B346 File Offset: 0x00069546
		// (set) Token: 0x06001D25 RID: 7461 RVA: 0x0006B34E File Offset: 0x0006954E
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

		// Token: 0x170009E9 RID: 2537
		// (get) Token: 0x06001D26 RID: 7462 RVA: 0x0006B371 File Offset: 0x00069571
		// (set) Token: 0x06001D27 RID: 7463 RVA: 0x0006B379 File Offset: 0x00069579
		[DataSourceProperty]
		public string LocationText
		{
			get
			{
				return this._locationText;
			}
			set
			{
				if (value != this._locationText)
				{
					this._locationText = value;
					base.OnPropertyChangedWithValue<string>(value, "LocationText");
				}
			}
		}

		// Token: 0x170009EA RID: 2538
		// (get) Token: 0x06001D28 RID: 7464 RVA: 0x0006B39C File Offset: 0x0006959C
		// (set) Token: 0x06001D29 RID: 7465 RVA: 0x0006B3A4 File Offset: 0x000695A4
		[DataSourceProperty]
		public string WorkshopText
		{
			get
			{
				return this._workshopsText;
			}
			set
			{
				if (value != this._workshopsText)
				{
					this._workshopsText = value;
					base.OnPropertyChangedWithValue<string>(value, "WorkshopText");
				}
			}
		}

		// Token: 0x170009EB RID: 2539
		// (get) Token: 0x06001D2A RID: 7466 RVA: 0x0006B3C7 File Offset: 0x000695C7
		// (set) Token: 0x06001D2B RID: 7467 RVA: 0x0006B3CF File Offset: 0x000695CF
		[DataSourceProperty]
		public string SupportersText
		{
			get
			{
				return this._supportersText;
			}
			set
			{
				if (value != this._supportersText)
				{
					this._supportersText = value;
					base.OnPropertyChangedWithValue<string>(value, "SupportersText");
				}
			}
		}

		// Token: 0x170009EC RID: 2540
		// (get) Token: 0x06001D2C RID: 7468 RVA: 0x0006B3F2 File Offset: 0x000695F2
		// (set) Token: 0x06001D2D RID: 7469 RVA: 0x0006B3FA File Offset: 0x000695FA
		[DataSourceProperty]
		public string AlleysText
		{
			get
			{
				return this._alleysText;
			}
			set
			{
				if (value != this._alleysText)
				{
					this._alleysText = value;
					base.OnPropertyChangedWithValue<string>(value, "AlleysText");
				}
			}
		}

		// Token: 0x170009ED RID: 2541
		// (get) Token: 0x06001D2E RID: 7470 RVA: 0x0006B41D File Offset: 0x0006961D
		// (set) Token: 0x06001D2F RID: 7471 RVA: 0x0006B425 File Offset: 0x00069625
		[DataSourceProperty]
		public string NoAdditionalIncomesText
		{
			get
			{
				return this._noAdditionalIncomesText;
			}
			set
			{
				if (this._noAdditionalIncomesText != value)
				{
					this._noAdditionalIncomesText = value;
					base.OnPropertyChangedWithValue<string>(value, "NoAdditionalIncomesText");
				}
			}
		}

		// Token: 0x170009EE RID: 2542
		// (get) Token: 0x06001D30 RID: 7472 RVA: 0x0006B448 File Offset: 0x00069648
		// (set) Token: 0x06001D31 RID: 7473 RVA: 0x0006B450 File Offset: 0x00069650
		[DataSourceProperty]
		public MBBindingList<ClanFinanceWorkshopItemVM> Incomes
		{
			get
			{
				return this._incomes;
			}
			set
			{
				if (value != this._incomes)
				{
					this._incomes = value;
					base.OnPropertyChangedWithValue<MBBindingList<ClanFinanceWorkshopItemVM>>(value, "Incomes");
				}
			}
		}

		// Token: 0x170009EF RID: 2543
		// (get) Token: 0x06001D32 RID: 7474 RVA: 0x0006B46E File Offset: 0x0006966E
		// (set) Token: 0x06001D33 RID: 7475 RVA: 0x0006B476 File Offset: 0x00069676
		[DataSourceProperty]
		public MBBindingList<ClanSupporterGroupVM> SupporterGroups
		{
			get
			{
				return this._supporterGroups;
			}
			set
			{
				if (value != this._supporterGroups)
				{
					this._supporterGroups = value;
					base.OnPropertyChangedWithValue<MBBindingList<ClanSupporterGroupVM>>(value, "SupporterGroups");
				}
			}
		}

		// Token: 0x170009F0 RID: 2544
		// (get) Token: 0x06001D34 RID: 7476 RVA: 0x0006B494 File Offset: 0x00069694
		// (set) Token: 0x06001D35 RID: 7477 RVA: 0x0006B49C File Offset: 0x0006969C
		[DataSourceProperty]
		public MBBindingList<ClanFinanceAlleyItemVM> Alleys
		{
			get
			{
				return this._alleys;
			}
			set
			{
				if (value != this._alleys)
				{
					this._alleys = value;
					base.OnPropertyChangedWithValue<MBBindingList<ClanFinanceAlleyItemVM>>(value, "Alleys");
				}
			}
		}

		// Token: 0x170009F1 RID: 2545
		// (get) Token: 0x06001D36 RID: 7478 RVA: 0x0006B4BA File Offset: 0x000696BA
		// (set) Token: 0x06001D37 RID: 7479 RVA: 0x0006B4C2 File Offset: 0x000696C2
		[DataSourceProperty]
		public ClanIncomeSortControllerVM SortController
		{
			get
			{
				return this._sortController;
			}
			set
			{
				if (value != this._sortController)
				{
					this._sortController = value;
					base.OnPropertyChangedWithValue<ClanIncomeSortControllerVM>(value, "SortController");
				}
			}
		}

		// Token: 0x04000D97 RID: 3479
		private readonly Action _onRefresh;

		// Token: 0x04000D98 RID: 3480
		private readonly Action<ClanCardSelectionInfo> _openCardSelectionPopup;

		// Token: 0x04000D9A RID: 3482
		private MBBindingList<ClanFinanceWorkshopItemVM> _incomes;

		// Token: 0x04000D9B RID: 3483
		private MBBindingList<ClanSupporterGroupVM> _supporterGroups;

		// Token: 0x04000D9C RID: 3484
		private MBBindingList<ClanFinanceAlleyItemVM> _alleys;

		// Token: 0x04000D9D RID: 3485
		private ClanFinanceAlleyItemVM _currentSelectedAlley;

		// Token: 0x04000D9E RID: 3486
		private ClanFinanceWorkshopItemVM _currentSelectedIncome;

		// Token: 0x04000D9F RID: 3487
		private ClanSupporterGroupVM _currentSelectedSupporterGroup;

		// Token: 0x04000DA0 RID: 3488
		private bool _isSelected;

		// Token: 0x04000DA1 RID: 3489
		private string _nameText;

		// Token: 0x04000DA2 RID: 3490
		private string _incomeText;

		// Token: 0x04000DA3 RID: 3491
		private string _locationText;

		// Token: 0x04000DA4 RID: 3492
		private string _workshopsText;

		// Token: 0x04000DA5 RID: 3493
		private string _supportersText;

		// Token: 0x04000DA6 RID: 3494
		private string _alleysText;

		// Token: 0x04000DA7 RID: 3495
		private string _noAdditionalIncomesText;

		// Token: 0x04000DA8 RID: 3496
		private bool _isAnyValidAlleySelected;

		// Token: 0x04000DA9 RID: 3497
		private bool _isAnyValidIncomeSelected;

		// Token: 0x04000DAA RID: 3498
		private bool _isAnyValidSupporterSelected;

		// Token: 0x04000DAB RID: 3499
		private ClanIncomeSortControllerVM _sortController;
	}
}
